using System.Text;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MeridianLogistics.Api.Observability;

/// <summary>
/// The entire observability wiring. Langfuse and LangSmith are nothing more than
/// two OTLP exporter configs — zero coupling to business code. Adding a backend
/// means adding another AddOtlpExporter(...) here, nowhere else.
/// </summary>
public static class TelemetryExtensions
{
    public static IServiceCollection AddMeridianTelemetry(
        this IServiceCollection services, IConfiguration config)
    {
        var langfuse = config.GetSection(LangfuseOptions.SectionName).Get<LangfuseOptions>() ?? new();
        var langsmith = config.GetSection(LangSmithOptions.SectionName).Get<LangSmithOptions>() ?? new();

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("meridian-logistics-api"))
            .WithTracing(tracing =>
            {
                // Only our own spans. No AspNetCore instrumentation, so the
                // chat root span is the root of the trace (clean traces on slides).
                tracing.AddSource("Meridian.AI");

                // Exporter 1 — Langfuse (always on).
                var lfAuth = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{langfuse.PublicKey}:{langfuse.SecretKey}"));
                tracing.AddOtlpExporter("langfuse", o =>
                {
                    o.Endpoint = new Uri(langfuse.BaseUrl.TrimEnd('/') + "/api/public/otel/v1/traces");
                    o.Protocol = OtlpExportProtocol.HttpProtobuf;
                    o.Headers = $"Authorization=Basic {lfAuth}";
                });

                // Exporter 2 — LangSmith (only when an API key is configured).
                if (!string.IsNullOrWhiteSpace(langsmith.ApiKey))
                {
                    tracing.AddOtlpExporter("langsmith", o =>
                    {
                        o.Endpoint = new Uri("https://api.smith.langchain.com/otel/v1/traces");
                        o.Protocol = OtlpExportProtocol.HttpProtobuf;
                        o.Headers = $"x-api-key={langsmith.ApiKey},Langsmith-Project={langsmith.Project}";
                    });
                }
            });

        return services;
    }
}
