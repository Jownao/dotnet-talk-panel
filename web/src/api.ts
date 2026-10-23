import type { ConversationDetail, ConversationSummary, Message, ReviewInput, Stats } from './types'

const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5080'

export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.status = status
  }
}

// Único ponto que fala HTTP: o resto do painel só chama as funções de `api` abaixo.
async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response
  try {
    response = await fetch(`${BASE_URL}${path}`, {
      ...init,
      headers: { 'Content-Type': 'application/json', ...init?.headers },
    })
  } catch {
    throw new ApiError('Não foi possível falar com a API. Ela está rodando?', 0)
  }

  if (!response.ok) {
    // `detail` vem em português dos nossos ProblemDetails; o `title` padrão do ASP.NET vem em inglês, então não é usado.
    const problem = await response.json().catch(() => null)
    const fallback = response.status === 404 ? 'Não encontrado.' : `Erro ${response.status} da API.`
    throw new ApiError(problem?.detail ?? fallback, response.status)
  }

  return response.json() as Promise<T>
}

export const api = {
  listConversations: (hasFailures: boolean) =>
    request<ConversationSummary[]>(`/api/conversations?hasFailures=${hasFailures}`),

  getConversation: (id: number) => request<ConversationDetail>(`/api/conversations/${id}`),

  reviewMessage: (id: number, input: ReviewInput) =>
    request<Message>(`/api/messages/${id}/review`, {
      method: 'PATCH',
      body: JSON.stringify(input),
    }),

  getStats: () => request<Stats>('/api/stats'),
}
