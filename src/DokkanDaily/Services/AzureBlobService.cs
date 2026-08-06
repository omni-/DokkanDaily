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
        private readonly IRngHelperService _rngHelperService;
        private readonly IUploadAttemptLimiter _uploadAttemptLimiter;
        private readonly string _connectionString;
        private readonly string _containerName;

        private static readonly SemaphoreSlim _ocrThrottle = new(Math.Max(1, Environment.ProcessorCount / 2));
        private static readonly ConcurrentDictionary<Guid, Task> _pendingAnalysis = new();
        private static readonly SemaphoreSlim _resetBarrier = new(1, 1);

        private const int maxFileSize = 1024 * 8192;
        private const string ResetInProgressMessage = "Daily results are being calculated. Please try your upload again shortly";
        private const string ChallengeChangedMessage = "The daily challenge changed while this page was open. Refresh the page and try again";

        private string TodaysBucketFullName => GetBucketNameForDate(DokkanDailyHelper.GetUtcNowDateTag());

        public AzureBlobService(IOptions<DokkanDailySettings> settings, ILogger<AzureBlobService> logger, IOcrService ocrService, IRngHelperService rngHelperService, IUploadAttemptLimiter uploadAttemptLimiter)
        {
            _settings = settings.Value;
            _logger = logger;
            _connectionString = _settings.AzureBlobConnectionString;
            _containerName = _settings.AzureBlobContainerName;
            _ocrService = ocrService;
            _rngHelperService = rngHelperService;
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
            bool lifecycleRegistered = false;
            bool lifecycleHandedToAnalysis = false;
            MemoryStream uploadStream = null;

            try
            {
                if (!await _resetBarrier.WaitAsync(0))
                    throw new UploadRejectedException(ResetInProgressMessage);

                try
                {
                    _pendingAnalysis[analysisId] = analysisLifecycle.Task;
                    lifecycleRegistered = true;
                }
                finally
                {
                    _resetBarrier.Release();
                }

                Challenge currentChallenge = await _rngHelperService.GetDailyChallenge();
                if (model is null || currentChallenge is null || model.Date != currentChallenge.Date)
                    throw new UploadRejectedException(ChallengeChangedMessage);

                model = currentChallenge;

                UploadAdmission admission = await _uploadAttemptLimiter.TryAcceptAsync(discordId, remoteIp);
                if (!admission.Accepted)
                    throw new UploadRejectedException(admission.RejectionMessage);

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
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unhandled exception in background OCR task");
                    }
                    finally
                    {
                        if (throttleHeld) _ocrThrottle.Release();
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
                if (uploadStream != null) await uploadStream.DisposeAsync();
                if (lifecycleRegistered && !lifecycleHandedToAnalysis)
                    CompletePendingAnalysis(analysisId, analysisLifecycle);
            }
        }

        public async Task<IAsyncDisposable> AcquireResetBarrierAsync()
        {
            await _resetBarrier.WaitAsync();
            return new ResetBarrierLease(_resetBarrier);
        }

        public async Task WaitForPendingAnalysis(TimeSpan warningInterval)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(warningInterval, TimeSpan.Zero);

            while (true)
            {
                Task[] pending = [.. _pendingAnalysis.Values];
                if (pending.Length == 0) return;

                _logger.LogInformation("Waiting for {Count} in-flight OCR task(s) to finish.", pending.Length);

                Task all = Task.WhenAll(pending);
                if (await Task.WhenAny(all, Task.Delay(warningInterval)) != all)
                {
                    _logger.LogWarning("OCR is still running after {Interval}. Continuing to wait for {Count} clear(s) so they are not skipped.", warningInterval, _pendingAnalysis.Count);
                    continue;
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

        private sealed class ResetBarrierLease(SemaphoreSlim resetBarrier) : IAsyncDisposable
        {
            private SemaphoreSlim _resetBarrier = resetBarrier;

            public ValueTask DisposeAsync()
            {
                Interlocked.Exchange(ref _resetBarrier, null)?.Release();
                return ValueTask.CompletedTask;
            }
        }
    }
}
