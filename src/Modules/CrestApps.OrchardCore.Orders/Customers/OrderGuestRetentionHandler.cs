using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;

namespace CrestApps.OrchardCore.Orders.Customers;

/// <summary>
/// Anonymizes guest orders, and what paid for them, once the Customers retention window for guests' orders runs out.
/// Orders still owed keep their contact until they are settled or canceled.
/// </summary>
public sealed class OrderGuestRetentionHandler : IGuestRetentionHandler
{
    private readonly OrderAnonymizer _anonymizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderGuestRetentionHandler"/> class.
    /// </summary>
    /// <param name="anonymizer">The order anonymizer.</param>
    public OrderGuestRetentionHandler(OrderAnonymizer anonymizer)
    {
        _anonymizer = anonymizer;
    }

    /// <inheritdoc/>
    public Task<int> AnonymizeGuestRecordsAsync(DateTime createdBeforeUtc, int limit, CancellationToken cancellationToken = default)
        => _anonymizer.AnonymizeAsync(new OrderQuery
        {
            GuestsOnly = true,
            CreatedBefore = createdBeforeUtc,
            Statuses = [OrderStatus.Paid, OrderStatus.Canceled],
        }, limit, cancellationToken);
}
