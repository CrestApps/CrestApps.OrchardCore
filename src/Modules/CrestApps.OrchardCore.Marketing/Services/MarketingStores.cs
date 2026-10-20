using CrestApps.Core.Services;
using CrestApps.OrchardCore.Marketing.Indexes;
using CrestApps.OrchardCore.Marketing.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Marketing.Services;

public interface ICustomerPurchasesStore : ICatalog<CustomerPurchases>
{
    /// <summary>
    /// Returns the customers a segment's rules match, in the order they were first counted, after the document
    /// <paramref name="afterId"/> (none for the first).
    /// </summary>
    Task<IReadOnlyList<CustomerPurchases>> ListAsync(SegmentRules rules, DateTime utcNow, string afterId, int take, CancellationToken cancellationToken = default);

    Task<int> CountAsync(SegmentRules rules, DateTime utcNow, CancellationToken cancellationToken = default);
}

public sealed class CustomerPurchasesStore : DocumentCatalog<CustomerPurchases, CustomerPurchasesIndex>, ICustomerPurchasesStore
{
    public CustomerPurchasesStore(ISession session)
        : base(session)
    {
        CollectionName = MarketingConstants.CollectionName;
    }

    public async Task<IReadOnlyList<CustomerPurchases>> ListAsync(SegmentRules rules, DateTime utcNow, string afterId, int take, CancellationToken cancellationToken = default)
    {
        var query = Filter(Session.Query<CustomerPurchases, CustomerPurchasesIndex>(collection: CollectionName), rules, utcNow);

        // A cursor on the document id, which only grows: customers counted while a campaign sends come after it.
        if (long.TryParse(afterId, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var after))
        {
            query = query.Where(index => index.DocumentId > after);
        }

        return [.. await query.OrderBy(index => index.DocumentId).Take(take).ListAsync(cancellationToken)];
    }

    public Task<int> CountAsync(SegmentRules rules, DateTime utcNow, CancellationToken cancellationToken = default)
        => Filter(Session.Query<CustomerPurchases, CustomerPurchasesIndex>(collection: CollectionName), rules, utcNow).CountAsync(cancellationToken);

    internal static IQuery<CustomerPurchases, CustomerPurchasesIndex> Filter(IQuery<CustomerPurchases, CustomerPurchasesIndex> query, SegmentRules rules, DateTime utcNow)
    {
        rules ??= new SegmentRules();

        // Only customers who've bought belong to a segment.
        query = query.Where(index => index.OrderCount > 0);

        if (rules.MinOrders is { } minOrders)
        {
            query = query.Where(index => index.OrderCount >= minOrders);
        }

        if (rules.MaxOrders is { } maxOrders)
        {
            query = query.Where(index => index.OrderCount <= maxOrders);
        }

        if (rules.MinSpent is { } minSpent)
        {
            query = query.Where(index => index.TotalSpent >= minSpent);
        }

        if (rules.MaxSpent is { } maxSpent)
        {
            query = query.Where(index => index.TotalSpent <= maxSpent);
        }

        if (rules.LastOrderAtLeastDaysAgo is { } lapsedDays)
        {
            var before = utcNow.AddDays(-lapsedDays);

            query = query.Where(index => index.LastOrderUtc <= before);
        }

        if (rules.LastOrderWithinDays is { } recentDays)
        {
            var since = utcNow.AddDays(-recentDays);

            query = query.Where(index => index.LastOrderUtc >= since);
        }

        if (rules.FirstOrderWithinDays is { } newDays)
        {
            var since = utcNow.AddDays(-newDays);

            query = query.Where(index => index.FirstOrderUtc >= since);
        }

        if (!string.IsNullOrWhiteSpace(rules.Country))
        {
            var country = rules.Country.Trim().ToUpperInvariant();

            query = query.Where(index => index.Country == country);
        }

        return query;
    }
}

public interface ISegmentStore : ICatalog<Segment>
{
    Task<(int Count, IReadOnlyList<Segment> Segments)> PageAsync(string search, int skip, int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Segment>> ListAllAsync(CancellationToken cancellationToken = default);
}

public sealed class SegmentStore : DocumentCatalog<Segment, SegmentIndex>, ISegmentStore
{
    public SegmentStore(ISession session)
        : base(session)
    {
        CollectionName = MarketingConstants.CollectionName;
    }

    public async Task<(int Count, IReadOnlyList<Segment> Segments)> PageAsync(string search, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<Segment, SegmentIndex>(collection: CollectionName);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();

            query = query.Where(index => index.Name.Contains(text));
        }

        var count = await query.CountAsync(cancellationToken);

        return (count, [.. await query.OrderBy(index => index.Name).Skip(skip).Take(take).ListAsync(cancellationToken)]);
    }

    public async Task<IReadOnlyList<Segment>> ListAllAsync(CancellationToken cancellationToken = default)
        => [.. await Session.Query<Segment, SegmentIndex>(collection: CollectionName).OrderBy(index => index.Name).ListAsync(cancellationToken)];
}

public interface ICampaignStore : ICatalog<Campaign>
{
    Task<(int Count, IReadOnlyList<Campaign> Campaigns)> PageAsync(string search, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the campaigns to send now: sending, or scheduled for a time that's come.
    /// </summary>
    Task<IReadOnlyList<Campaign>> ListDueAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}

public sealed class CampaignStore : DocumentCatalog<Campaign, CampaignIndex>, ICampaignStore
{
    public CampaignStore(ISession session)
        : base(session)
    {
        CollectionName = MarketingConstants.CollectionName;
    }

    public async Task<(int Count, IReadOnlyList<Campaign> Campaigns)> PageAsync(string search, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<Campaign, CampaignIndex>(collection: CollectionName);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();

            query = query.Where(index => index.Name.Contains(text));
        }

        var count = await query.CountAsync(cancellationToken);

        return (count, [.. await query.OrderByDescending(index => index.CreatedUtc).Skip(skip).Take(take).ListAsync(cancellationToken)]);
    }

    public async Task<IReadOnlyList<Campaign>> ListDueAsync(DateTime utcNow, CancellationToken cancellationToken = default)
        => [.. await Session.Query<Campaign, CampaignIndex>(
            index => index.Status == CampaignStatus.Sending || (index.Status == CampaignStatus.Scheduled && index.ScheduledUtc <= utcNow),
            collection: CollectionName).ListAsync(cancellationToken)];
}
