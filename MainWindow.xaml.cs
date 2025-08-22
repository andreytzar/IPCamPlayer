
using IPCamPlayer.ViewModels;
using System.Windows;
using System.Windows.Controls;


namespace IPCamPlayer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        VMMain vm;
        public MainWindow()
        {
            InitializeComponent();
            DataContext= vm = new(OnActivateChanged);

        }

        private void NavBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
                if (btn.DataContext is VMNavBtn navbtn)
                    OnActivateChanged(vm, navbtn);
        }
        void OnActivateChanged(object? sender, VMNavBtn navbtn)
        {
            if (sender is VMMain vM)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    frame.Navigate(navbtn.Page);
                    foreach (var nav in vM.NavBtns)
                        nav.IsActive = false;
                    navbtn.IsActive = true;
                });
        }


    }
}