const dateTime = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' })

export const formatDateTime = (iso: string) => dateTime.format(new Date(iso))

export const formatPercent = (rate: number) => `${Math.round(rate * 100)}%`
