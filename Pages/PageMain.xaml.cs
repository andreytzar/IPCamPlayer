using IPCamPlayer.Helpers.VM;
using System.Windows.Controls;

namespace IPCamPlayer.Pages
{
    public partial class PageMain : Page
    {
        public event EventHandler<string> StatusChanged { add { vm.statusChanged += value; } remove { vm.statusChanged -= value; } }
        VMPageMain vm;
        public PageMain(EventHandler<string> statusChanged)
        {
            InitializeComponent();
            DataContext = vm = new();
            StatusChanged += statusChanged;
        }
    }
    public class VMPageMain:VMNotifyPropretyChanged
    {
        internal event EventHandler<string>? statusChanged;
        public VMPageMain()
        {

        }
    }
}
