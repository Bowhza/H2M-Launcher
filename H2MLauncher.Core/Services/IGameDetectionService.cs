namespace H2MLauncher.Core.Services;

public interface IGameDetectionService
{
    DetectedGame? DetectedGame { get; }
    bool IsGameDetectionRunning { get; }

    event Action<DetectedGame>? GameDetected;
    event Action? GameExited;

    void StartGameDetection();
    void StopGameDetection();
}