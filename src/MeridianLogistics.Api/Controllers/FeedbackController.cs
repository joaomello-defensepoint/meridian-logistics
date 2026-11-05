using Microsoft.AspNetCore.Mvc;
using MeridianLogistics.Api.Feedback;

namespace MeridianLogistics.Api.Controllers;

public record FeedbackRequest(string TraceId, int Value, string? Comment);

[ApiController]
[Route("api/feedback")]
public sealed class FeedbackController : ControllerBase
{
    private readonly FeedbackService _feedback;

    public FeedbackController(FeedbackService feedback) => _feedback = feedback;

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] FeedbackRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.TraceId))
            return BadRequest(new { error = "traceId is required." });

        await _feedback.SubmitAsync(req.TraceId, req.Value, req.Comment, ct);
        return Accepted();
    }
}
