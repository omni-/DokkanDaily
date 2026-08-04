namespace DokkanDaily.Extensions
{
    public static class HttpContextExtensions
    {
        /// <summary>
        /// Gets the client address for the current request.
        /// </summary>
        /// <remarks>
        /// This deliberately does not read <c>X-Forwarded-For</c> directly. The forwarded headers
        /// middleware has already resolved that header into <see cref="ConnectionInfo.RemoteIpAddress"/>,
        /// honouring only the entry the trusted proxy appended. Reading the raw header instead
        /// would return the whole client-controlled chain and let anyone spoof their address.
        /// </remarks>
        public static string GetUserIpAddress(this HttpContext context)
            => context?.Connection?.RemoteIpAddress?.ToString();
    }
}
