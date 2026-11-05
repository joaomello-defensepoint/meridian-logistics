using System.Diagnostics;
using System.Text.Json;

namespace MeridianLogistics.Api.Llm;

/// <summary>
/// Observability as a DECORATOR. It wraps the real provider and emits one
/// OpenTelemetry GenAI span per call. There is zero vendor-specific code here
/// and zero business logic — Langfuse and LangSmith just read these attributes.
/// </summary>
public sealed class TracedLlmProvider : ILlmProvider
{
    // Shared across the app; Program.cs registers it with AddSource("Meridian.AI").
    public static readonly ActivitySource Source = new("Meridian.AI");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly ILlmProvider _inner;

    public TracedLlmProvider(ILlmProvider inner) => _inner = inner;

    public string ProviderName => _inner.ProviderName;
    public string Model => _inner.Model;

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct)
    {
        using var activity = Source.StartActivity($"chat {_inner.Model}", ActivityKind.Client);

        // OTel GenAI semantic-convention request attributes.
        activity?.SetTag("gen_ai.operation.name", "chat");
        activity?.SetTag("gen_ai.system", _inner.ProviderName);
        activity?.SetTag("gen_ai.request.model", _inner.Model);

        // Link this generation to a named (and versioned) prompt in Langfuse.
        if (request.PromptName is { } promptName)
        {
            activity?.SetTag("langfuse.observation.prompt.name", promptName);
            if (request.PromptVersion is { } promptVersion)
                activity?.SetTag("langfuse.observation.prompt.version", promptVersion);
        }

        var promptJson = JsonSerializer.Serialize(request.Messages, Json);
        activity?.SetTag("gen_ai.prompt", promptJson);
        activity?.SetTag("input.value", promptJson); // Langfuse + LangSmith both map this

        try
        {
            // Demo switch: pretend the provider is unreachable so an error is
            // recorded on this generation span and surfaces in the UI.
            if (request.SimulateFailure)
                throw new HttpRequestException("Simulated outage — the AI provider is unavailable.");

            var response = await _inner.CompleteAsync(request, ct);

            activity?.SetTag("gen_ai.response.model", response.Model);
            activity?.SetTag("gen_ai.usage.input_tokens", response.Usage.InputTokens);
            activity?.SetTag("gen_ai.usage.output_tokens", response.Usage.OutputTokens);

            var completionJson = JsonSerializer.Serialize(response.Message, Json);
            activity?.SetTag("gen_ai.completion", completionJson);
            activity?.SetTag("output.value", completionJson);

            return response;
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
