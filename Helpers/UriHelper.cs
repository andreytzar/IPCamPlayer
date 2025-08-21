
namespace IPCamPlayer.Helpers
{
    public static class UriHelper
    {
        public static bool IsValidAbsoluteUri(string uriString)
        {
            return Uri.TryCreate(uriString, UriKind.Absolute, out var uri) && uri.IsAbsoluteUri;
        }

        public static Uri? TryCreateUri(string uriString, UriKind kind = UriKind.Absolute)
        =>Uri.TryCreate(uriString, kind, out var uri) ? uri : null;
        
        public static bool IsHttpOrHttps(string uriString)
        {
            if (Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
                return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
            return false;
        }
    }
}
