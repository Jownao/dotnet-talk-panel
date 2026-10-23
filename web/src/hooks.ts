import { useCallback, useEffect, useRef, useState } from 'react'
import type { DependencyList } from 'react'
import { api } from './api'
import type { ConversationDetail, ConversationSummary, Stats } from './types'

interface FetchState<T> {
  data: T | null
  error: string | null
  loading: boolean
}

/**
 * Busca dados quando o componente aparece (e quando `deps` mudam) e, se `refreshMs` for passado,
 * busca de novo a cada intervalo. Durante uma atualização os dados antigos continuam na tela.
 */
function useFetch<T>(fetcher: () => Promise<T>, deps: DependencyList, refreshMs?: number) {
  const [state, setState] = useState<FetchState<T>>({ data: null, error: null, loading: true })
  const [reloadCount, setReloadCount] = useState(0)

  // `fetcher` é uma função nova a cada render. Guardá-la numa ref evita refazer a busca à toa:
  // só `deps`, `reload()` e `refreshMs` disparam uma nova busca.
  const fetcherRef = useRef(fetcher)
  useEffect(() => {
    fetcherRef.current = fetcher
  })

  const depsKey = JSON.stringify(deps)

  useEffect(() => {
    let cancelled = false

    const load = () => {
      fetcherRef
        .current()
        .then((data) => {
          if (!cancelled) setState({ data, error: null, loading: false })
        })
        .catch((e: unknown) => {
          if (!cancelled) {
            setState((s) => ({ ...s, error: e instanceof Error ? e.message : 'Erro inesperado.', loading: false }))
          }
        })
    }

    load()
    const timer = refreshMs ? setInterval(load, refreshMs) : undefined

    return () => {
      cancelled = true
      if (timer) clearInterval(timer)
    }
  }, [depsKey, reloadCount, refreshMs])

  const reload = useCallback(() => setReloadCount((n) => n + 1), [])

  return { ...state, reload }
}

const REFRESH_MS = 5000

export const useConversations = (hasFailures: boolean) =>
  useFetch<ConversationSummary[]>(() => api.listConversations(hasFailures), [hasFailures], REFRESH_MS)

export const useConversation = (id: number) =>
  useFetch<ConversationDetail>(() => api.getConversation(id), [id], REFRESH_MS)

export const useStats = () => useFetch<Stats>(() => api.getStats(), [], REFRESH_MS)
