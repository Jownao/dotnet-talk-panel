namespace Api.Models.Dtos;

public record ConversationDetailDto(
    int Id,
    int StudentId,
    string StudentName,
    DateTime CreatedAt,
    List<MessageDto> Messages);
