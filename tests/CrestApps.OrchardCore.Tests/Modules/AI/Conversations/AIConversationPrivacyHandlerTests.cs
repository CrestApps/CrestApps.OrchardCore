using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.Privacy;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Conversations;

public sealed class AIConversationPrivacyHandlerTests
{
    private static readonly DateTime _started = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Evaluations_AreRedactedWithTheConversation()
    {
        var evaluation = NewEvaluation();
        var store = NewEvaluationStore(evaluation);
        var handler = new AIConversationEvaluationPrivacyHandler(store.Object);
        var context = new AIConversationPrivacyContext(NewSession(), AIDataStorageMode.RedactPersonalData);

        await handler.CollectingAsync(context, TestContext.Current.CancellationToken);

        Assert.Contains("Customer at jane@example.com asked to move the booking.", context.Texts);
        Assert.Contains("Call me on (702) 555-0147", context.Texts);

        await handler.ApplyingAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal("Customer at [EMAIL] asked to move the booking.", evaluation.Summary);
        Assert.Equal("Confirmed [EMAIL].", evaluation.Criteria[0].Reason);
        Assert.Equal("Read back [PHONE] wrongly.", evaluation.Issues[0].Detail);
        Assert.Equal("Call me on [PHONE]", evaluation.Issues[0].Quote);
        store.Verify(value => value.SaveAsync(evaluation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Evaluations_AreClearedWhenOnlyMetadataIsKept()
    {
        var evaluation = NewEvaluation();
        var store = NewEvaluationStore(evaluation);
        var handler = new AIConversationEvaluationPrivacyHandler(store.Object);
        var context = new AIConversationPrivacyContext(NewSession(), AIDataStorageMode.MetadataOnly);

        await handler.CollectingAsync(context, TestContext.Current.CancellationToken);
        await handler.ApplyingAsync(context, TestContext.Current.CancellationToken);

        Assert.Null(evaluation.Summary);
        Assert.Null(evaluation.Criteria[0].Reason);
        Assert.Null(evaluation.Issues[0].Detail);
        Assert.Null(evaluation.Issues[0].Quote);
        Assert.Equal(55, evaluation.Score);
        store.Verify(value => value.SaveAsync(evaluation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Evaluations_AreDeletedWithTheConversation()
    {
        var store = new Mock<IAIConversationEvaluationStore>();
        var handler = new AIConversationEvaluationPrivacyHandler(store.Object);

        await handler.DeletingAsync(NewSession(), TestContext.Current.CancellationToken);

        store.Verify(value => value.DeleteBySessionAsync("s1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExtractedData_OfTheConversation_IsRedactedOrCleared()
    {
        var other = NewRecord("s2");
        var record = NewRecord("s1");
        var store = new Mock<IAIChatSessionExtractedDataStore>();
        store
            .Setup(value => value.GetAsync("p1", _started.AddDays(-1), _started.AddDays(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync([other, record]);
        var handler = new AIChatSessionExtractedDataPrivacyHandler(store.Object);
        var context = new AIConversationPrivacyContext(NewSession(), AIDataStorageMode.RedactPersonalData);

        await handler.CollectingAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(["jane@example.com", "Tuesday"], context.Texts);

        await handler.ApplyingAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(["[EMAIL]"], record.Values["email"]);
        Assert.Equal(["Tuesday"], record.Values["day"]);
        Assert.Equal(["jane@example.com"], other.Values["email"]);
        store.Verify(value => value.SaveAsync(record, It.IsAny<CancellationToken>()), Times.Once);

        var stripped = new AIConversationPrivacyContext(NewSession(), AIDataStorageMode.MetadataOnly);
        await handler.CollectingAsync(stripped, TestContext.Current.CancellationToken);
        await handler.ApplyingAsync(stripped, TestContext.Current.CancellationToken);

        Assert.Empty(record.Values["email"]);
        Assert.Empty(record.Values["day"]);
    }

    [Fact]
    public async Task ExtractedData_IsLeftAloneWithoutARecord_AndDeletedWithTheConversation()
    {
        var store = new Mock<IAIChatSessionExtractedDataStore>();
        store
            .Setup(value => value.GetAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([NewRecord("s2")]);
        var handler = new AIChatSessionExtractedDataPrivacyHandler(store.Object);
        var context = new AIConversationPrivacyContext(NewSession(), AIDataStorageMode.RedactPersonalData);

        await handler.CollectingAsync(context, TestContext.Current.CancellationToken);
        await handler.ApplyingAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(context.Texts);
        store.Verify(value => value.SaveAsync(It.IsAny<AIChatSessionExtractedDataRecord>(), It.IsAny<CancellationToken>()), Times.Never);

        await handler.DeletingAsync(NewSession(), TestContext.Current.CancellationToken);

        store.Verify(value => value.DeleteAsync("s1", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AIChatSession NewSession()
        => new() { SessionId = "s1", ProfileId = "p1", CreatedUtc = _started };

    private static AIConversationEvaluation NewEvaluation()
        => new()
        {
            SessionId = "s1",
            Score = 55,
            Summary = "Customer at jane@example.com asked to move the booking.",
            Criteria = [new AIConversationCriterionResult { Name = "Confirms the email", Reason = "Confirmed jane@example.com." }],
            Issues = [new AIConversationIssue { Kind = AIConversationIssueKinds.Repetition, Detail = "Read back (702) 555-0147 wrongly.", Quote = "Call me on (702) 555-0147" }],
        };

    private static Mock<IAIConversationEvaluationStore> NewEvaluationStore(AIConversationEvaluation evaluation)
    {
        var store = new Mock<IAIConversationEvaluationStore>();
        store
            .Setup(value => value.ListBySessionAsync("s1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([evaluation]);

        return store;
    }

    private static AIChatSessionExtractedDataRecord NewRecord(string sessionId)
        => new()
        {
            SessionId = sessionId,
            ProfileId = "p1",
            Values = new()
            {
                ["email"] = ["jane@example.com"],
                ["day"] = ["Tuesday"],
            },
        };
}
