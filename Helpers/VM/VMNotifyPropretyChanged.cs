using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IPCamPlayer.Helpers.VM
{
    public class VMNotifyPropretyChanged : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
