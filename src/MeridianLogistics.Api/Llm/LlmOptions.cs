namespace MeridianLogistics.Api.Llm;

/// <summary>Bound from the "Llm" configuration section.</summary>
public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>"openai" (default, OpenAI-compatible incl. Ollama) or "anthropic".</summary>
    public string Provider { get; set; } = "openai";

    // OpenAI-compatible settings (Ollama, OpenAI, Azure gateways, Groq, ...).
    public string BaseUrl { get; set; } = "http://localhost:11434/v1";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "qwen2.5:7b";

    // Anthropic settings.
    public string AnthropicApiKey { get; set; } = "";

    public double Temperature { get; set; } = 0.2;
}
