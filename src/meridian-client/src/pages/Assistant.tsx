import { useEffect, useRef, useState } from 'react'
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Box,
  Checkbox,
  Chip,
  CircularProgress,
  FormControlLabel,
  IconButton,
  Link,
  Paper,
  Snackbar,
  Stack,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Tooltip,
  Typography,
} from '@mui/material'
import ExpandMoreIcon from '@mui/icons-material/ExpandMore'
import SendIcon from '@mui/icons-material/Send'
import ThumbUpIcon from '@mui/icons-material/ThumbUp'
import ThumbDownIcon from '@mui/icons-material/ThumbDown'
import OpenInNewIcon from '@mui/icons-material/OpenInNew'
import { api, type ChatMode, type ToolStep } from '../api'

const LANGFUSE_TRACE_URL = (traceId: string) =>
  `http://localhost:3001/project/meridian-logistics/traces/${traceId}`

interface Message {
  id: string
  role: 'user' | 'assistant'
  text: string
  traceId?: string
  toolSteps?: ToolStep[]
  feedback?: 1 | -1
}

const suggestions: Record<ChatMode, string[]> = {
  support: [
    'What services does Meridian offer?',
    'What are your business hours?',
    'Where is Meridian headquartered?',
  ],
  agent: [
    'Where is MER-2026-00417?',
    'Quote 12kg Lisbon → Berlin express',
    'Schedule a pickup tomorrow morning at Av. Liberdade 100',
  ],
}

function pretty(jsonStr: string): string {
  try {
    return JSON.stringify(JSON.parse(jsonStr), null, 2)
  } catch {
    return jsonStr
  }
}

export default function Assistant() {
  const [mode, setMode] = useState<ChatMode>('support')
  const [sessionId, setSessionId] = useState(() => crypto.randomUUID())
  const [messages, setMessages] = useState<Message[]>([])
  const [input, setInput] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [ollamaDown, setOllamaDown] = useState(false)
  const [simulateFailure, setSimulateFailure] = useState(false)
  const threadEnd = useRef<HTMLDivElement>(null)

  useEffect(() => {
    api
      .health()
      .then((h) => setOllamaDown(!h.ollama))
      .catch(() => setOllamaDown(true))
  }, [])

  useEffect(() => {
    threadEnd.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, busy])

  function switchMode(_: unknown, next: ChatMode | null) {
    if (!next || next === mode) return
    setMode(next)
    setSessionId(crypto.randomUUID()) // fresh conversation per mode
    setMessages([])
  }

  async function send(text: string) {
    const trimmed = text.trim()
    if (!trimmed || busy) return

    const userMsg: Message = { id: crypto.randomUUID(), role: 'user', text: trimmed }
    setMessages((prev) => [...prev, userMsg])
    setInput('')
    setBusy(true)

    try {
      const res = await api.chat(sessionId, mode, trimmed, simulateFailure)
      setMessages((prev) => [
        ...prev,
        {
          id: crypto.randomUUID(),
          role: 'assistant',
          text: res.reply,
          traceId: res.traceId,
          toolSteps: res.toolSteps,
        },
      ])
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Chat request failed')
    } finally {
      setBusy(false)
    }
  }

  async function vote(messageId: string, traceId: string, value: 1 | -1) {
    setMessages((prev) =>
      prev.map((m) => (m.id === messageId ? { ...m, feedback: value } : m)),
    )
    try {
      await api.feedback(traceId, value)
    } catch {
      setError('Could not send feedback (the trace is still recorded).')
    }
  }

  return (
    <Box sx={{ maxWidth: 860, mx: 'auto' }}>
      <Typography variant="h5" gutterBottom>
        AI Assistant
      </Typography>

      {ollamaDown && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          Ollama not reachable — start it with <code>ollama serve</code>.
        </Alert>
      )}

      <Stack
        direction="row"
        sx={{ mb: 2, flexWrap: 'wrap', gap: 1, alignItems: 'center', justifyContent: 'space-between' }}
      >
        <ToggleButtonGroup value={mode} exclusive onChange={switchMode} color="primary">
          <ToggleButton value="support">Support Chat</ToggleButton>
          <ToggleButton value="agent">Operations Agent</ToggleButton>
        </ToggleButtonGroup>
        <FormControlLabel
          control={
            <Checkbox
              size="small"
              color="warning"
              checked={simulateFailure}
              onChange={(e) => setSimulateFailure(e.target.checked)}
            />
          }
          label={
            <Typography variant="body2" color="text.secondary">
              Simulate LLM failure
            </Typography>
          }
        />
      </Stack>

      <Paper
        variant="outlined"
        sx={{ p: 2, minHeight: 360, mb: 2, bgcolor: 'background.default' }}
      >
        {messages.length === 0 && (
          <Typography color="text.secondary" sx={{ textAlign: 'center', mt: 6 }}>
            {mode === 'support'
              ? 'Ask about Meridian Logistics and its services.'
              : 'Ask the agent to track a package, quote a shipment, or schedule a pickup.'}
          </Typography>
        )}

        <Stack spacing={1.5}>
          {messages.map((m) => (
            <MessageBubble key={m.id} message={m} onVote={vote} />
          ))}
          {busy && (
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, pl: 1 }}>
              <CircularProgress size={16} />
              <Typography variant="body2" color="text.secondary">
                thinking…
              </Typography>
            </Box>
          )}
        </Stack>
        <div ref={threadEnd} />
      </Paper>

      <Stack direction="row" spacing={1} sx={{ mb: 1, flexWrap: 'wrap', gap: 1 }}>
        {suggestions[mode].map((s) => (
          <Chip
            key={s}
            label={s}
            variant="outlined"
            onClick={() => send(s)}
            disabled={busy}
          />
        ))}
      </Stack>

      <Stack direction="row" spacing={1}>
        <TextField
          fullWidth
          size="small"
          placeholder="Type a message and press Enter…"
          value={input}
          disabled={busy}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
              e.preventDefault()
              void send(input)
            }
          }}
        />
        <IconButton color="primary" disabled={busy || !input.trim()} onClick={() => void send(input)}>
          <SendIcon />
        </IconButton>
      </Stack>

      <Snackbar
        open={!!error}
        autoHideDuration={5000}
        onClose={() => setError(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert severity="error" onClose={() => setError(null)}>
          {error}
        </Alert>
      </Snackbar>
    </Box>
  )
}

function MessageBubble({
  message,
  onVote,
}: {
  message: Message
  onVote: (messageId: string, traceId: string, value: 1 | -1) => void
}) {
  const isUser = message.role === 'user'
  return (
    <Box sx={{ display: 'flex', justifyContent: isUser ? 'flex-end' : 'flex-start' }}>
      <Box sx={{ maxWidth: '85%' }}>
        <Paper
          elevation={0}
          sx={{
            p: 1.5,
            bgcolor: isUser ? 'primary.main' : 'background.paper',
            color: isUser ? 'primary.contrastText' : 'text.primary',
            border: isUser ? 'none' : '1px solid',
            borderColor: 'divider',
          }}
        >
          <Typography variant="body1" sx={{ whiteSpace: 'pre-wrap' }}>
            {message.text}
          </Typography>
        </Paper>

        {message.toolSteps && message.toolSteps.length > 0 && (
          <Accordion disableGutters sx={{ mt: 0.5 }}>
            <AccordionSummary expandIcon={<ExpandMoreIcon />}>
              <Typography variant="body2">
                🔧 {message.toolSteps.length} tool call
                {message.toolSteps.length > 1 ? 's' : ''}
              </Typography>
            </AccordionSummary>
            <AccordionDetails>
              <Stack spacing={1.5}>
                {message.toolSteps.map((step, i) => (
                  <Box key={i}>
                    <Typography variant="subtitle2" sx={{ fontFamily: 'monospace' }}>
                      {step.tool}
                    </Typography>
                    <ToolJson label="args" json={step.argumentsJson} />
                    <ToolJson label="result" json={step.resultJson} />
                  </Box>
                ))}
              </Stack>
            </AccordionDetails>
          </Accordion>
        )}

        {!isUser && message.traceId && (
          <Stack direction="row" spacing={0.5} sx={{ mt: 0.5, alignItems: 'center' }}>
            <Tooltip title="Helpful">
              <IconButton
                size="small"
                color={message.feedback === 1 ? 'primary' : 'default'}
                onClick={() => onVote(message.id, message.traceId!, 1)}
              >
                <ThumbUpIcon fontSize="small" />
              </IconButton>
            </Tooltip>
            <Tooltip title="Not helpful">
              <IconButton
                size="small"
                color={message.feedback === -1 ? 'error' : 'default'}
                onClick={() => onVote(message.id, message.traceId!, -1)}
              >
                <ThumbDownIcon fontSize="small" />
              </IconButton>
            </Tooltip>
            <Link
              href={LANGFUSE_TRACE_URL(message.traceId)}
              target="_blank"
              rel="noopener"
              variant="caption"
              sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.3 }}
            >
              View trace in Langfuse
              <OpenInNewIcon sx={{ fontSize: 12 }} />
            </Link>
          </Stack>
        )}
      </Box>
    </Box>
  )
}

function ToolJson({ label, json }: { label: string; json: string }) {
  return (
    <Box sx={{ mt: 0.5 }}>
      <Typography variant="caption" color="text.secondary">
        {label}
      </Typography>
      <Box
        component="pre"
        sx={{
          m: 0,
          p: 1,
          borderRadius: 1,
          bgcolor: 'grey.100',
          fontFamily: 'monospace',
          fontSize: 12,
          overflowX: 'auto',
          whiteSpace: 'pre',
        }}
      >
        {pretty(json)}
      </Box>
    </Box>
  )
}
