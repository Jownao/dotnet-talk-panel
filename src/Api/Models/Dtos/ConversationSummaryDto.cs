namespace Api.Models.Dtos;

public record ConversationSummaryDto(
    int Id,
    string StudentName,
    string? LastMessagePreview,
    DateTime LastActivityAt,
    int FailureCount);
