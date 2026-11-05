namespace MeridianLogistics.Api.Llm;

// Vendor-neutral chat contracts. Nothing here mentions Ollama, OpenAI or
// Anthropic — that is the whole point of the facade.

public record LlmMessage(
    string Role,
    string? Content,
    IReadOnlyList<LlmToolCall>? ToolCalls = null,
    string? ToolCallId = null);

public record LlmToolCall(string Id, string Name, string ArgumentsJson);

public record LlmTool(string Name, string Description, string ParametersJsonSchema);

public record LlmRequest(
    IReadOnlyList<LlmMessage> Messages,
    IReadOnlyList<LlmTool>? Tools = null,
    double Temperature = 0.2,
    string? PromptName = null,
    int? PromptVersion = null,
    bool SimulateFailure = false);

public record LlmUsage(int InputTokens, int OutputTokens);

public record LlmResponse(
    LlmMessage Message,
    LlmUsage Usage,
    string Model,
    string FinishReason);

/// <summary>The single seam every caller depends on. Vendors live behind it.</summary>
public interface ILlmProvider
{
    string ProviderName { get; }
    string Model { get; }
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct);
}
