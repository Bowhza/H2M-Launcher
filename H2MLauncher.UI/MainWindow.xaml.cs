using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

using H2MLauncher.Core.ViewModels;

namespace H2MLauncher.UI
{
    public partial class MainWindow : Window
    {
        private readonly ServerBrowserViewModel _viewModel;

        public MainWindow(ServerBrowserViewModel serverBrowserViewModel)
        {
            InitializeComponent();
            DataContext = _viewModel = serverBrowserViewModel;

            foreach (var tab in serverBrowserViewModel.ServerTabs)
            {
                InitializeServerFilterAndSorting(tab);
            }

            serverBrowserViewModel.RefreshServersCommand.Execute(this);
        }

        void InitializeServerFilterAndSorting(ServerTabViewModel tab)
        {
            ICollectionView collectionView = CollectionViewSource.GetDefaultView(tab.Servers);
            collectionView.Filter = o => _viewModel.ServerFilter((ServerViewModel)o);
            collectionView.SortDescriptions.Add(new SortDescription(nameof(ServerViewModel.ClientNum), ListSortDirection.Descending));
            collectionView.SortDescriptions.Add(new SortDescription(nameof(ServerViewModel.Ping), ListSortDirection.Ascending));
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

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(_viewModel.SelectedTab.Servers).Refresh();
        }

        private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            //if (_viewModel.JoinServerCommand.CanExecute(null))
            //{
            //    _viewModel.JoinServerCommand.Execute(null);
            //}
        }

        private void DataGridRow_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGridRow row)
            {
                return;
            }

            if (row.DataContext is not ServerViewModel serverVM)
            {
                return;
            }

            if (_viewModel.CopyToClipBoardCommand.CanExecute(serverVM))
            {
                _viewModel.CopyToClipBoardCommand.Execute(serverVM);
            }
        }

        private void DataGridRow_GotFocus(object sender, RoutedEventArgs e)
        {
            ((DataGridRow)sender).IsSelected = true;
        }
    }
}