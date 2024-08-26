using CommunityToolkit.Mvvm.ComponentModel;

using H2MLauncher.Core.Services;

namespace H2MLauncher.Core.ViewModels;

public partial class GameStateViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isGameRunning;

    [ObservableProperty]
    private bool _isUserPlaying;

    public int? ProcessId => DetectedGame?.Process.Id;

    [ObservableProperty]
    private string _displayText = "";

    private DetectedGame? _detectedGame;
    public DetectedGame? DetectedGame
    {
        get
        {
            return _detectedGame;
        }
        set
        {
            _detectedGame = value;

            if (value is null)
            {
                IsGameRunning = false;
                IsUserPlaying = false;
                DisplayText = "Game not detected";
            }
            else
            {
                IsGameRunning = true;
                DisplayText = $"Game detected ('{value.Process.ProcessName}' {value.Version.FileVersion}, PID: {value.Process.Id})";
            }
        }
    }
}
