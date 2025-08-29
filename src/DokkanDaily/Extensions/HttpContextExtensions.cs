namespace DokkanDaily.Extensions
{
    public static class HttpContextExtensions
    {
        public static string GetUserIpAddress(this HttpContext context)
        {
            // Check X-Forwarded-For header
            if (!string.IsNullOrEmpty(context.Request.Headers["X-Forwarded-For"]))
                return context.Request.Headers["X-Forwarded-For"];

            // Fallback to RemoteIpAddress
            return context.Connection.RemoteIpAddress?.ToString();
        }

    }
}
