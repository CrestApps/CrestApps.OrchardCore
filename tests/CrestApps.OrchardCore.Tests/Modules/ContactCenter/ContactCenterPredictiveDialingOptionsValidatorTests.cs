using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// A wrong predictive dialing timing does not degrade gracefully: a pacing lock that expires mid-cycle lets two nodes pace
/// one campaign, and a connect wait as long as the abandonment threshold abandons every call it waits on. The validator
/// refuses such a configuration at startup and names the key.
/// </summary>
public sealed class ContactCenterPredictiveDialingOptionsValidatorTests
{
    [Fact]
    public void Validate_TheDefaults_Succeeds()
    {
        // Act
        var result = new ContactCenterPredictiveDialingOptionsValidator().Validate(null, new ContactCenterPredictiveDialingOptions());

        // Assert
        Assert.True(result.Succeeded);
    }

    public static TheoryData<string, string> InvalidCases => new()
    {
        { "interval zero", nameof(ContactCenterPredictiveDialingOptions.PacingInterval) },
        { "debounce zero", nameof(ContactCenterPredictiveDialingOptions.PacingDebounce) },
        { "debounce not shorter than interval", nameof(ContactCenterPredictiveDialingOptions.PacingDebounce) },
        { "lock not longer than interval", nameof(ContactCenterPredictiveDialingOptions.PacingLockExpiration) },
        { "connect lock wait as long as the threshold", nameof(ContactCenterPredictiveDialingOptions.ConnectLockWait) },
        { "connect lock wait zero", nameof(ContactCenterPredictiveDialingOptions.ConnectLockWait) },
        { "sweep within the threshold", nameof(ContactCenterPredictiveDialingOptions.AnsweredUnconnectedSweepAfter) },
        { "ring horizon zero", nameof(ContactCenterPredictiveDialingOptions.DefaultRingHorizon) },
        { "cache zero", nameof(ContactCenterPredictiveDialingOptions.StatisticsCacheDuration) },
        { "no dials per cycle", nameof(ContactCenterPredictiveDialingOptions.MaxDialsPerCycle) },
        { "too many dials per cycle", nameof(ContactCenterPredictiveDialingOptions.MaxDialsPerCycle) },
        { "no compliance window", nameof(ContactCenterPredictiveDialingOptions.ComplianceWindowDays) },
        { "compliance window too long", nameof(ContactCenterPredictiveDialingOptions.ComplianceWindowDays) },
        { "no timing samples", nameof(ContactCenterPredictiveDialingOptions.MaxTimingSamples) },
        { "agent leg timeout too short", nameof(ContactCenterPredictiveDialingOptions.AgentLegAnswerTimeout) },
        { "agent leg timeout too long", nameof(ContactCenterPredictiveDialingOptions.AgentLegAnswerTimeout) },
        { "lock retry zero", nameof(ContactCenterPredictiveDialingOptions.PacingLockRetryDelay) },
        { "lock retry not shorter than the lock", nameof(ContactCenterPredictiveDialingOptions.PacingLockRetryDelay) },
    };

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public void Validate_AnUnsafeValue_FailsNamingTheKey(string scenario, string key)
    {
        // Arrange
        var options = new ContactCenterPredictiveDialingOptions();

        switch (scenario)
        {
            case "interval zero": options.PacingInterval = TimeSpan.Zero; break;
            case "debounce zero": options.PacingDebounce = TimeSpan.Zero; break;
            case "debounce not shorter than interval": options.PacingDebounce = options.PacingInterval; break;
            case "lock not longer than interval": options.PacingLockExpiration = options.PacingInterval; break;
            case "connect lock wait as long as the threshold": options.ConnectLockWait = DialerAbandonment.ConnectThreshold; break;
            case "connect lock wait zero": options.ConnectLockWait = TimeSpan.Zero; break;
            case "sweep within the threshold": options.AnsweredUnconnectedSweepAfter = DialerAbandonment.ConnectThreshold; break;
            case "ring horizon zero": options.DefaultRingHorizon = TimeSpan.Zero; break;
            case "cache zero": options.StatisticsCacheDuration = TimeSpan.Zero; break;
            case "no dials per cycle": options.MaxDialsPerCycle = 0; break;
            case "too many dials per cycle": options.MaxDialsPerCycle = ContactCenterPredictiveDialingOptionsValidator.MaxDialsPerCycleLimit + 1; break;
            case "no compliance window": options.ComplianceWindowDays = 0; break;
            case "compliance window too long": options.ComplianceWindowDays = ContactCenterPredictiveDialingOptionsValidator.MaxComplianceWindowDays + 1; break;
            case "no timing samples": options.MaxTimingSamples = 0; break;
            case "agent leg timeout too short": options.AgentLegAnswerTimeout = ContactCenterPredictiveDialingOptionsValidator.MinAgentLegAnswerTimeout - TimeSpan.FromMilliseconds(1); break;
            case "agent leg timeout too long": options.AgentLegAnswerTimeout = ContactCenterPredictiveDialingOptionsValidator.MaxAgentLegAnswerTimeout + TimeSpan.FromSeconds(1); break;
            case "lock retry zero": options.PacingLockRetryDelay = TimeSpan.Zero; break;
            case "lock retry not shorter than the lock": options.PacingLockRetryDelay = options.PacingLockExpiration; break;
            default: throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }

        // Act
        var result = new ContactCenterPredictiveDialingOptionsValidator().Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains($"{ContactCenterPredictiveDialingOptions.SectionName}:{key}", StringComparison.Ordinal));
    }
}
