using System.Runtime.InteropServices;

namespace H2MLauncher.UI
{
    internal static class WindowUtils
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        private const uint GW_HWNDNEXT = 2;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;

        private const nint HWND_BOTTOM = 1;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;       // x position of lower-right corner
            public int Bottom;      // y position of lower-right corner
        }

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        // Method to make window non-focusable
        public static void MakeWindowNonFocusable(IntPtr hWnd)
        {
            // Get current window style
            int style = GetWindowLong(hWnd, GWL_EXSTYLE);

            // Add WS_EX_NOACTIVATE to make the window non-activating
            SetWindowLong(hWnd, GWL_EXSTYLE, style | WS_EX_NOACTIVATE);
        }


        // Move the window just one level behind the current top window
        public static void SendWindowToBack(IntPtr hWnd)
        {
            // Get the window just below the current one
            IntPtr nextWindow = GetWindow(hWnd, GW_HWNDNEXT);

            if (nextWindow != IntPtr.Zero)
            {
                SetWindowPos(hWnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
        }
    }
}