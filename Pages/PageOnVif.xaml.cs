using IPCamPlayer.Classes;
using IPCamPlayer.Helpers.VM;
using System.Security;
using System.Windows.Controls;

namespace IPCamPlayer.Pages
{
    public partial class PageOnVif : Page
    {
        public event EventHandler<string> StatusChanged { add { vm.statusChanged += value; } remove { vm.statusChanged -= value; } }
        VMPageOnVif vm;
        public PageOnVif(EventHandler<string> statusChanged)
        {
            InitializeComponent();
            DataContext = vm = new();
            StatusChanged += statusChanged;
        }

        private void Connect_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            SecureString securePassword = passBox.SecurePassword;
            vm.Connect(securePassword);
        }
    }
    public class VMPageOnVif: VMNotifyPropretyChanged
    {
        string _Login = string.Empty;
        public string Login { get => _Login; set { if (_Login != value) { _Login = value; OnPropertyChanged(); } } }
        string _Url = "http://10.254.158.2:90/";
        public string Url { get => _Url; set { if (_Url != value) { _Url = value; OnPropertyChanged(); } } }

        string _StatusOnvif = string.Empty;
        public string StatusOnvif { get => _StatusOnvif; set { if (_StatusOnvif != value) { _StatusOnvif = value; OnPropertyChanged(); } } }
        
        internal event EventHandler<string>? statusChanged;
        OnVifDev _device = new();
        public VMPageOnVif()
        {
            _device.Status += _device_Status;
        }


        public void Connect(SecureString pass) => Task.Run(() => ConnectAsync(pass));


        async Task ConnectAsync(SecureString pass)
        {
            StatusOnvif=string.Empty;
            var res = await _device.Connect(Url, pass,Login);
        }

        private void _device_Status(object? sender, string e)
        =>StatusOnvif = $"{StatusOnvif}{DateTime.Now.ToString("HH:mm:ss")} {e}\r";
        
    }
}
