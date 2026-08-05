namespace DokkanDaily.Models
{
    public sealed record UploadAdmission(bool Accepted, string UploaderKey, DateOnly UtcDate, string RejectionMessage = null);
}
