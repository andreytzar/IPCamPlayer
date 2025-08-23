
namespace IPCamPlayer.Helpers
{
    public static class UriHelper
    {
        public static bool IsValidAbsoluteUri(string uriString)
        {
            return Uri.TryCreate(uriString, UriKind.Absolute, out var uri) && uri.IsAbsoluteUri;
        }

        public static Uri? TryCreateUri(string uriString, UriKind kind = UriKind.Absolute)
        => Uri.TryCreate(uriString, kind, out var uri) ? uri : null;

        public static bool IsHttpOrHttps(string uriString)
        {
            if (Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
                return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
            return false;
        }
        public static string UrlToURLWithCredentials(string originalUrl, string login, string password)
        {
            if (!IsValidAbsoluteUri(originalUrl)) return originalUrl;
            if(string.IsNullOrEmpty(login.Trim()) || string.IsNullOrEmpty(password.Trim())) return originalUrl;
            try
            {
                UriBuilder builder = new UriBuilder(originalUrl.Trim())
                {
                    UserName = login.Trim(),
                    Password = password.Trim()
                };
                return builder.Uri.ToString();
            }
            catch { }
            return originalUrl;

        }
    }
}
