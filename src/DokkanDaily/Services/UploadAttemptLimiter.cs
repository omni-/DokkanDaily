using DokkanDaily.Models;
using DokkanDaily.Repository;
using DokkanDaily.Services.Interfaces;
using System.Net;

namespace DokkanDaily.Services
{
    public sealed class UploadAttemptLimiter(IDokkanDailyRepository repository, TimeProvider timeProvider) : IUploadAttemptLimiter
    {
        public const string LimitReachedMessage = "You've reached the limit of five upload attempts for today (UTC). Please try again tomorrow";
        public const string MissingAddressMessage = "We couldn't verify your network address. Sign in with Discord or try again later";

        private readonly IDokkanDailyRepository _repository = repository;
        private readonly TimeProvider _timeProvider = timeProvider;

        public async Task<UploadAdmission> TryAcceptAsync(string discordId, string normalizedClientIp)
        {
            DateOnly utcDate = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
            string uploaderKey;

            if (!string.IsNullOrWhiteSpace(discordId))
            {
                uploaderKey = $"discord:{discordId.Trim()}";
            }
            else if (TryNormalizeIpAddress(normalizedClientIp, out string ipAddress))
            {
                uploaderKey = $"ip:{ipAddress}";
            }
            else
            {
                return new(false, null, utcDate, MissingAddressMessage);
            }

            bool accepted = await _repository.TryAcceptUploadAttempt(uploaderKey, utcDate);

            return accepted
                ? new(true, uploaderKey, utcDate)
                : new(false, uploaderKey, utcDate, LimitReachedMessage);
        }

        internal static bool TryNormalizeIpAddress(string value, out string normalized)
        {
            normalized = null;

            if (!IPAddress.TryParse(value, out IPAddress address) ||
                address.Equals(IPAddress.Any) ||
                address.Equals(IPAddress.IPv6Any))
            {
                return false;
            }

            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();

            normalized = address.ToString();
            return true;
        }
    }
}
