using CrestApps.OrchardCore.AI.Core.Conversations;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Conversations;

public sealed class AIQACohortTests
{
    [Fact]
    public void IsSampled_IsStableAndRoughlyTheShareAskedFor()
    {
        // Arrange
        var sessions = Enumerable.Range(0, 2000).Select(index => $"session-{index}").ToList();

        // Act
        var sampled = sessions.Count(session => AIQACohortEvaluator.IsSampled("cohort-1", session, 20));

        // Assert
        Assert.InRange(sampled, 300, 500);
        Assert.Equal(AIQACohortEvaluator.IsSampled("cohort-1", "session-7", 20), AIQACohortEvaluator.IsSampled("cohort-1", "session-7", 20));
        Assert.True(AIQACohortEvaluator.IsSampled("cohort-1", "session-7", 100));
        Assert.False(AIQACohortEvaluator.IsSampled("cohort-1", "session-7", 0));
    }

    [Fact]
    public void Summarize_GivesRatesIssuesAndTheLowestScores()
    {
        // Arrange
        var day = new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc);
        var evaluations = new List<AIConversationEvaluation>
        {
            new() { SessionId = "a", Score = 90, Passed = true, Resolved = true, Sentiment = "Positive", EvaluatedUtc = day.AddHours(1) },
            new() { SessionId = "b", Score = 40, Passed = false, Resolved = false, Sentiment = "Negative", EvaluatedUtc = day.AddHours(2), Issues = [new() { Kind = AIConversationIssueKinds.Hallucination }] },
            new() { SessionId = "c", Score = 70, Passed = true, Resolved = false, EvaluatedUtc = day.AddDays(1), Issues = [new() { Kind = AIConversationIssueKinds.Hallucination }, new() { Kind = "Tone" }] },
        };

        // Act
        var dashboard = AIQADashboardService.Summarize(evaluations);

        // Assert
        Assert.Equal(3, dashboard.Count);
        Assert.Equal(200d / 3, dashboard.AverageScore);
        Assert.Equal(2d / 3, dashboard.PassRate);
        Assert.Equal(1d / 3, dashboard.ResolutionRate);
        Assert.Equal(AIConversationIssueKinds.Hallucination, dashboard.Issues[0].Kind);
        Assert.Equal(2, dashboard.Issues[0].Count);
        Assert.Equal(2, dashboard.Days.Count);
        Assert.Equal("b", dashboard.Worst[0].SessionId);
        Assert.Equal(1, dashboard.Sentiments["Neutral"]);
    }

    [Theory]
    [InlineData("Hello, I want to check my order status please!", "check order status")]
    [InlineData("How can I check the status of my order?", "check status order")]
    [InlineData("", "")]
    public void QuestionKey_DropsFillerWords(string line, string expected)
        => Assert.Equal(expected, AIQADashboardService.QuestionKey(line));

    [Fact]
    public void Knowledge_KeepsOnlyWhatRetrievalAddsForTheLongestQuestions()
    {
        // Arrange
        var turns = new List<AIConversationTurn>
        {
            new(AIConversationTurn.CustomerRole, "Hi", DateTime.UtcNow),
            new(AIConversationTurn.CustomerRole, "What is your return policy for opened items?", DateTime.UtcNow),
            new(AIConversationTurn.AssistantRole, "Thirty days.", DateTime.UtcNow),
            new(AIConversationTurn.CustomerRole, "Do you ship to Canada and how long does it take?", DateTime.UtcNow),
        };

        // Act
        var questions = AIConversationKnowledgeCollector.SelectQuestions(turns);
        var added = AIConversationKnowledgeCollector.Added("You are a helpful assistant.", "You are a helpful assistant.\n[Source 1] Returns are accepted within 30 days.");

        // Assert
        Assert.Equal(2, questions.Count);
        Assert.StartsWith("What is your return policy", questions[0], StringComparison.Ordinal);
        Assert.Equal("\n[Source 1] Returns are accepted within 30 days.", added);
    }

    [Fact]
    public void TheJudge_IsGivenTheKnowledgeAndToldToFlagUnsupportedFacts()
    {
        // Arrange
        var request = new AIConversationEvaluationRequest
        {
            Turns = [new(AIConversationTurn.CustomerRole, "Can I return it?", DateTime.UtcNow)],
            Knowledge = "Returns are accepted within 30 days.",
        };

        // Act
        var message = AIConversationEvaluator.BuildRequest(request);

        // Assert
        Assert.Contains("Returns are accepted within 30 days.", message, StringComparison.Ordinal);
        Assert.Contains("Hallucination", message, StringComparison.Ordinal);
    }
}
