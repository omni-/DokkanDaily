using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using DokkanDaily.Configuration;
using DokkanDaily.Constants;
using DokkanDaily.Helpers;
using DokkanDaily.Models;
using DokkanDaily.Models.Enums;
using DokkanDaily.Services.Interfaces;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace DokkanDaily.Services
{
    public class AzureBlobService : IAzureBlobService
    {
        private readonly DokkanDailySettings _settings;
        private readonly ILogger<AzureBlobService> _logger;
        private readonly IOcrService _ocrService;
        private readonly string _connectionString;
        private readonly string _containerName;

        private const int maxFileSize = 1024 * 8192;

        // OCR is CPU-bound and runs off the request thread, so cap how many uploads can be
        // analysed at once - otherwise concurrent submissions can saturate every core.
        private static readonly SemaphoreSlim _ocrThrottle = new(Math.Max(1, Environment.ProcessorCount / 2));

        private string TodaysBucketFullName => GetBucketNameForDate(DokkanDailyHelper.GetUtcNowDateTag());

        public AzureBlobService(IOptions<DokkanDailySettings> settings, ILogger<AzureBlobService> logger, IOcrService ocrService)
        {
            _settings = settings.Value;
            _logger = logger;
            _connectionString = _settings.AzureBlobConnectionString;
            _containerName = _settings.AzureBlobContainerName;
            _ocrService = ocrService;
        }

        public string GetBucketNameForDate(string formattedDateTag)
        {
            return $"{_containerName}-{formattedDateTag}";
        }

        public async Task<BlobClient> UploadToAzureAsync(string userFileName, string contentType, IBrowserFile browserFile, Challenge model, string bucket = null, string userAgent = null, string discordUsername = null, string discordId = null, string remoteIp = null)
        {
            try
            {
                (BlobContainerClient container, _) = await GetOrCreate(bucket);

                string fileName = DokkanDailyHelper.BuildBlobName(userFileName, userAgent, discordId);

                BlobClient blob = container.GetBlobClient(fileName);

                await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);

                MemoryStream ms = new();
                using (Stream fileStream = browserFile.OpenReadStream(maxFileSize))
                {
                    await fileStream.CopyToAsync(ms);
                }
                ms.Position = 0;

                _logger.LogInformation("Uploading to `{Container}/{File}`...", container.Name, fileName);

                await blob.UploadAsync(ms, options: new BlobUploadOptions()
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                    Tags = new Dictionary<string, string> { { AzureConstants.DATE_TAG, DokkanDailyHelper.GetUtcNowDateTag() } }
                });

                _logger.LogInformation("Finished Azure upload.");

                // do OCR analysis, dont block the main thread
                _ = Task.Run(async () =>
                {
                    await _ocrThrottle.WaitAsync();
                    try
                    {
                        ClearMetadata metadata = _ocrService.ProcessImage(ms);
                        _logger.LogInformation("Finished processing image.");
                        Dictionary<string, string> tags = BuildTagDict(model, metadata, discordUsername, discordId, remoteIp);
                        await blob.SetMetadataAsync(tags);
                        _logger.LogInformation("Finished updating Azure metadata.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unhandled exception in background OCR task");
                    }
                    finally
                    {
                        _ocrThrottle.Release();
                        await ms.DisposeAsync();
                    }
                });

                return blob;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception while uploading to Azure");
                throw;
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

        private static Dictionary<string, string> BuildTagDict(Challenge model, ClearMetadata metadata, string discordUsername, string discordId, string remoteIp)
        {
            string invalid = null;
            if (metadata == null) invalid = true.ToString();

            var dict = new Dictionary<string, string>()
            {
                { AzureConstants.DAILY_TYPE_TAG, model.DailyType.ToString()},
                { AzureConstants.EVENT_TAG, model.TodaysEvent.FullName},
                { AzureConstants.USER_NAME_TAG, metadata?.Nickname?.EscapeUnicode()},
                { AzureConstants.ITEMLESS_TAG, (metadata?.ItemlessClear)?.ToString()},
                { AzureConstants.CLEAR_TIME_TAG, metadata?.ClearTime},
                { AzureConstants.DISCORD_NAME_TAG, discordUsername },
                { AzureConstants.DISCORD_ID,  discordId },
                { AzureConstants.INVALID_TAG, invalid },
                { AzureConstants.IP_TAG, remoteIp },
                { AzureConstants.CHALLENGE_TARGET_TAG, model.DailyType switch
                    {
                        DailyType.LinkSkill =>  model.LinkSkill.Name,
                        DailyType.Category => model.Category.Name,
                        DailyType.Character => model.Leader.FullName,
                        _ => null
                    }
                }
            };

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
    }
}
