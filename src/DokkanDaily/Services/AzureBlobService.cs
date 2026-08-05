using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using System.Collections.Concurrent;
using DokkanDaily.Configuration;
using DokkanDaily.Constants;
using DokkanDaily.Exceptions;
using DokkanDaily.Helpers;
using DokkanDaily.Models;
using DokkanDaily.Models.Enums;
using DokkanDaily.Services.Interfaces;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;

namespace DokkanDaily.Services
{
    public class AzureBlobService : IAzureBlobService
    {
        private readonly DokkanDailySettings _settings;
        private readonly ILogger<AzureBlobService> _logger;
        private readonly IOcrService _ocrService;
        private readonly IUploadAttemptLimiter _uploadAttemptLimiter;
        private readonly string _connectionString;
        private readonly string _containerName;

        private static readonly SemaphoreSlim _ocrThrottle = new(Math.Max(1, Environment.ProcessorCount / 2));
        private static readonly ConcurrentDictionary<Guid, Task> _pendingAnalysis = new();
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _uploaderLocks = new();

        private const int maxFileSize = 1024 * 8192;

        private string TodaysBucketFullName => GetBucketNameForDate(DokkanDailyHelper.GetUtcNowDateTag());

        public AzureBlobService(IOptions<DokkanDailySettings> settings, ILogger<AzureBlobService> logger, IOcrService ocrService, IUploadAttemptLimiter uploadAttemptLimiter)
        {
            _settings = settings.Value;
            _logger = logger;
            _connectionString = _settings.AzureBlobConnectionString;
            _containerName = _settings.AzureBlobContainerName;
            _ocrService = ocrService;
            _uploadAttemptLimiter = uploadAttemptLimiter;
        }

        public string GetBucketNameForDate(string formattedDateTag)
        {
            return $"{_containerName}-{formattedDateTag}";
        }

        public async Task<BlobClient> UploadToAzureAsync(string userFileName, string contentType, IBrowserFile browserFile, Challenge model, string bucket = null, string userAgent = null, string discordUsername = null, string discordId = null, string remoteIp = null)
        {
            Guid analysisId = Guid.NewGuid();
            TaskCompletionSource analysisLifecycle = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingAnalysis[analysisId] = analysisLifecycle.Task;

            SemaphoreSlim uploaderLock = string.IsNullOrWhiteSpace(discordId)
                ? null
                : _uploaderLocks.GetOrAdd(discordId, _ => new SemaphoreSlim(1, 1));
            bool uploaderLockHeld = false;
            bool lifecycleHandedToAnalysis = false;
            MemoryStream uploadStream = null;

            try
            {
                UploadAdmission admission = await _uploadAttemptLimiter.TryAcceptAsync(discordId, remoteIp);
                if (!admission.Accepted)
                    throw new UploadRejectedException(admission.RejectionMessage);

                if (uploaderLock != null)
                {
                    await uploaderLock.WaitAsync();
                    uploaderLockHeld = true;
                }

                var (container, _) = await GetOrCreate(bucket);

                string fileName = DokkanDailyHelper.BuildBlobName(userFileName, discordId);

                BlobClient blob = container.GetBlobClient(fileName);

                uploadStream = new MemoryStream();
                using Stream fileStream = browserFile.OpenReadStream(maxFileSize);
                await fileStream.CopyToAsync(uploadStream);
                uploadStream.Position = 0;

                _logger.LogInformation("Uploading to `{Container}/{File}`...", container.Name, fileName);

                await blob.UploadAsync(uploadStream, options: new BlobUploadOptions()
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                    Tags = new Dictionary<string, string> { { AzureConstants.DATE_TAG, DokkanDailyHelper.GetUtcNowDateTag() } },
                    Metadata = BuildIdentityTagDict(model, discordUsername, discordId, remoteIp, userAgent)
                });

                _logger.LogInformation("Finished Azure upload.");

                MemoryStream analysisStream = uploadStream;
                bool releaseUploaderLock = uploaderLockHeld;

                _ = Task.Run(async () =>
                {
                    bool throttleHeld = false;
                    try
                    {
                        await _ocrThrottle.WaitAsync();
                        throttleHeld = true;

                        var metadata = _ocrService.ProcessImage(analysisStream);
                        _logger.LogInformation("Finished processing image.");
                        var tags = BuildTagDict(model, metadata, discordUsername, discordId, remoteIp, userAgent);
                        await blob.SetMetadataAsync(tags);
                        _logger.LogInformation("Finished updating Azure metadata.");

                        if (metadata != null)
                            await DeletePreviousUploads(container, blob, discordId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unhandled exception in background OCR task");
                    }
                    finally
                    {
                        if (throttleHeld) _ocrThrottle.Release();
                        if (releaseUploaderLock) uploaderLock.Release();
                        try
                        {
                            await analysisStream.DisposeAsync();
                        }
                        finally
                        {
                            CompletePendingAnalysis(analysisId, analysisLifecycle);
                        }
                    }
                });

                uploadStream = null;
                uploaderLockHeld = false;
                lifecycleHandedToAnalysis = true;

                return blob;
            }
            catch (UploadRejectedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception while uploading to Azure");
                throw;
            }
            finally
            {
                if (uploaderLockHeld) uploaderLock.Release();
                if (uploadStream != null) await uploadStream.DisposeAsync();
                if (!lifecycleHandedToAnalysis) CompletePendingAnalysis(analysisId, analysisLifecycle);
            }
        }

        public async Task WaitForPendingAnalysis(TimeSpan timeout)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);

            while (true)
            {
                Task[] pending = [.. _pendingAnalysis.Values];
                if (pending.Length == 0) return;

                TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    _logger.LogWarning("Timed out waiting for OCR to finish. {Count} clear(s) remain pending and will be skipped.", _pendingAnalysis.Count);
                    return;
                }

                _logger.LogInformation("Waiting up to {Timeout} for {Count} in-flight OCR task(s) to finish.", remaining, pending.Length);

                Task all = Task.WhenAll(pending);
                if (await Task.WhenAny(all, Task.Delay(remaining)) != all)
                {
                    _logger.LogWarning("Timed out waiting for OCR to finish. {Count} clear(s) remain pending and will be skipped.", _pendingAnalysis.Count);
                    return;
                }
            }
        }

        private static void CompletePendingAnalysis(Guid analysisId, TaskCompletionSource lifecycle)
        {
            lifecycle.TrySetResult();
            _pendingAnalysis.TryRemove(analysisId, out _);
        }

        // TODO: test this
        public async Task PruneContainers(int daysToKeep)
        {
            if (_settings.FeatureFlags.EnablePruneJob)
            {
                try
                {
                    DateTime today = DateTime.UtcNow;
                    DateTime cutoffDate = today - TimeSpan.FromDays(daysToKeep);

                    BlobServiceClient client = new(_connectionString);

                    var containerList = client.GetBlobContainers();

                    foreach (var container in containerList)
                    {
                        string date = string.Join('-', container.Name.Split('-').Skip(2));

                        if (DateTime.TryParse(date, out DateTime parsedDate) && parsedDate < cutoffDate)
                        {
                            _logger.LogInformation("Container {C} is older than {Days} old. Deleting...", container.Name, daysToKeep);

                            try
                            {
                                await client.DeleteBlobContainerAsync(container.Name);
                            }
                            catch (RequestFailedException ex)
                            {
                                _logger.LogError("Failed to delete container {C}. Exception: `{@Ex}`", container.Name, ex);
                            }

                            _logger.LogInformation("Container {C} deleted.", container.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception while pruning containers");
                }
            }
            else
            {
                _logger.LogInformation("`EnablePruneJob` was not configured or set to false. Skipping Prune Job.");
            }
        }

        public string GetBlobSasTokenByFile(string fileName, string bucket = null)
        {
            try
            {
                BlobContainerClient container = new(_connectionString, bucket ?? TodaysBucketFullName);
                BlobClient blob = container.GetBlobClient(fileName);

                if (!blob.CanGenerateSasUri)
                {
                    _logger.LogError("The configured blob connection string cannot sign a read SAS for `{File}`.", fileName);
                    return null;
                }

                BlobSasBuilder blobSasBuilder = new()
                {
                    BlobContainerName = container.Name,
                    BlobName = fileName,
                    StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
                    ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(15)
                };

                blobSasBuilder.SetPermissions(BlobSasPermissions.Read);

                return blob.GenerateSasUri(blobSasBuilder).Query.TrimStart('?');
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception while getting SAS token");
                throw;
            }
        }

        public async Task<int> GetFileCountForTag(string tagName, string bucket = null)
        {
            try
            {
                _logger.LogInformation("Getting file count by tag `{T}`", tagName);

                var (container, created) = await GetOrCreate(bucket);

                if (created) return 0;

                string searchExpression = $"\"date\" = '{tagName}'";

                int ctr = 0;
                await foreach (var b in container.FindBlobsByTagsAsync(searchExpression))
                {
                    ctr++;
                }

                return ctr;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception while getting file count from Azure");
                throw;
            }
        }

        public async Task<List<BlobClient>> GetFilesForTag(string tagName, string bucket = null)
        {
            List<BlobClient> files = [];

            try
            {
                _logger.LogInformation("Getting files by tag `{T}`", tagName);

                var (container, created) = await GetOrCreate(bucket);
                if (created) return files;

                string searchExpression = $"\"date\" = '{tagName}'";

                await foreach (var b in container.FindBlobsByTagsAsync(searchExpression))
                {
                    var blob = container.GetBlobClient(b.BlobName);
                    files.Add(blob);
                }

                return files;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception while getting files from Azure");
                throw;
            }
        }

        internal static Dictionary<string, string> BuildIdentityTagDict(Challenge model, string discordUsername, string discordId, string remoteIp, string userAgent)
        {
            var dict = new Dictionary<string, string>()
            {
                { AzureConstants.DAILY_TYPE_TAG, model.DailyType.ToString()},
                { AzureConstants.EVENT_TAG, model.TodaysEvent.FullName?.EscapeUnicode()},
                { AzureConstants.DISCORD_NAME_TAG, discordUsername?.EscapeUnicode() },
                { AzureConstants.DISCORD_ID,  discordId },
                { AzureConstants.IP_TAG, remoteIp },
                { AzureConstants.USER_AGENT_TAG, userAgent?.EscapeUnicode() },
                { AzureConstants.UPLOAD_STATUS_TAG, AzureConstants.UPLOAD_STATUS_PENDING },
                { AzureConstants.CHALLENGE_TARGET_TAG, (model.DailyType switch
                    {
                        DailyType.LinkSkill =>  model.LinkSkill.Name,
                        DailyType.Category => model.Category.Name,
                        DailyType.Character => model.Leader.FullName,
                        _ => null
                    })?.EscapeUnicode()
                }
            };

            return dict.Where(kv => !string.IsNullOrEmpty(kv.Value)).ToDictionary();
        }

        private static Dictionary<string, string> BuildTagDict(Challenge model, ClearMetadata metadata, string discordUsername, string discordId, string remoteIp, string userAgent)
        {
            Dictionary<string, string> dict = BuildIdentityTagDict(model, discordUsername, discordId, remoteIp, userAgent);

            if (metadata == null)
            {
                dict[AzureConstants.UPLOAD_STATUS_TAG] = AzureConstants.UPLOAD_STATUS_INVALID;
                dict[AzureConstants.INVALID_TAG] = true.ToString();
                return dict;
            }

            dict[AzureConstants.UPLOAD_STATUS_TAG] = AzureConstants.UPLOAD_STATUS_VALID;
            dict[AzureConstants.USER_NAME_TAG] = metadata.Nickname?.EscapeUnicode();
            dict[AzureConstants.ITEMLESS_TAG] = metadata.ItemlessClear.ToString();
            dict[AzureConstants.CLEAR_TIME_TAG] = metadata.ClearTime;

            return dict.Where(kv => !string.IsNullOrEmpty(kv.Value)).ToDictionary();
        }

        private async Task DeletePreviousUploads(BlobContainerClient container, BlobClient replacement, string discordId)
        {
            string prefix = DokkanDailyHelper.GetUserBlobPrefix(discordId);
            if (prefix == null) return;

            try
            {
                DateTimeOffset? replacementCreatedOn = (await replacement.GetPropertiesAsync()).Value.CreatedOn;
                if (replacementCreatedOn == null) return;

                await foreach (BlobItem existing in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, CancellationToken.None))
                {
                    if (existing.Name == replacement.Name ||
                        existing.Properties.CreatedOn == null ||
                        existing.Properties.CreatedOn >= replacementCreatedOn)
                    {
                        continue;
                    }

                    await container.DeleteBlobIfExistsAsync(existing.Name, DeleteSnapshotsOption.IncludeSnapshots);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not remove prior uploads for Discord user `{DiscordId}`.", discordId);
            }
        }

        private async Task<(BlobContainerClient, bool)> GetOrCreate(string bucket)
        {
            bool created = false;

            var container = new BlobContainerClient(_connectionString, bucket ?? TodaysBucketFullName);

            _logger.LogInformation("Requesting container {@C}", container);

            var createResponse = await container.CreateIfNotExistsAsync();

            if (createResponse?.GetRawResponse()?.Status == 201)
            {
                await container.SetAccessPolicyAsync(PublicAccessType.None);
                created = true;
            }

            return (container, created);
        }
    }
}
