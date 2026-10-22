using System.ComponentModel.DataAnnotations;

namespace Api.Models.Dtos;

public record CreateConversationRequest(
    long TelegramChatId,
    [Required, StringLength(100)] string StudentName);
