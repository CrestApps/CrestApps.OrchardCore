using CrestApps.Core.Services;
using CrestApps.OrchardCore.ServiceLevels.Models;

namespace CrestApps.OrchardCore.ServiceLevels.Services;

/// <summary>
/// Manages service level policies.
/// </summary>
public interface IServiceLevelPolicyManager : ICatalogManager<ServiceLevelPolicy>
{
    /// <summary>
    /// Lists the policies of a scope, by name.
    /// </summary>
    /// <param name="scope">The scope's name.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The policies.</returns>
    Task<IReadOnlyList<ServiceLevelPolicy>> ListByScopeAsync(string scope, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the policy of a name in a scope.
    /// </summary>
    /// <param name="scope">The scope's name.</param>
    /// <param name="name">The policy's name.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The policy, or <see langword="null"/>.</returns>
    Task<ServiceLevelPolicy> FindByNameAsync(string scope, string name, CancellationToken cancellationToken = default);
}
