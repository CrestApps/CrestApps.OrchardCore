using CrestApps.OrchardCore.Fulfillment.Models;
using CrestApps.OrchardCore.Orders.Core.Models;

namespace CrestApps.OrchardCore.Fulfillment.Core.Services;

/// <summary>
/// Whether an order may go to fulfillment partners, and its description when it may.
/// </summary>
public sealed class FulfillmentOrderCheck
{
    private FulfillmentOrderCheck(Order order, FulfillmentOrder description, string reason, IReadOnlyList<string> arguments, bool nothingToSend)
    {
        Order = order;
        Description = description;
        Reason = reason;
        Arguments = arguments ?? [];
        NothingToSend = nothingToSend;
    }

    /// <summary>
    /// Gets the order as stored now.
    /// </summary>
    public Order Order { get; }

    /// <summary>
    /// Gets the order's description with the quantities left to ship, or <see langword="null"/> when it must not be sent.
    /// </summary>
    public FulfillmentOrder Description { get; }

    /// <summary>
    /// Gets why the order must not be sent, as one of the <see cref="Models.FulfillmentReasons"/> codes.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Gets the values the reason's sentence names.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// Gets a value indicating whether the order simply has nothing for partners, rather than a problem a person must
    /// look at.
    /// </summary>
    public bool NothingToSend { get; }

    /// <summary>
    /// Gets a value indicating whether the order may be sent.
    /// </summary>
    public bool CanSend
        => Description is not null;

    /// <summary>
    /// Creates the result for an order that may be sent.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="description">The order's description.</param>
    /// <returns>The result.</returns>
    public static FulfillmentOrderCheck Ready(Order order, FulfillmentOrder description)
        => new(order, description, null, null, false);

    /// <summary>
    /// Creates the result for an order that must not be sent.
    /// </summary>
    /// <param name="order">The order, when it exists.</param>
    /// <param name="reason">Why, as a reason code.</param>
    /// <param name="nothingToSend">Whether the order simply has nothing for partners.</param>
    /// <param name="arguments">The values the reason's sentence names.</param>
    /// <returns>The result.</returns>
    public static FulfillmentOrderCheck Blocked(Order order, string reason, bool nothingToSend, params string[] arguments)
        => new(order, null, reason, arguments, nothingToSend);
}
