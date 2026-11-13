using CrestApps.Core.Services;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ServiceLevels.Core.Services;

/// <summary>
/// The default <see cref="IServiceLevelPolicyManager"/>.
/// </summary>
internal sealed class ServiceLevelPolicyManager : CatalogManager<ServiceLevelPolicy>, IServiceLevelPolicyManager
{
    private readonly ServiceLevelPolicyStore _store;

    public ServiceLevelPolicyManager(
        ServiceLevelPolicyStore store,
        IEnumerable<ICatalogEntryHandler<ServiceLevelPolicy>> handlers,
        ILogger<CatalogManager<ServiceLevelPolicy>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<ServiceLevelPolicy>> ListByScopeAsync(string scope, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(scope))
        {
            return [];
        }

        var policies = await _store.ListByScopeAsync(scope, cancellationToken);

        foreach (var policy in policies)
        {
            await LoadAsync(policy, cancellationToken);
        }

        return policies;
    }
}
