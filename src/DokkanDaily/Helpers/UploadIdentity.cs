using System.Net;

namespace DokkanDaily.Helpers
{
    internal static class UploadIdentity
    {
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
