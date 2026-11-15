using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

public sealed class ItemAnalysisCalculatorTests
{
    [Fact]
    public void Difficulty_IsTheMeanShareOfPoints_AndDiscrimination_ComparesTheTopAndBottomAttempts()
    {
        // Arrange: q1 only the strong learners get right (discriminates); q2 everybody gets right (easy, no discrimination).
        var attempts = new List<AssessmentAttempt>();

        for (var i = 0; i < 10; i++)
        {
            var strong = i < 5;
            attempts.Add(new AssessmentAttempt
            {
                ItemId = "a" + i,
                QuizId = "quiz",
                Status = AttemptStatus.Graded,
                Percent = strong ? 100 - i : 40 - i,
                Items =
                [
                    new AttemptItem { QuestionId = "q1", Title = "Hard", QuestionType = "SingleChoice", Points = 1, AutoScore = strong ? 1 : 0 },
                    new AttemptItem { QuestionId = "q2", Title = "Easy", QuestionType = "SingleChoice", Points = 2, AutoScore = 2 },
                ],
            });
        }

        attempts.Add(new AssessmentAttempt { ItemId = "void", QuizId = "quiz", Status = AttemptStatus.Voided, Percent = 0, Items = [new AttemptItem { QuestionId = "q1", Points = 1, AutoScore = 0 }] });

        // Act
        var analysis = ItemAnalysisCalculator.Analyze(attempts).ToDictionary(item => item.QuestionId);

        // Assert
        Assert.Equal(10, analysis["q1"].Answers);
        Assert.Equal(0.5, analysis["q1"].Difficulty);
        Assert.Equal(1.0, analysis["q1"].Discrimination);
        Assert.Equal(1.0, analysis["q2"].Difficulty);
        Assert.Equal(0.0, analysis["q2"].Discrimination);
    }

    [Fact]
    public void OneAttempt_HasNoDiscrimination()
    {
        var analysis = ItemAnalysisCalculator.Analyze(
        [
            new AssessmentAttempt { ItemId = "a1", QuizId = "quiz", Status = AttemptStatus.Graded, Percent = 50, Items = [new AttemptItem { QuestionId = "q1", Points = 1, AutoScore = 1 }] },
        ]);

        Assert.Null(Assert.Single(analysis).Discrimination);
    }
}
