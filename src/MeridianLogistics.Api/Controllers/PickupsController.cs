using Microsoft.AspNetCore.Mvc;
using MeridianLogistics.Api.Data;

namespace MeridianLogistics.Api.Controllers;

public record PickupDto(string ConfirmationId, string Address, string Date, string TimeWindow);

[ApiController]
[Route("api/pickups")]
public sealed class PickupsController : ControllerBase
{
    private readonly FakeDataStore _store;

    public PickupsController(FakeDataStore store) => _store = store;

    // Pickups the Operations Agent has scheduled this session (in-memory; most recent first).
    [HttpGet]
    public ActionResult<IEnumerable<PickupDto>> Get() =>
        Ok(_store.AllPickups()
            .Reverse()
            .Select(p => new PickupDto(p.ConfirmationId, p.Address, p.Date, p.TimeWindow)));
}
