using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace MeridianLogistics.Api.Llm;

/// <summary>
/// One class speaks the OpenAI chat-completions dialect, so it covers Ollama,
/// OpenAI, Azure-compatible gateways, Groq, etc. purely by configuration.
/// It is just HTTP + JSON — no vendor SDK. That is the facade's selling point.
/// </summary>
public sealed class OpenAiCompatibleLlmProvider : ILlmProvider
{
    private readonly HttpClient _http;
    private readonly LlmOptions _options;

    public OpenAiCompatibleLlmProvider(HttpClient http, IOptions<LlmOptions> options)
    {
        _options = options.Value;
        _http = http;
        _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    public string ProviderName => "openai";
    public string Model => _options.Model;

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct)
    {
        var payload = BuildPayload(request);
        using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

        using var resp = await _http.PostAsync("chat/completions", content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"LLM call failed ({(int)resp.StatusCode}): {body}");

        return ParseResponse(body);
    }

    private JsonObject BuildPayload(LlmRequest request)
    {
        var messages = new JsonArray();
        foreach (var m in request.Messages)
        {
            var obj = new JsonObject { ["role"] = m.Role };

            // tool results carry the originating call id
            if (m.ToolCallId is not null)
                obj["tool_call_id"] = m.ToolCallId;

            obj["content"] = m.Content; // may be null for assistant tool-call turns

            if (m.ToolCalls is { Count: > 0 })
            {
                var calls = new JsonArray();
                foreach (var tc in m.ToolCalls)
                    calls.Add(new JsonObject
                    {
                        ["id"] = tc.Id,
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = tc.Name,
                            ["arguments"] = tc.ArgumentsJson,
                        },
                    });
                obj["tool_calls"] = calls;
            }

            messages.Add(obj);
        }

        var payload = new JsonObject
        {
            ["model"] = _options.Model,
            ["temperature"] = request.Temperature,
            ["messages"] = messages,
        };

        if (request.Tools is { Count: > 0 })
        {
            var tools = new JsonArray();
            foreach (var t in request.Tools)
                tools.Add(new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = t.Name,
                        ["description"] = t.Description,
                        ["parameters"] = JsonNode.Parse(t.ParametersJsonSchema),
                    },
                });
            payload["tools"] = tools;
        }

        return payload;
    }

    private LlmResponse ParseResponse(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var choice = root.GetProperty("choices")[0];
        var message = choice.GetProperty("message");

        var contentText = message.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
            ? c.GetString()
            : null;

        List<LlmToolCall>? toolCalls = null;
        if (message.TryGetProperty("tool_calls", out var tcs) && tcs.ValueKind == JsonValueKind.Array)
        {
            toolCalls = new List<LlmToolCall>();
            foreach (var tc in tcs.EnumerateArray())
            {
                var fn = tc.GetProperty("function");
                toolCalls.Add(new LlmToolCall(
                    Id: tc.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                    Name: fn.GetProperty("name").GetString() ?? "",
                    ArgumentsJson: fn.TryGetProperty("arguments", out var a) ? a.GetString() ?? "{}" : "{}"));
            }
        }

        var finish = choice.TryGetProperty("finish_reason", out var fr) ? fr.GetString() ?? "stop" : "stop";
        var model = root.TryGetProperty("model", out var md) ? md.GetString() ?? _options.Model : _options.Model;

        int input = 0, output = 0;
        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("prompt_tokens", out var p)) input = p.GetInt32();
            if (usage.TryGetProperty("completion_tokens", out var o)) output = o.GetInt32();
        }

        return new LlmResponse(
            new LlmMessage("assistant", contentText, toolCalls),
            new LlmUsage(input, output),
            model,
            finish);
    }
}
