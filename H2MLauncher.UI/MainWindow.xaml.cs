using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

using H2MLauncher.Core.ViewModels;
using H2MLauncher.UI.Dialog;

namespace H2MLauncher.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ICollectionView _collectionView;
        private readonly DialogService _dialogService;

        public MainWindow(ServerBrowserViewModel serverBrowserViewModel, DialogService dialogService)
        {
            InitializeComponent();
            DataContext = serverBrowserViewModel;
            serverBrowserViewModel.RefreshServersCommand.Execute(this);
            _collectionView = CollectionViewSource.GetDefaultView(serverBrowserViewModel.Servers);
            _collectionView.Filter = o => string.IsNullOrEmpty(serverBrowserViewModel.Filter) ? true : ((ServerViewModel)o).HostName.ToLower().Contains(serverBrowserViewModel.Filter.ToLower());
            _collectionView.SortDescriptions.Add(new SortDescription("Occupation", ListSortDirection.Descending));
            _dialogService = dialogService;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
                _dialogService.OpenTextDialog("AYOO", "DO YOU EVEN LIFT?");
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
            _collectionView.Refresh();
        }
    }
}