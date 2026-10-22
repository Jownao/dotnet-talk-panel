namespace Api.Models.Entities;

public class Student
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public long TelegramChatId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Conversation? Conversation { get; set; }
}
