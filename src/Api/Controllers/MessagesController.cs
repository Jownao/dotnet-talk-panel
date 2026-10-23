using Api.Models.Dtos;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/messages")]
public class MessagesController : ControllerBase
{
    private readonly IReviewService _reviews;

    public MessagesController(IReviewService reviews)
    {
        _reviews = reviews;
    }

    /// <summary>Marca uma resposta do agente como Boa ou Falha, com comentário opcional.</summary>
    [HttpPatch("{id:int}/review")]
    [ProducesResponseType<MessageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MessageDto>> Review(int id, ReviewRequest request, CancellationToken ct)
    {
        var (outcome, message) = await _reviews.ReviewAsync(id, request, ct);

        return outcome switch
        {
            ReviewOutcome.NotFound => NotFound(),
            ReviewOutcome.NotAgentMessage => Problem(
                title: "Mensagem não avaliável",
                detail: "Só respostas do agente podem ser avaliadas.",
                statusCode: StatusCodes.Status400BadRequest),
            _ => message!
        };
    }
}
