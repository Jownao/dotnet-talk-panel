# Fase 4 — Painel React Implementation Plan

> **Execução:** inline, TDD no backend, sem commit até o dono autorizar (nunca com coautor). Plano compacto: o código vive nos arquivos.

**Goal:** painel em `web/` para revisar conversas: lista com resumo e filtro de falhas, detalhe com Boa/Falha + comentário por resposta do agente (RF-05, RF-06, RF-08, RF-30 a RF-36).

**Architecture:** backend ganha `ReviewService`, `StatsService`, `MessagesController`, `StatsController` e CORS; frontend Vite + React + TypeScript, `react-router`, `fetch` num módulo `api.ts` consumido por hooks (`useFetch`), sem biblioteca de UI.

## Global Constraints

- `PATCH /api/messages/{id}/review` `{ rating: "Good" | "Failure", comment? }` → 200 `MessageDto`; 404 se não existe; 400 se a mensagem não é do agente ou o rating é inválido/ausente; reavaliar sobrescreve; comentário vazio vira `null` (máx. 1000).
- `GET /api/stats` → `{ totalConversations, totalAgentMessages, reviewedCount, goodCount, failureCount, failureRate, byPromptVersion[] }`; `failureRate` = falhas / avaliadas (0 sem avaliações).
- CORS: origens em `Cors:AllowedOrigins` (padrão `http://localhost:5173`).
- UI toda em PT-BR; tipos TS espelham os DTOs; chamadas só em `api.ts`; estados de carregando, erro e vazio tratados.
- Sem login (limitação documentada).

## Tarefas

1. **Review (backend, TDD)** — `IReviewService.ReviewAsync(int messageId, ReviewRequest, ct) → (ReviewOutcome, MessageDto?)` com `ReviewOutcome { Ok, NotFound, NotAgentMessage }`. Testes: avalia Good/Failure com comentário e `ReviewedAt`; comentário só com espaços → `null`; mensagem de aluno → `NotAgentMessage`; id inexistente → `NotFound`; reavaliar sobrescreve.
2. **Stats (backend, TDD)** — `IStatsService.GetAsync(ct) → StatsDto`. Testes: banco vazio zerado; contagens e `failureRate`; mensagens de aluno não contam; agrupamento por `promptVersion`.
3. **CORS** — policy `Panel` no `Program.cs`, `UseCors` antes de `MapControllers`.
4. **Frontend base** — `web/` (Vite react-ts), `react-router-dom`, `src/types.ts`, `src/api.ts`, `src/hooks.ts`.
5. **Telas** — `ConversationListPage` (resumo no topo, filtro "só com falhas", atualização automática), `ConversationDetailPage` (histórico + `ReviewControls` por resposta do agente), estilos.
6. **Verificação** — `npm run lint` e `npm run build` verdes; API + Vite no ar; fluxo no navegador: ver a conversa do Telegram, marcar uma resposta como falha com comentário, ver o resumo e o filtro mudarem.
