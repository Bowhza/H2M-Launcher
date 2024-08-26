using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;
using H2MLauncher.Core.ViewModels;

namespace H2MLauncher.UI
{
    public partial class MainWindow : Window
    {
        private readonly ICollectionView _collectionView;
        private readonly ICollectionView _collectionViewFavorites;

        private readonly ServerBrowserViewModel _viewModel;

        private TabsEnum _selectedTab;

        private IPasswordDialogService _passwordDialogService;

        public MainWindow(ServerBrowserViewModel serverBrowserViewModel)
        {
            InitializeComponent();
            _passwordDialogService = new PasswordDialogService();
            _selectedTab = TabsEnum.AllServers;
            DataContext = _viewModel = serverBrowserViewModel;
            serverBrowserViewModel.RefreshServersCommand.Execute(this);
            _collectionView = CollectionViewSource.GetDefaultView(serverBrowserViewModel.Servers);
            _collectionView.Filter = o => _viewModel.ServerFilter((ServerViewModel)o);
            _collectionView.SortDescriptions.Add(new SortDescription("ClientNum", ListSortDirection.Descending));

            _collectionViewFavorites = CollectionViewSource.GetDefaultView(serverBrowserViewModel.FavoriteServers);
            _collectionViewFavorites.Filter = o => _viewModel.ServerFilter((ServerViewModel)o);
            _collectionViewFavorites.SortDescriptions.Add(new SortDescription("ClientNum", ListSortDirection.Descending));

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
            _collectionView.Refresh();
            _collectionViewFavorites.Refresh();
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl)
            {
                TabItem selectedTab = ((sender as TabControl).SelectedItem as TabItem);
                if (selectedTab.Header.ToString() == "All Servers")
                {
                    _selectedTab = TabsEnum.AllServers;

                    _viewModel.TotalPlayers = _viewModel.TotalPlayersOverAll;
                    _viewModel.TotalServers = _viewModel.TotalServersOverAll;

                }
                else if (selectedTab.Header.ToString() == "Favourites")
                {
                    _selectedTab = TabsEnum.Favorites;

                    _viewModel.TotalPlayers = _viewModel.TotalPlayersFavorites;
                    _viewModel.TotalServers = _viewModel.FavoriteServers.Count;
                }
            }
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