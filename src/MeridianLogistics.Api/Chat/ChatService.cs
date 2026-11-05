using System.Collections.Concurrent;
using System.Diagnostics;
using MeridianLogistics.Api.Llm;

namespace MeridianLogistics.Api.Chat;

public record ToolStep(string Tool, string ArgumentsJson, string ResultJson);

public record ChatResult(string Reply, string TraceId, IReadOnlyList<ToolStep> ToolSteps);

/// <summary>
/// Orchestrates a chat turn for either the plain "support" chatbot or the
/// tool-calling "agent". It owns the ROOT span of each trace; the LLM decorator
/// and tool spans hang off it as children.
/// </summary>
public sealed class ChatService
{
    private const int MaxToolIterations = 5;

    private readonly ILlmProvider _llm;
    private readonly ToolRegistry _tools;

    // Per-session, per-mode conversation memory. Key: "{sessionId}:{mode}".
    private readonly ConcurrentDictionary<string, List<LlmMessage>> _history = new();

    public ChatService(ILlmProvider llm, ToolRegistry tools)
    {
        _llm = llm;
        _tools = tools;
    }

    public async Task<ChatResult> ChatAsync(string sessionId, string mode, string message, bool simulateFailure, CancellationToken ct)
    {
        mode = mode == "agent" ? "agent" : "support";

        // ASP.NET Core creates its own (unsampled) Activity per request; with the
        // default ParentBased sampler that would drop every child span we start.
        // Detach from it so our chat span is a true sampled root of the trace.
        Activity.Current = null;

        // Root span: the trace root. Everything else becomes a child of this.
        // Its name becomes the trace name shown in Langfuse.
        var traceName = mode == "agent" ? "meridian support agent" : "meridian support chat";
        using var activity = TracedLlmProvider.Source.StartActivity(
            traceName, ActivityKind.Server);

        activity?.SetTag("session.id", sessionId);
        activity?.SetTag("langfuse.session.id", sessionId);
        activity?.SetTag("user.id", "demo-user");
        activity?.SetTag("langfuse.user.id", "demo-user");
        activity?.SetTag("chat.mode", mode);
        activity?.SetTag("langfuse.trace.tags", new[] { "meridian", mode, "demo" });
        activity?.SetTag("input.value", message);

        var history = _history.GetOrAdd($"{sessionId}:{mode}", _ => NewConversation(mode));
        history.Add(new LlmMessage("user", message));

        var toolSteps = new List<ToolStep>();
        try
        {
            var reply = mode == "agent"
                ? await RunAgentLoopAsync(history, toolSteps, simulateFailure, ct)
                : await RunSupportAsync(history, simulateFailure, ct);

            activity?.SetTag("output.value", reply);

            var traceId = activity?.TraceId.ToString() ?? "";
            return new ChatResult(reply, traceId, toolSteps);
        }
        catch (Exception ex)
        {
            // Flag the whole trace as errored, not just the generation span.
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    // Prompt identities — these names show on each generation in Langfuse and
    // link to managed prompts if you register them there.
    private const string SupportPromptName = "meridian-support";
    private const string AgentPromptName = "meridian-agent";
    private const int PromptVersion = 1;

    private async Task<string> RunSupportAsync(List<LlmMessage> history, bool simulateFailure, CancellationToken ct)
    {
        var response = await _llm.CompleteAsync(
            new LlmRequest(history.ToList(), PromptName: SupportPromptName, PromptVersion: PromptVersion,
                SimulateFailure: simulateFailure), ct);
        var reply = response.Message.Content ?? "";
        history.Add(new LlmMessage("assistant", reply));
        return reply;
    }

    private async Task<string> RunAgentLoopAsync(
        List<LlmMessage> history, List<ToolStep> toolSteps, bool simulateFailure, CancellationToken ct)
    {
        for (var i = 0; i < MaxToolIterations; i++)
        {
            var response = await _llm.CompleteAsync(
                new LlmRequest(history.ToList(), _tools.Tools,
                    PromptName: AgentPromptName, PromptVersion: PromptVersion,
                    SimulateFailure: simulateFailure), ct);

            var msg = response.Message;

            if (msg.ToolCalls is not { Count: > 0 })
            {
                var reply = msg.Content ?? "";
                history.Add(new LlmMessage("assistant", reply));
                return reply;
            }

            // Record the assistant's tool-call turn, then execute each call.
            history.Add(msg);

            foreach (var call in msg.ToolCalls)
            {
                using var toolActivity = TracedLlmProvider.Source.StartActivity(
                    $"tool {call.Name}", ActivityKind.Internal);
                toolActivity?.SetTag("tool.name", call.Name);
                toolActivity?.SetTag("input.value", call.ArgumentsJson);

                var result = _tools.Execute(call.Name, call.ArgumentsJson);

                toolActivity?.SetTag("output.value", result);

                toolSteps.Add(new ToolStep(call.Name, call.ArgumentsJson, result));
                history.Add(new LlmMessage("tool", result, ToolCallId: call.Id));
            }
        }

        var fallback = "I wasn't able to complete that within the allowed number of steps. Could you rephrase or narrow the request?";
        history.Add(new LlmMessage("assistant", fallback));
        return fallback;
    }

    private static List<LlmMessage> NewConversation(string mode) =>
        [new LlmMessage("system", mode == "agent" ? AgentPrompt : SupportPrompt)];

    private static readonly string SupportPrompt =
        """
        You are the virtual support assistant for Meridian Logistics, a logistics company.
        Answer customer questions about the company and its shipping services clearly and concisely.

        Company facts:
        - Meridian Logistics was founded in 2009 and is headquartered in Lisbon, Portugal.
        - Services: Ground (road freight across Europe), Air (fast international), and Express
          (next-day priority).
        - Operating region: Portugal and across the European Union.
        - Business hours: Monday-Friday 08:00-19:00 WET; Saturday 09:00-13:00; closed Sundays
          and Portuguese public holidays.
        - Customer support: support@meridian-logistics.example, +351 210 000 000.
        - Tracking numbers look like MER-2026-00417.

        If a customer asks you to track a specific package, compute a quote, or schedule a pickup,
        explain that those actions are handled by the Operations Agent (the other mode), since you
        do not have live system access here. Never invent tracking statuses or prices.
        """;

    private static readonly string AgentPrompt =
        $"""
        You are the Meridian Logistics Operations Agent. You help operators and customers by
        calling tools to track packages, compute shipping quotes, and schedule pickups.

        Guidelines:
        - Use tools whenever the user asks for live data or an action; do not guess tracking
          statuses, prices, or confirmation ids.
        - Be concise. After a tool returns, summarize the result in one or two sentences.
        - If required details are missing (e.g. a weight or destination), ask one brief question.
        - Today's date is {DateOnly.FromDateTime(DateTime.UtcNow.Date):yyyy-MM-dd}. Resolve relative
          dates like "tomorrow" against it when scheduling pickups.
        """;
}
