namespace MeridianLogistics.Api.Observability;

public static class TraceIdFormatter
{
    /// <summary>Format a 32-char hex OTel trace id as a UUID (8-4-4-4-12).</summary>
    public static string ToUuid(string traceId)
    {
        if (traceId.Length != 32)
            return traceId; // leave non-standard ids untouched
        return $"{traceId[..8]}-{traceId[8..12]}-{traceId[12..16]}-{traceId[16..20]}-{traceId[20..]}";
    }
}
