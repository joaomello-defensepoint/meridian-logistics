# AI Observability — 30-minute talk kit

Everything for the presentation lives here:

| Folder | What it is |
|---|---|
| `slides/index.html` | The deck. 15 slides, fully offline — open in any browser. Keys: `←`/`→` navigate, `f` fullscreen, `n` presenter notes, `#/7` deep-links. |
| `docker/` | Self-hosted **Langfuse v4** (official compose, web on **:3001**). Credentials are auto-provisioned on first start. |
| `src/` | The demo app: **Meridian Logistics** — ASP.NET Core API (.NET 10) + React/MUI SPA, chatbot + tool-calling agent on a local Ollama model. See `src/README.md` for architecture and config. |
| `DEMO_SCRIPT.md` | Minute-by-minute run book for the talk, including pre-flight checks and fallbacks. |

## Quick start (full stack)

```bash
# 1. Langfuse (self-hosted) — http://localhost:3001
cd docker && cp .env.example .env && docker compose up -d

# 2. Local LLM
ollama serve &            # if not already running
ollama pull qwen2.5:7b    # once, ~4.7 GB

# 3. The app — launches the SPA dev server on http://localhost:3000 via SpaProxy
cd ../src/MeridianLogistics.Api && dotnet run
```

## Logins & keys (demo values)

| What | Value |
|---|---|
| Langfuse UI | http://localhost:3001 — `joao@defensepoint.com` / `MeridianDemo1!` |
| Langfuse project | `meridian-logistics` (org `defensepoint`) |
| Langfuse API keys | `pk-lf-meridian-demo` / `sk-lf-meridian-demo` (pre-wired into the app) |
| LangSmith | Optional — paste an API key into `src/MeridianLogistics.Api/appsettings.json` → `LangSmith:ApiKey` and restart; traces then ship to both backends. |

> Requirements: [Ollama](https://ollama.com), Docker, the .NET 10 SDK, and Node 18+.
> The LLM runs entirely locally via Ollama — no cloud keys needed. Switch providers
> (OpenAI / Anthropic) or enable LangSmith purely via `appsettings.json`.
