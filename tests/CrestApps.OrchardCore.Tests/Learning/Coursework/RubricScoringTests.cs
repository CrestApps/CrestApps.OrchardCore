using CrestApps.OrchardCore.Learning.Coursework.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Coursework;

/// <summary>
/// Learning plan Phase 7: a rubric's total is the sum of the chosen level of each criterion, out of the sum of each
/// criterion's best level.
/// </summary>
public sealed class RubricScoringTests
{
    private static readonly RubricPart _rubric = new()
    {
        Criteria =
        [
            new RubricCriterion
            {
                Id = "argument",
                Levels =
                [
                    new RubricLevel { Id = "weak", Points = 1 },
                    new RubricLevel { Id = "strong", Points = 4 },
                ],
            },
            new RubricCriterion
            {
                Id = "sources",
                Levels =
                [
                    new RubricLevel { Id = "none", Points = 0 },
                    new RubricLevel { Id = "some", Points = 3 },
                    new RubricLevel { Id = "many", Points = 6 },
                ],
            },
        ],
    };

    [Fact]
    public void Score_SumsTheChosenLevels_OutOfTheBestLevelOfEachCriterion()
    {
        // Act
        var score = RubricScoring.Score(_rubric, new Dictionary<string, string> { ["argument"] = "strong", ["sources"] = "some" });

        // Assert
        Assert.True(score.Complete);
        Assert.Equal(7m, score.Points);
        Assert.Equal(10m, score.MaxPoints);
        Assert.Equal(70m, score.Percent);
    }

    [Fact]
    public void Score_RoundsThePercentageToTwoDecimals()
    {
        // Arrange
        var rubric = new RubricPart
        {
            Criteria = [new RubricCriterion { Id = "c", Levels = [new RubricLevel { Id = "a", Points = 1 }, new RubricLevel { Id = "b", Points = 3 }] }],
        };

        // Act
        var score = RubricScoring.Score(rubric, new Dictionary<string, string> { ["c"] = "a" });

        // Assert
        Assert.Equal(33.33m, score.Percent);
    }

    [Fact]
    public void Score_ACriterionWithoutAChoice_IsNotComplete()
    {
        // Act
        var score = RubricScoring.Score(_rubric, new Dictionary<string, string> { ["argument"] = "strong" });

        // Assert
        Assert.False(score.Complete);
        Assert.Equal(4m, score.Points);
        Assert.Equal(10m, score.MaxPoints);
    }

    [Fact]
    public void Score_ALevelOfAnotherCriterion_DoesNotCount()
    {
        // Act
        var score = RubricScoring.Score(_rubric, new Dictionary<string, string> { ["argument"] = "many", ["sources"] = "some" });

        // Assert
        Assert.False(score.Complete);
        Assert.Equal(3m, score.Points);
    }

    [Fact]
    public void MaxPoints_IsTheSumOfEachCriterionsBestLevel()
    {
        // Act
        var max = RubricScoring.MaxPoints(_rubric);

        // Assert
        Assert.Equal(10m, max);
    }

    [Fact]
    public void Score_ARubricWorthNoPoints_IsZeroPercent()
    {
        // Arrange
        var rubric = new RubricPart { Criteria = [new RubricCriterion { Id = "c", Levels = [new RubricLevel { Id = "a", Points = 0 }] }] };

        // Act
        var score = RubricScoring.Score(rubric, new Dictionary<string, string> { ["c"] = "a" });

        // Assert
        Assert.True(score.Complete);
        Assert.Equal(0m, score.Percent);
    }
}
