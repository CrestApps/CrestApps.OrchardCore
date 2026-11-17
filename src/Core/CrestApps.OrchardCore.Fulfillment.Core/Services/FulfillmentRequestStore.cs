using CrestApps.OrchardCore.Fulfillment.Core.Indexes;
using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

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
    /// Lists the requests of an order.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The requests, one per partner.</returns>
    public async Task<IReadOnlyList<FulfillmentRequest>> ListByOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(orderId);

        var items = await Session.Query<FulfillmentRequest, FulfillmentRequestIndex>(index => index.OrderId == orderId, collection: CollectionName)
            .ListAsync(cancellationToken);

        return items.ToList();
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
    /// Writes the changes of this session to the database now, so a request marked as being sent is stored before the
    /// partner is called.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task.</returns>
    public Task CommitAsync(CancellationToken cancellationToken = default)
        => Session.SaveChangesAsync(cancellationToken);
}
