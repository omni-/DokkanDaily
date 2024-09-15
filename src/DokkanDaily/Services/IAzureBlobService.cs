using Azure.Storage.Blobs;

namespace DokkanDaily.Services
{
    public interface IAzureBlobService
    {
        Task<List<BlobClient>> GetFilesForTag(string tag);

        Task<string> UploadFileToBlobAsync(string strFileName, string contecntType, Stream fileStream);

        Task<int> GetFileCountForTag(string tagName);

        Task DeleteByTagAsync(string tagName);

        string GetBlobSASTOkenByFile(string fileName);
    }
}
