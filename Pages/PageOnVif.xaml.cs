using IPCamPlayer.Classes;
using IPCamPlayer.Classes.FFMPG;
using IPCamPlayer.Helpers.VM;
using System.Security;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace IPCamPlayer.Pages
{
    public partial class PageOnVif : Page
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
        =>vm.Connect(passBox.SecurePassword);
        
    }
    public class VMPageOnVif : VMNotifyPropretyChanged
    {
        string _Login = string.Empty;
        public string Login { get => _Login; set { if (_Login != value) { _Login = value; OnPropertyChanged(); } } }
        string _Url = "http://10.254.158.2:90/";
        public string Url { get => _Url; set { if (_Url != value) { _Url = value; OnPropertyChanged(); } } }

        string _StatusOnvif = string.Empty;
        public string StatusOnvif { get => _StatusOnvif; set { if (_StatusOnvif != value) { _StatusOnvif = value; OnPropertyChanged(); } } }
        WriteableBitmap? _Image = null;
        public WriteableBitmap? Image { get => _Image; set { if (_Image != value) { _Image = value; OnPropertyChanged(); } } }

        public event EventHandler<string>? StatusChanged;
        OnVifDev _device = new();
        FFMPGPlayer _player=new();
        public VMPageOnVif()
        {
            _device.Status += _device_Status;
            _player.OnSatus += _device_Status;
            _player.OnError += _device_Status;
            _player.OnImageSourceChanged += OmImage;
            Task.Run(() => _player.PlayRtspInternal("rtsp://user2:Port45Fops3E@185.0.1.14:554/Streaming/Channels/1502"));
        }

        void OmImage(object? o, WriteableBitmap bitmap)
        {
            Image=bitmap;
        }

        public void Connect(SecureString pass) => Task.Run(() => ConnectAsync(pass));


        async Task ConnectAsync(SecureString pass)
        {
            try
            {
                StatusOnvif = string.Empty;
                StatusChanged?.Invoke(this, "Connecting to OnVifDevice");
                CommandManager.InvalidateRequerySuggested();
                var res = await _device.Connect(Url, pass, Login);
                if (res) StatusChanged?.Invoke(this, $"OnVifDevice Connected. Found {_device.MediaTokens.Count} media");
                else StatusChanged?.Invoke(this, $"OnVifDevice:  No media found");
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, "Exception while Connecting to OnVifDevice");
                _device_Status(this, "Exception while Connecting to OnVifDevice");
            }
        }

        private void _device_Status(object? sender, string e)
        => StatusOnvif = $"{StatusOnvif}{DateTime.Now.ToString("HH:mm:ss")} {e}\r";

    }
}
