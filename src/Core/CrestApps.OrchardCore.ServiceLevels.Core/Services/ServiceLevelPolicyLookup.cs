using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;

namespace CrestApps.OrchardCore.ServiceLevels.Core.Services;

/// <summary>
/// Reads policies through the policy manager, so its handlers run.
/// </summary>
internal sealed class ServiceLevelPolicyLookup : IServiceLevelPolicyLookup
{
    private readonly IServiceLevelPolicyManager _manager;

    public ServiceLevelPolicyLookup(IServiceLevelPolicyManager manager)
    {
        _manager = manager;
    }

    public ValueTask<ServiceLevelPolicy> FindAsync(string policyId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(policyId) ? ValueTask.FromResult<ServiceLevelPolicy>(null) : _manager.FindByIdAsync(policyId, cancellationToken);
}
