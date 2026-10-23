import { useState } from 'react'
import { Link } from 'react-router-dom'
import { StatsBar } from '../components/StatsBar'
import { formatDateTime } from '../format'
import { useConversations } from '../hooks'

export function ConversationListPage() {
  const [onlyFailures, setOnlyFailures] = useState(false)
  const { data: conversations, error, loading, reload } = useConversations(onlyFailures)

  return (
    <>
      <StatsBar />

      <div className="toolbar">
        <h2>Conversas</h2>
        <label className="check">
          <input type="checkbox" checked={onlyFailures} onChange={(e) => setOnlyFailures(e.target.checked)} />
          Só com falhas
        </label>
      </div>

      {error && (
        <p className="notice error">
          {error} <button type="button" className="btn" onClick={reload}>Tentar de novo</button>
        </p>
      )}
      {loading && !conversations && <p className="notice">Carregando conversas…</p>}

      {conversations && conversations.length === 0 && (
        <p className="notice">
          {onlyFailures
            ? 'Nenhuma conversa com falhas marcadas.'
            : 'Nenhuma conversa ainda. Mande uma mensagem para o bot no Telegram.'}
        </p>
      )}

      <ul className="conversation-list">
        {conversations?.map((c) => (
          <li key={c.id}>
            <Link to={`/conversas/${c.id}`} className="conversation-row">
              <div className="conversation-main">
                <strong>{c.studentName}</strong>
                <span className="preview">{c.lastMessagePreview ?? 'Sem mensagens'}</span>
              </div>
              <div className="conversation-meta">
                {c.failureCount > 0 && <span className="badge bad">{c.failureCount} falha(s)</span>}
                <time dateTime={c.lastActivityAt}>{formatDateTime(c.lastActivityAt)}</time>
              </div>
            </Link>
          </li>
        ))}
      </ul>
    </>
  )
}
