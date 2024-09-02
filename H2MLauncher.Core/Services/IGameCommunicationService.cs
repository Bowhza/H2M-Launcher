using System.Diagnostics;

namespace H2MLauncher.Core.Services;

public interface IGameCommunicationService
{
    GameState CurrentGameState { get; }
    Process? GameProcess { get; }
    bool IsGameCommunicationRunning { get; }

    event Action<GameState>? GameStateChanged;
    event Action<Process> Started;
    event Action<Exception?> Stopped;

    void StartGameCommunication(Process process);
    void StopGameCommunication();

    Task<bool> HasInGameMapAsync(string mapName);
    Task<IReadOnlyDictionary<int, string>> GetInGameMapsAsync();

    void Dispose();
}