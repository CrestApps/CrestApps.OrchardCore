using CrestApps.OrchardCore.ServiceLevels.Core.Indexes;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ServiceLevels.Core.Services;

/// <summary>
/// Stores service level policies.
/// </summary>
public sealed class ServiceLevelPolicyStore : DocumentCatalog<ServiceLevelPolicy, ServiceLevelPolicyIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceLevelPolicyStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ServiceLevelPolicyStore(ISession session)
        : base(session)
    {
        CollectionName = ServiceLevelsConstants.CollectionName;
    }

    /// <summary>
    /// Lists every policy, by name.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The policies.</returns>
    public Task<IReadOnlyList<ServiceLevelPolicy>> ListAsync(CancellationToken cancellationToken = default)
        => ListByNameAsync(index => index.Name, cancellationToken);

    /// <summary>
    /// Lists the policies of a scope, by name.
    /// </summary>
    /// <param name="scope">The scope's name.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The policies.</returns>
    public async Task<IReadOnlyList<ServiceLevelPolicy>> ListByScopeAsync(string scope, CancellationToken cancellationToken = default)
    {
        var policies = await Session.Query<ServiceLevelPolicy, ServiceLevelPolicyIndex>(index => index.Scope == scope, collection: CollectionName)
            .OrderBy(index => index.Name)
            .ListAsync(cancellationToken);

        return policies.ToArray();
    }

    /// <summary>
    /// Finds the policy of a name in a scope.
    /// </summary>
    /// <param name="scope">The scope's name.</param>
    /// <param name="name">The policy's name.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The policy, or <see langword="null"/>.</returns>
    public async Task<ServiceLevelPolicy> FindByNameAsync(string scope, string name, CancellationToken cancellationToken = default)
        => await Session.Query<ServiceLevelPolicy, ServiceLevelPolicyIndex>(index => index.Scope == scope && index.Name == name, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
}
