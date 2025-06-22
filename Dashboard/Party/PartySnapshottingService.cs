using Dashboard.Database;

namespace Dashboard.Party;

public class PartySnapshottingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventBus _eventBus;

    public PartySnapshottingService(IServiceScopeFactory scopeFactory, IEventBus eventBus)
    {
        _scopeFactory = scopeFactory;
        _eventBus = eventBus;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await FetchAndStoreParties();
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task FetchAndStoreParties()
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var partiesApi = scope.ServiceProvider.GetRequiredService<IPartiesApiClient>();

        try
        {
            // Fetch all current parties from the game backend API
            var apiResponse = await partiesApi.GetParties();

            if (apiResponse.IsSuccessStatusCode && apiResponse.Content != null)
            {
                var parties = apiResponse.Content;
                var timestamp = DateTimeOffset.UtcNow; // Consistent timestamp for this snapshot batch

                var snapshotsToStore = new List<PartySnapshot>();
                foreach (var party in parties)
                {
                    var partySnapshot = new PartySnapshot
                    {
                        PartyId = party.PartyId,
                        Size = party.Members.Count,
                        Timestamp = timestamp
                    };

                    snapshotsToStore.Add(partySnapshot);

                    await _eventBus.PublishAsync(new PartySnapshotAddedNotification(partySnapshot));
                }

                if (snapshotsToStore.Any())
                {
                    dbContext.PartySnapshots.AddRange(snapshotsToStore);
                    await dbContext.SaveChangesAsync();
                    Console.WriteLine($"Party Snapshot Service: Stored {snapshotsToStore.Count} party snapshots.");
                }
                else
                {
                    Console.WriteLine("Party Snapshot Service: No active parties to snapshot.");
                }
            }
            else
            {
                Console.Error.WriteLine($"Party Snapshot Service: Failed to fetch parties from API. Status: {apiResponse.StatusCode}, Error: {apiResponse.Error?.Content}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Party Snapshot Service (General Exception): {ex.Message}");
        }
    }
}