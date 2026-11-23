using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Tests.Surveys.Results;

/// <summary>
/// The one small-group rule of anonymous surveys every output shares, and the label staff see for a response.
/// </summary>
public sealed class SurveyGroupGateTests
{
    [Fact]
    public void Decide_BelowTheMinimumOfCompletedResponses_HidesAll()
    {
        // Act
        var decision = SurveyGroupGate.Decide(5, [], Counts(completed: 4, inProgress: 20));

        // Assert
        Assert.True(decision.HidesAll);
        Assert.Equal(5, decision.MinimumGroupSize);
    }

    [Fact]
    public void Decide_EveryStatus_LeavesOutEachStatusTooFewShare()
    {
        // Act
        var decision = SurveyGroupGate.Decide(5, [], Counts(completed: 5, inProgress: 5, abandoned: 1));

        // Assert
        Assert.False(decision.HidesAll);
        Assert.Equal([SurveyResponseStatus.Abandoned], decision.HiddenStatuses);
        Assert.True(decision.Shows(SurveyResponseStatus.InProgress, isTest: false));
        Assert.False(decision.Shows(SurveyResponseStatus.Abandoned, isTest: false));
        Assert.True(decision.Shows(SurveyResponseStatus.Abandoned, isTest: true));
    }

    [Fact]
    public void Decide_FilteredToAStatusTooFewShare_HidesAll()
    {
        // Act
        var decision = SurveyGroupGate.Decide(5, [SurveyResponseStatus.Abandoned], Counts(completed: 9, abandoned: 1));

        // Assert
        Assert.True(decision.HidesAll);
    }

    [Fact]
    public void Decide_FinishedWithFewScreenedOut_KeepsTheCompletedRows()
    {
        // Act
        var decision = SurveyGroupGate.Decide(5, [SurveyResponseStatus.Completed, SurveyResponseStatus.ScreenedOut], Counts(completed: 6, screenedOut: 2));

        // Assert
        Assert.False(decision.HidesAll);
        Assert.Equal([SurveyResponseStatus.ScreenedOut], decision.HiddenStatuses);
    }

    [Fact]
    public void Decide_WithoutAMinimum_IsOpen()
    {
        // Act
        var decision = SurveyGroupGate.Decide(0, [], Counts(completed: 1, abandoned: 1));

        // Assert
        Assert.Same(SurveyGroupDecision.Open, decision);
    }

    [Fact]
    public async Task DecideAsync_OnAnIdentifiedSurvey_CountsNothing()
    {
        // Arrange
        var survey = new Survey { Access = new SurveyAccessSettings { Anonymity = AnonymityMode.Identified } };

        // Act
        var decision = await SurveyGroupGate.DecideAsync(survey, null, [], _ => throw new InvalidOperationException("Counted."));

        // Assert
        Assert.False(decision.HidesAll);
    }

    [Fact]
    public void ResponseLabel_OnAnAnonymousSurvey_IsTheStartOfTheRandomId_NotTheSiteWideNumber()
    {
        // Arrange
        var anonymous = new Survey { Access = new SurveyAccessSettings { Anonymity = AnonymityMode.Anonymous } };
        var identified = new Survey { Access = new SurveyAccessSettings { Anonymity = AnonymityMode.Identified } };
        var response = new SurveyResponse { ItemId = "4k7x2m9qabcdefghjkmnpqrstv", Number = "R0004212" };

        // Act and assert
        Assert.Equal("4k7x2m9qab", SurveyAnonymity.ResponseLabel(anonymous, response));
        Assert.Equal("4k7x2m9qab", SurveyAnonymity.ResponseLabel(null, response));
        Assert.Equal("R0004212", SurveyAnonymity.ResponseLabel(identified, response));
    }

    private static Dictionary<SurveyResponseStatus, long> Counts(long completed = 0, long inProgress = 0, long abandoned = 0, long screenedOut = 0)
        => new()
        {
            [SurveyResponseStatus.Completed] = completed,
            [SurveyResponseStatus.InProgress] = inProgress,
            [SurveyResponseStatus.Abandoned] = abandoned,
            [SurveyResponseStatus.ScreenedOut] = screenedOut,
        };
}
