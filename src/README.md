# Meridian Logistics — AI Observability Demo

A fictional logistics company used to demonstrate **AI observability** with
**Langfuse** and **LangSmith**. The whole app is built so that observability is a
thin, swappable layer:

- a clean **facade** over the LLM vendor (`ILlmProvider`),
- observability as a **decorator** (`TracedLlmProvider`) emitting OpenTelemetry
  **GenAI** spans,
- Langfuse and LangSmith as mere **OTLP exporter configs** — zero business-code
  coupling.

Two AI features, both powered by a local Ollama model through its
OpenAI-compatible endpoint:

1. **Support Chat** — a plain chatbot answering shipping / company questions.
2. **Operations Agent** — a tool-calling agent that tracks packages, computes
   shipping quotes, and schedules pickups.

---

## Architecture

```
                         Browser (React + MUI SPA, :3000)
                                     │  /api  (Vite proxy)
                                     ▼
                     ASP.NET Core API  (MeridianLogistics.Api, :5210)
                                     │
                                     ▼
                              ILlmProvider  (facade)
                                     │
                          TracedLlmProvider  (decorator)
                       emits OTel GenAI spans ──────────────┐
                                     │                       │ OTLP / HttpProtobuf
                                     ▼                       ▼
                      OpenAiCompatibleLlmProvider     ┌──────────────┬──────────────┐
                        (raw HttpClient + JSON)       │  Langfuse    │  LangSmith   │
                                     │                │  :3001       │  cloud       │
                                     ▼                │ (always on)  │ (if API key) │
                          Ollama  (:11434/v1)         └──────────────┴──────────────┘
```

- The chat **root span** (`meridian.chat {mode}`) is the trace root. The LLM
  decorator span (`chat {model}`) and each `tool {name}` span are its children.
- Swapping the LLM vendor (Ollama → OpenAI / Anthropic / Groq / Azure gateway)
  is a **config change only** — no business code changes.
- Adding or removing a telemetry backend is a change in **one file**
  (`Observability/TelemetryExtensions.cs`) — nothing else.

---

## Prerequisites

- .NET SDK **10.0**
- Node **20+** (built with v24)
- Docker (for self-hosted Langfuse)
- [Ollama](https://ollama.com)

---

## Running the demo

### 1. Start Langfuse (self-hosted)

```bash
cd ../docker          # AiObservability/docker
docker compose up -d
```

Langfuse UI: <http://localhost:3001> (port 3001 because the SPA dev server owns
3000). The compose file auto-provisions an org, project `meridian-logistics`,
a user, and the API keys `pk-lf-meridian-demo` / `sk-lf-meridian-demo` that the
API is pre-configured to use.

Login (from `docker/.env`): `joao@defensepoint.com` / `MeridianDemo1!`

### 2. Start Ollama and pull the model

```bash
ollama serve                 # if not already running
ollama pull qwen2.5:7b       # the default model in appsettings.json
```

> A tool-capable model is recommended for the Operations Agent (e.g.
> `qwen2.5:7b`, `llama3.1:8b`). Change `Llm:Model` in `appsettings.json` to match
> whatever you have pulled.

### 3. Run the app

```bash
cd MeridianLogistics.Api
dotnet run
```

`dotnet run` uses **SpaProxy**: it launches the Vite dev server
(`npm run dev`) on :3000, opens the browser, and the API listens on :5210.
Vite proxies `/api` → :5210. The first run triggers `npm install` in
`meridian-client` if needed.

> If the SPA dependencies are not installed yet:
> `cd meridian-client && npm install`.

---

## Configuration

All settings live in `MeridianLogistics.Api/appsettings.json`.

| Section     | Key              | Default                      | Purpose |
|-------------|------------------|------------------------------|---------|
| `Llm`       | `Provider`       | `openai`                     | `openai` (OpenAI-compatible, incl. Ollama) or `anthropic`. |
| `Llm`       | `BaseUrl`        | `http://localhost:11434/v1`  | OpenAI-compatible endpoint (Ollama, OpenAI, Groq, Azure gateway, …). |
| `Llm`       | `ApiKey`         | *(empty)*                    | Optional Bearer token for the OpenAI-compatible endpoint. |
| `Llm`       | `Model`          | `qwen2.5:7b`                 | Model id. For `anthropic`, defaults to `claude-sonnet-5`. |
| `Llm`       | `AnthropicApiKey`| *(empty)*                    | API key used when `Provider = anthropic`. |
| `Llm`       | `Temperature`    | `0.2`                        | Sampling temperature. |
| `Langfuse`  | `BaseUrl`        | `http://localhost:3001`      | Self-hosted Langfuse base URL. |
| `Langfuse`  | `PublicKey`      | `pk-lf-meridian-demo`        | Langfuse public key (Basic auth + scores API). |
| `Langfuse`  | `SecretKey`      | `sk-lf-meridian-demo`        | Langfuse secret key. |
| `LangSmith` | `ApiKey`         | *(empty)*                    | **Paste a key to enable** the LangSmith exporter + feedback sink. |
| `LangSmith` | `Project`        | `meridian-logistics`         | LangSmith project name. |

### Switch the LLM vendor (config only)

**OpenAI:**
```jsonc
"Llm": { "Provider": "openai", "BaseUrl": "https://api.openai.com/v1",
         "ApiKey": "sk-...", "Model": "gpt-4o-mini" }
```

**Anthropic** (proves the facade — a different wire format behind the same
`ILlmProvider`):
```jsonc
"Llm": { "Provider": "anthropic", "AnthropicApiKey": "sk-ant-...",
         "Model": "claude-sonnet-5" }
```

### Enable LangSmith

Paste a LangSmith API key:
```jsonc
"LangSmith": { "ApiKey": "lsv2_pt_...", "Project": "meridian-logistics" }
```
On startup the API then registers a **second OTLP exporter** (spans go to both
Langfuse and LangSmith) and a **second feedback sink**. Leave the key empty to
run Langfuse-only. No code changes either way.

---

## API endpoints

| Method | Route            | Purpose |
|--------|------------------|---------|
| POST   | `/api/chat`      | `{ sessionId, mode, message }` → `{ reply, traceId, toolSteps[] }` |
| POST   | `/api/feedback`  | `{ traceId, value, comment? }` → `202`; fans out to all sinks |
| GET    | `/api/shipments` | Seeded shipment list (dashboard) |
| GET    | `/api/health`    | `{ api, ollama, provider, model }` |

---

## Project layout

```
src/
├── MeridianLogistics.Api/        ASP.NET Core API (net10.0)
│   ├── Llm/                      ILlmProvider facade + providers + tracing decorator
│   ├── Chat/                     ChatService (root span, agent loop) + ToolRegistry
│   ├── Feedback/                 IFeedbackSink + Langfuse/LangSmith sinks + fan-out
│   ├── Observability/            OTel setup + options (Langfuse / LangSmith OTLP)
│   ├── Data/                     In-memory FakeDataStore (seeded shipments)
│   └── Controllers/              chat, feedback, shipments, health
└── meridian-client/             Vite + React + TS SPA (MUI)
    └── src/pages/                Dashboard, Assistant
```
