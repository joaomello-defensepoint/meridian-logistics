using System.Collections.Concurrent;

namespace MeridianLogistics.Api.Data;

public record Shipment(
    string TrackingNumber,
    string Origin,
    string Destination,
    string Status,
    string Service,
    DateOnly Eta,
    double WeightKg);

public record Pickup(
    string ConfirmationId,
    string Address,
    string Date,
    string TimeWindow);

/// <summary>In-memory stand-in for a shipments/pickups database. Seeded with
/// realistic-looking data so the demo tools return believable answers.</summary>
public sealed class FakeDataStore
{
    private readonly ConcurrentDictionary<string, Shipment> _shipments = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<Pickup> _pickups = new();
    private int _pickupSeq = 0;

    public FakeDataStore()
    {
        Shipment[] seed =
        [
            new("MER-2026-00417", "Lisbon",   "Berlin",     "In Transit", "Air",     Today(2),  12.4),
            new("MER-2026-00440", "Coimbra",  "Barcelona",  "Delayed",    "Express", Today(2),  2.2),
            new("MER-2026-00430", "Lisbon",   "Rotterdam",  "Delivered",  "Ground",  Today(-1), 45.2),
        ];
        foreach (var s in seed) _shipments[s.TrackingNumber] = s;
    }

    private static DateOnly Today(int offsetDays) =>
        DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(offsetDays));

    public IReadOnlyCollection<Shipment> AllShipments() => _shipments.Values.ToList();

    public IReadOnlyCollection<Pickup> AllPickups() => _pickups.ToList();

    public Shipment? FindShipment(string trackingNumber) =>
        _shipments.TryGetValue(trackingNumber.Trim(), out var s) ? s : null;

    public Pickup AddPickup(string address, string date, string timeWindow)
    {
        var id = $"PU-{DateTime.UtcNow:yyyyMMdd}-{Interlocked.Increment(ref _pickupSeq):D3}";
        var pickup = new Pickup(id, address, date, timeWindow);
        _pickups.Enqueue(pickup);
        return pickup;
    }
}
