using Microsoft.AspNetCore.Mvc;
using MeridianLogistics.Api.Data;

namespace MeridianLogistics.Api.Controllers;

public record ShipmentDto(
    string TrackingNumber,
    string Origin,
    string Destination,
    string Status,
    string Service,
    string Eta,
    double WeightKg);

[ApiController]
[Route("api/shipments")]
public sealed class ShipmentsController : ControllerBase
{
    private readonly FakeDataStore _store;

    public ShipmentsController(FakeDataStore store) => _store = store;

    [HttpGet]
    public ActionResult<IEnumerable<ShipmentDto>> Get() =>
        Ok(_store.AllShipments()
            .OrderBy(s => s.TrackingNumber)
            .Select(s => new ShipmentDto(
                s.TrackingNumber, s.Origin, s.Destination, s.Status, s.Service,
                s.Eta.ToString("yyyy-MM-dd"), s.WeightKg)));
}
