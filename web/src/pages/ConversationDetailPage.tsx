import { Link } from 'react-router-dom'
import { ReviewControls } from '../components/ReviewControls'
import { formatDateTime } from '../format'
import { useConversation } from '../hooks'

export function ConversationDetailPage({ id }: { id: number }) {
  const { data: conversation, error, loading, reload } = useConversation(id)

  return (
    <>
      <p>
        <Link to="/">← Voltar para as conversas</Link>
      </p>

      {error && (
        <p className="notice error">
          {error} <button type="button" className="btn" onClick={reload}>Tentar de novo</button>
        </p>
      )}
      {loading && !conversation && <p className="notice">Carregando conversa…</p>}

      {conversation && (
        <>
          <header className="detail-header">
            <h2>{conversation.studentName}</h2>
            <span className="muted">Conversa iniciada em {formatDateTime(conversation.createdAt)}</span>
          </header>

          {conversation.messages.length === 0 && <p className="notice">Esta conversa ainda não tem mensagens.</p>}

          <ol className="messages">
            {conversation.messages.map((m) => (
              <li key={m.id} className={`message ${m.role === 'Agent' ? 'agent' : 'user'}`}>
                <div className="message-head">
                  <strong>{m.role === 'Agent' ? 'Agente' : conversation.studentName}</strong>
                  <time dateTime={m.createdAt}>{formatDateTime(m.createdAt)}</time>
                  {m.promptVersion && <span className="badge">prompt {m.promptVersion}</span>}
                  {m.rating === 'Good' && <span className="badge good">Boa</span>}
                  {m.rating === 'Failure' && <span className="badge bad">Falha</span>}
                </div>
                <p className="message-body">{m.content}</p>
                {m.role === 'Agent' && <ReviewControls key={m.id} message={m} onSaved={reload} />}
              </li>
            ))}
          </ol>
        </>
      )}
    </>
  )
}
