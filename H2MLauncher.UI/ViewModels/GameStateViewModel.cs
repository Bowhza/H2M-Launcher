using System.Net;

using CommunityToolkit.Mvvm.ComponentModel;

using H2MLauncher.Core.Services;

namespace H2MLauncher.UI.ViewModels;

public partial class GameStateViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isGameRunning;

    [ObservableProperty]
    private bool _isConnected;

    public int? ProcessId => DetectedGame?.Process.Id;

    [ObservableProperty]
    private string _displayText = "Game not detected";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionText))]
    private string? _connectedIp;


    private GameState? _state;
    public GameState? State
    {
        get
        {
            return _state;
        }
        set
        {
            _state = value;
            ConnectedIp = _state?.Endpoint?.Address.ToString();
            IsConnected = _state?.IsConnected ?? false;
            OnPropertyChanged(nameof(ConnectionText));
        }
    }

    public string ConnectionText
    {
        get
        {
            if (State is null)
            {
                return "";
            }

            if (State.IsInMainMenu)
            {
                return "Main Menu";
            }

            if (State.IsConnected)
            {
                if (State.Endpoint is null)
                {
                    return "Connected";
                }
                else if (State.Endpoint.Address.Equals(IPAddress.Any))
                {
                    return "Private Match";
                }
                else
                {
                    return $"Connected to {ConnectedIp}";
                }
            }

            if (State.IsConnecting)
            {
                if (State.Endpoint is not null)
                {
                    return $"Connecting to {ConnectedIp}";
                }
                else
                {
                    return "Connecting";
                }
            }

            return "Main Menu";
        }
    }


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
                State = null;
                DisplayText = "Game not detected";
            }
            else
            {
                IsGameRunning = true;
                DisplayText = $"Game detected: '{value.Process.ProcessName}' (v{value.Version.FileVersion}, PID: {value.Process.Id})";
            }
        }
    }
}
