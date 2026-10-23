// Tipos que espelham os DTOs do backend (src/Api/Models/Dtos). Datas chegam como string ISO (UTC).

export type MessageRole = 'User' | 'Agent'
export type MessageRating = 'Good' | 'Failure'

export interface Message {
  id: number
  role: MessageRole
  content: string
  createdAt: string
  rating: MessageRating | null
  reviewComment: string | null
  reviewedAt: string | null
  promptVersion: string | null
}

export interface ConversationSummary {
  id: number
  studentName: string
  lastMessagePreview: string | null
  lastActivityAt: string
  failureCount: number
}

export interface ConversationDetail {
  id: number
  studentId: number
  studentName: string
  createdAt: string
  messages: Message[]
}

export interface PromptVersionStats {
  version: string
  agentMessages: number
  reviewed: number
  good: number
  failures: number
  failureRate: number
}

export interface Stats {
  totalConversations: number
  totalAgentMessages: number
  reviewedCount: number
  goodCount: number
  failureCount: number
  failureRate: number
  byPromptVersion: PromptVersionStats[]
}

export interface ReviewInput {
  rating: MessageRating
  comment: string
}
