using CrestApps.OrchardCore.Fulfillment.Models;

namespace CrestApps.OrchardCore.Fulfillment.Core.Services;

/// <summary>
/// Whether an order may go to fulfillment partners, and its description when it may.
/// </summary>
public sealed class FulfillmentOrderCheck
{
    private FulfillmentOrderCheck(FulfillmentOrder order, string reason, bool nothingToSend)
    {
        Order = order;
        Reason = reason;
        NothingToSend = nothingToSend;
    }

    /// <summary>
    /// Gets the order's description, or <see langword="null"/> when it must not be sent.
    /// </summary>
    public FulfillmentOrder Order { get; }

    /// <summary>
    /// Gets why the order must not be sent.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Gets a value indicating whether the order simply has nothing for partners (nothing ships), rather than a
    /// problem a person must look at.
    /// </summary>
    public bool NothingToSend { get; }

    /// <summary>
    /// Gets a value indicating whether the order may be sent.
    /// </summary>
    public bool CanSend
        => Order is not null;

    /// <summary>
    /// Creates the result for an order that may be sent.
    /// </summary>
    /// <param name="order">The order's description.</param>
    /// <returns>The result.</returns>
    public static FulfillmentOrderCheck Ready(FulfillmentOrder order)
        => new(order, null, false);

    /// <summary>
    /// Creates the result for an order that must not be sent.
    /// </summary>
    /// <param name="reason">Why.</param>
    /// <param name="nothingToSend">Whether the order simply has nothing for partners.</param>
    /// <returns>The result.</returns>
    public static FulfillmentOrderCheck Blocked(string reason, bool nothingToSend)
        => new(null, reason, nothingToSend);
}
