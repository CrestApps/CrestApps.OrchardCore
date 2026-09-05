namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Evaluates outbound compliance before every dialing attempt. The gate enforces do-not-call and
/// communication preferences, national do-not-call registries, calling windows, retry cool-down, and
/// attempt limits, and records an auditable suppression reason when an attempt must be blocked.
/// </summary>
public interface IDialerEligibilityService
{
    /// <summary>
    /// Evaluates whether the activity in the supplied context may be dialed.
    /// </summary>
    /// <param name="context">The evaluation context containing the dialer profile and activity.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The eligibility result describing whether the attempt is allowed and, if not, why.</returns>
    Task<DialerEligibilityResult> EvaluateAsync(DialerEligibilityContext context, CancellationToken cancellationToken = default);
}
