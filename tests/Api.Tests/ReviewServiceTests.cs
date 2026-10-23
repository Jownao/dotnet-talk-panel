using Api.Models.Dtos;
using Api.Models.Entities;
using Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

public class ReviewServiceTests
{
    private static async Task<Message> SeedMessageAsync(TestDb db, MessageRole role)
    {
        var student = new Student { Name = "Ana", TelegramChatId = 1, Conversation = new Conversation() };
        var message = new Message { Role = role, Content = "texto", PromptVersion = role == MessageRole.Agent ? "v1" : null };
        student.Conversation.Messages.Add(message);
        db.Context.Students.Add(student);
        await db.Context.SaveChangesAsync();
        return message;
    }

    [Theory]
    [InlineData(MessageRating.Good)]
    [InlineData(MessageRating.Failure)]
    public async Task Review_AgentMessage_SavesRatingCommentAndReviewedAt(MessageRating rating)
    {
        using var db = new TestDb();
        var message = await SeedMessageAsync(db, MessageRole.Agent);
        var service = new ReviewService(db.Context);

        var (outcome, dto) = await service.ReviewAsync(
            message.Id, new ReviewRequest(rating, "Inventou um preço."), CancellationToken.None);

        Assert.Equal(ReviewOutcome.Ok, outcome);
        Assert.Equal(rating, dto!.Rating);
        Assert.Equal("Inventou um preço.", dto.ReviewComment);
        Assert.NotNull(dto.ReviewedAt);

        var saved = await db.Context.Messages.AsNoTracking().SingleAsync();
        Assert.Equal(rating, saved.Rating);
        Assert.Equal("Inventou um preço.", saved.ReviewComment);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Review_BlankComment_IsStoredAsNull(string? comment)
    {
        using var db = new TestDb();
        var message = await SeedMessageAsync(db, MessageRole.Agent);
        var service = new ReviewService(db.Context);

        var (_, dto) = await service.ReviewAsync(
            message.Id, new ReviewRequest(MessageRating.Good, comment), CancellationToken.None);

        Assert.Null(dto!.ReviewComment);
    }

    [Fact]
    public async Task Review_CommentIsTrimmed()
    {
        using var db = new TestDb();
        var message = await SeedMessageAsync(db, MessageRole.Agent);
        var service = new ReviewService(db.Context);

        var (_, dto) = await service.ReviewAsync(
            message.Id, new ReviewRequest(MessageRating.Failure, "  longa demais  "), CancellationToken.None);

        Assert.Equal("longa demais", dto!.ReviewComment);
    }

    [Fact]
    public async Task Review_UserMessage_ReturnsNotAgentMessageAndChangesNothing()
    {
        using var db = new TestDb();
        var message = await SeedMessageAsync(db, MessageRole.User);
        var service = new ReviewService(db.Context);

        var (outcome, dto) = await service.ReviewAsync(
            message.Id, new ReviewRequest(MessageRating.Failure, null), CancellationToken.None);

        Assert.Equal(ReviewOutcome.NotAgentMessage, outcome);
        Assert.Null(dto);
        Assert.Null((await db.Context.Messages.AsNoTracking().SingleAsync()).Rating);
    }

    [Fact]
    public async Task Review_UnknownMessage_ReturnsNotFound()
    {
        using var db = new TestDb();
        var service = new ReviewService(db.Context);

        var (outcome, dto) = await service.ReviewAsync(
            999, new ReviewRequest(MessageRating.Good, null), CancellationToken.None);

        Assert.Equal(ReviewOutcome.NotFound, outcome);
        Assert.Null(dto);
    }

    [Fact]
    public async Task Review_ReviewingAgain_OverwritesPreviousReview()
    {
        using var db = new TestDb();
        var message = await SeedMessageAsync(db, MessageRole.Agent);
        var service = new ReviewService(db.Context);
        await service.ReviewAsync(message.Id, new ReviewRequest(MessageRating.Failure, "ruim"), CancellationToken.None);

        var (_, dto) = await service.ReviewAsync(
            message.Id, new ReviewRequest(MessageRating.Good, null), CancellationToken.None);

        Assert.Equal(MessageRating.Good, dto!.Rating);
        Assert.Null(dto.ReviewComment);
    }
}
