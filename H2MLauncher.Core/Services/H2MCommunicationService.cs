using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

using static H2MLauncher.Core.Services.H2MCommunicationService;

namespace H2MLauncher.Core.Services
{
    public class H2MCommunicationService(IErrorHandlingService errorHandlingService)
    {
        private readonly IErrorHandlingService _errorHandlingService = errorHandlingService ?? throw new ArgumentNullException(nameof(errorHandlingService));

        //Windows API functions to send input to a window
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        internal static IEnumerable<IntPtr> EnumerateProcessWindowHandles(int processId)
        {
            var handles = new List<IntPtr>();

            foreach (ProcessThread thread in Process.GetProcessById(processId).Threads)
                EnumThreadWindows(thread.Id,
                    (hWnd, lParam) => { handles.Add(hWnd); return true; }, IntPtr.Zero);

            return handles;
        }


        [DllImport("user32.dll")]
        internal static extern bool EnumThreadWindows(int dwThreadId, EnumWindowsProc lpfn,
           IntPtr lParam);

        internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        internal static IEnumerable<IntPtr> EnumerateWindowHandles()
        {
            var handles = new List<IntPtr>();

            EnumWindows((hWnd, lParam) => { handles.Add(hWnd); return true; }, IntPtr.Zero);

            return handles;
        }

        [DllImport("user32.dll")]
        internal static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        private static string? GetWindowTitle(IntPtr hWnd)
        {
            const int length = 256;
            StringBuilder sb = new(length);

            if (GetWindowText(hWnd, sb, length) > 0)
            {
                return sb.ToString();
            }

            return null;
        }


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
                    _errorHandlingService.HandleException(
                        new FileNotFoundException("h2m-mod.exe was not found."),
                        "The h2m-mod.exe could not be found!");
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleException(ex, "Error launching h2m-mod.");
            }
        }

        public void JoinServer(string ip, string port)
        {
            const string disconnectCommand = "disconnect";
            string connectCommand = $"connect {ip}:{port}";

            Process? h2mModProcess = FindH2MModProcess();
            if (h2mModProcess == null)
            {
                _errorHandlingService.HandleError("Could not find the h2m-mod terminal window.");
                return;
            }

            IntPtr conHostHandle = FindH2MConHostProcess();

            // Grab the handle of conhost or main window
            nint hWindow = conHostHandle == IntPtr.Zero ? h2mModProcess.MainWindowHandle : conHostHandle;

            ReleaseCapture();

            // Open In Game Terminal Window
            SendMessage(hWindow, WM_KEYDOWN, 192, IntPtr.Zero);

            // Send the "disconnect" command to the terminal window
            foreach (char c in disconnectCommand)
            {
                SendMessage(hWindow, WM_CHAR, c, IntPtr.Zero);
                Thread.Sleep(1);
            }

            // Sleep for 1ms to allow the command to be processed
            Thread.Sleep(1);

            // Simulate pressing the Enter key
            SendMessage(hWindow, WM_KEYDOWN, 13, IntPtr.Zero);
            SendMessage(hWindow, WM_KEYUP, 13, IntPtr.Zero);

            // Send the "connect" command to the terminal window
            foreach (char c in connectCommand)
            {
                SendMessage(hWindow, WM_CHAR, c, IntPtr.Zero);
                Thread.Sleep(1);
            }

            // Sleep for 1ms to allow the command to be processed
            Thread.Sleep(1);

            // Simulate pressing the Enter key
            SendMessage(hWindow, WM_KEYDOWN, 13, IntPtr.Zero);
            SendMessage(hWindow, WM_KEYUP, 13, IntPtr.Zero);

            SendMessage(hWindow, WM_KEYDOWN, 192, IntPtr.Zero);

            // Set H2M to foreground window
            var hGameWindow = FindH2MModGameWindow(h2mModProcess);
            SetForegroundWindow(h2mModProcess.MainWindowHandle);
        }

        private static IntPtr FindH2MConHostProcess()
        {
            foreach (var handle in EnumerateWindowHandles())
            {
                string? title = GetWindowTitle(handle);
                if (title != null && title.Contains("h2m-mod", StringComparison.OrdinalIgnoreCase))
                {
                    GetWindowThreadProcessId(handle, out var processId);

                    if (title == GAME_WINDOW_TITLE)
                    {
                        continue;
                    }

                    var associatedProcess = Process.GetProcessById((int)processId);
                    if (associatedProcess is not null && IsH2MModProcess(associatedProcess))
                    {
                        // This window has the correct process but is not the game window,
                        // so we assume it's the conhost because Widows terminal has the 'WindowsTerminal.exe' process
                        return handle;
                    }
                }
            }

            return IntPtr.Zero;
        }

        private static Process? FindH2MModProcess()
        {
            // find processes with matching title
            var processesWithTitle = Process.GetProcesses().Where(p =>
                p.MainWindowTitle.Contains("h2m-mod", StringComparison.OrdinalIgnoreCase)).ToList();

            // find process that loaded H1 MP binary
            var gameProc = processesWithTitle.FirstOrDefault(p =>
                p.Modules.OfType<ProcessModule>().Any(m => m.ModuleName.Equals("h1_mp64_ship.exe")));

            return gameProc;
        }

        private static bool IsH2MModProcess(Process p)
        {
            return p.MainWindowTitle.Contains("h2m-mod", StringComparison.OrdinalIgnoreCase) &&
                p.Modules.OfType<ProcessModule>().Any(m => m.ModuleName.Equals("h1_mp64_ship.exe"));
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private const string GAME_WINDOW_TITLE = "H2M-Mod";

        private static IntPtr FindH2MModGameWindow(Process process)
        {
            // find game window (title is exactly "H2M-Mod")
            foreach (IntPtr hChild in EnumerateProcessWindowHandles(process.Id))
            {
                string? title = GetWindowTitle(hChild);
                if (title != null && title.Equals("H2M-Mod"))
                {
                    //return hChild;
                }
            }

            // otherwise return just the main window, whatever it is
            return process.MainWindowHandle;
        }
    }
}
