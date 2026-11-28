using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.PersonalData;
using Microsoft.Extensions.AI;
using Moq;

namespace CrestApps.OrchardCore.Tests.PersonalData;

/// <summary>
/// A privacy export carries the AI conversation an automated activity (an SMS or email exchange) held with the person.
/// </summary>
public sealed class CrmActivityAIConversationExportTests
{
    [Fact]
    public async Task ExportAISessionAsync_AddsTheConversationAndEveryTurnOfIt()
    {
        // Arrange
        var createdUtc = new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
        var prompts = new Mock<IAIChatSessionPromptStore>();
        prompts
            .Setup(store => store.GetPromptsAsync("session-1"))
            .ReturnsAsync(new List<AIChatSessionPrompt>
            {
                new() { ItemId = "turn-1", SessionId = "session-1", Role = ChatRole.Assistant, Content = "How can I help?", CreatedUtc = createdUtc },
                new() { ItemId = "turn-2", SessionId = "session-1", Role = ChatRole.User, Content = "It's Ann, about order 1001", CreatedUtc = createdUtc.AddMinutes(1) },
            });

        var sessions = new Mock<IAIChatSessionManager>();
        sessions
            .Setup(manager => manager.FindByIdAsync("session-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIChatSession { SessionId = "session-1", Title = "Order 1001", CreatedUtc = createdUtc });

        var contributor = new CrmActivityPersonalDataContributor(null, null, null, [prompts.Object], [sessions.Object], []);
        var export = new PersonalDataExport();

        // Act
        await contributor.ExportAISessionAsync(
            new PersonalDataExportContext(new PersonalDataSubject(email: "ann@example.com"), export),
            "session-1",
            "activity-1",
            TestContext.Current.CancellationToken);

        // Assert
        var conversation = Assert.Single(export.Records, record => record.Kind == CrmActivityPersonalDataContributor.AISessionKind);
        Assert.Equal("session-1", conversation.Id);
        Assert.Equal("activity-1", conversation.Fields["ActivityId"]);

        var turns = export.Records.Where(record => record.Kind == CrmActivityPersonalDataContributor.AIMessageKind).ToList();
        Assert.Equal(["turn-1", "turn-2"], turns.Select(record => record.Id));
        Assert.Equal("It's Ann, about order 1001", turns[1].Fields["Content"]);
        Assert.All(turns, record => Assert.Equal("activity-1", record.Fields["ActivityId"]));
    }

    [Fact]
    public async Task ExportAISessionAsync_WithoutTheAIFeatures_AddsNothing()
    {
        // Arrange
        var contributor = new CrmActivityPersonalDataContributor(null, null, null, [], [], []);
        var export = new PersonalDataExport();

        // Act
        await contributor.ExportAISessionAsync(
            new PersonalDataExportContext(new PersonalDataSubject(email: "ann@example.com"), export),
            "session-1",
            "activity-1",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(export.Records);
    }
}
