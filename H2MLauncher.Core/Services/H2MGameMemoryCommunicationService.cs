using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;

using H2MLauncher.Core.Utilities;

using Microsoft.Extensions.Logging;

namespace H2MLauncher.Core.Services
{
    public sealed class H2MGameMemoryCommunicationService : IDisposable, IGameCommunicationService
    {
        private const int GAME_MEMORY_READ_INTERVAL = 200;

        private readonly ILogger<H2MGameMemoryCommunicationService> _logger;

        private readonly SemaphoreSlim _memorySemaphore = new(1, 1);
        private CancellationTokenSource _gameCommunicationCancellation = new();
        private Task? _gameCommunicationTask;
        private bool _isCommunicationRunning;
        private GameMemory? _gameMemory;

        private GameState _currentGameState = new(false, default, null, null);
        public GameState CurrentGameState
        {
            get => _currentGameState;
            private set
            {
                if (value.Equals(_currentGameState))
                {
                    return;
                }

                _currentGameState = value;
                GameStateChanged?.Invoke(value);
            }
        }

        [MemberNotNullWhen(true, nameof(_gameCommunicationTask))]
        public bool IsGameCommunicationRunning => _gameCommunicationTask != null && _isCommunicationRunning;
        public Process? GameProcess => _gameMemory?.Process;


        public event Action<GameState>? GameStateChanged;
        public event Action<Process>? Started;
        public event Action<Exception?>? Stopped;

        public H2MGameMemoryCommunicationService(ILogger<H2MGameMemoryCommunicationService> logger)
        {
            _logger = logger;
        }

        public void StartGameCommunication(Process process)
        {
            if (IsGameCommunicationRunning || process.HasExited)
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
                   process.ProcessName, process.Id);

                _gameMemory = new GameMemory(process, Constants.GAME_EXECUTABLE_NAME);
                _gameCommunicationCancellation = new CancellationTokenSource();
                _gameCommunicationTask = Task.Run(
                    function: () => GameMemoryCommunicationLoop(_gameMemory, _gameCommunicationCancellation.Token),
                    cancellationToken: _gameCommunicationCancellation.Token
                 ).ContinueWith(OnGameCommunicationTerminated);

                _isCommunicationRunning = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while starting game memory communication");
            }
            finally
            {
                _memorySemaphore.Release();
            }

            // raise event outside of semaphore to avoid deadlocks
            Started?.Invoke(process);

            _logger.LogInformation("Game memory communication started with {processName} ({pid})",
                process.ProcessName, process.Id);
        }

        private void OnGameCommunicationTerminated(Task loopTask)
        {
            _isCommunicationRunning = false;

            if (loopTask.IsFaulted)
            {
                _logger.LogError(loopTask.Exception, "Game memory communication loop terminated with error:");
            }
            else if (loopTask.IsCanceled)
            {
                _logger.LogInformation("Game memory communication loop canceled.");
            }
            else
            {
                _logger.LogInformation("Game memory communication loop terminated.");
            }

            _gameMemory?.Dispose();
            _gameMemory = null;
            CurrentGameState = new(false, ConnectionState.CA_DISCONNECTED, null, null);

            Stopped?.Invoke(loopTask.Exception);
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
                bool virtualLobbyLoaded;
                ConnectionState connectionState;

                // read values
                await _memorySemaphore.WaitAsync(cancellationToken);
                try
                {
                    _logger.LogTrace("Reading memory from process {processName} ({pid})...",
                        gameMemory.Process.ProcessName, gameMemory.Process.Id);

                    connectionState = gameMemory.GetConnectionState() ?? ConnectionState.CA_DISCONNECTED;

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

                    virtualLobbyLoaded = gameMemory.GetVirtualLobbyLoaded() ?? false;
                }
                finally
                {
                    _memorySemaphore.Release();
                }

                // process values and update state
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

                await Task.Delay(GAME_MEMORY_READ_INTERVAL, cancellationToken).ConfigureAwait(true);
            };
        }

        public async Task<IReadOnlyDictionary<int, string>> GetInGameMapsAsync()
        {
            if (_gameMemory is null || _gameMemory.Process.HasExited)
            {
                throw new InvalidOperationException("Game communication not running");
            }

            await _memorySemaphore.WaitAsync();
            try
            {
                return _gameMemory.GetInGameMaps().ToDictionary(_ => _.id, _ => _.name).AsReadOnly();
            }
            finally
            {
                _memorySemaphore.Release();
            }
        }

        public void Dispose()
        {
            StopGameCommunication();
            _gameMemory?.Dispose();
            _gameMemory = null;
            _gameCommunicationCancellation?.Dispose();
            _gameCommunicationTask?.Wait();
        }
    }
}
