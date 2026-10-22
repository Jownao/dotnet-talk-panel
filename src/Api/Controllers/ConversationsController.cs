using Api.Ai;
using Api.Models.Dtos;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/conversations")]
public class ConversationsController : ControllerBase
{
    private readonly IConversationService _conversations;
    private readonly IAgentService _agent;
    private readonly ILogger<ConversationsController> _logger;

    public ConversationsController(
        IConversationService conversations,
        IAgentService agent,
        ILogger<ConversationsController> logger)
    {
        _conversations = conversations;
        _agent = agent;
        _logger = logger;
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

    /// <summary>Recebe a mensagem do aluno e devolve a resposta do agente.</summary>
    [HttpPost("{id:int}/messages")]
    [ProducesResponseType<MessageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<MessageDto>> SendMessage(
        int id, SendMessageRequest request, CancellationToken ct)
    {
        try
        {
            var reply = await _agent.ReplyAsync(id, request.Content, ct);
            return reply is null ? NotFound() : reply;
        }
        catch (AiClientException ex)
        {
            _logger.LogError(ex, "Falha ao obter resposta da IA na conversa {ConversationId}", id);
            return Problem(
                title: "Agente indisponível",
                detail: "Não foi possível obter a resposta da IA agora. Tente novamente em instantes.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
