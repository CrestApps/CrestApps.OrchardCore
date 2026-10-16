using CrestApps.OrchardCore.AI.Chat.Interactions.Models;
using CrestApps.OrchardCore.AI.Chat.Interactions.Services;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat.Interactions;

public sealed class ProfileBuilderCoachTests
{
    [Fact]
    public void BuildCoachRequest_ListsEveryEarlierAttemptBeforeTheNewFeedback()
    {
        // Arrange
        var current = new ProfileBuilderSettings { SystemMessage = "You book appointments.", Temperature = 0.7f };
        var history = new List<ProfileBuilderFeedback>
        {
            new() { Id = "1", Kind = ProfileBuilderFeedbackKind.FeedbackAndRegenerate, CustomerMessage = "Can I come Monday?", AssistantReply = "Attempt one", Comment = "Too long.", WasRegenerated = true, RevisionNumber = 2 },
            new() { Id = "2", Kind = ProfileBuilderFeedbackKind.FeedbackAndRegenerate, CustomerMessage = "Can I come Monday?", AssistantReply = "Attempt two", Comment = "Ask for a time.", WasRegenerated = true, RevisionNumber = 3 },
            new() { Id = "3", Kind = ProfileBuilderFeedbackKind.Approved, CustomerMessage = "Thanks", AssistantReply = "You're welcome!" },
        };
        var newFeedback = new ProfileBuilderFeedback { Id = "4", Kind = ProfileBuilderFeedbackKind.Feedback, CustomerMessage = "Can I come Monday?", AssistantReply = "Attempt three", Comment = "Confirm the date back." };
        history.Add(newFeedback);

        var transcript = new List<ProfileBuilderTurn>
        {
            new() { Role = "user", Text = "Can I come Monday?" },
            new() { Role = "assistant", Text = "Attempt three" },
        };

        var revisions = new List<ProfileBuilderRevision>
        {
            new() { Number = 1, Source = ProfileBuilderRevisionSource.Initial, Summary = "Started." },
            new() { Number = 2, Source = ProfileBuilderRevisionSource.Feedback, Summary = "Shorter replies." },
        };

        // Act
        var request = ProfileBuilderCoachPrompt.BuildCoachRequest(current, transcript, history, revisions, newFeedback);

        // Assert
        var newFeedbackAt = request.IndexOf("# New feedback to apply now", StringComparison.Ordinal);

        Assert.True(request.IndexOf("Attempt one", StringComparison.Ordinal) < newFeedbackAt);
        Assert.True(request.IndexOf("Attempt two", StringComparison.Ordinal) < newFeedbackAt);
        Assert.Contains("Too long.", request);
        Assert.Contains("Ask for a time.", request);
        Assert.Contains("APPROVED", request);
        Assert.Contains("Temperature: 0.7", request);
        Assert.Contains("Revision 2 (Feedback): Shorter replies.", request);
        Assert.True(request.IndexOf("Confirm the date back.", StringComparison.Ordinal) > newFeedbackAt);

        // The new feedback appears once, after the heading, not again in the history.
        Assert.Equal(request.IndexOf("Confirm the date back.", StringComparison.Ordinal), request.LastIndexOf("Confirm the date back.", StringComparison.Ordinal));
    }

    [Fact]
    public void ApplyTo_ClampsParametersAndKeepsWhatTheCoachLeftOut()
    {
        // Arrange
        var current = new ProfileBuilderSettings
        {
            SystemMessage = "Current.",
            InitialPrompt = "Open.",
            Temperature = 0.5f,
            TopP = 0.9f,
            MaxTokens = 500,
            PastMessagesCount = 10,
        };

        var result = new ProfileBuilderCoachResult
        {
            SystemMessage = "  ",
            Temperature = 5,
            TopP = -1,
            MaxTokens = 1,
            PastMessagesCount = 99,
        };

        // Act
        var settings = result.ApplyTo(current);

        // Assert
        Assert.Equal("Current.", settings.SystemMessage);
        Assert.Equal("Open.", settings.InitialPrompt);
        Assert.Equal(2f, settings.Temperature);
        Assert.Equal(0f, settings.TopP);
        Assert.Equal(4, settings.MaxTokens);
        Assert.Equal(50, settings.PastMessagesCount);
        Assert.Equal(0.5f, current.Temperature);
    }

    [Theory]
    [InlineData("pass", ProfileBuilderReplayVerdict.Pass)]
    [InlineData(" FAIL ", ProfileBuilderReplayVerdict.Fail)]
    [InlineData("maybe", ProfileBuilderReplayVerdict.Unknown)]
    [InlineData(null, ProfileBuilderReplayVerdict.Unknown)]
    public void JudgeVerdict_IsReadLeniently(string value, ProfileBuilderReplayVerdict expected)
    {
        Assert.Equal(expected, new ProfileBuilderJudgeVerdict { Verdict = value }.ToVerdict());
    }

    [Fact]
    public void HasSameValues_IgnoresLineEndingsAndSurroundingWhitespace()
    {
        var first = new ProfileBuilderSettings { SystemMessage = "Line one\r\nLine two\n", Temperature = 0.3f };
        var second = new ProfileBuilderSettings { SystemMessage = "Line one\nLine two", Temperature = 0.3f };
        var third = new ProfileBuilderSettings { SystemMessage = "Line one\nLine two", Temperature = 0.4f };

        Assert.True(first.HasSameValues(second));
        Assert.False(first.HasSameValues(third));
    }
}
