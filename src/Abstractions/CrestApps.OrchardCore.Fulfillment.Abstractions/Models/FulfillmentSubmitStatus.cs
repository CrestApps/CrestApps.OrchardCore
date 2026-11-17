namespace CrestApps.OrchardCore.Fulfillment.Models;

/// <summary>
/// How sending an order to a fulfillment partner ended.
/// </summary>
public enum FulfillmentSubmitStatus
{
    /// <summary>
    /// The partner accepted the order.
    /// </summary>
    Sent,

    /// <summary>
    /// None of the order's lines is made by this partner, so nothing was sent.
    /// </summary>
    NothingToSend,

    /// <summary>
    /// The partner is busy or limiting requests and certainly did not create the order; sending again later is safe.
    /// </summary>
    RetryLater,

    /// <summary>
    /// A person must look at it: the partner refused the order, a line is not linked, or it is not known whether the
    /// partner created the order. It is never sent again by itself.
    /// </summary>
    NeedsAttention,
}
