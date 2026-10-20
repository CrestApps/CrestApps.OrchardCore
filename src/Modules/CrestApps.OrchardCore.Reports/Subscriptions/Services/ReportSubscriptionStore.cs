using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Reports.Subscriptions.Models;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Reports.Subscriptions.Services;

public interface IReportSubscriptionStore : ICatalog<ReportSubscription>
{
    Task<(int Count, IReadOnlyList<ReportSubscription> Subscriptions)> PageAsync(string search, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the subscriptions that are on and due to go out.
    /// </summary>
    Task<IReadOnlyList<ReportSubscription>> ListDueAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}

public sealed class ReportSubscriptionStore : DocumentCatalog<ReportSubscription, ReportSubscriptionIndex>, IReportSubscriptionStore
{
    public ReportSubscriptionStore(ISession session)
        : base(session)
    {
    }

    public async Task<(int Count, IReadOnlyList<ReportSubscription> Subscriptions)> PageAsync(string search, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<ReportSubscription, ReportSubscriptionIndex>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();

            query = query.Where(index => index.Name.Contains(text));
        }

        var count = await query.CountAsync(cancellationToken);

        return (count, [.. await query.OrderBy(index => index.Name).Skip(skip).Take(take).ListAsync(cancellationToken)]);
    }

    public async Task<IReadOnlyList<ReportSubscription>> ListDueAsync(DateTime utcNow, CancellationToken cancellationToken = default)
        => [.. await Session.Query<ReportSubscription, ReportSubscriptionIndex>(index => index.IsEnabled && index.NextRunUtc <= utcNow).Take(50).ListAsync(cancellationToken)];
}

public sealed class ReportSubscriptionIndex : CatalogItemIndex
{
    public string Name { get; set; }

    public string ReportName { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime? NextRunUtc { get; set; }
}

public sealed class ReportSubscriptionIndexProvider : IndexProvider<ReportSubscription>
{
    public override void Describe(DescribeContext<ReportSubscription> context)
        => context.For<ReportSubscriptionIndex>()
            .Map(subscription => new ReportSubscriptionIndex
            {
                ItemId = subscription.ItemId,
                Name = subscription.Name,
                ReportName = subscription.ReportName,
                IsEnabled = subscription.IsEnabled,
                NextRunUtc = subscription.NextRunUtc,
            });
}
