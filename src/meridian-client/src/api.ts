// Thin typed wrapper over the API. Same-origin: Vite proxies /api to the backend.

export interface Shipment {
  trackingNumber: string
  origin: string
  destination: string
  status: string
  service: string
  eta: string
  weightKg: number
}

export interface Pickup {
  confirmationId: string
  address: string
  date: string
  timeWindow: string
}

export interface ToolStep {
  tool: string
  argumentsJson: string
  resultJson: string
}

export interface ChatResponse {
  reply: string
  traceId: string
  toolSteps: ToolStep[]
}

export interface Health {
  api: boolean
  ollama: boolean
  provider: string
  model: string
}

export type ChatMode = 'support' | 'agent'

async function json<T>(res: Response): Promise<T> {
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
  return (await res.json()) as T
}

export const api = {
  shipments: () => fetch('/api/shipments').then(json<Shipment[]>),

  pickups: () => fetch('/api/pickups').then(json<Pickup[]>),

  health: () => fetch('/api/health').then(json<Health>),

  chat: (sessionId: string, mode: ChatMode, message: string, simulateFailure = false) =>
    fetch('/api/chat', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ sessionId, mode, message, simulateFailure }),
    }).then(json<ChatResponse>),

  feedback: (traceId: string, value: number, comment?: string) =>
    fetch('/api/feedback', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ traceId, value, comment }),
    }),
}
