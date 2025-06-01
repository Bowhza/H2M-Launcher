using H2MLauncher.Core.Party;

using MatchmakingServer.Authentication;
using MatchmakingServer.Parties;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatchmakingServer.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = ApiKeyDefaults.AuthenticationScheme)]
[Route("[controller]")]
public class PartiesController : ControllerBase
{
    private readonly PartyService _partyService;

    public PartiesController(PartyService partyService)
    {
        _partyService = partyService;
    }

    [HttpGet]
    public ActionResult<IEnumerable<PartyInfo>> GetParties()
    {
        var parties = _partyService.Parties.Select(PartyService.CreatePartyInfo);
        return Ok(parties);
    }

    [HttpGet("{id}")]
    public ActionResult<Party> GetParty(string id)
    {
        var party = _partyService.GetPartyById(id);
        if (party is null)
        {
            return NotFound();
        }
        return Ok(party);
    }
}
