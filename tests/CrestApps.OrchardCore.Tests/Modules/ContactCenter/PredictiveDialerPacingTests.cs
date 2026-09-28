using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Predictive dialing places more calls than there are agents, betting that most will not be answered. When the
/// bet is wrong a real person picks up and hears nobody, which is the abandoned call regulators cap and fine on.
/// The pacing calculation is therefore the safety mechanism, not an optimisation: it has to give ground the
/// moment the measured abandon rate approaches the configured ceiling.
/// </summary>
public sealed class PredictiveDialerPacingTests
{
    [Fact]
    public void Pace_IsAgentsTimesTheOverDialRatio_WhenAbandonmentIsWellUnderTheCap()
    {
        // Arrange
        var profile = Profile(callsPerAgent: 2, maxAbandonmentRatePercent: 3);

        // Act
        var pace = PredictiveDialerPacing.CalculatePace(profile, availableAgents: 4, measuredAbandonmentPercent: 0.2);

        // Assert
        Assert.Equal(8, pace);
    }

    [Fact]
    public void Pace_ThrottlesTowardOnePerAgent_AsAbandonmentApproachesTheCap()
    {
        // Arrange
        // Waiting for the cap to be breached before slowing down means the breach has already happened to real
        // callers, and a rolling rate falls far more slowly than it rises.
        var profile = Profile(callsPerAgent: 3, maxAbandonmentRatePercent: 4);

        // Act
        var relaxed = PredictiveDialerPacing.CalculatePace(profile, availableAgents: 4, measuredAbandonmentPercent: 0.5);
        var pressed = PredictiveDialerPacing.CalculatePace(profile, availableAgents: 4, measuredAbandonmentPercent: 3.5);

        // Assert
        Assert.True(pressed < relaxed);
    }

    [Fact]
    public void Pace_IsOnePerAgent_OnceTheCapIsReached()
    {
        // Arrange
        // At the cap the dialer must stop over-dialing entirely: one call per agent cannot abandon, because
        // there is somebody waiting for every call it places.
        var profile = Profile(callsPerAgent: 3, maxAbandonmentRatePercent: 3);

        // Act
        var pace = PredictiveDialerPacing.CalculatePace(profile, availableAgents: 5, measuredAbandonmentPercent: 3);

        // Assert
        Assert.Equal(5, pace);
    }

    [Fact]
    public void Pace_IsOnePerAgent_WhenTheCapIsExceeded()
    {
        // Arrange
        var profile = Profile(callsPerAgent: 3, maxAbandonmentRatePercent: 3);

        // Act
        var pace = PredictiveDialerPacing.CalculatePace(profile, availableAgents: 5, measuredAbandonmentPercent: 9);

        // Assert
        Assert.Equal(5, pace);
    }

    [Fact]
    public void Pace_IsNothing_WhenNoAgentIsAvailable()
    {
        // Arrange
        // Dialing with nobody to connect to guarantees an abandoned call for every answer.
        var profile = Profile(callsPerAgent: 3, maxAbandonmentRatePercent: 3);

        // Act
        var pace = PredictiveDialerPacing.CalculatePace(profile, availableAgents: 0, measuredAbandonmentPercent: 0);

        // Assert
        Assert.Equal(0, pace);
    }

    [Fact]
    public void Pace_NeverExceedsTheHardCeiling_HoweverGenerousTheProfile()
    {
        // Arrange
        // A profile configured with an absurd ratio is a misconfiguration, and honouring it would abandon calls
        // faster than any rolling measurement could notice.
        var profile = Profile(callsPerAgent: 50, maxAbandonmentRatePercent: 3);

        // Act
        var pace = PredictiveDialerPacing.CalculatePace(profile, availableAgents: 2, measuredAbandonmentPercent: 0);

        // Assert
        Assert.Equal(2 * PredictiveDialerPacing.MaxCallsPerAgent, pace);
    }

    [Fact]
    public void Pace_IsOnePerAgent_WhenTheProfileEnforcesNoCap()
    {
        // Arrange
        // A predictive profile with no abandonment cap has no safety mechanism at all, so it gets the one
        // pacing that cannot abandon rather than the benefit of the doubt.
        var profile = Profile(callsPerAgent: 3, maxAbandonmentRatePercent: 0);
        profile.EnforceAbandonmentCap = false;

        // Act
        var pace = PredictiveDialerPacing.CalculatePace(profile, availableAgents: 4, measuredAbandonmentPercent: 0);

        // Assert
        Assert.Equal(4, pace);
    }

    private static DialerProfile Profile(int callsPerAgent, double maxAbandonmentRatePercent)
        => new()
        {
            ItemId = "d1",
            Mode = DialerMode.Predictive,
            CallsPerAgent = callsPerAgent,
            EnforceAbandonmentCap = true,
            MaxAbandonmentRatePercent = maxAbandonmentRatePercent,
        };
}
