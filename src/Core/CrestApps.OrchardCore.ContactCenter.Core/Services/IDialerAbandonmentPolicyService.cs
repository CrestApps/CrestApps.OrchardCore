using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Evaluates a dialer profile against its rolling abandonment-rate cap before outbound dialing so regulated
/// campaigns cannot exceed their configured tolerance. The policy fails closed: when a cap is enforced for
/// an automated pacing mode but the statistics cannot be proven, dialing is suppressed.
/// </summary>
public interface IDialerAbandonmentPolicyService
{
    /// <summary>
    /// Evaluates whether outbound dialing is permitted for a dialer profile under its abandonment policy.
    /// </summary>
    /// <param name="profile">The dialer profile whose abandonment policy is evaluated.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>An auditable <see cref="DialerAbandonmentEvaluation"/> describing the decision.</returns>
    Task<DialerAbandonmentEvaluation> EvaluateAsync(DialerProfile profile, CancellationToken cancellationToken = default);
}
