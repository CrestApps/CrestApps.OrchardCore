using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Validates the predictive dialing timings and limits. A wrong value here does not degrade gracefully: a lock that
/// expires before the work it guards lets two nodes pace the same campaign, and a connect wait as long as the abandonment
/// threshold abandons every call it waits on. Failing at startup with the offending key named is how an operator finds
/// that out, rather than a regulated campaign over-dialing.
/// </summary>
public sealed class ContactCenterPredictiveDialingOptionsValidator : IValidateOptions<ContactCenterPredictiveDialingOptions>
{
    private const string Section = ContactCenterPredictiveDialingOptions.SectionName;

    /// <summary>
    /// The most dials one pacing cycle may be configured to place.
    /// </summary>
    public const int MaxDialsPerCycleLimit = 500;

    /// <summary>
    /// The most days the long-run abandonment rate may be measured over.
    /// </summary>
    public const int MaxComplianceWindowDays = 90;

    /// <summary>
    /// The shortest time an agent's leg may be given to answer.
    /// </summary>
    public static readonly TimeSpan MinAgentLegAnswerTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The longest time an agent's leg may be given to answer.
    /// </summary>
    public static readonly TimeSpan MaxAgentLegAnswerTimeout = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string name, ContactCenterPredictiveDialingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        RequirePositive(failures, options.PacingInterval, nameof(options.PacingInterval));
        RequirePositive(failures, options.PacingDebounce, nameof(options.PacingDebounce));
        RequirePositive(failures, options.PacingLockExpiration, nameof(options.PacingLockExpiration));
        RequirePositive(failures, options.ConnectLockWait, nameof(options.ConnectLockWait));
        RequirePositive(failures, options.DefaultRingHorizon, nameof(options.DefaultRingHorizon));
        RequirePositive(failures, options.AnsweredUnconnectedSweepAfter, nameof(options.AnsweredUnconnectedSweepAfter));
        RequirePositive(failures, options.StatisticsCacheDuration, nameof(options.StatisticsCacheDuration));

        if (options.PacingDebounce >= options.PacingInterval)
        {
            failures.Add($"'{Section}:{nameof(options.PacingDebounce)}' must be shorter than '{nameof(options.PacingInterval)}', otherwise a campaign is never paced as often as configured.");
        }

        if (options.PacingLockExpiration <= options.PacingInterval)
        {
            failures.Add($"'{Section}:{nameof(options.PacingLockExpiration)}' must exceed '{nameof(options.PacingInterval)}', otherwise the lock expires while a cycle is still placing calls and a second node paces the same campaign.");
        }

        if (options.ConnectLockWait >= DialerAbandonment.ConnectThreshold)
        {
            failures.Add($"'{Section}:{nameof(options.ConnectLockWait)}' must be shorter than {DialerAbandonment.ConnectThreshold.TotalSeconds:0} seconds, otherwise waiting for one agent abandons the call.");
        }

        if (options.AnsweredUnconnectedSweepAfter <= DialerAbandonment.ConnectThreshold)
        {
            failures.Add($"'{Section}:{nameof(options.AnsweredUnconnectedSweepAfter)}' must exceed {DialerAbandonment.ConnectThreshold.TotalSeconds:0} seconds, otherwise calls still being connected are swept up as abandoned.");
        }

        if (options.AgentLegAnswerTimeout < MinAgentLegAnswerTimeout || options.AgentLegAnswerTimeout > MaxAgentLegAnswerTimeout)
        {
            failures.Add($"'{Section}:{nameof(options.AgentLegAnswerTimeout)}' must be between {MinAgentLegAnswerTimeout.TotalSeconds:0} and {MaxAgentLegAnswerTimeout.TotalSeconds:0} seconds.");
        }

        if (options.PacingLockRetryDelay <= TimeSpan.Zero || options.PacingLockRetryDelay >= options.PacingLockExpiration)
        {
            failures.Add($"'{Section}:{nameof(options.PacingLockRetryDelay)}' must be greater than zero and shorter than '{nameof(options.PacingLockExpiration)}'.");
        }

        if (options.MaxDialsPerCycle is < 1 or > MaxDialsPerCycleLimit)
        {
            failures.Add($"'{Section}:{nameof(options.MaxDialsPerCycle)}' must be between 1 and {MaxDialsPerCycleLimit}.");
        }

        if (options.ComplianceWindowDays is < 1 or > MaxComplianceWindowDays)
        {
            failures.Add($"'{Section}:{nameof(options.ComplianceWindowDays)}' must be between 1 and {MaxComplianceWindowDays} days.");
        }

        if (options.MaxTimingSamples < 1)
        {
            failures.Add($"'{Section}:{nameof(options.MaxTimingSamples)}' must be greater than zero, otherwise no answer rate is ever measured and over-dialing never starts.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void RequirePositive(List<string> failures, TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero)
        {
            failures.Add($"'{Section}:{name}' must be greater than zero.");
        }
    }
}
