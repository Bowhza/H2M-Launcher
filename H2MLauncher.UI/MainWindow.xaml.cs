using H2MLauncher.Core.ViewModels;
using System.Windows;

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
    }
}