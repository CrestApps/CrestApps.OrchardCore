using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.WorkOrders.Services;

/// <summary>
/// What the Work Orders page filters its list by.
/// </summary>
internal sealed class WorkOrderListFilter
{
    /// <summary>
    /// Gets the status: <see cref="WorkOrderListQuery.OpenFilter"/>, <see cref="WorkOrderListQuery.AllFilter"/> or a
    /// <see cref="WorkOrderStatus"/> name.
    /// </summary>
    public string Status { get; init; }

    /// <summary>
    /// Gets the work order type, or <see langword="null"/> for every type.
    /// </summary>
    public string TypeId { get; init; }

    /// <summary>
    /// Gets the <see cref="WorkOrderPriority"/> name, or <see langword="null"/> for every priority.
    /// </summary>
    public string Priority { get; init; }

    /// <summary>
    /// Gets the text the number or the summary contains, or <see langword="null"/>.
    /// </summary>
    public string Search { get; init; }

    /// <summary>
    /// Gets the asset the work orders are for, or <see langword="null"/>.
    /// </summary>
    public string AssetId { get; init; }
}

/// <summary>
/// The query of the Work Orders page: by status (or every open one), type, priority, asset and a search on the number or
/// the summary, newest first.
/// </summary>
internal static class WorkOrderListQuery
{
    /// <summary>
    /// The status filter for the work orders still to finish.
    /// </summary>
    public const string OpenFilter = "open";

    /// <summary>
    /// The status filter for every work order.
    /// </summary>
    public const string AllFilter = "all";

    /// <summary>
    /// Gives a page of the work orders that match a filter, newest first, and how many match.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="filter">The filter.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to give at most.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many match, and the page.</returns>
    public static async Task<(int Count, IReadOnlyList<WorkOrder> Items)> PageAsync(ISession session, WorkOrderListFilter filter, int skip, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(filter);

        var query = session.Query<WorkOrder, WorkOrderIndex>(collection: WorkOrdersConstants.CollectionName);

        if (filter.Status == OpenFilter)
        {
            query = query.Where(index => index.IsOpen);
        }
        else if (Enum.TryParse<WorkOrderStatus>(filter.Status, out var status))
        {
            var value = status.ToString();

            query = query.Where(index => index.Status == value);
        }

        if (!string.IsNullOrEmpty(filter.TypeId))
        {
            var typeId = filter.TypeId;

            query = query.Where(index => index.TypeId == typeId);
        }

        if (Enum.TryParse<WorkOrderPriority>(filter.Priority, out var priority))
        {
            var value = priority.ToString();

            query = query.Where(index => index.Priority == value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var q = filter.Search.Trim();

            query = query.Where(index => index.Number.Contains(q) || index.Summary.Contains(q));
        }

        if (!string.IsNullOrEmpty(filter.AssetId))
        {
            var assetId = filter.AssetId;
            var ids = (await session.QueryIndex<WorkOrderAssetIndex>(index => index.AssetId == assetId, collection: WorkOrdersConstants.CollectionName).ListAsync(cancellationToken))
                .Select(index => index.WorkOrderId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            query = query.Where(index => index.ItemId.IsIn(ids));
        }

        var count = await query.CountAsync(cancellationToken);
        var page = await query.OrderByDescending(index => index.CreatedUtc).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToList());
    }
}
