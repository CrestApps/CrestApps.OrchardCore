using CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Verifies the hysteresis that governs whether a node reports itself able to serve.
/// </summary>
/// <remarks>
/// Hysteresis is the entire point of this type. Draining on a single failed probe converts routine transient
/// failures into lost capacity, and returning on a single success lets a genuinely broken node flap back into
/// rotation. These tests pin both thresholds and the reset behaviour that makes them meaningful.
/// </remarks>
public sealed class NodeServingStateTrackerTests
{
    [Fact]
    public void Node_StartsAbleToServe()
    {
        // A node must not be held out of rotation by a probe that has not run yet.
        var tracker = new NodeServingStateTracker(3, 2);

        Assert.True(tracker.IsServing);
    }

    [Fact]
    public void Node_KeepsServing_UntilTheFailureThresholdIsReached()
    {
        var tracker = new NodeServingStateTracker(3, 2);

        Assert.True(tracker.Record(false));
        Assert.True(tracker.Record(false));
        Assert.False(tracker.Record(false));
    }

    [Fact]
    public void ASingleSuccess_ResetsTheFailureRun()
    {
        // This is the transient-failure regression: two failures, a success, then two more failures must not
        // drain the node, because they are not consecutive.
        var tracker = new NodeServingStateTracker(3, 2);

        tracker.Record(false);
        tracker.Record(false);
        tracker.Record(true);
        tracker.Record(false);

        Assert.True(tracker.Record(false));
        Assert.True(tracker.IsServing);
    }

    [Fact]
    public void ADrainingNode_DoesNotReturnOnASingleSuccess()
    {
        var tracker = new NodeServingStateTracker(2, 3);

        tracker.Record(false);
        tracker.Record(false);

        Assert.False(tracker.IsServing);
        Assert.False(tracker.Record(true));
        Assert.False(tracker.Record(true));
        Assert.True(tracker.Record(true));
    }

    [Fact]
    public void ASingleFailure_ResetsTheRecoveryRun()
    {
        var tracker = new NodeServingStateTracker(2, 3);

        tracker.Record(false);
        tracker.Record(false);
        tracker.Record(true);
        tracker.Record(true);
        tracker.Record(false);
        tracker.Record(true);

        Assert.False(tracker.Record(true));
        Assert.False(tracker.IsServing);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Thresholds_AreRaisedToAtLeastOne(int configured)
    {
        // A zero threshold would otherwise drain the node before any probe had run.
        var tracker = new NodeServingStateTracker(configured, configured);

        Assert.True(tracker.IsServing);
        Assert.False(tracker.Record(false));
        Assert.True(tracker.Record(true));
    }

    [Fact]
    public async Task ConcurrentOutcomes_DoNotCorruptTheState()
    {
        // Readiness is probed concurrently, so the transitions must be atomic.
        var tracker = new NodeServingStateTracker(1_000_000, 1);

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 10_000),
            TestContext.Current.CancellationToken,
            (_, _) =>
            {
                tracker.Record(false);

                return ValueTask.CompletedTask;
            });

        Assert.True(tracker.IsServing);
        Assert.True(tracker.Record(true));
    }
}
