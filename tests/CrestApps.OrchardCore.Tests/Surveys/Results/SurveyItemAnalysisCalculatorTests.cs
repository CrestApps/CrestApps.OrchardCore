using CrestApps.OrchardCore.Surveys.Core.Results;

namespace CrestApps.OrchardCore.Tests.Surveys.Results;

public sealed class SurveyItemAnalysisCalculatorTests
{
    [Fact]
    public void Analyze_Difficulty_IsTheMeanShareOfPoints_AndDiscrimination_ComparesTheTopAndBottomResponses()
    {
        // Arrange: q1 only the strong respondents get right (discriminates); q2 everybody gets right (easy, no discrimination).
        var percents = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var scores = new List<SurveyQuestionScore>();

        for (var i = 0; i < 10; i++)
        {
            var strong = i < 5;
            var id = "r" + i;
            percents[id] = strong ? 100 - i : 40 - i;
            scores.Add(new SurveyQuestionScore(id, "q1", strong ? 1 : 0, 1));
            scores.Add(new SurveyQuestionScore(id, "q2", 2, 2));
        }

        // A score of a response outside the scored set is left out.
        scores.Add(new SurveyQuestionScore("voided", "q1", 0, 1));

        // Act
        var analysis = SurveyItemAnalysisCalculator.Analyze(percents, scores).ToDictionary(item => item.QuestionId);

        // Assert
        Assert.Equal(10, analysis["q1"].Answers);
        Assert.Equal(0.5, analysis["q1"].Difficulty);
        Assert.Equal(1.0, analysis["q1"].Discrimination);
        Assert.Equal(1.0, analysis["q2"].Difficulty);
        Assert.Equal(0.0, analysis["q2"].Discrimination);
    }

    [Fact]
    public void Analyze_OneResponse_HasNoDiscrimination()
    {
        // Act
        var analysis = SurveyItemAnalysisCalculator.Analyze(
            new Dictionary<string, decimal> { ["r1"] = 50 },
            [new SurveyQuestionScore("r1", "q1", 1, 1)]);

        // Assert
        Assert.Null(Assert.Single(analysis).Discrimination);
    }

    [Fact]
    public void Analyze_QuestionWithoutPoints_IsLeftOut()
    {
        // Act
        var analysis = SurveyItemAnalysisCalculator.Analyze(
            new Dictionary<string, decimal> { ["r1"] = 50, ["r2"] = 70 },
            [new SurveyQuestionScore("r1", "q1", 0, 0), new SurveyQuestionScore("r2", "q1", 0, 0)]);

        // Assert
        Assert.Empty(analysis);
    }
}
