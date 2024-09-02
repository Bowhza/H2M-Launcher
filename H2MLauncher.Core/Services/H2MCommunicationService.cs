using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

using H2MLauncher.Core.Settings;

using Microsoft.Extensions.Logging;

using Nogic.WritableOptions;

namespace H2MLauncher.Core.Services
{
    public sealed class H2MCommunicationService : IDisposable
    {
        private const string GAME_WINDOW_TITLE = "H2M-Mod";
        private const string GAME_EXECUTABLE_NAME = "h1_mp64_ship.exe";

        //Windows API constants
        private const int WM_CHAR = 0x0102; // Message code for sending a character
        private const int WM_KEYDOWN = 0x0100; // Message code for key down
        private const int WM_KEYUP = 0x0101;   // Message code for key up

        private readonly IWritableOptions<H2MLauncherSettings> _h2mLauncherSettings;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly ILogger<H2MCommunicationService> _logger;
        private readonly IDisposable? _optionsChangeRegistration;

        public H2MCommunicationService(IErrorHandlingService errorHandlingService, IWritableOptions<H2MLauncherSettings> options,
            ILogger<H2MCommunicationService> logger)
        {
            _errorHandlingService = errorHandlingService;
            _h2mLauncherSettings = options;
            _logger = logger;

            if (options.Value.AutomaticGameDetection)
            {
                StartGameDetection();
            }

            _optionsChangeRegistration = options.OnChange((settings, _) =>
            {
                if (!settings.AutomaticGameDetection && IsGameDetectionRunning)
                {
                    StopGameDetection();
                }
                else if (settings.AutomaticGameDetection && !IsGameDetectionRunning)
                {
                    StartGameDetection();
                }

                if (!settings.GameMemoryCommunication && IsGameCommunicationRunning)
                {
                    StopGameCommunication();
                }
                else if (settings.GameMemoryCommunication && !IsGameCommunicationRunning)
                {
                    StartGameCommunication();
                }
            });
        }

        #region Game Communication 

        private const int GAME_MEMORY_READ_INTERVAL = 1000;

        private readonly SemaphoreSlim _memorySemaphore = new(1, 1);
        private CancellationTokenSource _gameCommunicationCancellation = new();
        private Task? _gameCommunicationTask;
        private GameMemory? _gameMemory;

        [MemberNotNullWhen(true, nameof(_gameCommunicationTask))]
        public bool IsGameCommunicationRunning => _gameCommunicationTask != null && !_gameCommunicationTask.IsCompleted;
        public GameState CurrentGameState { get; private set; } = new(false, default, null, null);

        public event Action<GameState>? GameStateChanged;

        public void StartGameCommunication()
        {
            if (IsGameCommunicationRunning ||
                DetectedGame is null || DetectedGame.Process.HasExited)
            {
                _logger.LogDebug("Cannot start game memory communication: already running or no game process available");
                return;
            }

            _memorySemaphore.Wait();
            try
            {
                if (IsGameCommunicationRunning)
                {
                    return;
                }

                _logger.LogDebug("Starting game memory communication with {processName} ({pid})...",
                    DetectedGame.Process.ProcessName, DetectedGame.Process.Id);

                _gameMemory = new GameMemory(DetectedGame.Process, GAME_EXECUTABLE_NAME);
                _gameCommunicationCancellation = new CancellationTokenSource();
                _gameCommunicationTask = Task.Run(
                    function: () => GameMemoryCommunicationLoop(_gameMemory, _gameCommunicationCancellation.Token)
                                        .ContinueWith(t =>
                                        {
                                            if (t.IsFaulted)
                                            {
                                                _errorHandlingService.HandleError("Error during game communication");
                                                _logger.LogError(t.Exception, "Game memory communication loop terminated with error:");
                                            }
                                            else if (t.IsCanceled)
                                            {
                                                _logger.LogInformation("Game memory communication loop canceled.");
                                            }
                                            else
                                            {
                                                _logger.LogInformation("Game memory communication loop terminated.");
                                            }
                                        }),
                    cancellationToken: _gameDetectionCancellation.Token
                 );


                _logger.LogInformation("Game memory communication started with {processName} ({pid})",
                    DetectedGame.Process.ProcessName, DetectedGame.Process.Id);
            }
            finally
            {
                _memorySemaphore.Release();
            }
        }

        public void StopGameCommunication()
        {
            if (!IsGameCommunicationRunning)
            {
                return;
            }

            _logger.LogDebug("Stopping game memory communication...");

            try
            {
                _gameCommunicationCancellation.Cancel();
                _gameCommunicationTask.Wait();
                _gameCommunicationTask = null;
                _gameMemory?.Dispose();
                _gameMemory = null;
                CurrentGameState = new(false, ConnectionState.CA_DISCONNECTED, null, null);

                _logger.LogInformation("Game memory communication stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while stopping game communication");
            }
        }

        private async Task GameMemoryCommunicationLoop(GameMemory gameMemory, CancellationToken cancellationToken)
        {
            while (!gameMemory.Process.HasExited && !cancellationToken.IsCancellationRequested)
            {
                ConnectState? connectState;
                IPEndPoint? endPoint;

                await _memorySemaphore.WaitAsync(cancellationToken);
                try
                {
                    _logger.LogTrace("Reading memory from process {processName} ({pid})...",
                        gameMemory.Process.ProcessName, gameMemory.Process.Id);

                    ConnectionState connectionState = gameMemory.GetConnectionState() ?? ConnectionState.CA_DISCONNECTED;

                    connectState = gameMemory.GetConnectState();
                    if (connectState.HasValue)
                    {
                        try
                        {
                            IPAddress ipAddress = new(connectState.Value.Address.IP);
                            int port = connectState.Value.Address.Port;
                            endPoint = new IPEndPoint(ipAddress, port);
                            _logger.LogTrace("Game connection state: {connectionState} - {endpoint}", connectionState, endPoint);
                        }
                        catch
                        {
                            endPoint = null;
                        }
                    }
                    else
                    {
                        _logger.LogTrace("Game connection state: {connectionState}", connectionState);
                        endPoint = null;
                    }

                    bool virtualLobbyLoaded = gameMemory.GetVirtualLobbyLoaded() ?? false;

                    GameState lastGameState = CurrentGameState;
                    if (virtualLobbyLoaded)
                    {
                        CurrentGameState = new(virtualLobbyLoaded, connectionState, null, null);
                    }
                    else
                    {
                        CurrentGameState = lastGameState with
                        {
                            ConnectionState = connectionState,
                            VirtualLobbyLoaded = virtualLobbyLoaded,
                            Endpoint = endPoint,
                            StartTime = lastGameState.StartTime ?? DateTimeOffset.Now
                        };
                    }

                    GameStateChanged?.Invoke(CurrentGameState);
                    await Task.Delay(GAME_MEMORY_READ_INTERVAL, cancellationToken).ConfigureAwait(true);
                }
                finally
                {
                    _memorySemaphore.Release();
                }
            };
        }

        #endregion

        #region Game Detection

        private const int GAME_DETECTION_POLLING_INTERVAL = 1000;

        private readonly object _gameDetectionLockObj = new();
        private CancellationTokenSource _gameDetectionCancellation = new();
        private Task? _gameDetectionTask;

        public DetectedGame? DetectedGame { get; private set; }

        [MemberNotNullWhen(true, nameof(_gameDetectionTask))]
        public bool IsGameDetectionRunning => _gameDetectionTask != null && !_gameDetectionTask.IsCompleted;


        public event Action<DetectedGame>? GameDetected;

        public event Action? GameExited;

        public void StartGameDetection()
        {
            if (IsGameDetectionRunning)
            {
                return;
            }

            lock (_gameDetectionLockObj)
            {
                if (IsGameDetectionRunning)
                {
                    return;
                }

                _logger.LogDebug("Starting game detection...");

                _gameDetectionCancellation = new();
                _gameDetectionTask = Task.Run(
                    function: () => GameDetectionLoop(OnGameDetected, OnGameExited, cancellationToken: _gameDetectionCancellation.Token)
                                        .ContinueWith(t =>
                                        {
                                            if (t.IsFaulted)
                                            {
                                                _errorHandlingService.HandleError("Game detection crashed");
                                                _logger.LogError(t.Exception, "Game detection loop terminated with error:");
                                            }
                                            else if (t.IsCanceled)
                                            {
                                                _logger.LogInformation("Game detection loop canceled.");
                                            }
                                            else
                                            {
                                                _logger.LogInformation("Game detection loop terminated.");
                                            }
                                        }),
                    cancellationToken: _gameDetectionCancellation.Token
                 );

                _logger.LogDebug("Game detection started");
            }
        }

        public void StopGameDetection()
        {
            if (!IsGameDetectionRunning)
            {
                return;
            }

            _logger.LogDebug("Stopping game detection...");

            lock (_gameDetectionLockObj)
            {
                try
                {
                    _gameDetectionCancellation.Cancel();
                    _gameDetectionTask.Wait();
                    _gameDetectionTask = null;

                    _logger.LogInformation("Game detection stopped");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while stopping game detection");
                }
            }
        }

        private void OnGameDetected(DetectedGame detectedGame)
        {
            DetectedGame = detectedGame;

            _logger.LogInformation("Detected game {gameProcessName} (v{gameVersion})",
                detectedGame.Process.ProcessName, detectedGame.Version.ToString());

            if (string.IsNullOrEmpty(_h2mLauncherSettings.CurrentValue.MWRLocation))
            {
                _logger.LogDebug("Game location empty, setting to {gameLocation}", detectedGame.FileName);

                _h2mLauncherSettings.Update(settings =>
                {
                    return settings with
                    {
                        MWRLocation = detectedGame.FileName
                    };
                });
            }

            if (_h2mLauncherSettings.Value.GameMemoryCommunication)
            {
                StartGameCommunication();
            }

            GameDetected?.Invoke(detectedGame);
        }

        private void OnGameExited()
        {
            _logger.LogInformation("Game process exited");

            DetectedGame = null;
            if (IsGameCommunicationRunning)
            {
                StopGameCommunication();
            }
            GameExited?.Invoke();
        }

        private static async Task GameDetectionLoop(
            Action<DetectedGame> onGameDetected, Action onGameExited, bool detectOnce = false, CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Process? process = FindH2MModProcess();
                if (process is not null && process.MainModule is not null)
                {
                    string fileName = process.MainModule.FileName;
                    string? gameDir = Path.GetDirectoryName(fileName);

                    if (!string.IsNullOrEmpty(gameDir) && File.Exists(Path.Combine(gameDir, GAME_EXECUTABLE_NAME)))
                    {
                        FileVersionInfo version = FileVersionInfo.GetVersionInfo(fileName);

                        // game dir found
                        onGameDetected(new DetectedGame(process, fileName, gameDir, version));

                        try
                        {
                            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                        }
                        catch (InvalidOperationException)
                        {
                            // sometimes throws when proess is killed
                        }

                        // process terminated
                        onGameExited();

                        if (detectOnce)
                        {
                            return;
                        }
                    }
                }

                await Task.Delay(GAME_DETECTION_POLLING_INTERVAL, cancellationToken).ConfigureAwait(false);
            }
        }

        #endregion

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

        public bool JoinServer(string ip, string port, string? password = null)
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

        public static Process? FindH2MModProcess()
        {
            // find processes with matching title
            var processesWithTitle = Process.GetProcesses().Where(p =>
                p.MainWindowTitle.Contains("h2m-mod", StringComparison.OrdinalIgnoreCase)).ToList();

            // find process that loaded H1 MP binary
            var gameProc = processesWithTitle.FirstOrDefault(p =>
                p.Modules.OfType<ProcessModule>().Any(m => m.ModuleName.Equals(GAME_EXECUTABLE_NAME)));

            return gameProc;
        }

        private static bool IsH2MModProcess(Process p)
        {
            return p.MainWindowTitle.Contains("h2m-mod", StringComparison.OrdinalIgnoreCase) &&
                p.Modules.OfType<ProcessModule>().Any(m => m.ModuleName.Equals(GAME_EXECUTABLE_NAME));
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

        public void Dispose()
        {
            if (IsGameCommunicationRunning)
            {
                StopGameCommunication();
            }

            if (IsGameDetectionRunning)
            {
                StopGameDetection();
            }

            _gameMemory?.Dispose();
            _memorySemaphore?.Dispose();
            _optionsChangeRegistration?.Dispose();
        }
    }
}
