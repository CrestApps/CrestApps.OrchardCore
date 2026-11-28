using CrestApps.OrchardCore.Learning.Assessments.Drivers;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

/// <summary>
/// Review round 7, R7-Mi23: the quiz editor warns when some score has no pass decision, not only when no band decides at
/// all: with no pass mark, a score that falls in no band, or in a band that neither passes nor fails, completes the quiz
/// with no pass or fail. The check agrees with <see cref="SurveyPassRule"/> for every score.
/// </summary>
public sealed class QuizPassDecisionWarningTests
{
    [Fact]
    public void R7Mi23_APassMark_DecidesEveryScore()
    {
        // Arrange
        var definition = Definition(passMark: 50, (90, null));

        // Assert
        Assert.True(QuizPartDisplayDriver.DecidesPass(definition));
    }

    [Fact]
    public void R7Mi23_APartialBand_WithoutAPassMark_LeavesScoresWithNoDecision()
    {
        // Arrange: "Distinction >= 90, Passing" and "Merit >= 70" (Passing unset); 75 and 40 have no decision.
        var definition = Definition(passMark: null, (90, true), (70, null));

        // Assert
        Assert.False(QuizPartDisplayDriver.DecidesPass(definition));
        Assert.Null(SurveyPassRule.Decide(definition, 75));
        Assert.Null(SurveyPassRule.Decide(definition, 40));
    }

    [Fact]
    public void R7Mi23_BandsWithAGapBelowTheLowest_LeaveScoresWithNoDecision()
    {
        // Arrange: every band decides, but nothing covers a score under 50.
        var definition = Definition(passMark: null, (80, true), (50, false));

        // Assert
        Assert.False(QuizPartDisplayDriver.DecidesPass(definition));
        Assert.Null(SurveyPassRule.Decide(definition, 30));
    }

    [Fact]
    public void R7Mi23_BandsThatCoverEveryScore_AndAllDecide_DecideEveryScore()
    {
        // Arrange
        var definition = Definition(passMark: null, (60, true), (0, false));

        // Assert
        Assert.True(QuizPartDisplayDriver.DecidesPass(definition));
    }

    [Fact]
    public void R7Mi23_NoPassMarkAndNoBand_DecidesNothing()
        => Assert.False(QuizPartDisplayDriver.DecidesPass(Definition(passMark: null)));

    private static SurveyDefinition Definition(decimal? passMark, params (decimal Min, bool? Passing)[] bands)
        => new()
        {
            Scoring = new SurveyScoringSettings { Enabled = true, PassMarkPercent = passMark },
            ScoreBands = bands.Select(band => new ScoreBand { Id = "b" + band.Min, MinPercent = band.Min, Passing = band.Passing }).ToList(),
        };
}
