using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class RetentionCutoffCalculatorTests
{
    private static readonly DateTime _now = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TryComputeCutoff_WhenRetentionDisabled_ReturnsFalse()
    {
        // Arrange
        var options = new ContactCenterRetentionOptions { InteractionEventRetentionDays = 0 };

        // Act
        var enabled = RetentionCutoffCalculator.TryComputeCutoff(_now, options, out _);

        // Assert
        Assert.False(enabled);
    }

    [Fact]
    public void TryComputeCutoff_WhenOnlyRetentionSet_UsesRetentionWindow()
    {
        // Arrange
        var options = new ContactCenterRetentionOptions { InteractionEventRetentionDays = 30 };

        // Act
        var enabled = RetentionCutoffCalculator.TryComputeCutoff(_now, options, out var cutoff);

        // Assert
        Assert.True(enabled);
        Assert.Equal(_now.AddDays(-30), cutoff);
    }

    [Fact]
    public void TryComputeCutoff_WhenReplayHorizonExceedsRetention_KeepsEventsForTheHorizon()
    {
        // Arrange
        var options = new ContactCenterRetentionOptions
        {
            InteractionEventRetentionDays = 30,
            ProjectionReplayHorizonDays = 90,
        };

        // Act
        var enabled = RetentionCutoffCalculator.TryComputeCutoff(_now, options, out var cutoff);

        // Assert
        Assert.True(enabled);
        Assert.Equal(_now.AddDays(-90), cutoff);
    }

    [Fact]
    public void TryComputeCutoff_WhenLegalHoldExceedsRetention_KeepsEventsForTheHold()
    {
        // Arrange
        var options = new ContactCenterRetentionOptions
        {
            InteractionEventRetentionDays = 30,
            ProjectionReplayHorizonDays = 45,
            LegalHoldMinimumDays = 365,
        };

        // Act
        var enabled = RetentionCutoffCalculator.TryComputeCutoff(_now, options, out var cutoff);

        // Assert
        Assert.True(enabled);
        Assert.Equal(_now.AddDays(-365), cutoff);
    }

    [Fact]
    public void TryComputeCutoff_WhenFloorsBelowRetention_RetentionWindowWins()
    {
        // Arrange
        var options = new ContactCenterRetentionOptions
        {
            InteractionEventRetentionDays = 120,
            ProjectionReplayHorizonDays = 30,
            LegalHoldMinimumDays = 60,
        };

        // Act
        var enabled = RetentionCutoffCalculator.TryComputeCutoff(_now, options, out var cutoff);

        // Assert
        Assert.True(enabled);
        Assert.Equal(_now.AddDays(-120), cutoff);
    }

    [Fact]
    public void TryComputeCutoff_WhenFloorsNegative_AreIgnored()
    {
        // Arrange
        var options = new ContactCenterRetentionOptions
        {
            InteractionEventRetentionDays = 30,
            ProjectionReplayHorizonDays = -10,
            LegalHoldMinimumDays = -5,
        };

        // Act
        var enabled = RetentionCutoffCalculator.TryComputeCutoff(_now, options, out var cutoff);

        // Assert
        Assert.True(enabled);
        Assert.Equal(_now.AddDays(-30), cutoff);
    }
}
