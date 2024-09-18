namespace DokkanDaily.Models.Dto
{
    public class FileUploadViewModel
    {
        public string FileName { get; init; }

        public string FileStorageUrl { get; init; }

        public string ContentType { get; init; }

        public IDictionary<string, string> Tags { get; set; }
    }
}
