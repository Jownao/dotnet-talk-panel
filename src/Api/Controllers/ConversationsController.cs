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
