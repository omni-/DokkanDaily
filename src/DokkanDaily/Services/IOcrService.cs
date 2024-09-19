using DokkanDaily.Models;

namespace DokkanDaily.Services
{
    public interface IOcrService
    {
        ClearMetadata ProcessImage(MemoryStream imageStream);
    }
}
