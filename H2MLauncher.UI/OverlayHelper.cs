using System.Windows;
using System.Windows.Interop;

namespace H2MLauncher.UI
{
    internal class OverlayHelper(Window window)
    {
        private readonly WindowInteropHelper _interopHelper = new(window);

        private bool _showOverlay;
        public bool ShowOverlay
        {
            get => _showOverlay;
            set
            {
                if (_showOverlay == value)
                {
                    return;
                }

                _showOverlay = value;
                OnIsOverlayChanged();
            }
        }

        private bool _makeNonFocusable;
        public bool MakeNonFocusable
        {
            get => _makeNonFocusable;
            set
            {
                if (_makeNonFocusable == value)
                {
                    return;
                }

                if (!value && _showOverlay)
                {
                    ComponentDispatcher.ThreadFilterMessage -= new ThreadMessageEventHandler(ComponentDispatcher_ThreadFilterMessage);
                }

                if (value && _showOverlay)
                {
                    ComponentDispatcher.ThreadFilterMessage += new ThreadMessageEventHandler(ComponentDispatcher_ThreadFilterMessage);
                }

                _makeNonFocusable = value;
            }
        }

        private void OnIsOverlayChanged()
        {
            IntPtr hWnd = _interopHelper.Handle;
            if (!_showOverlay)
            {
                if (_makeNonFocusable)
                {
                    ComponentDispatcher.ThreadFilterMessage -= new ThreadMessageEventHandler(ComponentDispatcher_ThreadFilterMessage);
                }

                WindowUtils.SendWindowToBack(hWnd);

                window.Topmost = false;
                window.Opacity = 1;
            }
            else
            {
                if (_makeNonFocusable)
                {
                    // Handle the window activation event to keep the non-focus behavior consistent
                    ComponentDispatcher.ThreadFilterMessage += new ThreadMessageEventHandler(ComponentDispatcher_ThreadFilterMessage);
                    WindowUtils.MakeWindowNonFocusable(hWnd);
                }

                window.Topmost = true;
                window.Opacity = 0.8;
            }
        }

        // Handle Windows messages to prevent the window from taking focus
        private static void ComponentDispatcher_ThreadFilterMessage(ref MSG msg, ref bool handled)
        {
            const int WM_MOUSEACTIVATE = 0x0021;

            if (msg.message == WM_MOUSEACTIVATE)
            {
                // Prevent the window from taking focus
                handled = true;
            }
        }
    }
}