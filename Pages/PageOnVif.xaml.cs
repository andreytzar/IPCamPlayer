
using IPCamPlayer.Classes.FFMPG;
using IPCamPlayer.Helpers.VM;
using OnVifLibrary;
using OnVifLibrary.Helpers;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Security;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace IPCamPlayer.Pages
{
    public partial class PageOnVif : Page, IDisposable
    {
        public event EventHandler<string> StatusChanged { add { vm.StatusChanged += value; } remove { vm.StatusChanged -= value; } }
        VMPageOnVif vm;
        public PageOnVif(EventHandler<string> statusChanged)
        {
            InitializeComponent();
            DataContext = vm = new();
            StatusChanged += statusChanged;
        }

        private void Connect_Click(object sender, System.Windows.RoutedEventArgs e)
        => vm.Connect(passBox.SecurePassword);

        public void Dispose()
        {
            vm.Dispose();
        }
    }
    public class VMPageOnVif : VMNotifyPropretyChanged, IDisposable
    {
        string _Login = string.Empty;
        public string Login { get => _Login; set { if (_Login != value) { _Login = value; OnPropertyChanged(); } } }
        string _Url = "";
        public string Url { get => _Url; set { if (_Url != value) { _Url = value; OnPropertyChanged(); } } }
        string _StatusOnvif = string.Empty;
        public string StatusOnvif { get => _StatusOnvif; set { if (_StatusOnvif != value) { _StatusOnvif = value; OnPropertyChanged(); } } }
        //WriteableBitmap? _Image = null;
        public WriteableBitmap? Image { get => _player.Image; }
        public Array Modes { get; private set; } = Enum.GetValues(typeof(BasicHttpSecurityMode));
        public Array CredentialTypes { get; private set; } = Enum.GetValues(typeof(HttpClientCredentialType));
        BasicHttpSecurityMode _Mode = BasicHttpSecurityMode.TransportCredentialOnly;
        public BasicHttpSecurityMode Mode { get => _Mode; set { if (_Mode != value) { _Mode = value; OnPropertyChanged(); } } }

        HttpClientCredentialType _CredentialType = HttpClientCredentialType.Digest;
        public HttpClientCredentialType CredentialType { get => _CredentialType; set { if (_CredentialType != value) { _CredentialType = value; OnPropertyChanged(); } } }
        public ObservableCollection<MediaToken> Profiles { get; private set; } = new();
        MediaToken? _Profile;
        public MediaToken? Profile { get => _Profile; set { if (_Profile != value) { _Profile = value;OnPropertyChanged(); OnProfileChanged(); CommandManager.InvalidateRequerySuggested(); } } }
        string _Rtsp = string.Empty;
        public string Rtsp { get => _Rtsp; set { if (_Rtsp != value) { _Rtsp = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } } }

        public BCommand BCPlay {  get; private set; }
        public BCommand BCStop { get; private set; }

        public event EventHandler<string>? StatusChanged;
        OnVifDev _device = new();
        FFMPGPlayer _player = new();
        SecureString _pass;
        public VMPageOnVif()
        {
            _device.Status += _device_Status;
            _player.OnSatus += _device_Status;
            _player.OnError += _device_Status;
            _player.OnImageSourceChanged += OmImage;
            _player.OnPlayerSratusChanged += OnPlayerSratusChanged;
            BCPlay = new((o)=> Play(), (o)=>UriHelper.IsValidAbsoluteUri(Rtsp));
            BCStop = new((o) => _player.Stop(), (o)=>_player.PlayerSratus== PlayerSratus.Play);
            CommandManager.InvalidateRequerySuggested();
        }
        void OnPlayerSratusChanged(object? o, PlayerSratus status)
        {
            _device_Status(o, $"Player Status: {status}");
            CommandManager.InvalidateRequerySuggested();
        }
        void OmImage(object? o, WriteableBitmap bitmap)
        {
            OnPropertyChanged(nameof(Image));
        }

        public void Connect(SecureString pass) {
            _pass = pass; Task.Run(() => ConnectAsync(pass));
        }

        void Play() =>Task.Run(() => _player.Play(Rtsp));
        

        async Task ConnectAsync(SecureString pass)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() => Profiles.Clear());
                StatusOnvif = string.Empty;
                StatusChanged?.Invoke(this, "Connecting to OnVifDevice");
                CommandManager.InvalidateRequerySuggested();
                var res = await _device.Connect(Url, pass, Login);
                if (res)
                {
                    StatusChanged?.Invoke(this, $"OnVifDevice Connected. Found {_device.MediaTokens.Count} media");
                    foreach (var token in _device.MediaTokens)
                        Application.Current.Dispatcher.Invoke(() => Profiles.Add(token));
                    Profile = Profiles.FirstOrDefault();
                }
                else StatusChanged?.Invoke(this, $"OnVifDevice:  No media found");
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, "Exception while Connecting to OnVifDevice");
                _device_Status(this, "Exception while Connecting to OnVifDevice");
            }
        }
        private void OnProfileChanged()
        {

            if (Profile == null)
            {
                Rtsp = "";
                return;
            }
            Application.Current.Dispatcher.Invoke(() => { Rtsp = UriHelper.UrlToURLWithCredentials(Profile.Rtsp, Login, UnsecureString(_pass));  Play(); });
        }
        private void _device_Status(object? sender, string e)
            =>Application.Current.Dispatcher.Invoke(()=> 
                StatusOnvif = $"{StatusOnvif}{DateTime.Now.ToString("HH:mm:ss")} {e}\r");

        public void Dispose()
        {
            _player.Stop();
        }
        static string UnsecureString(SecureString secure)
        {
            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.SecureStringToGlobalAllocUnicode(secure);
                return Marshal.PtrToStringUni(ptr)!;
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(ptr);
            }
        }
    }
}
