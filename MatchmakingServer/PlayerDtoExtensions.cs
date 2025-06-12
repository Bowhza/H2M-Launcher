using H2MLauncher.Core.Party;
using H2MLauncher.Core.Social;

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
                player.PlayingServer.TryGetPlayerJoinDate(player, out DateTimeOffset joinDate) ? joinDate : default)
            : null;
    }
}
