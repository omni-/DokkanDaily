namespace DokkanDaily.Models.ViewModel
{
    public class FileUploadViewModel
    {
        public string FileName { get; init; }

        public string FileStorageUrl { get; init; }

        public string ContentType { get; init; }

        /// <summary>
        /// The read URL including its SAS token. Resolved once when the model is built - minting a
        /// token from the render path would issue a fresh one on every render pass.
        /// </summary>
        public string SasUrl { get; set; }

        public IDictionary<string, string> Tags { get; set; }
    }
}
