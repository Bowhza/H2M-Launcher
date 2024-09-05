using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using H2MLauncher.UI.Model;
using H2MLauncher.UI.ViewModels;

namespace H2MLauncher.UI
{
    public partial class MainWindow : Window
    {
        private readonly ServerBrowserViewModel _viewModel;
        private IntPtr _targetWindowHandle;
        private bool _isOverlayVisible = true;
        private bool _overlayHiddenByUser = false;
        private readonly GlobalKeyboardHook _globalKeyboardHook;

        // Import Windows API methods for interacting with windows

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);


        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsIconic(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        public MainWindow(ServerBrowserViewModel serverBrowserViewModel)
        {
            InitializeComponent();

            DataContext = _viewModel = serverBrowserViewModel;


            serverBrowserViewModel.ServerFilterChanged += ServerBrowserViewModel_ServerFilterChanged;

            serverBrowserViewModel.RefreshServersCommand.Execute(this);

            // Initialize Global Keyboard Hook
            _globalKeyboardHook = new GlobalKeyboardHook();
            _globalKeyboardHook.KeyPressed += GlobalKeyboardHook_KeyPressed;

            // Configure the window to be an overlay
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = System.Windows.Media.Brushes.Transparent;
            this.Topmost = true;
            this.ResizeMode = ResizeMode.NoResize;
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

        private void ShowOverlay()
        {
            _overlayHiddenByUser = false; // Mark overlay as shown by user
            _isOverlayVisible = true; // Ensure overlay is visible
            this.Visibility = Visibility.Visible;
            this.Topmost = true;
            // Optionally, you can update its position
            UpdateOverlayPosition();
        }
        private void HideOverlay()
        {
            this.Topmost = false;
            _overlayHiddenByUser = true; // Mark overlay as hidden by user
            this.Visibility = Visibility.Hidden; // Simply hide the overlay
        }

        private void UpdateOverlayPosition()
        {
            if (_targetWindowHandle == IntPtr.Zero) return;

            if (IsIconic(_targetWindowHandle)) { this.Visibility = Visibility.Hidden; return; }

            // Only update if not hidden by the user
            if (!_overlayHiddenByUser)
            {
                // Show or hide the overlay based on the user's toggle action
                this.Visibility = _isOverlayVisible ? Visibility.Visible : Visibility.Hidden;

                // Set the overlay position with a default offset (you can adjust the offset value here)
                SetOverlayPosition();
            }
        }

        // Method to set the overlay position centered within the game window
        private void SetOverlayPosition()
        {
            if (_targetWindowHandle == IntPtr.Zero || !GetWindowRect(_targetWindowHandle, out RECT rect))
                return;

            // Calculate the width and height of the game window
            int gameWindowWidth = rect.Right - rect.Left;
            int gameWindowHeight = rect.Bottom - rect.Top;

            // Calculate position to center the overlay within the game window
            int overlayWidth = (int)this.ActualWidth;
            int overlayHeight = (int)this.ActualHeight;

            int xPosition = rect.Left + (gameWindowWidth - overlayWidth) / 2;
            int yPosition = rect.Top + (gameWindowHeight - overlayHeight) / 2;

            // Move the WPF window to the calculated position
            this.Left = xPosition;
            this.Top = yPosition;
        }

        private void AttachToH2MModWindow()
        {
            _targetWindowHandle = FindWindow(null, "H2M-Mod");

            if (_targetWindowHandle == IntPtr.Zero)
            {
                MessageBox.Show("H2M-Mod window not found.");
                return;
            }
        }

        private void GlobalKeyboardHook_KeyPressed(Key key, ModifierKeys modifiers)
        {
            if (key == Key.S && modifiers.HasFlag(ModifierKeys.Control) && modifiers.HasFlag(ModifierKeys.Alt))
            {
                if (_overlayHiddenByUser)
                {
                    ShowOverlay();
                }
                else
                {
                    HideOverlay();
                }
            }

        }
    }
}