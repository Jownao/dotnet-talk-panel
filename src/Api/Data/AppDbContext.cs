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
