using DokkanDaily.Models;

namespace DokkanDaily.Services.Interfaces
{
    public interface IUploadAttemptLimiter
    {
        Task<UploadAdmission> TryAcceptAsync(string discordId, string normalizedClientIp);
    }
}
