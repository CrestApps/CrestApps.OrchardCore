using CrestApps.OrchardCore.ServiceLevels.Models;

namespace CrestApps.OrchardCore.ServiceLevels.ViewModels;

/// <summary>
/// The facts of a policy's row in the list of policies.
/// </summary>
public class ServiceLevelPolicyRowViewModel
{
    /// <summary>
    /// Gets or sets the policy.
    /// </summary>
    public ServiceLevelPolicy Policy { get; set; }

    /// <summary>
    /// Gets or sets the name of the policy's scope shown to users.
    /// </summary>
    public string ScopeDisplayName { get; set; }
}
