using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using DokkanDaily.Configuration;
using DokkanDaily.Constants;
using DokkanDaily.Helpers;
using DokkanDaily.Models;
using Microsoft.Extensions.Options;

namespace DokkanDaily.Services
{
    public class AzureBlobService : IAzureBlobService
    {
        private readonly DokkanDailySettings _settings;
        private readonly ILogger<AzureBlobService> _logger;
        private readonly string _connectionString;
        private readonly string _azureKey;
        private readonly string _containerName;
        private readonly string _accountName;

        private string TodaysBucketFullName => $"{_containerName}-{DDHelper.GetUtcNowDateTag()}";

        public AzureBlobService(IOptions<DokkanDailySettings> settings, ILogger<AzureBlobService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
            _connectionString = _settings.AzureBlobConnectionString;
            _azureKey = _settings.AzureBlobKey;
            _containerName = _settings.AzureBlobContainerName;
            _accountName = _settings.AzureAccountName;
        }

        public async Task<string> UploadToAzureAsync(string strFileName, string contentType, Stream fileStream, Challenge model, string bucket = null)
        {
            try
            {
                var (container, _) = await GetOrCreate(bucket);

                var blob = container.GetBlobClient(strFileName);

                await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);

                await blob.UploadAsync(fileStream, options: new BlobUploadOptions()
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                    Tags = BuildTagDict(model)
                });

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

        private Dictionary<string, string> BuildTagDict(Challenge model)
        {
            string tag = DDHelper.GetUtcNowDateTag();

            return new Dictionary<string, string>()
            {
                { DDConstants.DATE_TAG, tag },
                { DDConstants.DAILY_TYPE_TAG, model.DailyType.ToString() },
                { DDConstants.EVENT_TAG, DDConstants.AlphaNumericRegex().Replace(model.TodaysEvent.FullName, "") }
            };
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
