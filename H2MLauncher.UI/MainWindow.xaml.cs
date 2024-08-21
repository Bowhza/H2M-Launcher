using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

using H2MLauncher.Core.ViewModels;

namespace H2MLauncher.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ICollectionView collectionView;

        public MainWindow(ServerBrowserViewModel serverBrowserViewModel)
        {
            InitializeComponent();
            DataContext = serverBrowserViewModel;
            serverBrowserViewModel.RefreshServersCommand.Execute(this);
            collectionView = CollectionViewSource.GetDefaultView(serverBrowserViewModel.Servers);
            collectionView.Filter = o => string.IsNullOrEmpty(serverBrowserViewModel.Filter) ? true : ((ServerViewModel)o).HostName.Contains(serverBrowserViewModel.Filter);
            collectionView.SortDescriptions.Add(new SortDescription("Occupation", ListSortDirection.Descending));
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

        private void TextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            collectionView.Refresh();
        }
    }
}