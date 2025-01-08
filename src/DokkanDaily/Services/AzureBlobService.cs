using Azure;
using Azure.Storage;
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

namespace DokkanDaily.Services
{
    public class AzureBlobService : IAzureBlobService
    {
        private readonly DokkanDailySettings _settings;
        private readonly ILogger<AzureBlobService> _logger;
        private readonly IOcrService _ocrService;
        private readonly string _connectionString;
        private readonly string _azureKey;
        private readonly string _containerName;
        private readonly string _accountName;

        private const int maxFileSize = 1024 * 8192;

        private string TodaysBucketFullName => GetBucketNameForDate(DokkanDailyHelper.GetUtcNowDateTag());

        public AzureBlobService(IOptions<DokkanDailySettings> settings, ILogger<AzureBlobService> logger, IOcrService ocrService)
        {
            _settings = settings.Value;
            _logger = logger;
            _connectionString = _settings.AzureBlobConnectionString;
            _azureKey = _settings.AzureBlobKey;
            _containerName = _settings.AzureBlobContainerName;
            _accountName = _settings.AzureAccountName;
            _ocrService = ocrService;
        }

        public string GetBucketNameForDate(string formattedDateTag)
        {
            return $"{_containerName}-{formattedDateTag}";
        }

        public async Task<BlobClient> UploadToAzureAsync(string userFileName, string contentType, IBrowserFile browserFile, Challenge model, string bucket = null, string userAgent = null, string discordUsername = null)
        {
            try
            {
                var (container, _) = await GetOrCreate(bucket);

                string fileName = DokkanDailyHelper.AddUserAgentToFileName(userFileName, userAgent);

                BlobClient blob = container.GetBlobClient(fileName);

                await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);

                MemoryStream ms = new();
                using Stream fileStream = browserFile.OpenReadStream(maxFileSize);
                await fileStream.CopyToAsync(ms);
                ms.Position = 0;

                _logger.LogInformation("Uploading to `{Container}/{File}`...", container.Name, fileName);

                await blob.UploadAsync(ms, options: new BlobUploadOptions()
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                    Tags = new Dictionary<string, string> { { AzureConstants.DATE_TAG, DokkanDailyHelper.GetUtcNowDateTag() } }
                });

                _logger.LogInformation("Finished Azure upload.");

                // do OCR analysis, dont block the main thread
                _ = Task.Run(() =>
                {
                    var metadata = _ocrService.ProcessImage(ms);
                    _logger.LogInformation("Finished processing image.");
                    var tags = BuildTagDict(model, metadata, discordUsername);
                    blob.SetMetadataAsync(tags);
                    _logger.LogInformation("Finished updating Azure metadata.");
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
                BlobSasBuilder blobSasBuilder = new()
                {
                    BlobContainerName = bucket ?? TodaysBucketFullName,
                    BlobName = fileName,
                    ExpiresOn = DateTime.UtcNow.AddMinutes(2)
                };

                blobSasBuilder.SetPermissions(BlobSasPermissions.Read);

                var sasToken = blobSasBuilder.ToSasQueryParameters(
                    new StorageSharedKeyCredential(
                        _accountName,
                        _azureKey))
                    .ToString();

                return sasToken;
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

        private static Dictionary<string, string> BuildTagDict(Challenge model, ClearMetadata metadata, string discordUsername)
        {
            string invalid = null;
            if (metadata == null) invalid = true.ToString();

            var dict = new Dictionary<string, string>()
            {
                { AzureConstants.DAILY_TYPE_TAG, model.DailyType.ToString()},
                { AzureConstants.EVENT_TAG, model.TodaysEvent.FullName},
                { AzureConstants.USER_NAME_TAG, DokkanDailyHelper.EscapeUnicode(DokkanDailyHelper.CheckUsername(metadata?.Nickname))},
                { AzureConstants.ITEMLESS_TAG, metadata?.ItemlessClear.ToString()},
                { AzureConstants.CLEAR_TIME_TAG, metadata?.ClearTime},
                { AzureConstants.DISCORD_NAME_TAG, discordUsername },
                { AzureConstants.INVALID_TAG, invalid },
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
