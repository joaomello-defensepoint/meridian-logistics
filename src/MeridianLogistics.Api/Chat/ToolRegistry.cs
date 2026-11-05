using System.Text.Json;
using MeridianLogistics.Api.Data;
using MeridianLogistics.Api.Llm;

namespace MeridianLogistics.Api.Chat;

/// <summary>
/// The agent's three tools over the in-memory store. Each exposes a JSON-schema
/// parameter definition for the LLM and executes with tolerant argument parsing.
/// </summary>
public sealed class ToolRegistry
{
    private static readonly JsonSerializerOptions Json =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly FakeDataStore _store;

    public ToolRegistry(FakeDataStore store) => _store = store;

    public IReadOnlyList<LlmTool> Tools { get; } =
    [
        new LlmTool(
            "track_package",
            "Look up the current status, route and ETA of a shipment by its tracking number.",
            """
            {
              "type": "object",
              "properties": {
                "trackingNumber": { "type": "string", "description": "e.g. MER-2026-00417" }
              },
              "required": ["trackingNumber"]
            }
            """),
        new LlmTool(
            "get_shipping_quote",
            "Compute an estimated shipping price between two cities for a given weight.",
            """
            {
              "type": "object",
              "properties": {
                "originCity": { "type": "string" },
                "destinationCity": { "type": "string" },
                "weightKg": { "type": "number" },
                "express": { "type": "boolean", "description": "true for Express service" }
              },
              "required": ["originCity", "destinationCity", "weightKg"]
            }
            """),
        new LlmTool(
            "schedule_pickup",
            "Schedule a package pickup at an address on a date within a time window.",
            """
            {
              "type": "object",
              "properties": {
                "address": { "type": "string" },
                "date": { "type": "string", "description": "YYYY-MM-DD or natural date" },
                "timeWindow": { "type": "string", "description": "e.g. 'morning' or '09:00-12:00'" }
              },
              "required": ["address", "date", "timeWindow"]
            }
            """),
    ];

    /// <summary>Execute a tool by name. Returns a JSON string result.</summary>
    public string Execute(string name, string argumentsJson)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
        var args = doc.RootElement;

        return name switch
        {
            "track_package" => TrackPackage(args),
            "get_shipping_quote" => GetShippingQuote(args),
            "schedule_pickup" => SchedulePickup(args),
            _ => Serialize(new { error = $"Unknown tool '{name}'." }),
        };
    }

    private string TrackPackage(JsonElement args)
    {
        var tn = GetString(args, "trackingNumber");
        if (string.IsNullOrWhiteSpace(tn))
            return Serialize(new { error = "trackingNumber is required." });

        var s = _store.FindShipment(tn);
        return s is null
            ? Serialize(new { found = false, trackingNumber = tn, message = "No shipment found for that tracking number." })
            : Serialize(new
            {
                found = true,
                s.TrackingNumber,
                route = $"{s.Origin} -> {s.Destination}",
                s.Status,
                s.Service,
                eta = s.Eta.ToString("yyyy-MM-dd"),
                s.WeightKg,
            });
    }

    private string GetShippingQuote(JsonElement args)
    {
        var origin = GetString(args, "originCity") ?? "";
        var dest = GetString(args, "destinationCity") ?? "";
        var weight = GetDouble(args, "weightKg") ?? 0;
        var express = GetBool(args, "express") ?? false;

        if (weight <= 0)
            return Serialize(new { error = "weightKg must be greater than zero." });

        // Deterministic demo formula: base + per-kg, +60% for express.
        const double baseFee = 8.50;
        const double perKg = 1.75;
        var price = baseFee + perKg * weight;
        if (express) price *= 1.6;
        price = Math.Round(price, 2);

        var transitDays = express ? 1 : 3;

        return Serialize(new
        {
            origin,
            destination = dest,
            weightKg = weight,
            service = express ? "Express" : "Ground",
            currency = "EUR",
            price,
            estimatedTransitDays = transitDays,
        });
    }

    private string SchedulePickup(JsonElement args)
    {
        var address = GetString(args, "address");
        var date = GetString(args, "date");
        var window = GetString(args, "timeWindow") ?? "morning";

        if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(date))
            return Serialize(new { error = "address and date are required." });

        var pickup = _store.AddPickup(address, date, window);
        return Serialize(new
        {
            scheduled = true,
            confirmationId = pickup.ConfirmationId,
            pickup.Address,
            pickup.Date,
            pickup.TimeWindow,
        });
    }

    // --- tolerant argument readers (LLMs send strings/numbers interchangeably) ---

    private static string? GetString(JsonElement o, string name) =>
        o.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : null;

    private static double? GetDouble(JsonElement o, string name)
    {
        if (!o.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDouble(),
            JsonValueKind.String when double.TryParse(v.GetString(), out var d) => d,
            _ => null,
        };
    }

    private static bool? GetBool(JsonElement o, string name)
    {
        if (!o.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(v.GetString(), out var b) => b,
            _ => null,
        };
    }

    private static string Serialize(object value) => JsonSerializer.Serialize(value, Json);
}
