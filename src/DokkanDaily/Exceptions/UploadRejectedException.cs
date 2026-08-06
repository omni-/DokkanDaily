namespace DokkanDaily.Exceptions
{
    public sealed class UploadRejectedException(string message) : Exception(message)
    {
    }
}
