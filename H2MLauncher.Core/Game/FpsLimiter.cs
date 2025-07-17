using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using H2MLauncher.Core.Game.Models;
using H2MLauncher.Core.Services;
using H2MLauncher.Core.Settings;
using H2MLauncher.Core.Utilities;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Nogic.WritableOptions;

using static H2MLauncher.Core.Services.GameDirectoryService;

namespace H2MLauncher.Core.Game;

public sealed class FpsLimiter : IDisposable
{
    private readonly CompositeDisposable _disposables = [];

    private readonly H2MCommunicationService _communicationService;
    private readonly IWritableOptions<H2MLauncherSettings> _options;
    private readonly ILogger<FpsLimiter> _logger;

    private readonly IObservable<int> _cfgMaxFpsO;
    private readonly IObservable<H2MLauncherSettings> _settingsO;
    private readonly IObservable<GameState> _gameStateO;

    /// <summary>
    /// Whether the game memory communication is running.
    /// </summary>
    private readonly BehaviorSubject<bool> _gameCommunicationRunningSubject;

    /// <summary>
    /// Controls whether the limiter is started.
    /// </summary>
    private readonly BehaviorSubject<bool> _limiterActiveSubject = new(false);

    /// <summary>
    /// Disposable subscriptions of the current limiter chains.
    /// </summary>
    private readonly SerialDisposable _limiterDisposable = new();

    /// <summary>
    /// Holds the status of the FPS limiter.
    /// </summary>
    private readonly BehaviorSubject<FpsLimiterStatus> _limiterStatusSubject = new(FpsLimiterStatus.Idle);

    /// <summary>
    /// Indicates whether the FPS limiter has currently applied the limited max FPS.
    /// </summary>
    private readonly BehaviorSubject<bool> _isLimitedSubject = new(false);

    /// <summary>
    /// The current state of the FPS limiter.
    /// </summary>
    public IObservable<FpsLimiterState> StateO { get; }

    public enum FpsLimiterStatus
    {
        /// <summary>
        /// Not attempting to limit FPS.
        /// </summary>
        Idle,

        /// <summary>
        /// Limiter is successfully applying limits
        /// </summary>
        Active,

        /// <summary>
        /// Limiter encountered an error and is not active
        /// </summary>
        Failed
    }

    public record FpsLimiterState(FpsLimiterStatus Status, bool IsLimited, int CurrentMaxFps);

    public FpsLimiter(
        GameDirectoryService gameDirectoryService,
        H2MCommunicationService communicationService,
        IWritableOptions<H2MLauncherSettings> options,
        ILogger<FpsLimiter> logger)
    {
        _communicationService = communicationService;
        _options = options;
        _logger = logger;

        // Observable that emits the current settings
        _settingsO = CreateSettingsObservable(options);

        // Observable that emits the current config
        _cfgMaxFpsO = CreateMaxFpsObservable(gameDirectoryService);

        // Observable that emits the current game state
        _gameStateO = CreateGameStateObservable(communicationService.GameCommunication);

        // Observable that emits whether game memory communication is running
        _gameCommunicationRunningSubject = new(communicationService.GameCommunication.IsGameCommunicationRunning);

        communicationService.GameCommunication.Started += OnGameCommunicationStarted;
        communicationService.GameCommunication.Stopped += OnGameCommunicationStopped;

        // Switch whether the limiter is active (setting + game communication)
        _disposables.Add(
            Observable.CombineLatest(
                // Feature is enabled
                _settingsO
                    .Select(settings => settings.FpsLimiterEnabled)
                    .DistinctUntilChanged(),

                // Whether game memory communication is running
                _gameCommunicationRunningSubject.DistinctUntilChanged())
            .Select(conditions => conditions.All(c => c == true)) // All conditions must be met
            .Do(_limiterActiveSubject.OnNext) // Push value to active subject
            .Where(active => active == true)
            .Subscribe(_ => StartLimiter())
        );

        // Compute the state
        StateO = Observable.CombineLatest(
            _isLimitedSubject.DistinctUntilChanged(),
            _limiterStatusSubject.DistinctUntilChanged(),
            _cfgMaxFpsO,
            (isLimited, status, fps) => new FpsLimiterState(status, isLimited, fps));
    }

    private void StartLimiter()
    {
        try
        {
            CompositeDisposable limiterDisposables = [];

            // Emits when limiter is stopped
            IObservable<bool> stopLimiterO = _limiterActiveSubject
                .DistinctUntilChanged()
                .Where(active => active == false)
                .Take(1);

            // Fps limit from settings
            IObservable<int> settingsMaxFpsO = _settingsO
                .Select(settings => settings.MaxFps)
                .DistinctUntilChanged();

            // Dispose previous limiter and set new one
            _limiterDisposable.Disposable = limiterDisposables;

            // Sync back user changes from config or game
            limiterDisposables.Add(Observable
                .CombineLatest(_isLimitedSubject.DistinctUntilChanged(), _cfgMaxFpsO, settingsMaxFpsO,
                    (isLimited, cfgMaxFps, settingsMaxFps) => (isLimited, cfgMaxFps, settingsMaxFps))
                .Throttle(TimeSpan.FromMilliseconds(2500)) // debounce a little to let updated settings reload
                .TakeUntil(stopLimiterO)
                .Finally(() => _logger.LogInformation("Max FPS config -> settings sync stopped."))
                .Subscribe((x) =>
                {
                    if (x.settingsMaxFps == -1)
                    {
                        // First time activating this -> Update settings from current in game limit
                        _logger.LogInformation(
                            "Max FPS in settings not set, updating unlimited max fps in settings to {newFpsLimit}.",
                            x.cfgMaxFps);
                    }
                    else if (!x.isLimited && x.cfgMaxFps != x.settingsMaxFps)
                    {
                        // The user changed max fps in game or config -> Update settings
                        _logger.LogInformation(
                            "Max FPS in config different than settings while limit not applied, " +
                            "updating unlimited max fps in settings from {currentFpsLimit} to {newFpsLimit}.",
                            x.settingsMaxFps,
                            x.cfgMaxFps);
                    }
                    else
                    {
                        return;
                    }

                    _options.Update((s) =>
                    {
                        return s with { MaxFps = x.cfgMaxFps };
                    });
                }));

            // Apply fps limit when in main menu
            limiterDisposables.Add(Observable
                .CombineLatest(_gameStateO, _settingsO, GetFpsLimit)
                .DistinctUntilChanged()
                .TakeUntil(stopLimiterO)
                .Finally(() => _logger.LogInformation("FPS limiter stopped."))
                .SelectMany(values => Observable.FromAsync(() =>
                {
                    return ApplyFpsLimit(values.maxFps, values.isLimited);
                }))
                .Subscribe(
                    _ => { },
                    ex => // OnError for the whole chain if something unexpected happens
                    {
                        _logger.LogError(ex, "An unhandled error occurred in FPS limiter application chain.");
                        _limiterStatusSubject.OnNext(FpsLimiterStatus.Failed);
                    },
                    () => // OnCompleted when TakeUntil triggers
                    {
                        _logger.LogInformation("FPS limiter application command chain completed.");

                        // When the TakeUntil (stopLimiterO) triggers, it means the limiter is stopping.
                        // If it stopped due to an error, FpsLimiterStatus.Failed would have been set.
                        // If it stopped gracefully, we transition to Idle.
                        if (_limiterStatusSubject.Value != FpsLimiterStatus.Failed)
                        {
                            _limiterStatusSubject.OnNext(FpsLimiterStatus.Idle);
                        }
                    })
                );

            // Subscription to reset FPS when limiter becomes inactive
            limiterDisposables.Add(
                stopLimiterO
                    .WithLatestFrom(_settingsO, (_, settings) => settings)
                    .SelectMany((H2MLauncherSettings settings) =>
                        Observable.FromAsync(() => ResetFpsLimit(settings.MaxFps))
                     )
                    .Subscribe()
                );

            _logger.LogInformation("FPS limiter successfully started.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while starting FPS limiter.");
            _limiterStatusSubject.OnNext(FpsLimiterStatus.Failed);
            _limiterDisposable.Disposable = null;
        }
    }

    private static (int maxFps, bool isLimited) GetFpsLimit(GameState gameState, H2MLauncherSettings settings)
    {
        bool isLimited = gameState.IsInMainMenu;
        int maxFps = isLimited
            ? settings.MaxFpsLimited
            : settings.MaxFps;

        return (maxFps, isLimited);
    }

    private async Task ApplyFpsLimit(int maxFps, bool isLimited)
    {
        try
        {
            if (maxFps < 0)
            {
                _logger.LogInformation("Skipping applying FPS limit because it is unset.");
                return;
            }

            _logger.LogDebug("Changing max FPS to {maxFps} (limited: {limited}).", maxFps, isLimited);

            bool success = await ExecuteMaxFpsCommand(maxFps);
            if (success)
            {
                _logger.LogInformation("Successfully changed max FPS to {maxFps}.", maxFps);
                _limiterStatusSubject.OnNext(FpsLimiterStatus.Active);
                _isLimitedSubject.OnNext(isLimited);
            }
            else
            {
                _logger.LogInformation("Failed to change max FPS to {maxFps}.", maxFps);
                _limiterStatusSubject.OnNext(FpsLimiterStatus.Failed);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while changing max FPS.");
            _limiterStatusSubject.OnNext(FpsLimiterStatus.Failed);
        }
    }

    private async Task ResetFpsLimit(int maxFps = 0)
    {
        _logger.LogInformation("FPS limiter has stopped. Attempting to reset game FPS limit to default.");

        try
        {
            bool success = await ExecuteMaxFpsCommand(maxFps);
            if (success)
            {
                _logger.LogInformation("Successfully reset max FPS to {defaultMaxFps}.", maxFps);

                // After reset, we are idle again if no other start conditions apply
                _limiterStatusSubject.OnNext(FpsLimiterStatus.Idle);
                _isLimitedSubject.OnNext(false);
            }
            else
            {
                _logger.LogWarning("Failed to reset max FPS to {defaultMaxFps}.", maxFps);
                _limiterStatusSubject.OnNext(FpsLimiterStatus.Failed);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while attempting to reset FPS limit.");
            _limiterStatusSubject.OnNext(FpsLimiterStatus.Failed);
        }
    }

    private Task<bool> ExecuteMaxFpsCommand(int maxFps)
    {
        return _communicationService.ExecuteCommandAsync(
               commands: [$"com_maxfps {maxFps}"],
               bringGameWindowToForeground: false);
    }

    private void OnGameCommunicationStopped(Exception? obj)
    {
        _gameCommunicationRunningSubject.OnNext(false);
    }

    private void OnGameCommunicationStarted(System.Diagnostics.Process obj)
    {
        _gameCommunicationRunningSubject.OnNext(true);
    }

    private static IObservable<H2MLauncherSettings> CreateSettingsObservable(IOptionsMonitor<H2MLauncherSettings> options)
    {
        return Observable
            .Create<H2MLauncherSettings>(observer =>
            {
                return options.OnChange((settings, _) => observer.OnNext(settings)) ?? Disposable.Empty;
            })
            .StartWith(options.CurrentValue)
            .Replay(1)
            .RefCount();
    }

    private static IObservable<int> CreateMaxFpsObservable(GameDirectoryService gameDirectoryService)
    {
        return Observable
            .FromEvent<ConfigChangedEventHandler, ConfigMpContent?>(
                (a) => (filePath, cfg) => a(cfg),
                (h) => gameDirectoryService.ConfigMpChanged += h,
                (h) => gameDirectoryService.ConfigMpChanged -= h
            )
            .StartWith(gameDirectoryService.CurrentConfigMp)
            .Where(cfg => cfg is not null)
            .Select(cfg => cfg!.MaxFps)
            .DistinctUntilChanged();
    }

    private static IObservable<GameState> CreateGameStateObservable(IGameCommunicationService gameCommunicationService)
    {
        return Observable
           .FromEvent<GameState>(
               (h) => gameCommunicationService.GameStateChanged += h,
               (h) => gameCommunicationService.GameStateChanged -= h)
           .StartWith(gameCommunicationService.CurrentGameState)
           .DistinctUntilChanged();
    }

    public void Dispose()
    {
        _disposables.Dispose();
        _limiterDisposable.Dispose();

        _limiterActiveSubject.Dispose();
        _gameCommunicationRunningSubject.Dispose();
        _limiterStatusSubject.Dispose();
        _isLimitedSubject.Dispose();

        _communicationService.GameCommunication.Started -= OnGameCommunicationStarted;
        _communicationService.GameCommunication.Stopped -= OnGameCommunicationStopped;
    }
}
