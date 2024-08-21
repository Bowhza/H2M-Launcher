using H2MLauncher.Core.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace H2MLauncher.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(ServerBrowserViewModel serverBrowserViewModel)
        {
            InitializeComponent();
            DataContext = serverBrowserViewModel;
            serverBrowserViewModel.RefreshServersCommand.Execute(this);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }
    }
}