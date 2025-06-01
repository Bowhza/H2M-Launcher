using Dashboard.Database;

using Microsoft.EntityFrameworkCore;

namespace Dashboard.Party;

public class PartySnapshotService(DatabaseContext dbContext)
{
    public async Task<Dictionary<DateTimeOffset, List<PartySnapshot>>> GetPartySnapshotsAsync()
    {
        return await dbContext.PartySnapshots
            .OrderBy(ps => ps.Timestamp)
            .GroupBy(ps => ps.Timestamp)
            .ToDictionaryAsync(grp => grp.Key, grp => grp.ToList());
    }

    public async Task<Dictionary<DateTimeOffset, int>> GetPartyCountAsync()
    {
        return await dbContext.PartySnapshots
            .OrderBy(ps => ps.Timestamp)
            .GroupBy(ps => ps.Timestamp)
            .ToDictionaryAsync(grp => grp.Key, grp => grp.Count());
    }
}
