using DokkanDaily.Models;

namespace DokkanDaily.Services.Interfaces
{
    public interface IOcrService
    {
        ClearMetadata ProcessImage(MemoryStream imageStream);
    }
}
