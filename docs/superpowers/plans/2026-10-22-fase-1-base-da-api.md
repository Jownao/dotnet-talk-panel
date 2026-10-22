# Fase 1 — Base da API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Neste projeto:** execução *inline* (executing-plans), porque o dono precisa entender cada arquivo (PRD R1) e escrever uma parte sozinho (R3, marcada `[SUA VEZ]`). Commit só quando o dono autorizar (um commit por fase, PRD R2).

**Goal:** API ASP.NET Core (.NET 8) rodando local com Swagger e PostgreSQL no Docker, com entidades `Student`/`Conversation`/`Message`, migrations e os endpoints de conversa (RF-01 a RF-03, RF-04 parcial sem IA, RF-07).

**Architecture:** Um projeto `src/Api` em camadas por pasta (`Controllers → Services → AppDbContext`), DTOs separados das entidades, serviços injetados por interface. Testes de serviço em `tests/Api.Tests` com SQLite em memória (o app usa Npgsql; o modelo não usa nada específico de provedor).

**Tech Stack:** .NET 8 SDK, ASP.NET Core controllers, EF Core 8 + Npgsql, Swashbuckle (Swagger), xUnit, SQLite in-memory (só nos testes), Docker Compose (postgres:16).

## Global Constraints

- Código em inglês; textos de commit, README e docs em PT-BR (PRD cabeçalho).
- .NET 8 LTS; `<Nullable>enable</Nullable>` e `<ImplicitUsings>enable</ImplicitUsings>`.
- Nenhuma chave/segredo no repositório; a senha do Postgres local (`agente_dev`) é descartável, só para `localhost`.
- DTOs separados das entidades; Services dependem de interfaces; `CancellationToken` nos endpoints (RNF-02).
- `Role` e `Rating` gravados como string no banco; JSON da API também serializa enums como string.
- `POST /api/conversations`: **201** ao criar, **200** ao reaproveitar (RF-01).
- `content` da mensagem: obrigatório, não vazio, máx. 2000 caracteres (RF-04).
- Todo service novo tem teste unitário (RNF-04).

## Pré-requisitos (checar antes de começar)

- [ ] .NET 8 SDK instalado: `dotnet --list-sdks` deve listar `8.0.x`. (Na máquina hoje só há *runtimes*; instalar com `winget install Microsoft.DotNet.SDK.8`.)
- [ ] Docker Desktop **aberto** (o daemon estava parado): `docker info` não pode dar erro.
- [ ] `dotnet-ef`: `dotnet tool install --global dotnet-ef --version 8.0.*` (se ainda não existir).

## File Structure

```
agente-atendimento-lab/                    (E:\-Progamacoes\projects\dotnet-talk-panel)
├─ .gitignore                               ignora bin/obj, .env, .claude/, node_modules
├─ .env.example                             variáveis do compose (sem segredos reais)
├─ CLAUDE.md                                (já criado)
├─ docker-compose.yml                       PostgreSQL 16
├─ AgenteAtendimento.sln
├─ src/Api/
│  ├─ Api.csproj
│  ├─ Program.cs                            DI, JSON enums, Swagger, migrate em Development
│  ├─ appsettings.json                      ConnectionStrings:Default (dev local)
│  ├─ Properties/launchSettings.json        porta http://localhost:5080
│  ├─ Models/Entities/  Student.cs, Conversation.cs, Message.cs, MessageRole.cs, MessageRating.cs
│  ├─ Models/Dtos/      CreateConversationRequest.cs, ConversationDto.cs, ConversationSummaryDto.cs,
│  │                    ConversationDetailDto.cs, MessageDto.cs, SendMessageRequest.cs
│  ├─ Data/AppDbContext.cs  e  Data/Migrations/ (gerado)
│  ├─ Services/  IConversationService.cs, ConversationService.cs, IMessageService.cs, MessageService.cs
│  └─ Controllers/ConversationsController.cs
└─ tests/Api.Tests/
   ├─ Api.Tests.csproj
   ├─ TestDb.cs                             SQLite in-memory + AppDbContext
   ├─ AppDbContextTests.cs
   ├─ ConversationServiceTests.cs
   └─ MessageServiceTests.cs
```

Responsabilidades: **Entities** = formato do banco; **Dtos** = contrato HTTP; **Services** = regras (get-or-create, listagem, salvar mensagem); **Controller** = só HTTP (status codes), sem regra; **AppDbContext** = mapeamento EF.

Convenção: os comandos abaixo rodam na raiz do projeto. Termine cada task com `dotnet test` verde.

---

### Task 1: Esqueleto do repositório, solution e projetos

**Files:**
- Create: `.gitignore`, `.env.example`, `docker-compose.yml`
- Create: `AgenteAtendimento.sln`, `src/Api/*` (template), `tests/Api.Tests/*` (template)
- Modify: `src/Api/Properties/launchSettings.json`, `src/Api/appsettings.json`

**Interfaces:**
- Produces: projetos `Api` (namespace raiz `Api`) e `Api.Tests` (namespace `Api.Tests`) compilando; Postgres acessível em `localhost:5432` (db `agente`, user `agente`, senha `agente_dev`); API em `http://localhost:5080`.

- [ ] **Step 1: Git e .gitignore**

```bash
git init -b main
```

Criar `.gitignore`:

```gitignore
# .NET
bin/
obj/
*.user
.vs/
TestResults/

# Node
node_modules/
dist/

# Segredos e ambiente
.env
appsettings.*.local.json

# Ferramentas locais (skills copiadas de outro projeto)
.claude/

# Sistema
Thumbs.db
.DS_Store
```

- [ ] **Step 2: Criar solution e projetos**

```bash
dotnet new sln -n AgenteAtendimento
dotnet new webapi -n Api -o src/Api --use-controllers --no-https
dotnet new xunit -n Api.Tests -o tests/Api.Tests
dotnet sln add src/Api/Api.csproj tests/Api.Tests/Api.Tests.csproj
dotnet add tests/Api.Tests reference src/Api
```

Apagar os arquivos de exemplo do template:

```bash
rm src/Api/WeatherForecast.cs src/Api/Controllers/WeatherForecastController.cs src/Api/Api.http tests/Api.Tests/UnitTest1.cs
```

(Se algum arquivo não existir, ignore o erro.)

Confirmar que `src/Api/Api.csproj` e `tests/Api.Tests/Api.Tests.csproj` têm `<TargetFramework>net8.0</TargetFramework>`, `<Nullable>enable</Nullable>` e `<ImplicitUsings>enable</ImplicitUsings>`.

- [ ] **Step 3: Pacotes**

```bash
dotnet add src/Api package Microsoft.EntityFrameworkCore --version 8.0.*
dotnet add src/Api package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.*
dotnet add src/Api package Microsoft.EntityFrameworkCore.Design --version 8.0.*
dotnet add tests/Api.Tests package Microsoft.EntityFrameworkCore.Sqlite --version 8.0.*
```

(O template já traz `Swashbuckle.AspNetCore`.)

- [ ] **Step 4: Porta fixa**

Em `src/Api/Properties/launchSettings.json`, no perfil `http`, deixar:

```json
"applicationUrl": "http://localhost:5080",
"launchUrl": "swagger",
"environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Development" }
```

- [ ] **Step 5: Connection string e compose**

Substituir `src/Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=agente;Username=agente;Password=agente_dev"
  },
  "Logging": {
    "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" }
  },
  "AllowedHosts": "*"
}
```

Criar `.env.example`:

```dotenv
POSTGRES_PASSWORD=agente_dev
```

Criar `docker-compose.yml`:

```yaml
services:
  db:
    image: postgres:16
    restart: unless-stopped
    environment:
      POSTGRES_DB: agente
      POSTGRES_USER: agente
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-agente_dev}
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U agente -d agente"]
      interval: 5s
      timeout: 3s
      retries: 10

volumes:
  pgdata:
```

- [ ] **Step 6: Verificar build e subir o banco**

Run: `dotnet build`
Expected: `Build succeeded` (0 erros). O `Program.cs` do template ainda compila (é substituído na Task 6).

Run: `docker compose up -d` e depois `docker compose ps`
Expected: serviço `db` com status `healthy` (pode levar ~10 s).

---

### Task 2: Entidades, DbContext e primeira migration

**Files:**
- Create: `src/Api/Models/Entities/MessageRole.cs`, `MessageRating.cs`, `Student.cs`, `Conversation.cs`, `Message.cs`
- Create: `src/Api/Data/AppDbContext.cs`
- Create: `tests/Api.Tests/TestDb.cs`, `tests/Api.Tests/AppDbContextTests.cs`
- Generate: `src/Api/Data/Migrations/*`

**Interfaces:**
- Produces:
  - `enum MessageRole { User, Agent }`, `enum MessageRating { Good, Failure }`
  - `Student { int Id; required string Name; long TelegramChatId; DateTime CreatedAt; Conversation? Conversation }`
  - `Conversation { int Id; int StudentId; Student Student; DateTime CreatedAt; List<Message> Messages }`
  - `Message { int Id; int ConversationId; Conversation Conversation; MessageRole Role; required string Content; DateTime CreatedAt; MessageRating? Rating; string? ReviewComment; DateTime? ReviewedAt; string? PromptVersion }`
  - `AppDbContext(DbContextOptions<AppDbContext>)` com `DbSet<Student> Students`, `DbSet<Conversation> Conversations`, `DbSet<Message> Messages`
  - `TestDb : IDisposable` com `AppDbContext Context` (usado pelas Tasks 3-5)

- [ ] **Step 1: Escrever as entidades**

`src/Api/Models/Entities/MessageRole.cs`:

```csharp
namespace Api.Models.Entities;

public enum MessageRole
{
    User,
    Agent
}
```

`src/Api/Models/Entities/MessageRating.cs`:

```csharp
namespace Api.Models.Entities;

public enum MessageRating
{
    Good,
    Failure
}
```

`src/Api/Models/Entities/Student.cs`:

```csharp
namespace Api.Models.Entities;

public class Student
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public long TelegramChatId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Conversation? Conversation { get; set; }
}
```

`src/Api/Models/Entities/Conversation.cs`:

```csharp
namespace Api.Models.Entities;

public class Conversation
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Message> Messages { get; set; } = [];
}
```

`src/Api/Models/Entities/Message.cs`:

```csharp
namespace Api.Models.Entities;

public class Message
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public MessageRole Role { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Preenchidos só em respostas do agente (revisão e versão do prompt).
    public MessageRating? Rating { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? PromptVersion { get; set; }
}
```

- [ ] **Step 2: Helper de teste (SQLite in-memory)**

`tests/Api.Tests/TestDb.cs` (referencia `AppDbContext`, criado no Step 4 — o projeto só compila depois dele):

```csharp
using Api.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

/// <summary>Banco SQLite em memória, novo para cada teste. A conexão precisa ficar aberta, senão o banco some.</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public AppDbContext Context { get; }

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new AppDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
```

- [ ] **Step 3: Escrever o teste que falha**

`tests/Api.Tests/AppDbContextTests.cs`:

```csharp
using Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

public class AppDbContextTests
{
    [Fact]
    public async Task Students_WithSameTelegramChatId_ViolatesUniqueIndex()
    {
        using var db = new TestDb();
        db.Context.Students.Add(new Student { Name = "Ana", TelegramChatId = 111 });
        await db.Context.SaveChangesAsync();

        db.Context.Students.Add(new Student { Name = "Outra Ana", TelegramChatId = 111 });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task Message_RoleAndRating_AreStoredAsStrings()
    {
        using var db = new TestDb();
        var student = new Student { Name = "Ana", TelegramChatId = 111, Conversation = new Conversation() };
        student.Conversation.Messages.Add(new Message
        {
            Role = MessageRole.Agent,
            Content = "Olá!",
            Rating = MessageRating.Failure
        });
        db.Context.Students.Add(student);
        await db.Context.SaveChangesAsync();

        var role = await db.Context.Database
            .SqlQueryRaw<string>("SELECT \"Role\" AS \"Value\" FROM \"Messages\"")
            .SingleAsync();

        Assert.Equal("Agent", role);
    }
}
```

- [ ] **Step 4: Rodar e ver falhar**

Run: `dotnet test`
Expected: FAIL de compilação — `The type or namespace name 'AppDbContext' could not be found`.

- [ ] **Step 5: Implementar o DbContext**

`src/Api/Data/AppDbContext.cs`:

```csharp
using Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>(e =>
        {
            e.Property(s => s.Name).HasMaxLength(100);
            e.HasIndex(s => s.TelegramChatId).IsUnique();
        });

        // Um aluno tem uma conversa contínua; a FK única em StudentId garante isso.
        modelBuilder.Entity<Conversation>(e =>
        {
            e.HasOne(c => c.Student)
                .WithOne(s => s.Conversation)
                .HasForeignKey<Conversation>(c => c.StudentId);
        });

        modelBuilder.Entity<Message>(e =>
        {
            e.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
            e.Property(m => m.Rating).HasConversion<string>().HasMaxLength(20);
            e.Property(m => m.Content).HasMaxLength(4000);
            e.Property(m => m.ReviewComment).HasMaxLength(1000);
            e.Property(m => m.PromptVersion).HasMaxLength(20);

            e.HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId);

            e.HasIndex(m => new { m.ConversationId, m.CreatedAt });
        });
    }
}
```

- [ ] **Step 6: Rodar e ver passar**

Run: `dotnet test`
Expected: PASS (2 testes).

- [ ] **Step 7: Gerar a migration e aplicar no Postgres**

Run: `dotnet ef migrations add InitialCreate --project src/Api --output-dir Data/Migrations`
Expected: `Done.` e arquivos criados em `src/Api/Data/Migrations/`.

Run: `dotnet ef database update --project src/Api`
Expected: `Done.` (com o Docker do banco de pé). Conferir as tabelas:

```bash
docker compose exec db psql -U agente -d agente -c "\dt"
```

Expected: tabelas `Students`, `Conversations`, `Messages` e `__EFMigrationsHistory`.

---

### Task 3: DTOs e `ConversationService.GetOrCreateAsync` (RF-01)

**Files:**
- Create: `src/Api/Models/Dtos/CreateConversationRequest.cs`, `ConversationDto.cs`
- Create: `src/Api/Services/IConversationService.cs`, `ConversationService.cs`
- Test: `tests/Api.Tests/ConversationServiceTests.cs`

**Interfaces:**
- Consumes: `AppDbContext`, entidades da Task 2, `TestDb`.
- Produces:
  - `record CreateConversationRequest(long TelegramChatId, string StudentName)` (StudentName: `[Required]`, `[StringLength(100)]`)
  - `record ConversationDto(int Id, int StudentId, string StudentName, DateTime CreatedAt)`
  - `interface IConversationService { Task<(ConversationDto Conversation, bool Created)> GetOrCreateAsync(CreateConversationRequest request, CancellationToken ct); }` (mais métodos nas Tasks 4)
  - `class ConversationService(AppDbContext db) : IConversationService` (construtor clássico, parâmetro `AppDbContext db`)

- [ ] **Step 1: DTOs**

`src/Api/Models/Dtos/CreateConversationRequest.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace Api.Models.Dtos;

public record CreateConversationRequest(
    long TelegramChatId,
    [Required, StringLength(100)] string StudentName);
```

`src/Api/Models/Dtos/ConversationDto.cs`:

```csharp
namespace Api.Models.Dtos;

public record ConversationDto(int Id, int StudentId, string StudentName, DateTime CreatedAt);
```

- [ ] **Step 2: Interface do serviço**

`src/Api/Services/IConversationService.cs`:

```csharp
using Api.Models.Dtos;

namespace Api.Services;

public interface IConversationService
{
    /// <summary>Acha a conversa do aluno pelo chatId do Telegram ou cria aluno + conversa. Created = true se criou.</summary>
    Task<(ConversationDto Conversation, bool Created)> GetOrCreateAsync(
        CreateConversationRequest request, CancellationToken ct);
}
```

- [ ] **Step 3: Escrever os testes que falham**

`tests/Api.Tests/ConversationServiceTests.cs`:

```csharp
using Api.Models.Dtos;
using Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

public class ConversationServiceTests
{
    [Fact]
    public async Task GetOrCreate_NewChatId_CreatesStudentAndConversation()
    {
        using var db = new TestDb();
        var service = new ConversationService(db.Context);

        var (conversation, created) = await service.GetOrCreateAsync(
            new CreateConversationRequest(111, "Ana"), CancellationToken.None);

        Assert.True(created);
        Assert.Equal("Ana", conversation.StudentName);
        Assert.Equal(1, await db.Context.Students.CountAsync());
        Assert.Equal(1, await db.Context.Conversations.CountAsync());
    }

    [Fact]
    public async Task GetOrCreate_SameChatId_ReturnsExistingWithoutDuplicating()
    {
        using var db = new TestDb();
        var service = new ConversationService(db.Context);
        var request = new CreateConversationRequest(111, "Ana");

        var (first, _) = await service.GetOrCreateAsync(request, CancellationToken.None);
        var (second, created) = await service.GetOrCreateAsync(request, CancellationToken.None);

        Assert.False(created);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await db.Context.Students.CountAsync());
        Assert.Equal(1, await db.Context.Conversations.CountAsync());
    }
}
```

- [ ] **Step 4: Rodar e ver falhar**

Run: `dotnet test --filter ConversationServiceTests`
Expected: FAIL de compilação — `ConversationService` não existe.

- [ ] **Step 5: Implementação mínima**

`src/Api/Services/ConversationService.cs`:

```csharp
using Api.Data;
using Api.Models.Dtos;
using Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class ConversationService : IConversationService
{
    private readonly AppDbContext _db;

    public ConversationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(ConversationDto Conversation, bool Created)> GetOrCreateAsync(
        CreateConversationRequest request, CancellationToken ct)
    {
        var existing = await _db.Conversations
            .AsNoTracking()
            .Include(c => c.Student)
            .FirstOrDefaultAsync(c => c.Student.TelegramChatId == request.TelegramChatId, ct);

        if (existing is not null)
        {
            return (ToDto(existing), false);
        }

        var student = new Student
        {
            Name = request.StudentName,
            TelegramChatId = request.TelegramChatId,
            Conversation = new Conversation()
        };
        _db.Students.Add(student);
        await _db.SaveChangesAsync(ct);

        student.Conversation.Student = student;
        return (ToDto(student.Conversation), true);
    }

    private static ConversationDto ToDto(Conversation c) =>
        new(c.Id, c.StudentId, c.Student.Name, c.CreatedAt);
}
```

(Limitação conhecida, a citar no README: duas requisições simultâneas com o mesmo `chatId` novo podem colidir no índice único; no uso real o Telegram entrega uma mensagem por vez.)

- [ ] **Step 6: Rodar e ver passar**

Run: `dotnet test`
Expected: PASS (4 testes no total).

---

### Task 4: Listagem e detalhe de conversas (RF-02, RF-03)

**Files:**
- Create: `src/Api/Models/Dtos/MessageDto.cs`, `ConversationSummaryDto.cs`, `ConversationDetailDto.cs`
- Modify: `src/Api/Services/IConversationService.cs`, `src/Api/Services/ConversationService.cs`
- Modify: `tests/Api.Tests/ConversationServiceTests.cs`

**Interfaces:**
- Consumes: `ConversationService`, `TestDb`, entidades.
- Produces:
  - `record MessageDto(int Id, MessageRole Role, string Content, DateTime CreatedAt, MessageRating? Rating, string? ReviewComment, DateTime? ReviewedAt, string? PromptVersion)` com `static MessageDto FromEntity(Message m)`
  - `record ConversationSummaryDto(int Id, string StudentName, string? LastMessagePreview, DateTime LastActivityAt, int FailureCount)`
  - `record ConversationDetailDto(int Id, int StudentId, string StudentName, DateTime CreatedAt, List<MessageDto> Messages)`
  - `Task<List<ConversationSummaryDto>> ListAsync(bool hasFailures, CancellationToken ct)` — mais recente primeiro
  - `Task<ConversationDetailDto?> GetByIdAsync(int id, CancellationToken ct)` — `null` se não existir; mensagens em ordem cronológica

- [ ] **Step 1: DTOs**

`src/Api/Models/Dtos/MessageDto.cs`:

```csharp
using Api.Models.Entities;

namespace Api.Models.Dtos;

public record MessageDto(
    int Id,
    MessageRole Role,
    string Content,
    DateTime CreatedAt,
    MessageRating? Rating,
    string? ReviewComment,
    DateTime? ReviewedAt,
    string? PromptVersion)
{
    public static MessageDto FromEntity(Message m) =>
        new(m.Id, m.Role, m.Content, m.CreatedAt, m.Rating, m.ReviewComment, m.ReviewedAt, m.PromptVersion);
}
```

`src/Api/Models/Dtos/ConversationSummaryDto.cs`:

```csharp
namespace Api.Models.Dtos;

public record ConversationSummaryDto(
    int Id,
    string StudentName,
    string? LastMessagePreview,
    DateTime LastActivityAt,
    int FailureCount);
```

`src/Api/Models/Dtos/ConversationDetailDto.cs`:

```csharp
namespace Api.Models.Dtos;

public record ConversationDetailDto(
    int Id,
    int StudentId,
    string StudentName,
    DateTime CreatedAt,
    List<MessageDto> Messages);
```

- [ ] **Step 2: Estender a interface**

Em `src/Api/Services/IConversationService.cs`, adicionar dentro da interface:

```csharp
    /// <summary>Lista conversas, mais recentes primeiro. Com hasFailures, só as que têm resposta marcada como Failure.</summary>
    Task<List<ConversationSummaryDto>> ListAsync(bool hasFailures, CancellationToken ct);

    /// <summary>Conversa com todas as mensagens em ordem cronológica, ou null se não existir.</summary>
    Task<ConversationDetailDto?> GetByIdAsync(int id, CancellationToken ct);
```

- [ ] **Step 3: Escrever os testes de listagem (falhando)**

Em `tests/Api.Tests/ConversationServiceTests.cs`, adicionar os usings `using Api.Models.Entities;` e, dentro da classe, o helper e os testes:

```csharp
    private static async Task<Conversation> AddConversationAsync(
        TestDb db, long chatId, string name, params Message[] messages)
    {
        var student = new Student { Name = name, TelegramChatId = chatId, Conversation = new Conversation() };
        student.Conversation.Messages.AddRange(messages);
        db.Context.Students.Add(student);
        await db.Context.SaveChangesAsync();
        return student.Conversation;
    }

    private static Message Msg(MessageRole role, string content, DateTime at, MessageRating? rating = null) =>
        new() { Role = role, Content = content, CreatedAt = at, Rating = rating };

    [Fact]
    public async Task List_OrdersByLastActivityAndShowsPreview()
    {
        using var db = new TestDb();
        var t = new DateTime(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc);
        await AddConversationAsync(db, 1, "Antiga", Msg(MessageRole.User, "oi antigo", t));
        await AddConversationAsync(db, 2, "Recente",
            Msg(MessageRole.User, "oi", t.AddHours(1)),
            Msg(MessageRole.Agent, "Olá, tudo bem?", t.AddHours(2)));
        var service = new ConversationService(db.Context);

        var result = await service.ListAsync(hasFailures: false, CancellationToken.None);

        Assert.Equal(["Recente", "Antiga"], result.Select(r => r.StudentName));
        Assert.Equal("Olá, tudo bem?", result[0].LastMessagePreview);
        Assert.Equal(t.AddHours(2), result[0].LastActivityAt);
    }

    [Fact]
    public async Task List_HasFailuresTrue_ReturnsOnlyConversationsWithFailureRatedMessages()
    {
        using var db = new TestDb();
        var t = new DateTime(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc);
        await AddConversationAsync(db, 1, "Com falha",
            Msg(MessageRole.Agent, "errei", t, MessageRating.Failure));
        await AddConversationAsync(db, 2, "Só boa",
            Msg(MessageRole.Agent, "acertei", t, MessageRating.Good));
        var service = new ConversationService(db.Context);

        var result = await service.ListAsync(hasFailures: true, CancellationToken.None);

        var only = Assert.Single(result);
        Assert.Equal("Com falha", only.StudentName);
        Assert.Equal(1, only.FailureCount);
    }

    [Fact]
    public async Task List_EmptyConversation_UsesCreatedAtAndNullPreview()
    {
        using var db = new TestDb();
        var conversation = await AddConversationAsync(db, 1, "Sem mensagens");
        var service = new ConversationService(db.Context);

        var result = await service.ListAsync(hasFailures: false, CancellationToken.None);

        var only = Assert.Single(result);
        Assert.Null(only.LastMessagePreview);
        Assert.Equal(conversation.CreatedAt, only.LastActivityAt);
    }
```

- [ ] **Step 4: Rodar e ver falhar**

Run: `dotnet test --filter ConversationServiceTests`
Expected: FAIL de compilação — `ConversationService` não implementa `ListAsync`/`GetByIdAsync`.

- [ ] **Step 5: Implementar `ListAsync`**

Em `src/Api/Services/ConversationService.cs`, adicionar:

```csharp
    public async Task<List<ConversationSummaryDto>> ListAsync(bool hasFailures, CancellationToken ct)
    {
        var query = _db.Conversations.AsNoTracking();

        if (hasFailures)
        {
            query = query.Where(c => c.Messages.Any(m => m.Rating == MessageRating.Failure));
        }

        var rows = await query
            .Select(c => new
            {
                c.Id,
                StudentName = c.Student.Name,
                c.CreatedAt,
                LastContent = c.Messages
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => m.Content)
                    .FirstOrDefault(),
                LastAt = c.Messages.Max(m => (DateTime?)m.CreatedAt),
                FailureCount = c.Messages.Count(m => m.Rating == MessageRating.Failure)
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new ConversationSummaryDto(
                r.Id, r.StudentName, Preview(r.LastContent), r.LastAt ?? r.CreatedAt, r.FailureCount))
            .OrderByDescending(r => r.LastActivityAt)
            .ToList();
    }

    private static string? Preview(string? content)
    {
        const int maxLength = 80;
        if (content is null) return null;
        return content.Length <= maxLength ? content : content[..maxLength] + "…";
    }
```

- [ ] **Step 6: Rodar e ver os testes de listagem passarem**

Run: `dotnet test --filter ConversationServiceTests`
Expected: ainda FAIL de compilação, por `GetByIdAsync` não existir. Adicionar um stub **temporário** para isolar:

```csharp
    public Task<ConversationDetailDto?> GetByIdAsync(int id, CancellationToken ct) =>
        throw new NotImplementedException();
```

Run: `dotnet test --filter "ConversationServiceTests&FullyQualifiedName~List"`
Expected: PASS (3 testes de `List_*`).

Se o EF reclamar de tradução de `Max`/`OrderBy` no SQLite, simplificar a projeção (ex.: buscar `LastAt` e `LastContent` com duas consultas) mantendo os mesmos testes verdes.

- [ ] **Step 7: `[SUA VEZ]` — `GetByIdAsync` (você escreve; PRD R3)**

Escreva você mesmo, sem copiar do gabarito:

1. Em `ConversationServiceTests`, dois testes: `GetById_Existing_ReturnsMessagesInChronologicalOrder` (insira 3 mensagens com `CreatedAt` fora de ordem e confira a ordem do resultado) e `GetById_Missing_ReturnsNull`.
2. Rode e veja falhar (o stub lança `NotImplementedException`).
3. Troque o stub por uma implementação real e veja passar.

Dicas: `Include(c => c.Student)`, `Include(c => c.Messages.OrderBy(...))` (filtered include do EF 8), `FirstOrDefaultAsync`, mapear com `MessageDto.FromEntity`.

<details>
<summary>Gabarito (abra só depois de tentar)</summary>

Testes:

```csharp
    [Fact]
    public async Task GetById_Existing_ReturnsMessagesInChronologicalOrder()
    {
        using var db = new TestDb();
        var t = new DateTime(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc);
        var conversation = await AddConversationAsync(db, 1, "Ana",
            Msg(MessageRole.Agent, "terceira", t.AddMinutes(3)),
            Msg(MessageRole.User, "primeira", t.AddMinutes(1)),
            Msg(MessageRole.Agent, "segunda", t.AddMinutes(2)));
        var service = new ConversationService(db.Context);

        var result = await service.GetByIdAsync(conversation.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Ana", result.StudentName);
        Assert.Equal(["primeira", "segunda", "terceira"], result.Messages.Select(m => m.Content));
    }

    [Fact]
    public async Task GetById_Missing_ReturnsNull()
    {
        using var db = new TestDb();
        var service = new ConversationService(db.Context);

        var result = await service.GetByIdAsync(999, CancellationToken.None);

        Assert.Null(result);
    }
```

Implementação (substitui o stub):

```csharp
    public async Task<ConversationDetailDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        var conversation = await _db.Conversations
            .AsNoTracking()
            .Include(c => c.Student)
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id))
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conversation is null) return null;

        return new ConversationDetailDto(
            conversation.Id,
            conversation.StudentId,
            conversation.Student.Name,
            conversation.CreatedAt,
            conversation.Messages.Select(MessageDto.FromEntity).ToList());
    }
```

</details>

- [ ] **Step 8: Rodar tudo**

Run: `dotnet test`
Expected: PASS (todos; 4 da Task 3 + 3 `List_*` + 2 `GetById_*` + 2 de `AppDbContextTests` = 11).

---

### Task 5: `MessageService.AddUserMessageAsync` (RF-04 parcial)

**Files:**
- Create: `src/Api/Models/Dtos/SendMessageRequest.cs`
- Create: `src/Api/Services/IMessageService.cs`, `MessageService.cs`
- Test: `tests/Api.Tests/MessageServiceTests.cs`

**Interfaces:**
- Consumes: `AppDbContext`, `MessageDto.FromEntity`, `TestDb`.
- Produces:
  - `record SendMessageRequest(string Content)` (`[Required]`, `[StringLength(2000)]`)
  - `interface IMessageService { Task<MessageDto?> AddUserMessageAsync(int conversationId, string content, CancellationToken ct); }` — `null` se a conversa não existe
  - Na Fase 2 este serviço passa a chamar o `AgentService` e devolver a resposta do agente; na Fase 1 devolve a mensagem do aluno salva.

- [ ] **Step 1: DTO e interface**

`src/Api/Models/Dtos/SendMessageRequest.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace Api.Models.Dtos;

public record SendMessageRequest([Required, StringLength(2000)] string Content);
```

`src/Api/Services/IMessageService.cs`:

```csharp
using Api.Models.Dtos;

namespace Api.Services;

public interface IMessageService
{
    /// <summary>Salva a mensagem do aluno. Devolve null se a conversa não existe.</summary>
    Task<MessageDto?> AddUserMessageAsync(int conversationId, string content, CancellationToken ct);
}
```

- [ ] **Step 2: Testes que falham**

`tests/Api.Tests/MessageServiceTests.cs`:

```csharp
using Api.Models.Entities;
using Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

public class MessageServiceTests
{
    [Fact]
    public async Task AddUserMessage_ExistingConversation_PersistsMessageWithUserRole()
    {
        using var db = new TestDb();
        var student = new Student { Name = "Ana", TelegramChatId = 1, Conversation = new Conversation() };
        db.Context.Students.Add(student);
        await db.Context.SaveChangesAsync();
        var service = new MessageService(db.Context);

        var result = await service.AddUserMessageAsync(
            student.Conversation!.Id, "  Quanto custa?  ", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(MessageRole.User, result.Role);
        Assert.Equal("Quanto custa?", result.Content);
        var saved = await db.Context.Messages.SingleAsync();
        Assert.Equal(student.Conversation.Id, saved.ConversationId);
    }

    [Fact]
    public async Task AddUserMessage_UnknownConversation_ReturnsNullAndSavesNothing()
    {
        using var db = new TestDb();
        var service = new MessageService(db.Context);

        var result = await service.AddUserMessageAsync(999, "oi", CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, await db.Context.Messages.CountAsync());
    }
}
```

- [ ] **Step 3: Rodar e ver falhar**

Run: `dotnet test --filter MessageServiceTests`
Expected: FAIL de compilação — `MessageService` não existe.

- [ ] **Step 4: Implementar**

`src/Api/Services/MessageService.cs`:

```csharp
using Api.Data;
using Api.Models.Dtos;
using Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class MessageService : IMessageService
{
    private readonly AppDbContext _db;

    public MessageService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<MessageDto?> AddUserMessageAsync(int conversationId, string content, CancellationToken ct)
    {
        var exists = await _db.Conversations.AnyAsync(c => c.Id == conversationId, ct);
        if (!exists) return null;

        var message = new Message
        {
            ConversationId = conversationId,
            Role = MessageRole.User,
            Content = content.Trim()
        };
        _db.Messages.Add(message);
        await _db.SaveChangesAsync(ct);

        return MessageDto.FromEntity(message);
    }
}
```

- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test`
Expected: PASS (13 testes).

---

### Task 6: Controller, `Program.cs`, Swagger e verificação ponta a ponta

**Files:**
- Create: `src/Api/Controllers/ConversationsController.cs`
- Modify (reescrever): `src/Api/Program.cs`

**Interfaces:**
- Consumes: `IConversationService`, `IMessageService`, DTOs, `AppDbContext`.
- Produces: endpoints
  - `POST /api/conversations` → 201/200 `ConversationDto`
  - `GET /api/conversations?hasFailures=` → 200 `ConversationSummaryDto[]`
  - `GET /api/conversations/{id}` → 200 `ConversationDetailDto` | 404
  - `POST /api/conversations/{id}/messages` → 200 `MessageDto` | 400 | 404

- [ ] **Step 1: Controller**

`src/Api/Controllers/ConversationsController.cs`:

```csharp
using Api.Models.Dtos;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/conversations")]
public class ConversationsController : ControllerBase
{
    private readonly IConversationService _conversations;
    private readonly IMessageService _messages;

    public ConversationsController(IConversationService conversations, IMessageService messages)
    {
        _conversations = conversations;
        _messages = messages;
    }

    [HttpPost]
    [ProducesResponseType<ConversationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ConversationDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationDto>> Create(
        CreateConversationRequest request, CancellationToken ct)
    {
        var (conversation, created) = await _conversations.GetOrCreateAsync(request, ct);

        return created
            ? CreatedAtAction(nameof(GetById), new { id = conversation.Id }, conversation)
            : Ok(conversation);
    }

    [HttpGet]
    public async Task<ActionResult<List<ConversationSummaryDto>>> List(
        [FromQuery] bool hasFailures, CancellationToken ct)
    {
        return await _conversations.ListAsync(hasFailures, ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ConversationDetailDto>> GetById(int id, CancellationToken ct)
    {
        var conversation = await _conversations.GetByIdAsync(id, ct);
        return conversation is null ? NotFound() : conversation;
    }

    [HttpPost("{id:int}/messages")]
    public async Task<ActionResult<MessageDto>> SendMessage(
        int id, SendMessageRequest request, CancellationToken ct)
    {
        var message = await _messages.AddUserMessageAsync(id, request.Content, ct);
        return message is null ? NotFound() : message;
    }
}
```

- [ ] **Step 2: Program.cs**

Substituir o conteúdo de `src/Api/Program.cs`:

```csharp
using System.Text.Json.Serialization;
using Api.Data;
using Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IMessageService, MessageService>();

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Em desenvolvimento, aplica as migrations ao subir (RNF-05).
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.MapControllers();

app.Run();
```

- [ ] **Step 3: Build e testes**

Run: `dotnet build` e `dotnet test`
Expected: `Build succeeded`; 13 testes PASS.

- [ ] **Step 4: Subir a API e testar de ponta a ponta**

Com o banco no ar (`docker compose up -d`), rodar em outro terminal (ou em background):

```bash
dotnet run --project src/Api
```

Expected: log `Now listening on: http://localhost:5080`.

Criar conversa (primeira vez → 201):

```bash
curl -s -i -X POST http://localhost:5080/api/conversations -H "Content-Type: application/json" -d '{"telegramChatId":111,"studentName":"Ana"}'
```

Expected: `HTTP/1.1 201 Created`, cabeçalho `Location: .../api/conversations/1` e JSON com `id`, `studentName":"Ana"`.

Repetir o mesmo comando → `HTTP/1.1 200 OK` com o mesmo `id`.

Enviar mensagem:

```bash
curl -s -i -X POST http://localhost:5080/api/conversations/1/messages -H "Content-Type: application/json" -d '{"content":"Quanto custa o curso?"}'
```

Expected: `200` e JSON com `"role":"User"`, `"content":"Quanto custa o curso?"`.

Casos de erro:

```bash
curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:5080/api/conversations/1/messages -H "Content-Type: application/json" -d '{"content":""}'
curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:5080/api/conversations/999/messages -H "Content-Type: application/json" -d '{"content":"oi"}'
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5080/api/conversations/999
```

Expected: `400`, `404`, `404`.

Listar e detalhar:

```bash
curl -s http://localhost:5080/api/conversations
curl -s "http://localhost:5080/api/conversations?hasFailures=true"
curl -s http://localhost:5080/api/conversations/1
```

Expected: primeira lista com a Ana e `lastMessagePreview":"Quanto custa o curso?"`; a segunda `[]`; a terceira com `messages` contendo a mensagem.

Abrir `http://localhost:5080/swagger` e confirmar que os 4 endpoints aparecem. Parar a API (Ctrl+C).

- [ ] **Step 5: Critério de pronto da Fase 1 (PRD seção 10)**

- [ ] `docker compose up -d` sobe o banco
- [ ] Swagger responde em `/swagger`
- [ ] Criar e listar conversa funciona
- [ ] `dotnet test` verde
- [ ] Nenhum segredo real no repositório (`git status` não lista `.env`)

- [ ] **Step 6: Explicação e commit (com autorização do dono)**

Antes do commit, o agente explica ao dono (linguagem simples, comparando com FastAPI) o fluxo `POST /api/conversations/{id}/messages` do controller ao banco; o dono reescreve com as próprias palavras na seção "Como funciona" do README (Fase 5).

```bash
git add .
git commit -m "feat: base da API com entidades e endpoints de conversa"
```

---

## Self-Review

**Cobertura do PRD (Fase 1):** RF-01 (Task 3 + 6), RF-02 incl. `hasFailures` (Task 4 + 6), RF-03 (Task 4 + 6), RF-04 parcial sem IA (Task 5 + 6: 400/404/200), RF-07 Swagger (Task 6), RNF-02/04/05 (nullable, ct, testes, compose + migrate em Development). Fora da Fase 1 de propósito: RF-05, RF-06 (Fase 4), RF-08 CORS (Fase 4), IA (Fase 2).

**Placeholders:** nenhum; o único stub (`NotImplementedException`) é temporário e substituído na Task 4 Step 7.

**Consistência de tipos:** `GetOrCreateAsync` retorna `(ConversationDto Conversation, bool Created)` nas Tasks 3 e 6; `MessageDto.FromEntity` definido na Task 4 e usado nas Tasks 4 e 5 (Task 5 depende da 4); `TestDb.Context` usado em todos os testes; `IMessageService.AddUserMessageAsync(int, string, CancellationToken)` igual na interface, no serviço, nos testes e no controller. Observação de ordem: Task 5 usa `MessageDto` criado na Task 4, então a ordem 4 → 5 deve ser mantida.
