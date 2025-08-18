using IPCamPlayer.Helpers.VM;
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
    }
    public class VMPageOnVif: VMNotifyPropretyChanged
    {
        internal event EventHandler<string>? statusChanged;
        public VMPageOnVif()
        {

        }
    }
}
