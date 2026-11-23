using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Scoring;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Tests.Surveys.Scoring;

/// <summary>
/// Production review P4-M2: one rule decides whether a percent passes a survey: the band it falls in when the band
/// passes or fails, otherwise the pass mark, and no pass decision when the survey has neither. A response's result uses
/// the same rule, so a combined quiz grade never disagrees with its attempts.
/// </summary>
public sealed class SurveyPassRuleTests
{
    [Theory]
    [InlineData(50, false)]
    [InlineData(69.99, false)]
    [InlineData(70, true)]
    [InlineData(95, true)]
    public void P4M2_BandsDecide_WhenThereIsNoPassMark(decimal percent, bool expected)
    {
        // Arrange
        var definition = Definition(passMark: null, Band("fail", 0, false), Band("pass", 70, true));

        // Act
        var passed = SurveyPassRule.Decide(definition, percent);

        // Assert
        Assert.Equal(expected, passed);
    }

    [Fact]
    public void P4M2_APassingBandBelowThePassMark_Passes()
    {
        // Arrange
        var definition = Definition(passMark: 70, Band("good", 60, true));

        // Act + Assert
        Assert.True(SurveyPassRule.Decide(definition, 65));
    }

    [Fact]
    public void P4M2_ABandThatFollowsThePassMark_LeavesItToThePassMark()
    {
        // Arrange
        var definition = Definition(passMark: 70, Band("info", 0, null));

        // Act + Assert
        Assert.False(SurveyPassRule.Decide(definition, 65));
        Assert.True(SurveyPassRule.Decide(definition, 75));
    }

    [Fact]
    public void P4M2_NoPassMarkAndNoDecidingBand_IsNoPassDecision()
    {
        // Arrange
        var definition = Definition(passMark: null);

        // Act + Assert
        Assert.Null(SurveyPassRule.Decide(definition, 100));
        Assert.Null(SurveyPassRule.Decide(definition, null));
    }

    [Fact]
    public void P4M2_TheScoringEngine_SetsAResponsesPass_WithTheSameRule()
    {
        // Arrange
        var definition = Definition(passMark: null, Band("fail", 0, false), Band("pass", 70, true));
        var response = new SurveyResponse { Grading = GradingStatus.Graded, Percent = 50 };

        // Act
        SurveyScoringEngine.ApplyResult(definition, response);

        // Assert
        Assert.Equal(SurveyPassRule.Decide(definition, 50), response.Passed);
        Assert.False(response.Passed);
        Assert.Equal("fail", response.ScoreBandId);
    }

    private static ScoreBand Band(string id, decimal min, bool? passing)
        => new() { Id = id, MinPercent = min, Label = id, Passing = passing };

    private static SurveyDefinition Definition(decimal? passMark, params ScoreBand[] bands)
    {
        var definition = new SurveyDefinition { ScoreBands = [.. bands] };
        definition.Scoring.Enabled = true;
        definition.Scoring.PassMarkPercent = passMark;

        return definition;
    }
}
