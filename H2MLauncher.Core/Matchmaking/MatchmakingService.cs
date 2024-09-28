using H2MLauncher.Core.Game;
using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.Models;
using H2MLauncher.Core.OnlineServices;
using H2MLauncher.Core.Services;
using H2MLauncher.Core.Settings;
using H2MLauncher.Core.Utilities.SignalR;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using TypedSignalR.Client;

namespace H2MLauncher.Core.Matchmaking;

public class MatchmakingService : HubClient<IMatchmakingHub>, IMatchmakingClient
{
    private readonly ILogger<MatchmakingService> _logger;
    private readonly IGameServerInfoService<ServerConnectionDetails> _gameServerInfoService;
    private readonly IMapsProvider _mapsProvider;
    private readonly IGameDetectionService _gameDetectionService;
    private readonly IGameCommunicationService _gameCommunicationService;
    private readonly IPlaylistService _playlistService;
    private readonly IOptionsMonitor<H2MLauncherSettings> _options;
    private readonly OnlineServiceManager _onlineServiceManager;

    private MatchmakingPreferences? _matchmakingPreferences = null;
    //private MatchSearchCriteria? _currentMatchSearchCriteria = null;
    private MatchmakingMetadata _currentMetadata = default;

    /// <summary>
    /// Gets the currently applied match search criteria.
    /// </summary>
    public MatchSearchCriteria? MatchSearchCriteria
    {
        get => _currentMetadata.SearchPreferences;
        private set
        {
            if (EqualityComparer<MatchSearchCriteria>.Default.Equals(_currentMetadata.SearchPreferences, value))
            {
                return;
            }

            MatchSearchCriteria? oldValue = _currentMetadata.SearchPreferences;
            _currentMetadata = _currentMetadata with { SearchPreferences = value };

            if (value is not null)
            {
                MatchSearchCriteriaChanged?.Invoke(value);
            }
        }
    }

    /// <summary>
    /// Gets the playlist currently searching a match in.
    /// </summary>
    public Playlist? Playlist => _currentMetadata.Playlist;

    /// <summary>
    /// Gets the number of search passes since entering matchmaking.
    /// </summary>
    public int SearchAttempts { get; private set; }

    /// <summary>
    /// Gets the time when entered matchmaking.
    /// </summary>
    public DateTimeOffset MatchSearchStartTime { get; private set; }

    public event Action<(string hostname, SearchMatchResult match)>? MatchFound;
    public event Action<MatchmakingError>? RemovedFromMatchmaking;
    public event Action<IEnumerable<SearchMatchResult>>? Matches;
    public event Action<MatchSearchCriteria>? MatchSearchCriteriaChanged;

    public MatchmakingService(
        OnlineServiceManager onlineServiceManager,
        ILogger<MatchmakingService> logger,
        IGameServerInfoService<ServerConnectionDetails> gameServerInfoService,
        IMapsProvider mapsProvider,
        IGameDetectionService gameDetectionService,
        IGameCommunicationService gameCommunicationService,
        IPlaylistService playlistService,
        IOptionsMonitor<H2MLauncherSettings> options,
        HubConnection connection) : base(connection)
    {
        _logger = logger;
        _gameServerInfoService = gameServerInfoService;
        _mapsProvider = mapsProvider;
        _gameDetectionService = gameDetectionService;
        _gameCommunicationService = gameCommunicationService;
        _playlistService = playlistService;
        _options = options;
        _onlineServiceManager = onlineServiceManager;

        connection.Register<IMatchmakingClient>(this);
    }

    protected override IMatchmakingHub CreateHubProxy(HubConnection hubConnection, CancellationToken hubCancellationToken)
    {
        return hubConnection.CreateHubProxy<IMatchmakingHub>(hubCancellationToken);
    }

    #region RPC handlers

    Task IMatchmakingClient.OnMatchmakingEntered(MatchmakingMetadata metadata)
    {
        _logger.LogDebug("OnMatchmakingEntered(): Received matchmaking metadata {@matchmakingMetdatada}", metadata);
        _currentMetadata = metadata;
        _onlineServiceManager.State = PlayerState.Matchmaking;

        if (metadata.SearchPreferences is not null)
        {
            MatchSearchCriteriaChanged?.Invoke(metadata.SearchPreferences);
        }

        return Task.CompletedTask;
    }

    async Task IMatchmakingClient.OnSearchMatchUpdate(IEnumerable<SearchMatchResult> searchMatchResults)
    {
        _logger.LogInformation("Received match search results: {n}", searchMatchResults.Count());

        Matches?.Invoke(searchMatchResults);

        if (MatchSearchCriteria is null || _onlineServiceManager.State is not PlayerState.Matchmaking)
        {
            return;
        }

        if (!_currentMetadata.IsActiveSearcher)
        {
            // only passive, dont participate in the matchmaking algorithm
            return;
        }

        try
        {
            // adjust the search criteria based on the possible matches
            bool adjustPing = AdjustSearchCriteria(searchMatchResults);

            // ping all servers and send updated data
            List<ServerConnectionDetails> serverConnectionDetails = searchMatchResults
                .Select(matchResult => new ServerConnectionDetails(matchResult.ServerIp, matchResult.ServerPort))
                .ToList();

            List<ServerPing> serverPings = await PingServersAndFilter(serverConnectionDetails);

            _logger.LogDebug("Found {n}/{total} potential servers with ping <= {maxPing} ms",
                serverPings.Count(x => x.Ping <= MatchSearchCriteria.MaxPing), serverPings.Count, MatchSearchCriteria.MaxPing);

            if (adjustPing && MatchSearchCriteria.MaxPing > 0 &&
                serverPings.Count > 0 &&
                serverPings.All(p => p.Ping > MatchSearchCriteria.MaxPing))
            {
                // adjusting ping
                MatchSearchCriteria = MatchSearchCriteria with
                {
                    MaxPing = (int)(serverPings.Min(p => p.Ping) + 5)
                };
            }

            if (await Hub.UpdateSearchSession(MatchSearchCriteria, serverPings))
            {
                _logger.LogDebug("Updated search session: {@searchCriteria}", MatchSearchCriteria);
            }
            else
            {
                _logger.LogWarning("Could not update search session");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during matchmaking");
        }
    }

    Task IMatchmakingClient.OnMatchFound(string hostName, SearchMatchResult matchResult)
    {
        _logger.LogInformation("Received match found result: {matchResult}", matchResult);

        MatchFound?.Invoke((hostName, matchResult));

        return Task.CompletedTask;
    }

    Task IMatchmakingClient.OnRemovedFromMatchmaking(MatchmakingError reason)
    {
        _logger.LogInformation("Removed from matchmaking. Reason: {reason}", reason);

        _onlineServiceManager.State = PlayerState.Connected;
        _currentMetadata = default;

        RemovedFromMatchmaking?.Invoke(reason);

        return Task.CompletedTask;
    }

    #endregion


    public async Task<bool> EnterMatchmakingAsync(MatchmakingPreferences? searchPreferences = null)
    {
        try
        {
            Playlist? playlist = await _playlistService.GetDefaultPlaylist(CancellationToken.None);
            if (playlist is null)
            {
                return false;
            }

            return await EnterMatchmakingAsync(playlist, searchPreferences).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting default playlist");
            return false;
        }
    }

    public async Task<bool> EnterMatchmakingAsync(Playlist playlist, MatchmakingPreferences? searchPreferences = null)
    {
        try
        {
#if DEBUG == false
            if (!_options.CurrentValue.ServerQueueing ||
                !_options.CurrentValue.GameMemoryCommunication ||
                !_gameDetectionService.IsGameDetectionRunning ||
                !_gameCommunicationService.IsGameCommunicationRunning)
            {
                return false;
            }
#endif

            if (_onlineServiceManager.State is PlayerState.Queued or PlayerState.Joining or PlayerState.Matchmaking)
            {
                return false;
            }

            if (playlist.Servers is null || playlist.Servers.Count == 0)
            {
                return false;
            }

            _logger.LogDebug("Entering matchmaking...");

            await StartConnection();

            _matchmakingPreferences = searchPreferences ??= new MatchmakingPreferences()
            {
                SearchCriteria = new MatchSearchCriteria()
                {
                    MaxPing = 300,
                    MinPlayers = 6,
                }
            };

            MatchmakingPreferences pref = _matchmakingPreferences;
            MatchSearchCriteria sc = _matchmakingPreferences.SearchCriteria;
            MatchSearchCriteria initialSearchCriteria = new()
            {
                MinPlayers = Math.Max(sc.MinPlayers, 6),
                MaxPing = GetMinWhenDefault(sc.MaxPing, 28),
                MaxScore = pref.TryFreshGamesFirst ? GetMinWhenDefault(sc.MaxScore, 2000) : sc.MaxScore,
                MaxPlayersOnServer = pref.TryFreshGamesFirst ? 0 : sc.MaxPlayersOnServer
            };

            SearchAttempts = 0;
            //Playlist = playlist;
            //MatchSearchCriteria = initialSearchCriteria;
            //MatchSearchStartTime = DateTimeOffset.Now;

            bool success = await Hub.SearchMatch(initialSearchCriteria, playlist.Id);
            if (!success)
            {
                _logger.LogDebug("Could not enter matchmaking for playlist '{playlist}'", playlist.Id);
                return false;
            }

            _currentMetadata = new()
            {
                IsActiveSearcher = true,
                JoinTime = DateTime.Now,
                Playlist = playlist,
                SearchPreferences = initialSearchCriteria
            };

            _onlineServiceManager.State = PlayerState.Matchmaking;

            MatchSearchCriteriaChanged?.Invoke(_currentMetadata.SearchPreferences);

            _logger.LogInformation("Entered matchmaking queue for playlist '{playlist}'", playlist.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while entering matchmaking");
            return false;
        }
    }

    public async Task LeaveQueueAsync()
    {
        try
        {
            if (Connection.State is HubConnectionState.Connected)
            {
                _logger.LogDebug("Leaving server queue...");

                await Hub.LeaveQueue();
                _onlineServiceManager.State = PlayerState.Connected;
                //Playlist = null;
                _currentMetadata = default;
                MatchSearchCriteria = null;
                SearchAttempts = 0;

                _logger.LogInformation("Server queue left.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while leaving server queue");
        }
    }

    private bool AdjustSearchCriteria(IEnumerable<SearchMatchResult> searchMatchResults)
    {
        if (MatchSearchCriteria is null || _matchmakingPreferences is null)
        {
            return false;
        }

        if (++SearchAttempts > 7 && _matchmakingPreferences.TryFreshGamesFirst)
        {
            // remove max score limit after 7 attempts
            MatchSearchCriteria = MatchSearchCriteria with
            {
                MaxScore = _matchmakingPreferences.SearchCriteria.MaxScore,
                MaxPlayersOnServer = _matchmakingPreferences.SearchCriteria.MaxPlayersOnServer
            };
        }

        if (SearchAttempts > 2 || SearchAttempts > 1 && _matchmakingPreferences.TryFreshGamesFirst)
        {
            // remove min player limit after 2 attempts
            MatchSearchCriteria = MatchSearchCriteria with
            {
                MinPlayers = Math.Max(1, _matchmakingPreferences.SearchCriteria.MinPlayers)
            };
        }

        if (!searchMatchResults.Any())
        {
            // min players is too high, or no server is available                    
            return false;
        }

        if (MatchSearchCriteria.MaxScore > 0 &&
            searchMatchResults.All(r => r.ServerScore is null || r.ServerScore > MatchSearchCriteria.MaxScore))
        {
            // max server score is too low for any server
            return false;
            //// up max score
            //CurrentMatchSearchCriteria = CurrentMatchSearchCriteria with
            //{
            //    MaxScore = (searchMatchResults.Min(p => p.ServerScore ?? CurrentMatchSearchCriteria.MaxScore) + 500)
            //};
        }

        return true;
    }

    private async Task<List<ServerPing>> PingServersAndFilter(IReadOnlyList<ServerConnectionDetails> servers)
    {
        _logger.LogDebug("Pinging {n} servers...", servers.Count);

        var responses = await _gameServerInfoService.GetInfoAsync(servers, requestTimeoutInMs: 3000);

        return await responses
            .Where(res => res.info is not null && _mapsProvider.InstalledMaps.Contains(res.info.MapName)) // filter out servers with missing maps
            .Select(res => new ServerPing(res.server.Ip, res.server.Port, (uint)res.info!.Ping))
            .ToListAsync();
    }

    private static int GetMinWhenDefault(int valueMaybeDefault, int value2, int defaultValue = -1)
    {
        return valueMaybeDefault == defaultValue ? value2 : Math.Min(valueMaybeDefault, value2);
    }
}
