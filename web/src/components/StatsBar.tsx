import { useStats } from '../hooks'
import { formatPercent } from '../format'

export function StatsBar() {
  const { data: stats, error } = useStats()

  if (error && !stats) return <p className="notice error">Resumo indisponível: {error}</p>
  if (!stats) return <p className="notice">Carregando resumo…</p>

  const goodRate = stats.reviewedCount === 0 ? 0 : stats.goodCount / stats.reviewedCount

  return (
    <section aria-label="Resumo de qualidade">
      <div className="stats">
        <Stat label="Conversas" value={stats.totalConversations} />
        <Stat label="Respostas do agente" value={stats.totalAgentMessages} />
        <Stat label="Avaliadas" value={stats.reviewedCount} />
        <Stat label="Boas" value={stats.reviewedCount === 0 ? '—' : formatPercent(goodRate)} tone="good" />
        <Stat label="Falhas" value={stats.reviewedCount === 0 ? '—' : formatPercent(stats.failureRate)} tone="bad" />
      </div>
      {stats.byPromptVersion.length > 0 && (
        <p className="by-version">
          Por versão do prompt:{' '}
          {stats.byPromptVersion
            .map((v) =>
              v.reviewed === 0
                ? `${v.version}: sem avaliações`
                : `${v.version}: ${v.failures} falha(s) em ${v.reviewed} avaliada(s) (${formatPercent(v.failureRate)})`,
            )
            .join(' · ')}
        </p>
      )}
    </section>
  )
}

function Stat({ label, value, tone }: { label: string; value: number | string; tone?: 'good' | 'bad' }) {
  return (
    <div className={`stat ${tone ?? ''}`}>
      <span className="stat-value">{value}</span>
      <span className="stat-label">{label}</span>
    </div>
  )
}
