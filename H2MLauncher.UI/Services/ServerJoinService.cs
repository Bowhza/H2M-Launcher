﻿using System.Windows;

using H2MLauncher.Core.Game;
using H2MLauncher.Core.Joining;
using H2MLauncher.Core.Matchmaking;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;
using H2MLauncher.Core.Settings;
using H2MLauncher.UI.Dialog;
using H2MLauncher.UI.ViewModels;

using Microsoft.Extensions.Options;

namespace H2MLauncher.UI.Services;

public class ServerJoinService : ServerJoinServiceBase
{
    private readonly DialogService _dialogService;
    private readonly H2MCommunicationService _communicationService;

    public ServerJoinService(
        DialogService dialogService,
        IOptionsMonitor<H2MLauncherSettings> options,
        H2MCommunicationService h2mCommunicationService,
        QueueingService queueingService,
        IMapsProvider mapsProvider,
        IGameServerInfoService<IServerConnectionDetails> gameServerInfoService)
        : base(options, h2mCommunicationService, queueingService, mapsProvider, gameServerInfoService)
    {
        _dialogService = dialogService;
        _communicationService = h2mCommunicationService;
    }

    protected override async ValueTask<string?> OnPasswordRequired(IServerInfo server)
    {
        PasswordViewModel passwordViewModel = new();

        bool? result = await _dialogService.ShowDialogAsync<PasswordDialog>(passwordViewModel);

        string? password = passwordViewModel.Password;

        if (result is null || result == false)
        {
            return null;
        }

        return password;
    }

    protected override ValueTask<bool> OnMissingMap(IServerInfo server)
    {
        bool? dialogResult = _dialogService.OpenTextDialog(
                title: "Missing Map",
                text: """
                    You are trying to join a server with a map that's not installed. This might crash your game. 
                    Do you want to continue?
                    """,
                buttons: MessageBoxButton.YesNo);        

        return ValueTask.FromResult(dialogResult == true);
    }

    protected override async ValueTask<JoinServerResult> OnServerFull(IServerInfo server, string? password)
    {
        JoinServerResult baseResult = await base.OnServerFull(server, password);

        if (baseResult is JoinServerResult.ServerFull &&
            _dialogService.OpenTextDialog("Server full", "The server you are trying to join is currently full. Join anyway?", MessageBoxButton.YesNo) == true)
        {
            bool joinedSuccessfully = await TryJoinServer(server, password);

            return joinedSuccessfully ? JoinServerResult.Success : JoinServerResult.JoinFailed;
        }

        if (baseResult is JoinServerResult.QueueUnavailable && 
            _dialogService.OpenTextDialog("Queue unavailable", "Could not join the queue, force join instead?", MessageBoxButton.YesNo) == true)
        {
            bool joinedSuccessfully = await TryJoinServer(server, password);
            return joinedSuccessfully ? JoinServerResult.ForceJoinSuccess : JoinServerResult.JoinFailed;
        }

        return baseResult;
    }

    protected override ValueTask<bool> OnGameNotRunning(IServerInfo server)
    {
        if (!_communicationService.GameDetection.IsGameDetectionRunning)
        {
            return ValueTask.FromResult(true);
        }

        bool? dialogResult = _dialogService.OpenTextDialog(
            title: "Game not running",
            text: "We could not detect your game. Do you want to launch the game now?",
            acceptButtonText: "Launch Game",
            cancelButtonText: "Cancel");

        if (dialogResult == true)
        {
            _communicationService.LaunchH2MMod();
        }

        return ValueTask.FromResult(false);
    }
}
