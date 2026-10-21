using CrestApps.OrchardCore.Orders.Core.Indexes;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Orders.Core.Services;

public interface IOrderReturnStore
{
    ValueTask<OrderReturn> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderReturn>> ListByOrderAsync(string orderId, CancellationToken cancellationToken = default);

    Task<(int Count, IReadOnlyList<OrderReturn> Returns)> PageAsync(OrderReturnQuery query, int skip, int take, CancellationToken cancellationToken = default);

    ValueTask CreateAsync(OrderReturn orderReturn, CancellationToken cancellationToken = default);

    ValueTask UpdateAsync(OrderReturn orderReturn, CancellationToken cancellationToken = default);
}

public sealed class OrderReturnQuery
{
    /// <summary>
    /// Gets or sets the start of a return or order number to find.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the status to show, or <see langword="null"/> for the returns still open.
    /// </summary>
    public OrderReturnStatus? Status { get; set; }

    public bool All { get; set; }
}

public sealed class OrderReturnStore : DocumentCatalog<OrderReturn, OrderReturnIndex>, IOrderReturnStore
{
    public OrderReturnStore(ISession session)
        : base(session)
    {
        CollectionName = OrderConstants.CollectionName;
    }

    protected override bool CheckConcurrency => true;

    public async Task<IReadOnlyList<OrderReturn>> ListByOrderAsync(string orderId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(orderId)
            ? []
            : (await Session.Query<OrderReturn, OrderReturnIndex>(index => index.OrderId == orderId, collection: CollectionName)
                .OrderBy(index => index.CreatedUtc)
                .ListAsync(cancellationToken)).ToArray();

    public async Task<(int Count, IReadOnlyList<OrderReturn> Returns)> PageAsync(OrderReturnQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        query ??= new OrderReturnQuery();

        var returns = Session.Query<OrderReturn, OrderReturnIndex>(collection: CollectionName);

        if (query.Status is { } status)
        {
            returns = returns.Where(index => index.Status == status);
        }
        else if (!query.All)
        {
            returns = returns.Where(index => index.Status == OrderReturnStatus.Requested || index.Status == OrderReturnStatus.Approved || index.Status == OrderReturnStatus.Received);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToUpperInvariant();

            returns = returns.Where(index => index.Number.StartsWith(search) || index.OrderNumber.StartsWith(search));
        }

        var count = await returns.CountAsync(cancellationToken);
        var page = await returns.OrderByDescending(index => index.CreatedUtc).ThenByDescending(index => index.Id).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToArray());
    }
}
