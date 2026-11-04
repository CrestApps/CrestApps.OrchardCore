using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores blackout dates.
/// </summary>
internal sealed class BlackoutPeriodStore : DocumentCatalog<BlackoutPeriod, BlackoutPeriodIndex>
{
    public BlackoutPeriodStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public Task<IReadOnlyList<BlackoutPeriod>> ListAsync(CancellationToken cancellationToken = default)
    {
        return ListByNameAsync(index => index.Name, cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>Pages list the latest blackout dates first.</remarks>
    protected override ValueTask PagingAsync<TQuery>(IQuery<BlackoutPeriod> query, TQuery context)
    {
        ArgumentNullException.ThrowIfNull(query);

        query.With<BlackoutPeriodIndex>().OrderByDescending(index => index.StartDate);

        return ValueTask.CompletedTask;
    }
}
