# Painel de revisão (web)

Painel interno em React + TypeScript (Vite) para revisar as conversas do agente. Veja o [README principal](../README.md) para o projeto completo.

```bash
npm ci
npm run dev      # http://localhost:5173 (a API precisa estar em http://localhost:5080)
npm run lint
npm run build
```

A URL da API vem de `VITE_API_URL` (padrão `http://localhost:5080`).

| Pasta | Conteúdo |
|---|---|
| `src/api.ts` | Único módulo que fala HTTP com a API. |
| `src/hooks.ts` | `useFetch` e os hooks `useConversations`, `useConversation`, `useStats` (atualizam a cada 5 s). |
| `src/types.ts` | Tipos TypeScript que espelham os DTOs do backend. |
| `src/pages` | Lista de conversas e detalhe da conversa. |
| `src/components` | Resumo de qualidade (`StatsBar`) e botões Boa/Falha com comentário (`ReviewControls`). |
