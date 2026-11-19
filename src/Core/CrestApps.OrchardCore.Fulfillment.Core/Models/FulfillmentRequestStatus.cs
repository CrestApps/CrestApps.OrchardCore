namespace CrestApps.OrchardCore.Fulfillment.Core.Models;

/// <summary>
/// Where an order stands with one fulfillment partner.
/// </summary>
public enum FulfillmentRequestStatus
{
    /// <summary>
    /// Waiting to be sent.
    /// </summary>
    Pending,

    /// <summary>
    /// Being sent now. A request that stays here (the site stopped while sending) needs a person, because the partner
    /// may have created the order.
    /// </summary>
    Sending,

    /// <summary>
    /// The partner accepted the order.
    /// </summary>
    Sent,

    /// <summary>
    /// The order has nothing this partner makes.
    /// </summary>
    NothingToSend,

    /// <summary>
    /// A person must look at it before it is sent again.
    /// </summary>
    NeedsAttention,
}
