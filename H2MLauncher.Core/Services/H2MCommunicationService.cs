using System.Diagnostics;
using System.Runtime.InteropServices;

namespace H2MLauncher.Core.Services
{
    public class H2MCommunicationService(IErrorHandlingService errorHandlingService)
    {
        private readonly IErrorHandlingService _errorHandlingService = errorHandlingService ?? throw new ArgumentNullException(nameof(errorHandlingService));

        //Windows API functions to send input to a window
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        //Windows API constants
        internal const int WM_CHAR = 0x0102; // Message code for sending a character
        internal const int WM_KEYDOWN = 0x0100; // Message code for key down
        internal const int WM_KEYUP = 0x0101;   // Message code for key up

        public void LaunchH2MMod()
        {
            ReleaseCapture();

            try
            {
                // Check if the process is already running
                Process? runningProcess = Process.GetProcessesByName("h2m-mod").FirstOrDefault();

                if (runningProcess != null)
                {
                    _errorHandlingService.HandleError("h2m-mod.exe is already running.");
                    return;
                }

                // Proceed to launch the process if it's not running
                if (File.Exists("./h2m-mod.exe"))
                    Process.Start("./h2m-mod.exe");
                else
                {
                    _errorHandlingService.HandleException(new FileNotFoundException("h2m-mod.exe was not found."), "The h2m-mod.exe could not be found!");
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleException(ex, "Error launching h2m-mod.");
            }
        }

        public void JoinServer(string ip, string port)
        {
            string command = $"connect {ip}:{port}";
            IntPtr h2mModWindow = FindH2MModWindow();

            ReleaseCapture();

            if (h2mModWindow != IntPtr.Zero)
            {
                //Open In Game Terminal Window
                SendMessage(h2mModWindow, WM_KEYDOWN, (IntPtr)192, IntPtr.Zero);

                //Send the "connect" command to the terminal window
                foreach (char c in command)
                {
                    SendMessage(h2mModWindow, WM_CHAR, (IntPtr)c, IntPtr.Zero);
                    Thread.Sleep(1);
                }

                //Sleep for 1ms to allow the command to be processed
                Thread.Sleep(1);

                //Simulate pressing the Enter key
                SendMessage(h2mModWindow, WM_KEYDOWN, (IntPtr)13, IntPtr.Zero);
                SendMessage(h2mModWindow, WM_KEYUP, (IntPtr)13, IntPtr.Zero);

                SendMessage(h2mModWindow, WM_KEYDOWN, (IntPtr)192, IntPtr.Zero);

                //Set H2M to foreground window
                SetForegroundWindow(h2mModWindow);
            }
            else
            {
                _errorHandlingService.HandleError("Could not find the h2m-mod terminal window.");
            }
        }

        private static IntPtr FindH2MModWindow()
        {
            foreach (Process proc in Process.GetProcesses())
            {
                // Find the process by name and check if the main window title contains "h2m-mod"
                if (proc.MainWindowTitle.Contains("h2m-mod", StringComparison.OrdinalIgnoreCase))
                {
                    return proc.MainWindowHandle;
                }
            }
            return IntPtr.Zero;
        }
    }
}
