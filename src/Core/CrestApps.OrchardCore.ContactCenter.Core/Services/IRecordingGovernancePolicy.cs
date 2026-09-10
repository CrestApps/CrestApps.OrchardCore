using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Evaluates the tenant recording governance policy that gates whether a voice interaction may be recorded and
/// resolves the retention and legal-hold metadata to apply when recording is captured.
/// </summary>
public interface IRecordingGovernancePolicy
{
    /// <summary>
    /// Evaluates whether recording may start or resume for the specified interaction under the tenant policy.
    /// </summary>
    /// <param name="interaction">The interaction whose recording is being evaluated.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A decision describing whether recording is permitted and, when permitted, the retention and legal-hold metadata to apply.</returns>
    Task<RecordingGovernanceDecision> EvaluateStartAsync(Interaction interaction, CancellationToken cancellationToken = default);
}
