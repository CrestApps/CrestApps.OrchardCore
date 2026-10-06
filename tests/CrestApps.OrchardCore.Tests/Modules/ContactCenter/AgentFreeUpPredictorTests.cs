using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Over-dialing may count busy agents who are about to free up. Counting one who does not free up in time abandons the
/// call they were counted for, so the prediction has to err toward fewer agents: no credit without a measured average,
/// and none for an agent whose call or wrap-up has already run longer than usual.
/// </summary>
public sealed class AgentFreeUpPredictorTests
{
    private static readonly TimeSpan _talk = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan _wrapUp = TimeSpan.FromSeconds(30);

    [Fact]
    public void PredictTimeToFree_ForAWrappingAgent_IsTheRestOfTheAverageWrapUp()
    {
        // Act
        var timeToFree = AgentFreeUpPredictor.PredictTimeToFree(Agent(AgentWorkPhase.WrappingUp, seconds: 20), _talk, _wrapUp);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(10), timeToFree);
    }

    [Fact]
    public void PredictTimeToFree_ForATalkingAgent_IsTheRestOfTheAverageCallPlusAWholeWrapUp()
    {
        // Act
        var timeToFree = AgentFreeUpPredictor.PredictTimeToFree(Agent(AgentWorkPhase.Talking, seconds: 100), _talk, _wrapUp);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(50), timeToFree);
    }

    [Theory]
    [InlineData(AgentWorkPhase.Talking, 120)]
    [InlineData(AgentWorkPhase.Talking, 600)]
    [InlineData(AgentWorkPhase.WrappingUp, 30)]
    [InlineData(AgentWorkPhase.WrappingUp, 90)]
    public void PredictTimeToFree_ForAnAgentPastTheAverage_MakesNoPrediction(AgentWorkPhase phase, int seconds)
    {
        // Act
        var timeToFree = AgentFreeUpPredictor.PredictTimeToFree(Agent(phase, seconds), _talk, _wrapUp);

        // Assert
        Assert.Null(timeToFree);
    }

    [Fact]
    public void PredictTimeToFree_WithoutAMeasuredWrapUp_MakesNoPredictionForAnyPhase()
    {
        // Act
        var talking = AgentFreeUpPredictor.PredictTimeToFree(Agent(AgentWorkPhase.Talking, seconds: 10), _talk, averageWrapUpTime: null);
        var wrapping = AgentFreeUpPredictor.PredictTimeToFree(Agent(AgentWorkPhase.WrappingUp, seconds: 1), _talk, averageWrapUpTime: null);

        // Assert
        Assert.Null(talking);
        Assert.Null(wrapping);
    }

    [Fact]
    public void PredictTimeToFree_WithoutAMeasuredTalkTime_StillPredictsWrappingAgents()
    {
        // Act
        var talking = AgentFreeUpPredictor.PredictTimeToFree(Agent(AgentWorkPhase.Talking, seconds: 10), averageTalkTime: null, _wrapUp);
        var wrapping = AgentFreeUpPredictor.PredictTimeToFree(Agent(AgentWorkPhase.WrappingUp, seconds: 10), averageTalkTime: null, _wrapUp);

        // Assert
        Assert.Null(talking);
        Assert.Equal(TimeSpan.FromSeconds(20), wrapping);
    }

    [Fact]
    public void PredictTimeToFree_WithANegativeElapsedTime_TreatsItAsJustStarted()
    {
        // Act
        var timeToFree = AgentFreeUpPredictor.PredictTimeToFree(Agent(AgentWorkPhase.WrappingUp, seconds: -5), _talk, _wrapUp);

        // Assert
        Assert.Equal(_wrapUp, timeToFree);
    }

    [Fact]
    public void CountFreeingWithin_CountsOnlyAgentsPredictedToFreeWithinTheHorizon()
    {
        // Arrange
        AgentWorkSnapshot[] agents =
        [
            Agent(AgentWorkPhase.WrappingUp, seconds: 25), // 5 s
            Agent(AgentWorkPhase.WrappingUp, seconds: 10), // 20 s
            Agent(AgentWorkPhase.Talking, seconds: 115), // 35 s
            Agent(AgentWorkPhase.Talking, seconds: 30), // 120 s
            Agent(AgentWorkPhase.Talking, seconds: 400), // past the average
            null,
        ];

        // Act
        var withinTwelve = AgentFreeUpPredictor.CountFreeingWithin(agents, _talk, _wrapUp, TimeSpan.FromSeconds(12));
        var withinTwenty = AgentFreeUpPredictor.CountFreeingWithin(agents, _talk, _wrapUp, TimeSpan.FromSeconds(20));
        var withinAMinute = AgentFreeUpPredictor.CountFreeingWithin(agents, _talk, _wrapUp, TimeSpan.FromMinutes(1));
        var withinAnHour = AgentFreeUpPredictor.CountFreeingWithin(agents, _talk, _wrapUp, TimeSpan.FromHours(1));

        // Assert
        Assert.Equal(1, withinTwelve);
        Assert.Equal(2, withinTwenty);
        Assert.Equal(3, withinAMinute);
        Assert.Equal(4, withinAnHour);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CountFreeingWithin_WithoutAHorizon_CountsNobody(int seconds)
    {
        // Arrange
        AgentWorkSnapshot[] agents = [Agent(AgentWorkPhase.WrappingUp, seconds: 29)];

        // Act
        var count = AgentFreeUpPredictor.CountFreeingWithin(agents, _talk, _wrapUp, TimeSpan.FromSeconds(seconds));

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public void CountFreeingWithin_WithoutMeasuredAverages_CountsNobody()
    {
        // Arrange
        AgentWorkSnapshot[] agents =
        [
            Agent(AgentWorkPhase.WrappingUp, seconds: 29),
            Agent(AgentWorkPhase.Talking, seconds: 119),
        ];

        // Act
        var count = AgentFreeUpPredictor.CountFreeingWithin(agents, averageTalkTime: null, averageWrapUpTime: null, TimeSpan.FromHours(1));

        // Assert
        Assert.Equal(0, count);
    }

    private static AgentWorkSnapshot Agent(AgentWorkPhase phase, int seconds)
        => new()
        {
            AgentId = Guid.NewGuid().ToString("N"),
            Phase = phase,
            Elapsed = TimeSpan.FromSeconds(seconds),
        };
}
