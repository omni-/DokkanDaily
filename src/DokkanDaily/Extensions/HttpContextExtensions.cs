namespace DokkanDaily.Extensions
{
    public static class HttpContextExtensions
    {
        public static string GetUserIpAddress(this HttpContext context)
        {
            System.Net.IPAddress address = context?.Connection?.RemoteIpAddress;

            if (address is null ||
                address.Equals(System.Net.IPAddress.Any) ||
                address.Equals(System.Net.IPAddress.IPv6Any))
            {
                return null;
            }

            return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
        }
    }
}
