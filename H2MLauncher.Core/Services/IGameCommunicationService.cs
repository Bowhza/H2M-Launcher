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

    void Dispose();
    void StartGameCommunication(Process process);
    void StopGameCommunication();
}