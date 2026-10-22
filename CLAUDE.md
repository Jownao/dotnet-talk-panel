# Projeto: agente-atendimento-lab

## Contexto
Agente de IA para atendimento de alunos de um curso fictício de idiomas, com painel interno para revisar conversas.
Projeto de portfólio para vaga júnior .NET, React e IA (entrevista em 27/10/2026).
O dono do projeto é júnior em C#, então explique o que gerar em linguagem simples e compare com Python/FastAPI quando ajudar.

A fonte da verdade do escopo é `docs/PRD.md`. Planos de implementação ficam em `docs/superpowers/plans/`.

## Stack
ASP.NET Core (.NET 8), EF Core, PostgreSQL, React com TypeScript (Vite), xUnit, Docker, n8n (Telegram), Gemini.

## Regras de arquitetura
- Camadas: Controllers -> Services -> DbContext (sem repositório extra)
- DTOs separados das entidades
- Dependências injetadas por interface (ex.: `IAiClient`)
- Nunca colocar chaves ou segredos no código ou no repositório
- Código simples e legível, sem abstrações desnecessárias (YAGNI)
- Todo service novo precisa de teste unitário (TDD: teste falhando primeiro)
- Nullable reference types ligado; `async/await` com `CancellationToken` nos endpoints

## Regras de processo (do PRD)
- Código em inglês; README, commits, UI e prompts em PT-BR
- Um commit por fase, mensagem em PT-BR (`feat:`, `test:`, `docs:`). Só commitar quando o dono pedir
- Em cada fase, o dono escreve ao menos uma parte sozinho (marcada `[SUA VEZ]` no plano)
- Ao terminar cada tarefa, resumir o que foi feito e como funciona
- Ao fim de cada fase, explicar o código para o dono reescrever com as próprias palavras no README
- Se o tempo apertar, cortar escopo e não qualidade (ordem de corte na seção 12 do PRD)

## Comandos
- Subir banco: `docker compose up -d` (Docker Desktop precisa estar aberto). Postgres do container na porta **5433** do host (a 5432 é de um Postgres nativo do Windows)
- Rodar API: `dotnet run --project src/Api` (Swagger em http://localhost:5080/swagger)
- Testes: `dotnet test`
- Nova migration: `dotnet ef migrations add <Nome> --project src/Api --output-dir Data/Migrations`
- Frontend (Fase 4): `cd web && npm run dev`
