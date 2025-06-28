using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core.Game;
using H2MLauncher.UI.Dialog;
using H2MLauncher.UI.Dialog.Views;
using H2MLauncher.UI.Services;
using H2MLauncher.UI.View.Controls;
using H2MLauncher.UI.ViewModels;


namespace H2MLauncher.UI
{
    [ObservableObject]
    public partial class MainWindow : Window
    {
        private readonly ServerBrowserViewModel _viewModel;
        private readonly OverlayHelper _overlayHelper;
        private readonly H2MCommunicationService _h2MCommunicationService;
        private bool _isFirstRender = true;

        [ObservableProperty]
        private bool _isPartyExpanded = true;

        [ObservableProperty]
        private CustomizationManager _customization;

        private readonly DialogService _dialogService;

        public ICommand ToggleOverlayCommand { get; }

        public MainWindow(
            ServerBrowserViewModel serverBrowserViewModel,
            H2MCommunicationService h2MCommunicationService,
            CustomizationManager customizationManager,
            DialogService dialogService)
        {
            InitializeComponent();

            DataContext = _viewModel = serverBrowserViewModel;
            _overlayHelper = new OverlayHelper(this);
            _h2MCommunicationService = h2MCommunicationService;
            Customization = customizationManager;

            serverBrowserViewModel.ServerFilterChanged += ServerBrowserViewModel_ServerFilterChanged;

            ToggleOverlayCommand = new RelayCommand(ToggleOverlay);

            _ = customizationManager.LoadInitialValues();
            _dialogService = dialogService;
        }

        private void CustomizeButton_Click(object sender, RoutedEventArgs e)
        {
            _dialogService.OpenDialog<CustomizationDialogView, CustomizationDialogViewModel>();
        }

        private void PartyViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not PartyViewModel partyViewModel)
            {
                return;
            }

            if (e.PropertyName == nameof(PartyViewModel.HasOtherMembers) && partyViewModel.HasOtherMembers)
            {
                // Auto expand the party when >1 members are available.
                IsPartyExpanded = true;
            }

            if (!partyViewModel.IsPartyActive)
            {
                // Collapse the party when not active
                IsPartyExpanded = false;
            }
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            if (!_isFirstRender)
            {
                return;
            }

            _isFirstRender = false;
            _viewModel.RefreshServersCommand.Execute(this);
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
            _viewModel.SelectedTab?.ServerCollectionView.Refresh();
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
            _viewModel.SelectedTab?.ServerCollectionView.Refresh();
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

        private void Border_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Clip the border to have rounded corners even with background extending beyond
            var control = (Border)sender;
            control.Clip = new RectangleGeometry
            {
                Rect = new Rect(0, 0, control.ActualWidth, control.ActualHeight),
                RadiusX = 10,
                RadiusY = 10
            };
        }

        private void MediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (sender is MediaElement media)
            {
                // loop the video
                media.Position = media.Source.AbsolutePath.EndsWith(".gif", StringComparison.InvariantCultureIgnoreCase)
                    ? TimeSpan.FromMilliseconds(1)
                    : TimeSpan.Zero;
                media.Play();
            }
        }

        private void MediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            Customization.OnBackgroundMediaFailed(e.ErrorException);
        }

        private void MediaElement_MediaOpened(object sender, RoutedEventArgs e)
        {
            Customization.OnBackgroundMediaLoaded();
        }

        private void OverflowedTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem) return;
            if (menuItem.DataContext is IServerTabViewModel tabViewModel)
            {
                _viewModel.SelectedTab = tabViewModel;
                return;
            }

            if (menuItem.DataContext is not TabItem tabItem) return;

            tabItem.IsSelected = true;
        }

        private CustomPopupPlacement[] PlaceTabsOverflowPopup(Size popupSize, Size targetSize, Point offset)
        {
            // Bottom-right relative to the placement target
            Point bottomRight = new Point(targetSize.Width - popupSize.Width, targetSize.Height);

            // Ensure popup is placed at bottom-right
            return [new CustomPopupPlacement(bottomRight, PopupPrimaryAxis.Horizontal)];
        }

        private void ServerTabControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Set the max width of the tab header panel to be left of overflow button
            if (ServerTabControl.Template.FindName("HeaderPanel", ServerTabControl) is OverflowTabPanel overflowTabPanel)
            {
                var relativeButtonPosition = HeaderControlsBorder
                    .TransformToVisual(ServerTabControl)
                    .Transform(new(-5, 0));

                overflowTabPanel.MaxWidth = relativeButtonPosition.X;
            }
        }
    }
}