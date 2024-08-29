using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

using H2MLauncher.Core.Settings;

using Microsoft.Extensions.Options;

namespace H2MLauncher.Core.Services
{
    public class H2MCommunicationService
    {
        private const string GAME_WINDOW_TITLE = "H2M-Mod";
        //Windows API constants
        private const int WM_CHAR = 0x0102; // Message code for sending a character
        private const int WM_KEYDOWN = 0x0100; // Message code for key down
        private const int WM_KEYUP = 0x0101;   // Message code for key up

        private readonly IOptionsMonitor<H2MLauncherSettings> _h2mLauncherSettings;
        private readonly IErrorHandlingService _errorHandlingService;

        public H2MCommunicationService(IErrorHandlingService errorHandlingService, IOptionsMonitor<H2MLauncherSettings> options)
        {
            _errorHandlingService = errorHandlingService ?? throw new ArgumentNullException(nameof(errorHandlingService));
            ArgumentNullException.ThrowIfNull(options, nameof(options));
            _h2mLauncherSettings = options;
        }

        //Windows API functions to send input to a window
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern bool EnumThreadWindows(int dwThreadId, EnumWindowsProc lpfn, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);


        internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        internal static IEnumerable<IntPtr> EnumerateProcessWindowHandles(int processId)
        {
            var handles = new List<IntPtr>();

            foreach (ProcessThread thread in Process.GetProcessById(processId).Threads)
                EnumThreadWindows(thread.Id,
                    (hWnd, lParam) => { handles.Add(hWnd); return true; }, IntPtr.Zero);

            return handles;
        }

        internal static IEnumerable<IntPtr> EnumerateWindowHandles()
        {
            var handles = new List<IntPtr>();

            EnumWindows((hWnd, lParam) => { handles.Add(hWnd); return true; }, IntPtr.Zero);

            return handles;
        }

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

        private bool TryFindValidGameFile(out string fileName)
        {
            const string exeFileName = "h2m-mod.exe";

            if (string.IsNullOrEmpty(_h2mLauncherSettings.CurrentValue.MWRLocation))
            {
                // no location set, try relative path
                fileName = Path.GetFullPath(exeFileName);
                return File.Exists(fileName);
            }

            string userDefinedLocation = Path.GetFullPath(_h2mLauncherSettings.CurrentValue.MWRLocation);

            if (!Path.Exists(userDefinedLocation))
            {
                // neither dir or file exists
                fileName = userDefinedLocation;
                return false;
            }

            if (File.GetAttributes(userDefinedLocation).HasFlag(FileAttributes.Directory))
            {
                // is a directory, get full file name
                fileName = Path.Combine(userDefinedLocation, exeFileName);

                return File.Exists(fileName);
            }

            // is a file?
            fileName = userDefinedLocation;
            return File.Exists(userDefinedLocation);
        }

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
                if (TryFindValidGameFile(out string gameFileName) && 
                    !string.IsNullOrEmpty(gameFileName))
                {
                    ProcessStartInfo startInfo = new(gameFileName)
                    {
                        WorkingDirectory = Path.GetDirectoryName(gameFileName)
                    };

                    Process.Start(startInfo);
                }
                else
                {
                    _errorHandlingService.HandleException(
                        new FileNotFoundException("h2m-mod.exe was not found."),
                        $"The h2m-mod.exe could not be found at {gameFileName}!");
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleException(ex, "Error launching h2m-mod.");
            }
        }

        public bool JoinServer(string ip, string port,string? password=null)
        {                       
            const string disconnectCommand = "disconnect";
            string connectCommand = $"connect {ip}:{port}";

            if (password is not null)
            {
                connectCommand += $";password {password}";
            }

            Process? h2mModProcess = FindH2MModProcess();
            if (h2mModProcess == null)
            {
                _errorHandlingService.HandleError("Could not find the h2m-mod terminal window.");
                return false;
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
            SetForegroundWindow(hGameWindow);


            // TODO: confirm the user joined this server


            return true;
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

        private static IntPtr FindH2MModGameWindow(Process process)
        {
            // find game window (title is exactly "H2M-Mod")
            foreach (IntPtr hChild in EnumerateProcessWindowHandles(process.Id))
            {
                string? title = GetWindowTitle(hChild);
                if (title != null && title.Equals(GAME_WINDOW_TITLE))
                {
                    return hChild;
                }
            }

            // otherwise return just the main window, whatever it is
            return process.MainWindowHandle;
        }
    }
}
