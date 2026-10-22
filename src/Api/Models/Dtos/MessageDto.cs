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
