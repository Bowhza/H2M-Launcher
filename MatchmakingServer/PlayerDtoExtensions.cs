using H2MLauncher.Core.Party;
using H2MLauncher.Core.Social.Player;
using H2MLauncher.Core.Social.Status;
using H2MLauncher.Core.Utilities;

namespace MatchmakingServer;

public static class PlayerDtoExtensions
{
    public static PartyStatusDto? ToPartyStatusDto(this Player player)
    {
        return player.Party is not null
            ? new PartyStatusDto(
                player.Party.Id,
                player.Party.Members.Count,
                player.Party.Privacy is not PartyPrivacy.Closed,
                player.Party.ValidInvites.ToList())
            : null;
    }

    public static MatchStatusDto? ToMatchStatusDto(this Player player)
    {
        return player.PlayingServer is not null
            ? new MatchStatusDto(
                (player.PlayingServer.ServerIp, player.PlayingServer.ServerPort),
                player.PlayingServer.LastServerInfo?.HostName ?? player.PlayingServer.ServerName,
                player.PlayingServer.LastServerInfo?.GameType,
                player.PlayingServer.LastServerInfo?.MapName,
                player.PlayingServerJoinDate ?? default)
            : null;
    }

    /// <summary>
    /// Creates a <see cref="ServerPlayerInfo"/> from the player and when he joined at, relative to another player encountering him.
    /// </summary>
    /// <param name="player">The player in the info.</param>
    /// <param name="joinDate">When the player joined the server.</param>
    /// <param name="encounteringPlayerJoinDate">When another player this info is created for joined the server.</param>
    public static ServerPlayerInfo ToServerPlayerInfo(this Player player,
        DateTimeOffset joinDate, DateTimeOffset? encounteringPlayerJoinDate)
    {        
        DateTimeOffset encounterDate = encounteringPlayerJoinDate.HasValue
            // The maximum of both dates is when they met
            ? GenericComparison.Max(encounteringPlayerJoinDate.Value, joinDate)

            // Just take the join date of the player
            : joinDate;

        return new ServerPlayerInfo
        {
            Id = player.Id,
            UserName = player.UserName,
            PlayerName = player.Name,
            EncounterDate = encounterDate
        };
    }
}
