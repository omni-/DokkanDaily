using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using DokkanDaily.Configuration;
using DokkanDaily.Constants;
using DokkanDaily.Helpers;
using DokkanDaily.Models;
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

        private string TodaysBucketFullName => $"{_containerName}-{DDHelper.GetUtcNowDateTag()}";

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

        public async Task<string> UploadToAzureAsync(string userFileName, string contentType, IBrowserFile browserFile, Challenge model, string bucket = null, string userAgent = null)
        {
            try
            {
                var (container, _) = await GetOrCreate(bucket);

                string fileName = DDHelper.AddUserAgentToFileName(userFileName, userAgent);

                var blob = container.GetBlobClient(fileName);

                await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);

                MemoryStream ms = new();
                using Stream fileStream = browserFile.OpenReadStream(maxFileSize);
                await fileStream.CopyToAsync(ms);
                ms.Position = 0;

                await blob.UploadAsync(ms, options: new BlobUploadOptions()
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                    Tags = new Dictionary<string, string>{ { DDConstants.DATE_TAG, DDHelper.GetUtcNowDateTag() } }
                });

                // do OCR analysis, dont block the main thread
                await Task.Run(async () =>
                {
                    var metadata = _ocrService.ProcessImage(ms);
                    var tags = BuildTagDict(model, metadata);
                    await blob.SetMetadataAsync(tags);

                }).ConfigureAwait(false);

                var urlString = blob.Uri.ToString();
                return urlString;
            }
            catch (Exception ex)
            {
                _logger?.LogError("Unhandled exception {@Ex}", ex);
                throw;
            }
        }

        public async Task PruneContainers(int daysToKeep)
        {
            try
            {
                // TODO
                throw new NotImplementedException();
            }
            catch (Exception ex)
            {
                _logger?.LogError("Unhandled exception {@Ex}", ex);
            }
        }

        public string GetBlobSASTOkenByFile(string fileName)
        {
            try
            {
                BlobSasBuilder blobSasBuilder = new()
                {
                    BlobContainerName = TodaysBucketFullName,
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
                _logger?.LogError("Unhandled exception {@Ex}", ex);
                throw;
            }
        }

        public async Task<int> GetFileCountForTag(string tagName, string bucket = null)
        {
            try
            {
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
                _logger?.LogError("Unhandled exception {@Ex}", ex);
                throw;
            }
        }

        public async Task<List<BlobClient>> GetFilesForTag(string tagName, string bucket = null)
        {
            List <BlobClient> files = [];

            try
            {
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
                _logger?.LogError("Unhandled exception {@Ex}", ex);
                throw;
            }
        }

        private Dictionary<string, string> BuildTagDict(Challenge model, ClearMetadata metadata)
        {
            var dict = new Dictionary<string, string>()
            {
                { DDConstants.DAILY_TYPE_TAG, model.DailyType.ToString()},
                { DDConstants.EVENT_TAG, model.TodaysEvent.FullName},
                { DDConstants.USER_NAME_TAG, metadata?.Nickname ?? ""},
                { DDConstants.ITEMLESS_TAG, metadata?.ItemlessClear.ToString() ?? ""},
                { DDConstants.CLEAR_TIME_TAG, metadata?.ClearTime ?? ""}
            };

            return dict.Where(kv => !string.IsNullOrEmpty(kv.Value)).ToDictionary();
        }

        private async Task<(BlobContainerClient, bool)> GetOrCreate(string bucket)
        {
            bool created = false;

            var container = new BlobContainerClient(_connectionString, bucket ?? TodaysBucketFullName);

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
