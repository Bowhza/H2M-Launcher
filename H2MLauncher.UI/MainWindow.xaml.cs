using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

using H2MLauncher.Core.ViewModels;
using H2MLauncher.UI.Model;

namespace H2MLauncher.UI
{
    public partial class MainWindow : Window
    {
        private readonly ICollectionView _collectionView;

        private readonly ServerBrowserViewModel _viewModel;
        private IntPtr _targetWindowHandle;
        private bool _isOverlayVisible = true;
        private bool _overlayHiddenByUser = false;

        private GlobalKeyboardHook _globalKeyboardHook;

        // Import Windows API methods for interacting with windows

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

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
            serverBrowserViewModel.RefreshServersCommand.Execute(this);
            _collectionView = CollectionViewSource.GetDefaultView(serverBrowserViewModel.Servers);
            _collectionView.Filter = o => _viewModel.ServerFilter((ServerViewModel)o);
            _collectionView.SortDescriptions.Add(new SortDescription("ClientNum", ListSortDirection.Descending));

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

            // Optionally, you can update its position
            UpdateOverlayPosition(null, null);
        }
        private void HideOverlay()
        {
            _overlayHiddenByUser = true; // Mark overlay as hidden by user
            this.Visibility = Visibility.Hidden; // Simply hide the overlay
        }

        private void UpdateOverlayPosition(object sender, EventArgs e)
        {
            if (_targetWindowHandle == IntPtr.Zero)
            {
                return;
            }

            // Check if the target window is minimized
            if (IsIconic(_targetWindowHandle))
            {
                this.Visibility = Visibility.Hidden; // Hide the overlay if the target window is minimized
            }
            else if (!_overlayHiddenByUser) // Only update if not hidden by the user
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
            if (_targetWindowHandle == IntPtr.Zero)
            {
                return;
            }

            // Get the target window's position and size
            if (GetWindowRect(_targetWindowHandle, out RECT rect))
            {
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
                // Show the overlay when Ctrl + Alt + S is pressed
                ShowOverlay();
            }
            else if (key == Key.H && modifiers.HasFlag(ModifierKeys.Control) && modifiers.HasFlag(ModifierKeys.Alt))
            {
                // Hide the overlay when Ctrl + Alt + H is pressed
                HideOverlay();
            }
        }
    }
}