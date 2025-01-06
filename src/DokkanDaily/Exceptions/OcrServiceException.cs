namespace DokkanDaily.Exceptions
{
    public class OcrServiceException : Exception
    {
        public OcrServiceException() { }

        public OcrServiceException(string message) : base(message) { }
    }
}
