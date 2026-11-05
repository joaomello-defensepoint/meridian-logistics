using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MeridianLogistics.Api.Llm;

namespace MeridianLogistics.Api.Controllers;

public record HealthResponse(bool Api, bool Ollama, string Provider, string Model);

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly LlmOptions _llm;
    private readonly ILlmProvider _provider;

    public HealthController(IHttpClientFactory httpFactory, IOptions<LlmOptions> llm, ILlmProvider provider)
    {
        _httpFactory = httpFactory;
        _llm = llm.Value;
        _provider = provider;
    }

    [HttpGet]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken ct)
    {
        var ollamaUp = await ProbeOllamaAsync(ct);
        return Ok(new HealthResponse(
            Api: true,
            Ollama: ollamaUp,
            Provider: _provider.ProviderName,
            Model: _provider.Model));
    }

    private async Task<bool> ProbeOllamaAsync(CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2));

            var client = _httpFactory.CreateClient();
            var url = _llm.BaseUrl.TrimEnd('/') + "/models";
            using var resp = await client.GetAsync(url, cts.Token);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
