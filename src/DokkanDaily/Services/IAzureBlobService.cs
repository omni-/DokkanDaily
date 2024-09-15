namespace DokkanDaily.Services
{
    public interface IAzureBlobService
    {
        Task<string> UploadFileToBlobAsync(string strFileName, string contecntType, Stream fileStream);

        Task DeleteByTagAsync(string tagName);

        string GetBlobSASTOkenByFile(string fileName);
    }
}
