using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core.Game;
using H2MLauncher.UI.ViewModels;

namespace H2MLauncher.UI
{
    public partial class MainWindow : Window
    {
        private readonly ServerBrowserViewModel _viewModel;
        private readonly OverlayHelper _overlayHelper;
        private readonly H2MCommunicationService _h2MCommunicationService;

        public ICommand ToggleOverlayCommand { get; }

        public MainWindow(ServerBrowserViewModel serverBrowserViewModel, H2MCommunicationService h2MCommunicationService)
        {
            InitializeComponent();

            DataContext = _viewModel = serverBrowserViewModel;
            _overlayHelper = new OverlayHelper(this);
            _h2MCommunicationService = h2MCommunicationService;

            serverBrowserViewModel.ServerFilterChanged += ServerBrowserViewModel_ServerFilterChanged;
            serverBrowserViewModel.RefreshServersCommand.Execute(this);

            ToggleOverlayCommand = new RelayCommand(ToggleOverlay);
        }

        void ToggleOverlay()
        {
            IntPtr hWndGame = _h2MCommunicationService.GetGameWindowHandle();

            _overlayHelper.CenterOverlayRelativeTo = hWndGame;
            _overlayHelper.ShowOverlay = !_overlayHelper.ShowOverlay;

            if (!_overlayHelper.ShowOverlay && hWndGame != IntPtr.Zero)
            {
                WindowUtils.SetForegroundWindow(hWndGame);
            }
        }

        private void ServerBrowserViewModel_ServerFilterChanged()
        {
            _viewModel.SelectedTab.ServerCollectionView.Refresh();
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

            if (e.Key == Key.LeftAlt)
            {
                e.Handled = true;
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState is WindowState.Minimized)
            {
                _overlayHelper.ShowOverlay = false;
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _viewModel.SelectedTab.ServerCollectionView.Refresh();
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState is WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
            MaximizeButtonText.Text = WindowState is WindowState.Normal ? "🗖︎" : "🗗︎";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.PartyViewModel.JoinPartyCommand.Execute(JoinIdTextBox.Text);
        }
    }
}