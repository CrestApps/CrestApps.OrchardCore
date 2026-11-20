using CrestApps.OrchardCore.Fulfillment.Core.Indexes;
using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Fulfillment.Core.Services;

/// <summary>
/// Stores the orders sent, or to be sent, to fulfillment partners. Saving checks for concurrent changes, so two
/// workers can never both send the same request.
/// </summary>
public sealed class FulfillmentRequestStore : DocumentCatalog<FulfillmentRequest, FulfillmentRequestIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FulfillmentRequestStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public FulfillmentRequestStore(ISession session)
        : base(session)
    {
        CollectionName = FulfillmentConstants.CollectionName;
    }

    /// <inheritdoc/>
    protected override bool CheckConcurrency => true;

    /// <summary>
    /// Reads a request again from the database, dropping the copy this session holds, so a decision is made on the
    /// state another worker may have saved meanwhile.
    /// </summary>
    /// <param name="loaded">The copy this session holds.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The stored request, or <see langword="null"/> when it was deleted.</returns>
    public async Task<FulfillmentRequest> FindCommittedAsync(FulfillmentRequest loaded, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loaded);

        Session.Detach(loaded, CollectionName);

        return await FindByIdAsync(loaded.ItemId, cancellationToken);
    }

    /// <summary>
    /// Lists the requests of an order.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The requests, oldest first.</returns>
    public async Task<IReadOnlyList<FulfillmentRequest>> ListByOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(orderId);

        var items = await Session.Query<FulfillmentRequest, FulfillmentRequestIndex>(index => index.OrderId == orderId, collection: CollectionName)
            .ListAsync(cancellationToken);

        return items.OrderBy(item => item.CreatedUtc).ToList();
    }

    /// <summary>
    /// Lists the pending requests that are due, oldest first.
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <param name="count">The most requests to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The due requests.</returns>
    public async Task<IReadOnlyList<FulfillmentRequest>> ListDueAsync(DateTime now, int count, CancellationToken cancellationToken = default)
    {
        var pending = nameof(FulfillmentRequestStatus.Pending);
        var items = await Session.Query<FulfillmentRequest, FulfillmentRequestIndex>(index => index.Status == pending && index.NextAttemptUtc <= now, collection: CollectionName)
            .OrderBy(index => index.NextAttemptUtc)
            .Take(count)
            .ListAsync(cancellationToken);

        return items.ToList();
    }

    /// <summary>
    /// Lists the requests left in <see cref="FulfillmentRequestStatus.Sending"/> since before a time.
    /// </summary>
    /// <param name="before">Requests changed before this time are returned.</param>
    /// <param name="count">The most requests to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The requests.</returns>
    public async Task<IReadOnlyList<FulfillmentRequest>> ListStuckAsync(DateTime before, int count, CancellationToken cancellationToken = default)
    {
        var sending = nameof(FulfillmentRequestStatus.Sending);
        var items = await Session.Query<FulfillmentRequest, FulfillmentRequestIndex>(index => index.Status == sending && index.ModifiedUtc < before, collection: CollectionName)
            .Take(count)
            .ListAsync(cancellationToken);

        return items.ToList();
    }

    /// <summary>
    /// Lists a page of requests, most recently changed first, for the list page.
    /// </summary>
    /// <param name="status">The status to show, or <see langword="null"/> for all.</param>
    /// <param name="search">Text the order number starts with, or <see langword="null"/>.</param>
    /// <param name="skip">How many requests to skip.</param>
    /// <param name="take">How many requests to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The page, and how many requests match in all.</returns>
    public async Task<(IReadOnlyList<FulfillmentRequest> Items, int Total)> ListPageAsync(FulfillmentRequestStatus? status, string search, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<FulfillmentRequest, FulfillmentRequestIndex>(collection: CollectionName);

        if (status is { } wanted)
        {
            var name = wanted.ToString();

            query = query.Where(index => index.Status == name);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();

            query = query.Where(index => index.OrderNumber.StartsWith(text));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(index => index.ModifiedUtc)
            .Skip(skip)
            .Take(take)
            .ListAsync(cancellationToken);

        return (items.ToList(), total);
    }

    /// <summary>
    /// Writes the changes of this session to the database now, so a request marked as being sent is stored before the
    /// partner is called.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task.</returns>
    public Task CommitAsync(CancellationToken cancellationToken = default)
        => Session.SaveChangesAsync(cancellationToken);
}
