using H2MLauncher.Core.Party;

using Refit;

namespace Dashboard.Party;

public interface IPartiesApiClient
{
    [Get("/api/parties")]
    Task<IApiResponse<IEnumerable<PartyInfo>>> GetParties(); // Use ApiResponse for more control over HTTP response details

    [Get("/api/parties/{id}")]
    Task<IApiResponse<PartyInfo>> GetParty(string id);
}
