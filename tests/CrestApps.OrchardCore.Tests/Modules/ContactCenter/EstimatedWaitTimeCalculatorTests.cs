using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// A caller who is told nothing has no way to decide whether to wait, so they hold and then abandon, which costs
/// the queue the call and the agent the context. The estimate does not have to be exact — it has to be honest
/// about the shape of the wait, and it must never claim a wait that cannot happen.
/// </summary>
public sealed class EstimatedWaitTimeCalculatorTests
{
    [Fact]
    public void Estimate_IsHandleTimeTimesPosition_DividedByAvailableAgents()
    {
        // Arrange
        // Third in line, four minutes a call, two agents working: roughly six minutes.
        var settings = new QueueTreatmentSettings();

        // Act
        var estimate = EstimatedWaitTimeCalculator.Estimate(
            position: 3,
            availableAgents: 2,
            averageHandleTime: TimeSpan.FromMinutes(4),
            settings);

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(6), estimate);
    }

    [Fact]
    public void Estimate_ForTheCallerAtTheHead_IsTheFloor_NotZero()
    {
        // Arrange
        // Telling the next caller in line "no wait" is a promise the queue cannot keep: the agent still has to
        // finish, and the caller hears the claim break in real time.
        var settings = new QueueTreatmentSettings { MinimumEstimateSeconds = 30 };

        // Act
        var estimate = EstimatedWaitTimeCalculator.Estimate(
            position: 1,
            availableAgents: 10,
            averageHandleTime: TimeSpan.FromSeconds(1),
            settings);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(30), estimate);
    }

    [Fact]
    public void Estimate_IsCapped_SoTheAnnouncementStaysCredible()
    {
        // Arrange
        // "Your estimated wait is four hours" is not information, it is an invitation to hang up angry. Past the
        // cap the queue should say something else entirely, which the cap is what lets the caller decide.
        var settings = new QueueTreatmentSettings { MaximumEstimateSeconds = 900 };

        // Act
        var estimate = EstimatedWaitTimeCalculator.Estimate(
            position: 50,
            availableAgents: 1,
            averageHandleTime: TimeSpan.FromMinutes(10),
            settings);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(900), estimate);
    }

    [Fact]
    public void Estimate_WithNobodyAvailable_IsUnknown_NotInfinite()
    {
        // Arrange
        // Dividing by zero agents produces a number that means nothing. The caller is better served by an
        // announcement that does not quote a time than by one that quotes an invented one.
        var settings = new QueueTreatmentSettings();

        // Act
        var estimate = EstimatedWaitTimeCalculator.Estimate(
            position: 3,
            availableAgents: 0,
            averageHandleTime: TimeSpan.FromMinutes(4),
            settings);

        // Assert
        Assert.Null(estimate);
    }

    [Fact]
    public void Estimate_WithNoHandleTimeHistory_IsUnknown()
    {
        // Arrange
        // A queue that has never handled a call has nothing to extrapolate from, and a made-up average would be
        // presented to the caller with exactly the same confidence as a real one.
        var settings = new QueueTreatmentSettings();

        // Act
        var estimate = EstimatedWaitTimeCalculator.Estimate(
            position: 3,
            availableAgents: 2,
            averageHandleTime: TimeSpan.Zero,
            settings);

        // Assert
        Assert.Null(estimate);
    }

    [Fact]
    public void Estimate_ForAPositionThatCannotExist_IsUnknown()
    {
        // Assert
        Assert.Null(EstimatedWaitTimeCalculator.Estimate(0, 2, TimeSpan.FromMinutes(4), new QueueTreatmentSettings()));
    }
}
