using H2MLauncher.Core.Models;

namespace H2MLauncher.Core.Party;

public record PartyInfo(string PartyId, SimpleServerInfo? Server, List<PartyPlayerInfo> Members);

public record PartyPlayerInfo(string Id, string Name, bool IsLeader);
