using Microsoft.AspNetCore.Mvc;
using MeridianLogistics.Api.Chat;

namespace MeridianLogistics.Api.Controllers;

public record ChatRequest(string SessionId, string Mode, string Message, bool SimulateFailure = false);
public record ToolStepDto(string Tool, string ArgumentsJson, string ResultJson);
public record ChatResponse(string Reply, string TraceId, IReadOnlyList<ToolStepDto> ToolSteps);

[ApiController]
[Route("api/chat")]
public sealed class ChatController : ControllerBase
{
    private readonly ChatService _chat;

    public ChatController(ChatService chat) => _chat = chat;

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Post([FromBody] ChatRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Message))
            return BadRequest(new { error = "message is required." });

        var sessionId = string.IsNullOrWhiteSpace(req.SessionId) ? Guid.NewGuid().ToString() : req.SessionId;

        try
        {
            var result = await _chat.ChatAsync(sessionId, req.Mode, req.Message, req.SimulateFailure, ct);

            var steps = result.ToolSteps
                .Select(s => new ToolStepDto(s.Tool, s.ArgumentsJson, s.ResultJson))
                .ToList();

            return Ok(new ChatResponse(result.Reply, result.TraceId, steps));
        }
        catch (HttpRequestException ex)
        {
            // Upstream LLM unavailable (e.g. Ollama not running or model not pulled).
            // The failure is still recorded on the trace by TracedLlmProvider.
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "The AI provider is unavailable.", detail = ex.Message });
        }
    }
}
