using IPCamPlayer.Helpers;
using onvifDevice;
using OnVifLibrary;
using onvifMedia;
using onvifMedia2;
using System.Security;
using System.ServiceModel;


namespace IPCamPlayer.Classes
{
    internal class OnVifDev
    {
        internal List<MediaToken> MediaTokens { get; private set; } = new();

        internal event EventHandler<string>? Status;

        const string onfivlocal = "/onvif/device_service";
        const string nsMedia = "onvif.org/ver10/media/wsdl";
        const string nsMedia2 = "onvif.org/ver20/media/wsdl";

        DeviceClient? _device;
        MediaClient? _media;
        Media2Client? _media2;
        List<Service> _services = new();

        string _deviceurl = string.Empty;
        string _host = string.Empty;
        object lockobj = new();
        bool isBusy = false;

        internal async Task<bool> Connect(string url, SecureString password, string login = "", BasicHttpSecurityMode mode = BasicHttpSecurityMode.TransportCredentialOnly, HttpClientCredentialType credentialType = HttpClientCredentialType.Digest)
        {
            lock (lockobj)
            {
                if (isBusy)
                {
                    Status?.Invoke(this, "OnVif Error: Connect Task is busy");
                    return false;
                }
                Status?.Invoke(this, "Connecting to OnVif Device...");
                isBusy = true;
            }
            try
            {
                _device?.Close(); MediaTokens.Clear();
                _device = null; _media = null; _media2 = null; _services.Clear();
                _deviceurl = _host = string.Empty;
                if (!CheckUri(url))
                {
                    Status?.Invoke(this, "OnVif Error: Uri Error");
                    return false;
                }
                if (!await NetworkHelper.IsDeviceOnlineAsync(_host))
                {
                    Status?.Invoke(this, "OnVif Error: Device is offline");
                    return false;
                }

                var bind = OnVifCamera.GettSecureBinding(mode: mode, credentialType: credentialType);
                _device = new DeviceClient(bind, new(_deviceurl));
                OnVifCamera.SetClientCredentials(_device.ClientCredentials, password, login);
                var t = await _device.GetSystemDateAndTimeAsync();
                if (t == null)
                {
                    Status?.Invoke(this, "OnVif Error: Device Time not available");
                    return false;
                }
                Status?.Invoke(this, $"OnVif Device Time: {t.LocalDateTime.Date.Month}/{t.LocalDateTime.Date.Day} {t.LocalDateTime.Time.Hour.ToString("00")}:{t.LocalDateTime.Time.Minute.ToString("00")}");
                var info = await GetDeviceInfo();
                if (info != null)
                    Status?.Invoke(this, $"OnVif Device Info: {info.Manufacturer} {info.HardwareId} {info.Model}");
                else
                    Status?.Invoke(this, $"OnVif Device Info not available");
                if (!await GetServices())
                {
                    Status?.Invoke(this, "OnVif Error: No Services available");
                    return false;
                }
                if (await GetServices())
                {
                    var serviceMedia = _services.FirstOrDefault(x => x.Namespace.Contains(nsMedia, StringComparison.InvariantCultureIgnoreCase));
                    if (serviceMedia != null)
                    {
                        _media = new MediaClient(bind, new(serviceMedia.XAddr));
                        OnVifCamera.SetClientCredentials(_media.ClientCredentials, password, login);
                        var result = await _media.GetProfilesAsync();
                        if (result != null)
                        {
                            Status?.Invoke(this, "Media Service:");
                            foreach (var prof in result.Profiles)
                            {
                                string rtsp = await GetMediaStreamRtspUri(prof.token);
                                string snapshot = await GetMediaShapShotUri(prof.token);
                                if (!string.IsNullOrEmpty(rtsp))
                                {
                                    MediaTokens.Add(new() { Name = $"Media: {prof.Name}", Rtsp = rtsp, Token = prof.token, SnapShot = snapshot });
                                    Status?.Invoke(this, $"{prof.token}:\r{rtsp}\r{snapshot}");
                                }
                            }
                        }
                    }
                    var serviceMedia2 = _services.FirstOrDefault(x => x.Namespace.Contains(nsMedia2, StringComparison.InvariantCultureIgnoreCase));
                    if (serviceMedia2 != null)
                    {
                        _media2 = new Media2Client(bind, new(serviceMedia2.XAddr));
                        OnVifCamera.SetClientCredentials(_media2.ClientCredentials, password, login);
                        var result = await _media2.GetProfilesAsync(string.Empty, []);
                        if (result != null)
                        {
                            Status?.Invoke(this, "Media2 Service:");
                            foreach (var prof in result.Profiles)
                            {
                                string rtsp = await GetMedia2StreamRtspUri(prof.token);
                                string snapshot = await GetMedia2ShapShotUri(prof.token);
                                if (!string.IsNullOrEmpty(rtsp))
                                {
                                    MediaTokens.Add(new() { Name = $"Media2: {prof.Name}", Rtsp = rtsp, Token = prof.token, SnapShot = snapshot });
                                    Status?.Invoke(this, $"{prof.token}:\r{rtsp}\r{snapshot}");
                                }
                            }
                        }
                    }
                }
                if (MediaTokens.Count > 0) Status?.Invoke(this, $"OnVif Device connected: Found {MediaTokens.Count} media");
                else Status?.Invoke(this, $"OnVif Device: no media found");
                return MediaTokens.Count > 0;
            }
            catch (Exception ex)
            {
                Status?.Invoke(this, $"OnVif Error: {ex.Message}");
                return false;
            }
            finally
            {
                lock (lockobj)
                {
                    isBusy = false;
                }
            }
        }
        async Task<bool> GetServices()
        {
            _services.Clear();
            if (_device == null) return false;
            var result = await _device.GetServicesAsync(false);
            if (result == null) return false;
            _services.AddRange(result.Service);
            return true;
        }
        async Task<string> GetMediaShapShotUri(string token)
        {
            try
            {
                if (_media == null) return string.Empty;
                var res = await _media.GetSnapshotUriAsync(token);
                if (res == null) return string.Empty;
                return res.Uri;
            }
            catch (Exception ex)
            {
                return "";
            }
        }
        async Task<string> GetMediaStreamRtspUri(string token)
        {
            try
            {
                if (_media == null) return string.Empty;
                onvifMedia.StreamSetup ss = new onvifMedia.StreamSetup
                {
                    Transport = new onvifMedia.Transport
                    {
                        Protocol = onvifMedia.TransportProtocol.RTSP
                    }
                };
                var res = await _media.GetStreamUriAsync(ss, token);
                if (res == null) return string.Empty;
                return res.Uri;
            }
            catch (Exception ex)
            {
                return "";
            }
        }
        async Task<string> GetMedia2ShapShotUri(string token)
        {
            try
            {
                if (_media2 == null) return string.Empty;
                var res = await _media2.GetSnapshotUriAsync(token);
                if (res == null) return string.Empty;
                return res.Uri;
            }
            catch (Exception ex)
            {
                return "";
            }
        }
        async Task<string> GetMedia2StreamRtspUri(string token)
        {
            try
            {
                if (_media2 == null) return string.Empty;
                var res = await _media2.GetStreamUriAsync("rtsp", token);
                return res?.Uri ?? "";
            }
            catch (Exception ex)
            {
                return "";
            }
        }
        public async Task<GetDeviceInformationResponse?> GetDeviceInfo()
        {
            try
            {
                if (_device == null) return null;
                return await _device.GetDeviceInformationAsync(new GetDeviceInformationRequest());

            }
            catch (Exception ex)
            {
                return null;
            }
        }

        bool CheckUri(string url)
        {
            _deviceurl = string.Empty;
            _host = string.Empty;
            try
            {
                url = url.Trim();
                if (string.IsNullOrEmpty(url) || !UriHelper.IsValidAbsoluteUri(url)) return false;
                Uri uri = new Uri(url);
                _deviceurl = $"{uri.Scheme}://{uri.Host}{(!uri.IsDefaultPort ? $":{uri.Port}" : "")}{onfivlocal}";
                _host = uri.Host;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class MediaToken
    {
        public string Name { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string Rtsp { get; set; } = string.Empty;
        public string SnapShot { get; set; } = string.Empty;
    }

}
