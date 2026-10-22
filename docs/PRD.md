# PRD — Agente de IA para atendimento com painel de análise de conversas

> Projeto de portfólio: `agente-atendimento-lab`
> Status: **aprovado no brainstorming (2026-10-22)** — aguardando revisão final antes do desenvolvimento.
> Idioma: código em inglês; README, commits, UI do painel e prompts do agente em PT-BR.

---

## 1. Visão geral

Um agente de IA atende, por mensagem no Telegram, alunos de um curso fictício de idiomas. Um painel web interno permite que a equipe leia as conversas, marque cada resposta do agente como **Boa** ou **Falha** (com comentário) e acompanhe um resumo de qualidade. As falhas marcadas alimentam a melhoria do prompt, e cada versão do prompt é documentada com antes e depois.

### 1.1 Por que existe (objetivo do projeto)

Chegar à entrevista de terça (27/10/2026) com um projeto funcionando em .NET, React e IA, publicado no GitHub, que o dono consiga explicar linha por linha. O projeto espelha as tarefas da vaga: melhorar agentes de IA, analisar conversas reais, integrar sistemas e construir telas internas.

### 1.2 Regras do jogo (restrições de processo)

| # | Regra |
|---|---|
| R1 | O dono entende tudo que a IA escrever. Ao fim de cada fase, pede explicação do código e reescreve com as próprias palavras no README. |
| R2 | Um commit por fase, com mensagem clara em PT-BR. |
| R3 | O dono escreve ao menos uma parte sozinho em cada fase (endpoint, teste ou componente). |
| R4 | Se o tempo apertar, corta escopo, não qualidade (ordem de corte na seção 12). |
| R5 | Chave de API nunca entra no repositório (variável de ambiente ou user-secrets). |

### 1.3 Personas

- **Aluno (fictício):** escreve no Telegram e espera respostas corretas, curtas e no tom do curso.
- **Revisor da equipe:** abre o painel, lê conversas, avalia respostas e identifica padrões de falha.
- **Dono do projeto (desenvolvedor júnior em C#):** constrói, explica e defende o projeto na entrevista.

---

## 2. Escopo

### 2.1 Dentro

1. Agente de FAQ com **base de conhecimento fixa** (preços, horários, níveis, matrícula, cancelamento do curso fictício), injetada no prompt de sistema.
2. Canal Telegram via n8n.
3. API ASP.NET Core com conversas, mensagens, avaliação e estatísticas.
4. Painel React: lista, detalhe, avaliação Boa/Falha com comentário, filtro de falhas, resumo no topo.
5. Testes xUnit, CI no GitHub Actions, README com diagrama e melhoria de prompt documentada.

### 2.2 Fora (explicitamente)

- Login, autenticação e autorização (limitação documentada; próximo passo: JWT/Identity).
- Dashboard com gráficos, tags de tipo de falha.
- Agente que consulta dados individuais do aluno (turma, próxima aula).
- Nova conversa por sessão/inatividade (existe 1 conversa contínua por aluno).
- Deploy em nuvem.
- Múltiplos canais além do Telegram.

---

## 3. Stack

| Camada | Tecnologia |
|---|---|
| Backend | C# · ASP.NET Core (.NET 8 LTS) |
| Banco | PostgreSQL 16 · Entity Framework Core (migrations) |
| IA | API do Gemini via `HttpClient` |
| Frontend | React + TypeScript (Vite), `react-router`, `fetch` e hooks próprios, sem biblioteca de UI |
| Canal | n8n (Telegram Trigger → HTTP → Telegram) + túnel cloudflared/ngrok |
| Testes | xUnit · SQLite em memória (testes de serviço) · `IAiClient` falso |
| Infra | Docker Compose (Postgres e n8n) · GitHub Actions |

---

## 4. Arquitetura

### 4.1 Camadas

```
Telegram ─► n8n ─► Controllers (HTTP) ─► Services ─► IAiClient ─► Gemini
                                            │
Painel React ──────► Controllers            └────► AppDbContext ─► PostgreSQL
```

`Controllers (HTTP) → Services (regras e chamada da IA) → DbContext (dados)`.

Pontos a saber explicar:

- **Injeção de dependência:** tudo registrado no `Program.cs`; Services dependem de interfaces.
- **`IAiClient`:** isola o provedor de IA. Permite trocar de provedor e testar o `AgentService` com um falso, sem chamar a API real.
- **DTOs separados das entidades:** a API não expõe o modelo do banco; o contrato HTTP pode evoluir sem migration.
- **Sem repositório extra:** o `DbContext` já é unit of work e repositório; uma camada a mais seria abstração desnecessária.
- **n8n:** o canal fica fora do código C#; trocar Telegram por outro canal não muda a API.

### 4.2 Estrutura de pastas

```
agente-atendimento-lab/
├─ CLAUDE.md
├─ docker-compose.yml
├─ docs/PRD.md
├─ n8n/                      # fluxo exportado (JSON)
├─ src/Api/
│  ├─ Controllers/
│  ├─ Services/
│  ├─ Data/                  # AppDbContext, Migrations
│  ├─ Models/
│  │  ├─ Entities/
│  │  └─ Dtos/
│  ├─ Ai/                    # IAiClient, GeminiClient
│  ├─ Prompts/               # system-v1.txt, system-v2.txt, knowledge-base.txt
│  └─ Program.cs
├─ tests/Api.Tests/
├─ web/                      # Vite + React + TS
└─ .github/workflows/ci.yml
```

### 4.3 Fluxo de uma mensagem

1. Aluno envia texto no Telegram.
2. n8n chama `POST /api/conversations` com `telegramChatId` e nome (get-or-create).
3. n8n chama `POST /api/conversations/{id}/messages` com o texto.
4. `MessageService` / `AgentService`:
   1. salva a mensagem do aluno (`Role = User`);
   2. carrega as últimas **N** mensagens (config `Agent:HistoryLimit`, padrão 20);
   3. monta `systemInstruction` (prompt da versão ativa + base de conhecimento) e `contents` (histórico, `User → "user"`, `Agent → "model"`);
   4. chama `IAiClient.GenerateAsync(...)` com timeout;
   5. salva a resposta (`Role = Agent`, `PromptVersion`).
5. API devolve a resposta; n8n envia ao Telegram.
6. **Falha da IA:** a mensagem do aluno permanece salva, a resposta do agente **não** é gravada, a API devolve `502` com `ProblemDetails`; o n8n responde ao aluno com texto fixo ("Tive um problema agora, pode tentar de novo?").

---

## 5. Modelo de dados

```
Student 1 ───── 1 Conversation 1 ───── * Message
```

| Entidade | Campos |
|---|---|
| `Student` | `Id`, `Name`, `TelegramChatId` (único), `CreatedAt` |
| `Conversation` | `Id`, `StudentId` (único: uma conversa por aluno), `CreatedAt` |
| `Message` | `Id`, `ConversationId`, `Role` (`User` \| `Agent`), `Content`, `CreatedAt` |
| _(só `Agent`)_ | `Rating` (`null` \| `Good` \| `Failure`), `ReviewComment` (opcional), `ReviewedAt` (opcional), `PromptVersion` (ex.: `"v1"`) |

Regras: `TelegramChatId` único; `Rating`, `ReviewComment` e `PromptVersion` só fazem sentido em mensagens do agente; `Role` e `Rating` armazenados como string (legíveis no banco).

---

## 6. Requisitos funcionais

### 6.1 API

| ID | Requisito |
|---|---|
| RF-01 | `POST /api/conversations` recebe `{ telegramChatId, studentName }`; cria `Student` e `Conversation` se não existirem; devolve a conversa. **201** ao criar, **200** ao reaproveitar. |
| RF-02 | `GET /api/conversations` lista conversas com aluno, data e última mensagem; aceita `?hasFailures=true` para filtrar as que têm ao menos uma resposta marcada como `Failure`. Ordenada pela atividade mais recente. |
| RF-03 | `GET /api/conversations/{id}` devolve a conversa com todas as mensagens em ordem cronológica. `404` se não existir. |
| RF-04 | `POST /api/conversations/{id}/messages` recebe `{ content }` (obrigatório, não vazio, máx. 2000 caracteres), executa o fluxo da seção 4.3 e devolve a mensagem do agente. `404` se a conversa não existir; `400` se o conteúdo for inválido; `502` se a IA falhar. |
| RF-05 | `PATCH /api/messages/{id}/review` recebe `{ rating: "Good" \| "Failure", comment? }`; grava avaliação e `ReviewedAt`. `400` se a mensagem não for do agente ou o rating for inválido; `404` se não existir. Reavaliar sobrescreve. |
| RF-06 | `GET /api/stats` devolve `{ totalConversations, totalAgentMessages, reviewedCount, goodCount, failureCount, failureRate }` (e o mesmo recorte por `promptVersion`). |
| RF-07 | Swagger/OpenAPI habilitado em Development. |
| RF-08 | CORS permite a origem do painel (configurável). |

### 6.2 Agente

| ID | Requisito |
|---|---|
| RF-10 | `IAiClient` expõe uma operação assíncrona que recebe instrução de sistema e a lista de turnos e devolve o texto da resposta. |
| RF-11 | `GeminiClient` implementa `IAiClient` com `HttpClient`, chamando `generateContent` do Gemini (instrução de sistema + `contents`). Modelo, URL base e timeout vêm da configuração. |
| RF-12 | Chave lida de `GEMINI_API_KEY` (env/user-secrets); a aplicação falha na subida com mensagem clara se estiver ausente fora de testes. |
| RF-13 | `AgentService` carrega `Prompts/system-{versão}.txt` + `Prompts/knowledge-base.txt`; a versão ativa vem de `Agent:PromptVersion` (padrão `v1`). |
| RF-14 | O agente responde **apenas** com base na base de conhecimento; fora do tema ou sem informação, diz que não sabe e sugere falar com a secretaria. Resposta curta (alvo: até ~3 frases), tom cordial em PT-BR. |
| RF-15 | Timeout e erros HTTP da IA são tratados em um único ponto (`GeminiClient` lança exceção própria; `AgentService`/controller traduz em `502`). Nada de exceção não tratada chegando ao cliente. |

### 6.3 Canal (n8n)

| ID | Requisito |
|---|---|
| RF-20 | Bot criado no BotFather; token somente no n8n (credencial), nunca no repositório. |
| RF-21 | Fluxo: Telegram Trigger → `POST /api/conversations` → `POST .../messages` → Telegram Send Message. |
| RF-22 | Ramo de erro: se a API devolver `502`/falha, envia texto fixo de indisponibilidade. |
| RF-23 | Fluxo exportado em `n8n/` (JSON sem segredos). n8n sobe no `docker-compose.yml`; túnel exposto para o webhook em desenvolvimento. |

### 6.4 Painel

| ID | Requisito |
|---|---|
| RF-30 | **Lista:** cada linha mostra aluno, data da última atividade e prévia da última mensagem; clicar abre o detalhe. |
| RF-31 | **Resumo no topo da lista:** total de conversas, respostas avaliadas, % Boa e % Falha (de `GET /api/stats`). |
| RF-32 | **Filtro:** alternância "Só com falhas" (usa `?hasFailures=true`). |
| RF-33 | **Detalhe:** histórico completo, diferenciando visualmente aluno e agente. |
| RF-34 | Em cada resposta do agente: botões **Boa** e **Falha**, campo de comentário e indicação do estado atual; salvar via `PATCH`. Feedback de carregando/erro/sucesso. |
| RF-35 | Estados vazios e de erro tratados (sem conversas, API fora do ar). |
| RF-36 | Tipos TypeScript espelham os DTOs; chamadas à API concentradas em um módulo (`api.ts`) e consumidas por hooks. |

### 6.5 Melhoria de prompt documentada

| ID | Requisito |
|---|---|
| RF-40 | Reproduzir ao menos uma falha real do agente (fora do tema, resposta longa demais, tom errado, informação inventada). |
| RF-41 | Marcar a resposta como `Failure` no painel com comentário. |
| RF-42 | Criar `system-v2.txt` corrigindo a falha; ativar via `Agent:PromptVersion`. |
| RF-43 | README documenta: problema, prompt antes, prompt depois, resultado (inclusive número de `GET /api/stats` por versão, se possível). |

---

## 7. Requisitos não funcionais

| ID | Requisito |
|---|---|
| RNF-01 | **Segredos:** nenhuma chave no código nem no histórico do Git; `.env.example` sem valores reais; `.gitignore` cobre `.env`, `appsettings.*.local.json`, credenciais do n8n. |
| RNF-02 | **Legibilidade:** código simples, sem abstrações desnecessárias; nullable reference types habilitado; `async/await` de ponta a ponta com `CancellationToken` nos endpoints. |
| RNF-03 | **Confiabilidade:** timeout de 20 s na chamada à IA (configurável); falha da IA não derruba a API nem corrompe dados. |
| RNF-04 | **Testabilidade:** todo Service novo tem teste unitário; `AgentService` testado com `IAiClient` falso. |
| RNF-05 | **Reprodutibilidade:** `docker compose up -d` sobe Postgres (e n8n); migrations aplicadas na subida em Development; README permite rodar do zero em outra pasta. |
| RNF-06 | **Observabilidade mínima:** logs estruturados (`ILogger`) de falhas da IA, sem logar chave nem conteúdo sensível. |
| RNF-07 | **Validação:** entradas validadas (data annotations / FluentValidation não necessário); erros em `ProblemDetails`. |
| RNF-08 | **CI:** pipeline roda `dotnet build`, `dotnet test` e `npm ci` + lint + build do frontend; precisa estar verde. |
| RNF-09 | **Limitação consciente:** sem autenticação; só uso local/demo (documentado no README). |

---

## 8. Contrato da API (resumo)

```http
POST /api/conversations
{ "telegramChatId": 123456789, "studentName": "Ana" }
→ 201/200 { "id": 1, "studentId": 1, "studentName": "Ana", "createdAt": "..." }

POST /api/conversations/1/messages
{ "content": "Quanto custa o curso de inglês?" }
→ 200 { "id": 42, "role": "Agent", "content": "...", "createdAt": "...", "promptVersion": "v1" }
→ 502 ProblemDetails (falha da IA)

PATCH /api/messages/42/review
{ "rating": "Failure", "comment": "Inventou um preço." }
→ 200 { "id": 42, "rating": "Failure", "reviewComment": "...", "reviewedAt": "..." }

GET /api/conversations?hasFailures=true
→ 200 [ { "id": 1, "studentName": "Ana", "lastMessagePreview": "...", "lastActivityAt": "...", "failureCount": 1 } ]

GET /api/stats
→ 200 { "totalConversations": 5, "totalAgentMessages": 40, "reviewedCount": 12,
        "goodCount": 9, "failureCount": 3, "failureRate": 0.25,
        "byPromptVersion": [ { "version": "v1", "reviewed": 12, "failures": 3 } ] }
```

Nomes finais de campos podem ser ajustados na Fase 1; o Swagger é a fonte da verdade depois disso.

---

## 9. Design do prompt

- `system-v1.txt`: papel (atendente do curso), idioma (PT-BR), regras de escopo (só responder com base na base de conhecimento), o que fazer quando não souber, formato (curto, cordial).
- `knowledge-base.txt`: texto livre estruturado (cursos, níveis, preços, horários, matrícula, cancelamento, contato). Fictício, mas coerente e sem lacunas óbvias.
- `system-v2.txt`: v1 corrigido para a falha escolhida no RF-40. Mantém a mesma base de conhecimento para o antes/depois ser comparável.
- A `PromptVersion` ativa é gravada em cada resposta, permitindo comparar taxa de falha por versão.

---

## 10. Plano por fases e critérios de aceite

| Fase | Data | Entregas | Critério de pronto | Commit |
|---|---|---|---|---|
| **0 — Setup** | qui 22/10 | .NET 8 SDK, Node 20+, Docker, repositório GitHub, `CLAUDE.md` | Ferramentas instaladas e repositório criado | — |
| **1 — Base da API** | qui 22/10 | Solution (`Api`, `Api.Tests`), Compose com Postgres, entidades, EF Core + migrations, endpoints RF-01 a RF-03 e RF-04 (sem IA ainda), Swagger | `docker compose up` sobe o banco, Swagger responde, cria e lista conversa | `feat: base da API com entidades e endpoints de conversa` |
| **2 — Agente de IA** | sex 23/10 | `IAiClient`, `GeminiClient`, `AgentService`, prompts v1 + base de conhecimento, RF-04 completo, tratamento de erro/timeout | Mensagem via Swagger devolve resposta do agente e persiste as duas | `feat: agente de IA com cliente Gemini` |
| **3 — Canal Telegram** | sex 23/10 | Bot, n8n no Compose, túnel, fluxo exportado | Mensagem no Telegram volta respondida, conversa salva no banco | `feat: integração Telegram via n8n` |
| **4 — Painel React** | sáb 24/10 | `web/`, lista, detalhe, `PATCH` de review (RF-05), stats (RF-06), CORS, filtro, resumo | Abre o painel, vê conversa do Telegram, marca Falha com comentário | `feat: painel React para revisão de conversas` |
| **5 — Testes, CI, README** | dom 25/10 | xUnit (seção 11), GitHub Actions, `system-v2.txt`, README completo | Pipeline verde, README com melhoria de prompt documentada | `test: testes unitários e CI` e `docs: README e melhoria de prompt` |
| **6 — Revisão e ensaio** | seg 26/10 | Rodar do zero seguindo só o README, simulação de entrevista, revisão de C#/React, demo de 3 min, LinkedIn/GitHub | Explica o projeto sem olhar o código | — |

**Milestones diários:** (1) API local com Swagger e banco no Docker; (2) mensagem do Telegram volta respondida; (3) painel funcional; (4) repositório público com pipeline verde; (5) projeto explicável sem consulta.

---

## 11. Estratégia de testes

| Alvo | Casos mínimos |
|---|---|
| `AgentService` | Monta o prompt (sistema + base de conhecimento) e o histórico na ordem correta com mapeamento de papéis; corta o histórico em N mensagens; trata falha/timeout do `IAiClient` sem quebrar e sem gravar resposta; grava `PromptVersion` da versão ativa. |
| Serviço de conversa | Get-or-create idempotente (mesmo `telegramChatId` não duplica); listagem com `hasFailures`; stats. |
| Review | Rejeita avaliação em mensagem de usuário; sobrescreve avaliação. |
| Endpoint (opcional) | Um teste com `WebApplicationFactory` para o fluxo feliz de `POST .../messages` com `IAiClient` falso. |
| React (opcional, 1º corte) | Um teste de componente de avaliação. |

Infra de teste: `IAiClient` falso escrito à mão (sem biblioteca de mock), SQLite em memória para dados.

---

## 12. Ordem de corte se o tempo apertar

1. **Primeiro:** teste de componente React e filtro de falhas no painel.
2. **Depois:** n8n e Telegram, substituindo por uma tela de chat simples no painel, ou testando apenas pelo Swagger.
3. **Nunca cortar:** testes do `AgentService`, README e melhoria de prompt documentada (são o que mais mostra maturidade).

---

## 13. Riscos e mitigação

| Risco | Impacto | Mitigação |
|---|---|---|
| Limite de cota/instabilidade da API do Gemini | Bloqueia Fases 2–3 | Modelo configurável; `IAiClient` permite um cliente falso para desenvolver o resto; timeout e `502` tratados. |
| Túnel/webhook do n8n instável | Fase 3 atrasa | Testar pelo Swagger primeiro; ver ordem de corte; ter túnel alternativo (ngrok ou cloudflared). |
| Dono não entende código gerado | Perde a entrevista | Regra R1: explicação ao fim de cada fase e resumo no README; parte escrita à mão por fase. |
| Escopo estoura em 5 dias | Entrega incompleta | Escopo enxuto; ordem de corte; um commit por fase como ponto de recuperação. |
| Vazamento de chave no Git | Segurança e imagem | `.gitignore`, `.env.example`, user-secrets, revisão do histórico antes de publicar. |
| Agente inventa fatos | Falha de qualidade | Base de conhecimento fixa, regra de "não sei", painel para marcar falhas e iterar o prompt. |
| Aluno enviar 2 mensagens seguidas sem resposta (IA caiu) | Histórico com dois turnos `User` seguidos | Aceitável para o Gemini; coberto em teste do `AgentService`. |

---

## 14. Decisões de design (para a entrevista)

| Decisão | Motivo |
|---|---|
| `IAiClient` | Isola dependência externa, permite trocar de provedor e testar com falso. |
| DTOs separados | Contrato HTTP independente do banco; evita expor campos internos. |
| Um projeto `Api` em camadas por pasta | Fácil de explicar; Clean Architecture em 4 projetos triplica boilerplate sem ganho neste porte. |
| `DbContext` direto nos Services | Já é repositório e unit of work; camada extra seria abstração desnecessária. |
| n8n para o canal | Mantém integração fora do código C#; trocar de canal não muda a API. |
| 1 conversa por aluno + últimas N mensagens | Simples, testável e controla contexto/custo. |
| Resposta da IA não gravada em falha | Evita lixo no histórico; o `502` é explícito e o canal decide o texto de fallback. |
| `PromptVersion` por mensagem | Torna mensurável o efeito do v1→v2. |
| Sem autenticação | Escopo de demo; limitação e próximo passo documentados. |
| SQLite em memória nos testes | Mais fiel a um banco relacional que o provider InMemory do EF. |

---

## 15. Entregáveis finais

- [ ] Repositório público com README claro (visão geral, diagrama, como rodar, decisões, limitações, próximos passos).
- [ ] Projeto roda do zero seguindo só o README.
- [ ] GitHub Actions verde.
- [ ] Nenhuma chave de API no histórico do Git.
- [ ] `n8n/` com fluxo exportado sem segredos.
- [ ] Seção "Melhoria de prompt" no README (problema, antes, depois, resultado).
- [ ] Explicação por camada e do fluxo de uma mensagem, em voz alta.
- [ ] Demo de 3 minutos ensaiada (contexto 20 s → fluxo Telegram → painel 60 s → análise de falha 60 s → código 40 s).
- [ ] Resposta pronta sobre disponibilidade e acúmulo de trabalho (transparente sobre o horário flexível atual).

---

## 16. Perguntas de entrevista previstas (respostas-guia)

- **"Você usou IA para escrever o código?"** Usei o Claude Code como assistente, com arquivo de regras e arquitetura definido por mim. Revisei cada fase, escrevi partes sozinho e consigo explicar o código. Minha base é Python; usei a comparação com FastAPI para aprender o equivalente em .NET.
- **"Por que uma interface para o cliente de IA?"** Isolar a dependência externa, trocar de provedor e testar o `AgentService` com falso sem chamar a API.
- **"Como avalia a qualidade do agente?"** No painel a equipe marca respostas como Boa ou Falha com comentário; as falhas viram exemplos para ajustar o prompt e cada versão é documentada com antes e depois, com taxa de falha por versão.
- **"Experiência com C#?"** Contato na faculdade, nunca profissionalmente; para a entrevista construí este projeto completo em ASP.NET Core, e a base em Python, FastAPI e camadas facilitou a transição.

---

## 17. Glossário

- **Agente:** componente que monta o prompt, chama a IA e persiste a resposta.
- **Base de conhecimento:** texto fixo com as informações do curso fictício.
- **Prompt de sistema:** instruções permanentes enviadas à IA junto com o histórico.
- **Versão de prompt:** arquivo `system-vN.txt`; a ativa vem da configuração.
- **Falha:** avaliação humana de que a resposta do agente foi incorreta, fora do tema, longa demais ou de tom errado.
