using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="ITimeOffPolicyManager"/>.
/// </summary>
internal sealed class TimeOffPolicyManager : CatalogManager<TimeOffPolicy>, ITimeOffPolicyManager
{
    private readonly TimeOffPolicyStore _store;
    private readonly TimeOffPolicyResolvers _resolvers;

    public TimeOffPolicyManager(
        TimeOffPolicyStore store,
        TimeOffPolicyResolvers resolvers,
        IEnumerable<ICatalogEntryHandler<TimeOffPolicy>> handlers,
        ILogger<CatalogManager<TimeOffPolicy>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
        _resolvers = resolvers;
    }

    public async Task<IReadOnlyList<TimeOffPolicy>> ListForWorkerAsync(WorkerProfile worker, DateOnly date, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        var resolver = await _resolvers.ForWorkerAsync(worker, cancellationToken);

        return resolver.ListOn(date);
    }

    public async Task<TimeOffPolicy> FindForWorkerAsync(WorkerProfile worker, string typeId, DateOnly date, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        var resolver = await _resolvers.ForWorkerAsync(worker, cancellationToken);

        return resolver.Resolve(typeId, date);
    }

    public Task<IReadOnlyList<TimeOffPolicy>> ListAssignedAsync(string workerId, IEnumerable<string> teamIds, CancellationToken cancellationToken = default)
    {
        return _store.ListAssignedAsync(workerId, (teamIds ?? []).ToArray(), cancellationToken);
    }
}
