using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Social;

public record RecentPlayerInfo(
    string Id,
    string UserName,
    string? PlayerName,
    SimpleServerInfo Server,
    DateTimeOffset EncounterDate);