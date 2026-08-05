using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using DokkanDaily.Configuration;
using DokkanDaily.Constants;
using DokkanDaily.Exceptions;
using DokkanDaily.Helpers;
using DokkanDaily.Models;
using DokkanDaily.Models.Enums;
using DokkanDaily.Services.Interfaces;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Globalization;

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

        private const int maxFileSize = 1024 * 8192;

        // OCR is CPU-bound and runs off the request thread, so cap how many uploads can be
        // analysed at once - otherwise concurrent submissions can saturate every core.
        private static readonly SemaphoreSlim _ocrThrottle = new(Math.Max(1, Environment.ProcessorCount / 2));

        // Tracks the full lifecycle of an accepted upload, from the first storage request through
        // background OCR. Registering before the first await prevents the reset from taking a
        // snapshot while an upload is still transferring and therefore not yet represented here.
        private static readonly ConcurrentDictionary<Guid, Task> _pendingAnalysis = new();

        // Replacements for the same logged-in user must be serialized through final OCR. Without
        // this, an older upload can finish analysis after a newer upload arrives and delete the
        // newer blob while cleaning up what it believes are earlier clears.
        // Entries are retained for the process lifetime; there is only one entry per authenticated
        // uploader, and avoiding unsafe removal keeps separate locks from being created for one id.
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _uploaderLocks = new();

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
            UploadAdmission admission = await _uploadAttemptLimiter.TryAcceptAsync(discordId, remoteIp);
            if (!admission.Accepted)
                throw new UploadRejectedException(admission.RejectionMessage);

            Guid analysisId = Guid.NewGuid();
            TaskCompletionSource<bool> analysisLifecycle = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingAnalysis[analysisId] = analysisLifecycle.Task;

            MemoryStream ms = null;
            SemaphoreSlim uploaderLock = string.IsNullOrWhiteSpace(discordId)
                ? null
                : _uploaderLocks.GetOrAdd(discordId, _ => new SemaphoreSlim(1, 1));
            bool lockHeld = false;
            bool lifecycleHandedToAnalysis = false;
            bool analysisOwnsUploaderLock = false;

            try
            {
                if (uploaderLock is not null)
                {
                    await uploaderLock.WaitAsync();
                    lockHeld = true;
                }

                (BlobContainerClient container, _) = await GetOrCreate(bucket);

                string fileName = DokkanDailyHelper.BuildBlobName(userFileName, discordId);

                BlobClient blob = container.GetBlobClient(fileName);

                ms = new();
                using (Stream fileStream = browserFile.OpenReadStream(maxFileSize))
                {
                    await fileStream.CopyToAsync(ms);
                }
                ms.Position = 0;

                _logger.LogInformation("Uploading to `{Container}/{File}`...", container.Name, fileName);

                await blob.UploadAsync(ms, options: new BlobUploadOptions()
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                    // Written with the upload so pending work remains attributable for diagnostics.
                    // UPLOAD_STATUS_PENDING prevents reset processing until OCR finalizes it.
                    Metadata = BuildIdentityTagDict(model, discordUsername, discordId, remoteIp, userAgent),
                    Tags = new Dictionary<string, string> { { AzureConstants.DATE_TAG, DokkanDailyHelper.GetUtcNowDateTag() } }
                });

                _logger.LogInformation("Finished Azure upload.");

                // do OCR analysis, dont block the main thread
                analysisOwnsUploaderLock = lockHeld;
                lockHeld = false;

                try
                {
                    _ = Task.Run(async () =>
                    {
                        bool throttleHeld = false;
                        try
                        {
                            await _ocrThrottle.WaitAsync();
                            throttleHeld = true;

                            ClearMetadata metadata = _ocrService.ProcessImage(ms);
                            _logger.LogInformation("Finished processing image.");
                            Dictionary<string, string> tags = BuildTagDict(model, metadata, discordUsername, discordId, remoteIp, userAgent);
                            await blob.SetMetadataAsync(tags);
                            _logger.LogInformation("Finished updating Azure metadata.");

                            // A replacement is authoritative only after OCR has validated it and
                            // Azure has durably recorded that final state. Invalid or interrupted
                            // attempts leave the uploader's previous valid clear intact.
                            if (metadata is not null)
                                await DeletePreviousUploads(container, discordId, blob.Name);
                        }
                        catch (Exception ex)
                        {
                            // The initial metadata remains `pending`, so the reset cannot score a
                            // clear whose analysis or final metadata write did not complete.
                            _logger.LogError(ex, "Unhandled exception in background OCR task");
                        }
                        finally
                        {
                            if (throttleHeld) _ocrThrottle.Release();
                            if (analysisOwnsUploaderLock) uploaderLock.Release();
                            await ms.DisposeAsync();
                            CompletePendingAnalysis(analysisId, analysisLifecycle);
                        }
                    });
                }
                catch
                {
                    // Task.Run failed before the background task took ownership.
                    lockHeld = analysisOwnsUploaderLock;
                    analysisOwnsUploaderLock = false;
                    throw;
                }

                lifecycleHandedToAnalysis = true;

                return blob;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception while uploading to Azure");
                throw;
            }
            finally
            {
                if (lockHeld) uploaderLock.Release();

                if (!lifecycleHandedToAnalysis)
                {
                    if (ms is not null) await ms.DisposeAsync();
                    CompletePendingAnalysis(analysisId, analysisLifecycle);
                }
            }
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

                    await foreach (BlobContainerItem container in client.GetBlobContainersAsync())
                    {
                        if (!TryGetContainerDate(container.Name, out DateTime parsedDate))
                        {
                            _logger.LogInformation("Container {C} has no parseable date suffix. Skipping.", container.Name);
                            continue;
                        }

                        if (parsedDate >= cutoffDate) continue;

                        _logger.LogInformation("Container {C} is older than {Days} days. Deleting...", container.Name, daysToKeep);

                        try
                        {
                            await client.DeleteBlobContainerAsync(container.Name);
                            _logger.LogInformation("Container {C} deleted.", container.Name);
                        }
                        catch (RequestFailedException ex)
                        {
                            _logger.LogError(ex, "Failed to delete container {C}.", container.Name);
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

        /// <summary>
        /// Extracts the date suffix from a bucket name of the form <c>{containerName}-{MM-dd-yyyy}</c>.
        /// The format is fixed, so it must be parsed with the invariant culture - a culture-sensitive
        /// parse would read <c>03-08-2026</c> as 3 August on a dd-MM-yyyy host and prune live data.
        /// </summary>
        private bool TryGetContainerDate(string containerName, out DateTime date)
        {
            date = default;

            if (!containerName.StartsWith($"{_containerName}-", StringComparison.Ordinal)) return false;

            string suffix = containerName[(_containerName.Length + 1)..];

            return DateTime.TryParseExact(suffix, "MM-dd-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        /// <summary>
        /// Builds a short-lived read URL for a blob, SAS token included.
        /// </summary>
        /// <remarks>
        /// The client signs the SAS itself using the account key already carried by the connection
        /// string, so no separate account name or key setting is needed - those were a second copy
        /// of the same credential and could silently drift out of sync with it.
        /// </remarks>
        public string GetBlobReadUri(string fileName, string bucket = null)
        {
            try
            {
                BlobContainerClient container = new(_connectionString, bucket ?? TodaysBucketFullName);
                BlobClient blob = container.GetBlobClient(fileName);

                if (!blob.CanGenerateSasUri)
                {
                    _logger.LogError("The configured blob connection string carries no account key, so a read SAS cannot be signed for `{File}`.", fileName);
                    return null;
                }

                BlobSasBuilder blobSasBuilder = new()
                {
                    BlobContainerName = container.Name,
                    BlobName = fileName,
                    // backdated to absorb clock skew between this host and Azure Storage
                    StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
                    ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(15)
                };

                blobSasBuilder.SetPermissions(BlobSasPermissions.Read);

                return blob.GenerateSasUri(blobSasBuilder).ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception while generating a read URI for `{File}`", fileName);
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

        /// <summary>
        /// Removes an uploader's earlier clears from the container so a re-upload replaces rather
        /// than accumulates.
        /// </summary>
        /// <remarks>
        /// Keyed on the uploader's blob prefix, so this works across devices - the old scheme put
        /// the user agent in the file name, which meant the same person on a phone and a desktop
        /// produced two blobs. Anonymous uploads have no prefix to key on and are left alone.
        /// </remarks>
        private async Task DeletePreviousUploads(BlobContainerClient container, string discordId, string replacementBlobName)
        {
            string prefix = DokkanDailyHelper.GetUserBlobPrefix(discordId);

            if (prefix is null) return;

            try
            {
                BlobProperties replacementProperties = (await container
                    .GetBlobClient(replacementBlobName)
                    .GetPropertiesAsync()).Value;
                DateTimeOffset? replacementCreatedOn = replacementProperties.CreatedOn;

                if (replacementCreatedOn is null)
                {
                    _logger.LogWarning(
                        "Could not determine when replacement `{File}` was created. Keeping previous clears to avoid deleting a newer upload.",
                        replacementBlobName);
                    return;
                }

                await foreach (BlobItem existing in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, CancellationToken.None))
                {
                    if (existing.Name == replacementBlobName) continue;

                    // The in-memory uploader lock protects one process. CreatedOn also protects a
                    // scaled-out deployment: an older instance finishing OCR late must never delete
                    // a newer upload created by another instance.
                    if (existing.Properties.CreatedOn is null || existing.Properties.CreatedOn >= replacementCreatedOn)
                        continue;

                    _logger.LogInformation("Replacing the uploader's previous clear `{File}`.", existing.Name);
                    await container.DeleteBlobIfExistsAsync(existing.Name, DeleteSnapshotsOption.IncludeSnapshots);
                }
            }
            catch (Exception ex)
            {
                // not worth failing the upload over - the worst case is the gallery showing both
                _logger.LogWarning(ex, "Could not remove previous clears for `{Prefix}`. The new clear will appear alongside them.", prefix);
            }
        }

        /// <summary>
        /// The metadata that is known without OCR. Written at upload time so a clear submitted just
        /// before the nightly reset can still be attributed to a logged in user.
        /// </summary>
        private static Dictionary<string, string> BuildIdentityTagDict(Challenge model, string discordUsername, string discordId, string remoteIp, string userAgent)
        {
            Dictionary<string, string> dict = new()
            {
                { AzureConstants.UPLOAD_STATUS_TAG, AzureConstants.UPLOAD_STATUS_PENDING },
                { AzureConstants.DAILY_TYPE_TAG, model.DailyType.ToString()},
                { AzureConstants.EVENT_TAG, model.TodaysEvent.FullName},
                { AzureConstants.DISCORD_NAME_TAG, discordUsername },
                { AzureConstants.DISCORD_ID,  discordId },
                { AzureConstants.IP_TAG, remoteIp },
                // kept for correlating OCR failures with a device or browser; it used to live in
                // the blob name, where it stacked up on every re-upload
                { AzureConstants.USER_AGENT_TAG, userAgent?.EscapeUnicode() },
                { AzureConstants.CHALLENGE_TARGET_TAG, model.DailyType switch
                    {
                        DailyType.LinkSkill =>  model.LinkSkill?.Name,
                        DailyType.Category => model.Category?.Name,
                        DailyType.Character => model.Leader?.FullName,
                        _ => null
                    }
                }
            };

            return dict.Where(kv => !string.IsNullOrEmpty(kv.Value)).ToDictionary();
        }

        private static Dictionary<string, string> BuildTagDict(Challenge model, ClearMetadata metadata, string discordUsername, string discordId, string remoteIp, string userAgent)
        {
            Dictionary<string, string> dict = BuildIdentityTagDict(model, discordUsername, discordId, remoteIp, userAgent);

            if (metadata is null)
            {
                dict[AzureConstants.UPLOAD_STATUS_TAG] = AzureConstants.UPLOAD_STATUS_INVALID;
                dict[AzureConstants.INVALID_TAG] = true.ToString();

                return dict;
            }

            dict[AzureConstants.UPLOAD_STATUS_TAG] = AzureConstants.UPLOAD_STATUS_VALID;

            string nickname = metadata.Nickname?.EscapeUnicode();

            if (!string.IsNullOrEmpty(nickname)) dict[AzureConstants.USER_NAME_TAG] = nickname;
            if (!string.IsNullOrEmpty(metadata.ClearTime)) dict[AzureConstants.CLEAR_TIME_TAG] = metadata.ClearTime;

            dict[AzureConstants.ITEMLESS_TAG] = metadata.ItemlessClear.ToString();

            return dict;
        }

        /// <summary>
        /// Waits for outstanding OCR analysis to finish, up to <paramref name="timeout"/>.
        /// </summary>
        /// <remarks>
        /// Uploads return as soon as the blob is stored; the metadata that identifies a clear is
        /// written afterwards, and may be queued behind the OCR throttle. The nightly reset calls
        /// this first so submissions accepted moments before the deadline are not read - and
        /// silently discarded - while their analysis is still pending.
        /// </remarks>
        public async Task WaitForPendingAnalysis(TimeSpan timeout)
        {
            Task[] pending = [.. _pendingAnalysis.Values];

            if (pending.Length == 0) return;

            _logger.LogInformation("Waiting up to {Timeout} for {Count} in-flight OCR task(s) to finish.", timeout, pending.Length);

            Task all = Task.WhenAll(pending);

            if (await Task.WhenAny(all, Task.Delay(timeout)) != all)
                _logger.LogWarning("Timed out waiting for OCR to finish. {Count} clear(s) may still be missing metadata and will be skipped.", _pendingAnalysis.Count);
            else
                _logger.LogInformation("All in-flight OCR tasks finished.");
        }

        private static void CompletePendingAnalysis(Guid analysisId, TaskCompletionSource<bool> lifecycle)
        {
            lifecycle.TrySetResult(true);
            _pendingAnalysis.TryRemove(analysisId, out _);
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
