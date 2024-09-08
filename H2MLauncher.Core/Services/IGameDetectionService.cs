namespace H2MLauncher.Core.Services;

public interface IGameDetectionService
{
    DetectedGame? DetectedGame { get; }
    bool IsGameDetectionRunning { get; }

    event Action<DetectedGame>? GameDetected;
    event Action? GameExited;
    event Action<Exception?>? Error;

    void StartGameDetection();
    void StopGameDetection();
}