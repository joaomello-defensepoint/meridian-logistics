using MeridianLogistics.Api.Chat;
using MeridianLogistics.Api.Data;
using MeridianLogistics.Api.Feedback;
using MeridianLogistics.Api.Llm;
using MeridianLogistics.Api.Observability;

var builder = WebApplication.CreateBuilder(args);

// camelCase JSON out of the box for the SPA.
builder.Services.AddControllers();

// --- Options ---
builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection(LlmOptions.SectionName));
builder.Services.Configure<LangfuseOptions>(builder.Configuration.GetSection(LangfuseOptions.SectionName));
builder.Services.Configure<LangSmithOptions>(builder.Configuration.GetSection(LangSmithOptions.SectionName));

// --- LLM facade: concrete provider by config, wrapped in the tracing decorator ---
builder.Services.AddLlmProvider(builder.Configuration);

// --- Domain ---
builder.Services.AddSingleton<FakeDataStore>();
builder.Services.AddSingleton<ToolRegistry>();
builder.Services.AddSingleton<ChatService>();

// --- Feedback fan-out ---
builder.Services.AddHttpClient<LangfuseFeedbackSink>();
builder.Services.AddSingleton<IFeedbackSink>(sp => sp.GetRequiredService<LangfuseFeedbackSink>());
if (!string.IsNullOrWhiteSpace(builder.Configuration[$"{LangSmithOptions.SectionName}:ApiKey"]))
{
    builder.Services.AddHttpClient<LangSmithFeedbackSink>();
    builder.Services.AddSingleton<IFeedbackSink>(sp => sp.GetRequiredService<LangSmithFeedbackSink>());
}
builder.Services.AddSingleton<FeedbackService>();

// --- Observability: Langfuse (+ LangSmith) OTLP exporters ---
builder.Services.AddMeridianTelemetry(builder.Configuration);

// Generic HttpClient for the health probe.
builder.Services.AddHttpClient();

var app = builder.Build();

app.MapControllers();

// Serve the built SPA in production; in Development SpaProxy forwards to Vite.
app.MapFallbackToFile("index.html");

app.Run();
