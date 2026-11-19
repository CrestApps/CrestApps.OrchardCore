using CrestApps.OrchardCore.ServiceLevels.Models;

namespace CrestApps.OrchardCore.ServiceLevels.Core.Services;

/// <summary>
/// Reads the policy a timer takes its target from.
/// </summary>
public interface IServiceLevelPolicyLookup
{
    /// <summary>
    /// Finds a policy.
    /// </summary>
    /// <param name="policyId">The policy's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The policy, or <see langword="null"/> when there is none.</returns>
    ValueTask<ServiceLevelPolicy> FindAsync(string policyId, CancellationToken cancellationToken = default);
}
