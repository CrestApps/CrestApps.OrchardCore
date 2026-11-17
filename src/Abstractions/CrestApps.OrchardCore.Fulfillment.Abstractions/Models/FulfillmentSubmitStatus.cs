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
    /// None of the lines sent is made by this partner after all, so nothing was created.
    /// </summary>
    NothingToSend,

    /// <summary>
    /// The partner is busy or limiting requests and certainly did not create the order; sending again later is safe.
    /// </summary>
    RetryLater,

    /// <summary>
    /// The partner refused the order and certainly did not create it, for example an invalid address or a refused
    /// access key. A person must fix the cause; the units are released so they can be shipped another way.
    /// </summary>
    Refused,

    /// <summary>
    /// It is not known whether the partner created the order. A person must check with the partner; the units stay
    /// reserved and the order is never sent again by itself.
    /// </summary>
    NeedsAttention,
}
