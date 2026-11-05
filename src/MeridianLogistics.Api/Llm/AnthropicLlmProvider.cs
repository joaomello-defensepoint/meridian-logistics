using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace MeridianLogistics.Api.Llm;

/// <summary>
/// Same <see cref="ILlmProvider"/> contract, different vendor wire format. This
/// class exists purely to prove the facade: swapping vendor is a config change,
/// not a code change. System messages become a top-level `system`, tools map to
/// Anthropic's input_schema, and tool_use / tool_result content blocks map back
/// to our neutral ToolCalls / ToolCallId messages.
/// </summary>
public sealed class AnthropicLlmProvider : ILlmProvider
{
    private const string MessagesUrl = "https://api.anthropic.com/v1/messages";
    private const string DefaultModel = "claude-sonnet-5";

    private readonly HttpClient _http;
    private readonly LlmOptions _options;
    private readonly string _model;

    public AnthropicLlmProvider(HttpClient http, IOptions<LlmOptions> options)
    {
        _http = http;
        _options = options.Value;
        _model = string.IsNullOrWhiteSpace(_options.Model) ? DefaultModel : _options.Model;

        _http.DefaultRequestHeaders.Add("x-api-key", _options.AnthropicApiKey);
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public string ProviderName => "anthropic";
    public string Model => _model;

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct)
    {
        var payload = BuildPayload(request);
        using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

        using var resp = await _http.PostAsync(MessagesUrl, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"Anthropic call failed ({(int)resp.StatusCode}): {body}");

        return ParseResponse(body);
    }

    private JsonObject BuildPayload(LlmRequest request)
    {
        var systemParts = new List<string>();
        var messages = new JsonArray();

        foreach (var m in request.Messages)
        {
            if (m.Role == "system")
            {
                if (!string.IsNullOrEmpty(m.Content)) systemParts.Add(m.Content);
                continue;
            }

            if (m.Role == "tool")
            {
                // our tool-result message -> Anthropic user turn with a tool_result block
                messages.Add(new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "tool_result",
                            ["tool_use_id"] = m.ToolCallId,
                            ["content"] = m.Content ?? "",
                        },
                    },
                });
                continue;
            }

            if (m.Role == "assistant" && m.ToolCalls is { Count: > 0 })
            {
                var blocks = new JsonArray();
                if (!string.IsNullOrEmpty(m.Content))
                    blocks.Add(new JsonObject { ["type"] = "text", ["text"] = m.Content });
                foreach (var tc in m.ToolCalls)
                    blocks.Add(new JsonObject
                    {
                        ["type"] = "tool_use",
                        ["id"] = tc.Id,
                        ["name"] = tc.Name,
                        ["input"] = JsonNode.Parse(string.IsNullOrWhiteSpace(tc.ArgumentsJson) ? "{}" : tc.ArgumentsJson),
                    });
                messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = blocks });
                continue;
            }

            messages.Add(new JsonObject { ["role"] = m.Role, ["content"] = m.Content ?? "" });
        }

        var payload = new JsonObject
        {
            ["model"] = _model,
            ["max_tokens"] = 1024,
            ["temperature"] = request.Temperature,
            ["messages"] = messages,
        };

        if (systemParts.Count > 0)
            payload["system"] = string.Join("\n\n", systemParts);

        if (request.Tools is { Count: > 0 })
        {
            var tools = new JsonArray();
            foreach (var t in request.Tools)
                tools.Add(new JsonObject
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["input_schema"] = JsonNode.Parse(t.ParametersJsonSchema),
                });
            payload["tools"] = tools;
        }

        return payload;
    }

    private LlmResponse ParseResponse(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        string? text = null;
        List<LlmToolCall>? toolCalls = null;

        foreach (var block in root.GetProperty("content").EnumerateArray())
        {
            var type = block.GetProperty("type").GetString();
            if (type == "text")
            {
                text = (text ?? "") + block.GetProperty("text").GetString();
            }
            else if (type == "tool_use")
            {
                toolCalls ??= new List<LlmToolCall>();
                toolCalls.Add(new LlmToolCall(
                    Id: block.GetProperty("id").GetString() ?? "",
                    Name: block.GetProperty("name").GetString() ?? "",
                    ArgumentsJson: block.GetProperty("input").GetRawText()));
            }
        }

        var model = root.TryGetProperty("model", out var md) ? md.GetString() ?? _model : _model;
        var finish = root.TryGetProperty("stop_reason", out var sr) ? sr.GetString() ?? "end_turn" : "end_turn";

        int input = 0, output = 0;
        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("input_tokens", out var i)) input = i.GetInt32();
            if (usage.TryGetProperty("output_tokens", out var o)) output = o.GetInt32();
        }

        return new LlmResponse(
            new LlmMessage("assistant", text, toolCalls),
            new LlmUsage(input, output),
            model,
            finish);
    }
}
