using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MeridianLogistics.Api.Observability;

namespace MeridianLogistics.Api.Feedback;

/// <summary>A destination that user feedback (a score on a trace) can be sent to.</summary>
public interface IFeedbackSink
{
    string Name { get; }
    Task SendAsync(string traceId, int value, string? comment, CancellationToken ct);
}

/// <summary>Sends scores to self-hosted Langfuse via its public scores API.</summary>
public sealed class LangfuseFeedbackSink : IFeedbackSink
{
    private readonly HttpClient _http;
    private readonly LangfuseOptions _options;
    private readonly ILogger<LangfuseFeedbackSink> _log;

    public LangfuseFeedbackSink(HttpClient http, IOptions<LangfuseOptions> options, ILogger<LangfuseFeedbackSink> log)
    {
        _http = http;
        _options = options.Value;
        _log = log;
    }

    public string Name => "langfuse";

    public async Task SendAsync(string traceId, int value, string? comment, CancellationToken ct)
    {
        var basic = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.PublicKey}:{_options.SecretKey}"));

        var url = _options.BaseUrl.TrimEnd('/') + "/api/public/scores";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("Authorization", $"Basic {basic}");
        req.Content = new StringContent(JsonSerializer.Serialize(new
        {
            traceId,
            name = "user-feedback",
            value,
            dataType = "NUMERIC",
            comment,
        }), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            _log.LogWarning("Langfuse feedback failed ({Status}): {Body}", (int)resp.StatusCode, body);
        }
    }
}

/// <summary>Best-effort feedback to LangSmith cloud. Never breaks the demo.</summary>
public sealed class LangSmithFeedbackSink : IFeedbackSink
{
    private readonly HttpClient _http;
    private readonly LangSmithOptions _options;
    private readonly ILogger<LangSmithFeedbackSink> _log;

    public LangSmithFeedbackSink(HttpClient http, IOptions<LangSmithOptions> options, ILogger<LangSmithFeedbackSink> log)
    {
        _http = http;
        _options = options.Value;
        _log = log;
    }

    public string Name => "langsmith";

    public async Task SendAsync(string traceId, int value, string? comment, CancellationToken ct)
    {
        try
        {
            var uuid = TraceIdFormatter.ToUuid(traceId);
            using var req = new HttpRequestMessage(
                HttpMethod.Post, "https://api.smith.langchain.com/api/v1/feedback");
            req.Headers.Add("x-api-key", _options.ApiKey);
            req.Content = new StringContent(JsonSerializer.Serialize(new
            {
                trace_id = uuid,
                run_id = uuid,
                key = "user-feedback",
                score = value,
                comment,
            }), Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _log.LogWarning("LangSmith feedback failed ({Status}): {Body}", (int)resp.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "LangSmith feedback threw; ignoring (best-effort).");
        }
    }
}

/// <summary>Fans a single piece of feedback out to every registered sink.</summary>
public sealed class FeedbackService
{
    private readonly IEnumerable<IFeedbackSink> _sinks;

    public FeedbackService(IEnumerable<IFeedbackSink> sinks) => _sinks = sinks;

    public Task SubmitAsync(string traceId, int value, string? comment, CancellationToken ct) =>
        Task.WhenAll(_sinks.Select(s => s.SendAsync(traceId, value, comment, ct)));
}
