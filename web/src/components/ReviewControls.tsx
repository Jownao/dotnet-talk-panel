import { useState } from 'react'
import { api } from '../api'
import type { Message, MessageRating } from '../types'

interface Props {
  message: Message
  /** Chamado depois que a avaliação foi salva, para a tela recarregar. */
  onSaved: () => void
}

export function ReviewControls({ message, onSaved }: Props) {
  const [comment, setComment] = useState(message.reviewComment ?? '')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const commentChanged = comment.trim() !== (message.reviewComment ?? '')

  async function save(rating: MessageRating) {
    setSaving(true)
    setError(null)
    try {
      await api.reviewMessage(message.id, { rating, comment })
      onSaved()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Não foi possível salvar.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="review">
      <div className="review-buttons">
        <button
          type="button"
          className="btn good"
          aria-pressed={message.rating === 'Good'}
          disabled={saving}
          onClick={() => save('Good')}
        >
          👍 Boa
        </button>
        <button
          type="button"
          className="btn bad"
          aria-pressed={message.rating === 'Failure'}
          disabled={saving}
          onClick={() => save('Failure')}
        >
          👎 Falha
        </button>
        {message.rating && commentChanged && (
          <button type="button" className="btn" disabled={saving} onClick={() => save(message.rating!)}>
            Salvar comentário
          </button>
        )}
        {saving && <span className="muted">Salvando…</span>}
      </div>
      <textarea
        value={comment}
        onChange={(e) => setComment(e.target.value)}
        placeholder="Comentário (opcional): o que deu errado ou por que foi boa?"
        maxLength={1000}
        rows={2}
        aria-label="Comentário da avaliação"
      />
      {error && <p className="notice error">{error}</p>}
    </div>
  )
}
