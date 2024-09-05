using System.Diagnostics;

namespace H2MLauncher.Core.Services;

public interface IGameCommunicationService : IDisposable
{
    GameState CurrentGameState { get; }
    Process? GameProcess { get; }
    bool IsGameCommunicationRunning { get; }

    event Action<GameState>? GameStateChanged;
    event Action<Process> Started;
    event Action<Exception?> Stopped;

    void StartGameCommunication(Process process);
    void StopGameCommunication();

    Task<IReadOnlyDictionary<int, string>> GetInGameMapsAsync();
}