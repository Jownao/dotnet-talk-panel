# Fase 2 — Agente de IA Implementation Plan

> **Execução:** inline, TDD, sem commit até o dono autorizar (nunca com coautor — preferência do dono).
> **Formato:** plano compacto (tarefas, arquivos, interfaces e casos de teste); o código completo vive nos arquivos do repositório, não duplicado aqui.

**Goal:** `POST /api/conversations/{id}/messages` salva a mensagem do aluno, chama o Gemini com o histórico e devolve/salva a resposta do agente (RF-04 completo, RF-10 a RF-15).

**Architecture:** `ConversationsController → IAgentService (AgentService) → IMessageService (salva msg do aluno) + AppDbContext (histórico) + IPromptProvider (arquivos) + IAiClient (GeminiClient/HttpClient)`. Falha da IA vira `AiClientException`, o controller traduz em 502 `ProblemDetails`.

**Tech Stack:** HttpClient tipado (`AddHttpClient`), `IOptions<T>`, Gemini `generateContent` (v1beta), xUnit com `HttpMessageHandler` falso e `IAiClient` falso.

## Global Constraints

- Chave só em `GEMINI_API_KEY` (user-secrets em dev / env em outros ambientes); nunca no repositório (RNF-01, RF-12). A aplicação falha na subida, com mensagem clara, se ela faltar — a checagem fica **depois** de `builder.Build()` para não quebrar o `dotnet ef`.
- Modelo, URL base e timeout em configuração: `Gemini:Model` (`gemini-3.5-flash`; os modelos 2.5 listados pela API devolveram 404 com a chave do dono), `Gemini:BaseUrl` (`https://generativelanguage.googleapis.com/v1beta/`), `Gemini:TimeoutSeconds` (20).
- Agente: `Agent:HistoryLimit` (20), `Agent:PromptVersion` (`v1`).
- Falha/timeout da IA: mensagem do aluno permanece salva; resposta do agente **não** é gravada; API devolve **502** `ProblemDetails` (RF-15).
- Cancelamento pelo cliente (`ct`) não é falha da IA: propaga como `OperationCanceledException`.
- Papéis para o Gemini: `User → "user"`, `Agent → "model"`; o primeiro turno enviado deve ser `user` (descartar turnos `Agent` iniciais após o corte do histórico).
- Prompts em PT-BR; agente responde só com base na base de conhecimento (RF-14).
- Todo service novo tem teste (RNF-04).

## File Structure

```
src/Api/Ai/        IAiClient.cs (+ AiTurn), AiClientException.cs, GeminiOptions.cs, GeminiClient.cs
src/Api/Services/  AgentOptions.cs, IPromptProvider.cs, FilePromptProvider.cs, IAgentService.cs, AgentService.cs
src/Api/Prompts/   system-v1.txt, knowledge-base.txt           (copiados para a saída do build)
src/Api/Controllers/ConversationsController.cs                 (usa IAgentService; 502)
src/Api/Program.cs, appsettings.json, Api.csproj
tests/Api.Tests/   GeminiClientTests.cs, FilePromptProviderTests.cs, AgentServiceTests.cs, Fakes.cs
```

## Task 1 — `IAiClient` e `GeminiClient`

**Interfaces (produz):**
- `record AiTurn(MessageRole Role, string Content)`
- `interface IAiClient { Task<string> GenerateAsync(string systemPrompt, IReadOnlyList<AiTurn> turns, CancellationToken ct); }`
- `class AiClientException : Exception` (mensagem + inner opcional)
- `class GeminiOptions { string ApiKey; string Model; string BaseUrl; int TimeoutSeconds }`
- `GeminiClient(HttpClient http, IOptions<GeminiOptions> options)`

**Testes (`GeminiClientTests`, handler falso):**
1. Sucesso: resposta com `candidates[0].content.parts[].text` → devolve o texto concatenado.
2. Requisição: método POST em `models/{Model}:generateContent`, header `x-goog-api-key`, corpo com `systemInstruction.parts[0].text` e `contents` com `role` `user`/`model` na ordem.
3. HTTP não-2xx (ex.: 429) → `AiClientException` (mensagem com o status, sem a chave).
4. 200 sem texto (sem `candidates` ou bloqueado) → `AiClientException`.
5. `HttpRequestException` → `AiClientException`.
6. Timeout (handler lança `TaskCanceledException` sem cancelar o `ct`) → `AiClientException`.
7. Cancelamento do `ct` → propaga `OperationCanceledException` (não é `AiClientException`).

## Task 2 — Prompts e `IPromptProvider`

**Interfaces:** `interface IPromptProvider { string GetSystemPrompt(string version); }` · `FilePromptProvider(string promptsDirectory)` lê `system-{version}.txt` + `knowledge-base.txt` e junta em `"{system}\n\n## Base de conhecimento\n{kb}"`. Versão inválida (fora de `^[a-z0-9-]+$`) ou arquivo ausente → `InvalidOperationException`.

**Testes (`FilePromptProviderTests`, pasta temporária):** junta sistema + base; versão inexistente lança; versão com `../` lança.

**Conteúdo:** `system-v1.txt` (persona de atendente do curso, PT-BR, responder só com a base, dizer que não sabe e indicar a secretaria, respostas curtas e cordiais) e `knowledge-base.txt` (curso fictício "Lingua Viva Idiomas": cursos, níveis, preços, horários, matrícula, cancelamento, contato). O `.csproj` copia `Prompts\**` para a saída.

## Task 3 — `AgentService`

**Interfaces:** `interface IAgentService { Task<MessageDto?> ReplyAsync(int conversationId, string userContent, CancellationToken ct); }` · `AgentOptions { int HistoryLimit = 20; string PromptVersion = "v1"; }` · `AgentService(IMessageService messages, AppDbContext db, IAiClient ai, IPromptProvider prompts, IOptions<AgentOptions> options)`.

**Fluxo:** `AddUserMessageAsync` (null → devolve null, sem chamar a IA) → carrega as últimas `HistoryLimit` mensagens (inclui a recém-salva) em ordem cronológica → descarta `Agent` iniciais → `GenerateAsync(prompts.GetSystemPrompt(version), turns)` → grava `Message(Role=Agent, PromptVersion=version)` → devolve `MessageDto`.

**Testes (`AgentServiceTests`, SQLite + fakes):**
1. Monta prompt de sistema da versão ativa e turnos em ordem cronológica com papéis corretos.
2. Corta o histórico em `HistoryLimit` mantendo as mais recentes.
3. Após o corte, não começa com turno `Agent`.
4. Grava a resposta como `Agent` com `PromptVersion`.
5. IA falha (`AiClientException`) → exceção propaga, mensagem do aluno salva, nenhuma mensagem de agente.
6. Conversa inexistente → `null`, IA não é chamada.

## Task 4 — Fiação, controller e teste real

- `Program.cs`: `Configure<GeminiOptions>` (seção + `ApiKey` de `GEMINI_API_KEY`), `Configure<AgentOptions>`, `AddHttpClient<IAiClient, GeminiClient>` (BaseAddress, Timeout), `AddSingleton<IPromptProvider>(FilePromptProvider em AppContext.BaseDirectory/Prompts)`, `AddScoped<IAgentService, AgentService>`; checagem da chave após `Build()`.
- Controller: `SendMessage` usa `IAgentService.ReplyAsync`; `AiClientException` → log + `Problem(502)`; `null` → 404.
- Verificação real: subir a API, criar conversa, perguntar preço e algo fora do tema, conferir a resposta, o histórico no detalhe e o 502 (chave inválida temporária via env var).
- Critério de pronto (PRD seção 10): mensagem via Swagger devolve a resposta do agente e persiste as duas.
