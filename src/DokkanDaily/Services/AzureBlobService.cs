using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using DokkanDaily.Configuration;
using DokkanDaily.Helpers;
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

        public AzureBlobService(IOptions<DokkanDailySettings> settings, ILogger<AzureBlobService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
            _connectionString = _settings.AzureBlobConnectionString;
            _azureKey = _settings.AzureBlobKey;
            _containerName = _settings.AzureBlobContainerName;
            _accountName = _settings.AzureAccountName;
        }

        public async Task<string> UploadFileToBlobAsync(string strFileName, string contentType, Stream fileStream)
        {
            try
            {
                var container = new BlobContainerClient(_connectionString, _containerName);

                var createResponse = await container.CreateIfNotExistsAsync();
                if (createResponse != null && createResponse.GetRawResponse().Status == 201)
                    await container.SetAccessPolicyAsync(PublicAccessType.Blob);

                var blob = container.GetBlobClient(strFileName);

                await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);

                string tag = DDHelper.GetUtcNowDateTag();
                await blob.UploadAsync(fileStream, options: new BlobUploadOptions()
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                    Tags = new Dictionary<string, string>() { { "date", tag } },
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
        public async Task DeleteByTagAsync(string tagName)
        {
            try
            {
                var container = new BlobContainerClient(_connectionString, _containerName);
                var createResponse = await container.CreateIfNotExistsAsync();

                if (createResponse != null && createResponse.GetRawResponse().Status == 201)
                    await container.SetAccessPolicyAsync(PublicAccessType.Blob);

                string searchExpression = $"\"date\" = '{tagName}'";

                await foreach (var b in container.FindBlobsByTagsAsync(searchExpression))
                {
                    var blob = container.GetBlobClient(b.BlobName);

                    await blob.DeleteAsync(DeleteSnapshotsOption.IncludeSnapshots);
                }
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
                    BlobContainerName = _containerName,
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

        public async Task<int> GetFileCountForTag(string tagName)
        {
            try
            {
                var container = new BlobContainerClient(_connectionString, _containerName);
                var createResponse = await container.CreateIfNotExistsAsync();

                if (createResponse != null && createResponse.GetRawResponse().Status == 201)
                    await container.SetAccessPolicyAsync(PublicAccessType.Blob);

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

        public async Task<List<BlobClient>> GetFilesForTag(string tagName)
        {
            List <BlobClient> files = [];

            try
            {
                var container = new BlobContainerClient(_connectionString, _containerName);
                var createResponse = await container.CreateIfNotExistsAsync();

                if (createResponse != null && createResponse.GetRawResponse().Status == 201)
                    await container.SetAccessPolicyAsync(PublicAccessType.Blob);

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
    }
}
