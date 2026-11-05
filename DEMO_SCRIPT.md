# Demo run book — "Do you actually know what your AI is doing?"

A ~30–35 min talk that **splits into two sessions** at the Part 2 divider (slide 18), or **stretches to 45 min** by driving evals + prompt management live instead of touring them.

## T-30 min: pre-flight checklist

Run these **before** the talk; everything must be green.

```bash
# 1. Langfuse up?
curl -s http://localhost:3001/api/public/health        # → {"status":"OK",...}

# 2. Ollama up with the model?
curl -s http://localhost:11434/api/tags | grep qwen2.5 # → model listed

# 3. App up? (starts Vite on :3000 automatically)
cd src/MeridianLogistics.Api && dotnet run
curl -s http://localhost:5210/api/health               # → {"api":true,"ollama":true,...}
```

Open four tabs, in order:
1. `slides/index.html` (press `f` for fullscreen, `n` for presenter notes)
2. http://localhost:3000 (Meridian Logistics app)
3. http://localhost:3001 (Langfuse — logged in: `joao@defensepoint.com` / `MeridianDemo1!`)
4. *(if you got a key)* https://smith.langchain.com — project `meridian-logistics`

**Warm the model**: send one throwaway message in the app before you start — the first Ollama call loads ~5 GB into RAM and is slow.

**Re-seed the synthetic cost** (only if you ran `docker compose down -v` since last time — the qwen price lives in Langfuse's DB):
```bash
auth=$(printf 'pk-lf-meridian-demo:sk-lf-meridian-demo' | base64)
curl -s -X POST http://localhost:3001/api/public/models -H "Authorization: Basic $auth" \
  -H "Content-Type: application/json" \
  -d '{"modelName":"qwen2.5:7b","matchPattern":"(?i)^(qwen2\\.5:7b)$","unit":"TOKENS","inputPrice":0.001,"outputPrice":0.003}'
```
Then fire one fresh agent message so a trace gets priced at ingestion.

## Timeline (flexible — baseline ~32 min)

| Time | What | Slides |
|---|---|---|
| 0:00–2:00 | Title + the provocation (plant the 3 questions) | 1–2 |
| 2:00–5:00 | The gap (200 OK ≠ right) + **the spine / ladder** | 3–4 |
| 5:00–8:00 | What's new + core vocabulary | 5–6 |
| 8:00–12:00 | Landscape, Langfuse, LangSmith, head-to-head | 7–10 |
| 12:00–16:00 | Vendor-agnostic play, architecture, the two code slides | 11–14 |
| 16:00–22:00 | **PART 1 — live**: traces & tool graph → cost dashboard | 15–17 |
| 22:00–28:00 | **PART 2 — live**: feedback → evals & prompt mgmt | 18–20 |
| 28:00–32:00 | What it unlocks, takeaways, Q&A | 21–22 |

**Splitting into two talks?** End session one at slide 17 (visibility), open session two at slide 18 (quality). **Going to 45?** Expand Part 2 (slide 20) from a tour into a live eval run.

Keep pointing back to the **ladder (slide 4)** between sections — every demo beat is one step rightward, from reactive to proactive.

## Live demo — step by step

### PART 1 · See what it's doing (slides 15–17)

**Act 1 — Trace & tool graph** → answers *"be in control of what it's doing"*
1. App → AI Assistant → **Operations Agent**.
2. Ask: *"Quote 25kg Lisbon → Munich express, and schedule a pickup for 2026-11-05 afternoon at Rua Augusta 50, Lisbon."*
3. Click **View trace in Langfuse ↗**. You get the tree on slide 16: root `meridian.chat agent` → `chat qwen2.5:7b` → `tool get_shipping_quote` → `tool schedule_pickup` → `chat qwen2.5:7b`.
4. **Expand a node** → real prompt, completion, tool args/result. Point out generation (has model + tokens + cost) vs span (tool/root).
5. Toggle **Tree → Timeline** (the two model calls ate ~14 s; tools were instant — the latency story) **→ Graph**.
6. *(optional, needs key)* Open the **same trace in LangSmith** → "no business code changed; one extra OTLP exporter."

**Act 2 — Cost** → answers *"do we wait for the month-end bill?"*
1. Langfuse → **Dashboards → Langfuse Cost Dashboard**: Total, Cost by Model, Cost by User, Top Use Cases — all from the traces.
2. The gag: *"My local 7B is free — but I gave it a $1,000 / $3,000-per-million 'enterprise' price so you can see it. It's the only model I had to price by hand; Claude and GPT are already in Langfuse's table."*

### PART 2 · Know when it's wrong (slides 18–20)

**Act 3 — Feedback** → answers *"do we wait for a customer complaint?"*
1. In the app, 👎 an agent reply.
2. Langfuse → that trace → **Scores**: `user-feedback = -1` with the comment.
3. **Sessions** → the whole conversation grouped; **Users** → `demo-user`. "The complaint is now a data point, pinned to the trace that caused it."

**Act 4 — Proactive (tour, or live at 45 min)**
1. **Datasets / Evaluators**: turn that thumbs-down into a test case; score every prompt/model change before prod.
2. **Prompts**: versioned prompts, swap without a deploy, watch scores, roll back. "Measured rollouts, not vibes."
3. Land on the far-right rung of the ladder — you've gone from reactive to proactive. → slides 21–22.

## If things go wrong

| Symptom | Fix |
|---|---|
| Chat errors / health `ollama:false` | `~/Applications/Ollama.app/Contents/Resources/ollama serve` |
| First reply very slow | Model cold-load; you warmed it pre-talk, right? |
| Agent picks wrong tool / rambles | **Use it** — "and THIS is why you need observability" → open the trace and show exactly what it did. Best recovery there is. |
| No traces in Langfuse | Batched — wait ~5 s, refresh. Check `docker compose ps` in `docker/`. |
| No cost on a trace | Cost is computed at **ingestion** — only traces created *after* the price exists show it. Fire a fresh message. |
| Langfuse down | `cd docker && docker compose up -d`; cold start ≈ 1 min. |
| Everything on fire | Slides stand alone; the ladder + architecture + trace-tree slides carry the story. |
