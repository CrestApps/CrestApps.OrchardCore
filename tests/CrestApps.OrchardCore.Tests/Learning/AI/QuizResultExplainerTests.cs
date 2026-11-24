using System.Text.Json;
using CrestApps.OrchardCore.Learning.AI.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;

namespace CrestApps.OrchardCore.Tests.Learning.AI;

/// <summary>
/// The explain-my-quiz-result tool tells the learner only what the quiz's review policy lets them see: never the answer
/// key of an attempt they may not review.
/// </summary>
public sealed class QuizResultExplainerTests
{
    private const string RightAnswerText = "Class C (electrical)";
    private const string KeyFeedback = "Water conducts electricity.";

    [Fact]
    public void ReviewAllowed_ExplainsTheQuestionsWithTheAnswersRightAnswersAndFeedback()
    {
        var explanation = QuizResultExplainer.Describe(Attempt(), canReview: true, canSeeScore: true);

        Assert.True(explanation.ReviewAvailable);
        Assert.Equal(1m, explanation.Score);
        var question = Assert.Single(explanation.Questions);
        Assert.Equal("Which extinguisher for an electrical fire?", question.Prompt);
        Assert.Equal("Water", question.YourAnswer);
        Assert.Equal(RightAnswerText, question.RightAnswer);
        Assert.Contains(KeyFeedback, question.Feedback, StringComparison.Ordinal);
        Assert.Equal("Well tried.", explanation.Feedback);
    }

    [Fact]
    public void ScoreOnly_GivesTheScoreAndNoQuestionAnswerKeyOrFeedback()
    {
        var explanation = QuizResultExplainer.Describe(Attempt(), canReview: false, canSeeScore: true);
        var json = JsonSerializer.Serialize(explanation);

        Assert.False(explanation.ReviewAvailable);
        Assert.Equal(1m, explanation.Score);
        Assert.Empty(explanation.Questions);
        Assert.Null(explanation.Feedback);
        Assert.DoesNotContain(RightAnswerText, json, StringComparison.Ordinal);
        Assert.DoesNotContain(KeyFeedback, json, StringComparison.Ordinal);
        Assert.False(string.IsNullOrEmpty(explanation.Message));
    }

    [Fact]
    public void Hidden_GivesNeitherTheScoreNorTheKey()
    {
        var explanation = QuizResultExplainer.Describe(Attempt(), canReview: false, canSeeScore: false);
        var json = JsonSerializer.Serialize(explanation);

        Assert.Null(explanation.Score);
        Assert.Null(explanation.Passed);
        Assert.Empty(explanation.Questions);
        Assert.DoesNotContain(RightAnswerText, json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AttemptStatus.NeedsGrading)]
    [InlineData(AttemptStatus.Submitted)]
    [InlineData(AttemptStatus.Voided)]
    public void AttemptsWithoutAResult_GiveNoScoreAndNoKey_WhateverThePolicy(AttemptStatus status)
    {
        var attempt = Attempt();
        attempt.Status = status;

        var explanation = QuizResultExplainer.Describe(attempt, canReview: true, canSeeScore: true);
        var json = JsonSerializer.Serialize(explanation);

        Assert.Null(explanation.Score);
        Assert.Empty(explanation.Questions);
        Assert.DoesNotContain(RightAnswerText, json, StringComparison.Ordinal);
    }

    private static AssessmentAttempt Attempt()
        => new()
        {
            ItemId = "a1",
            UserId = "learner",
            QuizTitle = "Fire safety quiz",
            Sequence = 1,
            Status = AttemptStatus.Graded,
            Score = 1,
            MaxScore = 2,
            Percent = 50,
            Passed = false,
            PassMark = 80,
            Feedback = "Well tried.",
            ReviewPolicy = ReviewPolicy.AfterGraded,
            Items =
            [
                new AttemptItem
                {
                    Index = 0,
                    QuestionType = "SingleChoice",
                    Title = "Electrical fires",
                    Prompt = "<p>Which extinguisher for an <strong>electrical</strong> fire?</p>".Replace("<strong>", string.Empty, StringComparison.Ordinal).Replace("</strong>", string.Empty, StringComparison.Ordinal),
                    Choices =
                    [
                        new AttemptChoice { Id = "w", Text = "Water", Feedback = KeyFeedback },
                        new AttemptChoice { Id = "c", Text = RightAnswerText },
                    ],
                    Key = new AttemptKey { ChoiceIds = ["c"] },
                    Points = 2,
                    Response = new AttemptResponse { ChoiceIds = ["w"] },
                    AutoScore = 0,
                },
            ],
        };
}
