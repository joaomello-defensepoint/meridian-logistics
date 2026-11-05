namespace MeridianLogistics.Api.Observability;

/// <summary>Bound from the "Langfuse" section. Always-on OTLP exporter target.</summary>
public sealed class LangfuseOptions
{
    public const string SectionName = "Langfuse";
    public string BaseUrl { get; set; } = "http://localhost:3001";
    public string PublicKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
}

/// <summary>Bound from the "LangSmith" section. Exporter/sink enabled only when ApiKey is set.</summary>
public sealed class LangSmithOptions
{
    public const string SectionName = "LangSmith";
    public string ApiKey { get; set; } = "";
    public string Project { get; set; } = "meridian-logistics";
}
