namespace CrestApps.OrchardCore.Fulfillment.Core.Models;

/// <summary>
/// The reason codes saved on a <see cref="FulfillmentRequest"/>. The order card and the list page turn each one, with
/// its <see cref="FulfillmentRequest.ReasonArguments"/>, into a localized sentence.
/// </summary>
public static class FulfillmentReasons
{
    /// <summary>The partner accepted the order. Arguments: the partner's order id.</summary>
    public const string Sent = "Sent";

    /// <summary>Waiting to be sent.</summary>
    public const string Waiting = "Waiting";

    /// <summary>The partner was busy and the order will be sent again later.</summary>
    public const string RetryLater = "RetryLater";

    /// <summary>The order is not paid. Arguments: the order status.</summary>
    public const string NotPaid = "NotPaid";

    /// <summary>The order no longer exists.</summary>
    public const string OrderDeleted = "OrderDeleted";

    /// <summary>The order was refunded in full.</summary>
    public const string Refunded = "Refunded";

    /// <summary>The order has an open payment dispute.</summary>
    public const string OpenDispute = "OpenDispute";

    /// <summary>The order's personal data was removed.</summary>
    public const string Anonymized = "Anonymized";

    /// <summary>The buyer picks the order up at the store.</summary>
    public const string Pickup = "Pickup";

    /// <summary>The order has no complete shipping address.</summary>
    public const string NoAddress = "NoAddress";

    /// <summary>Only store-shipped bundles remain.</summary>
    public const string Bundle = "Bundle";

    /// <summary>Nothing in the order is left to ship: every unit is already in a shipment.</summary>
    public const string NothingLeft = "NothingLeft";

    /// <summary>The partner makes none of the lines left to ship.</summary>
    public const string NotClaimed = "NotClaimed";

    /// <summary>Two partners claimed the same line. Arguments: the line, then the partners.</summary>
    public const string ConflictingClaims = "ConflictingClaims";

    /// <summary>The lines could not be reserved in a shipment because more units were asked than are left.</summary>
    public const string TooMany = "TooMany";

    /// <summary>The order was already sent, or is being sent, to this partner by another record.</summary>
    public const string AlreadySent = "AlreadySent";

    /// <summary>The partner's feature is turned off. Arguments: the partner's name.</summary>
    public const string ProviderDisabled = "ProviderDisabled";

    /// <summary>The reserved shipment was shipped by hand, so nothing was sent.</summary>
    public const string ShippedByHand = "ShippedByHand";

    /// <summary>The partner refused the order and did not create it; see the partner's message.</summary>
    public const string Refused = "Refused";

    /// <summary>The partner did not answer clearly, so it is not known whether it created the order.</summary>
    public const string Unclear = "Unclear";

    /// <summary>Sending was interrupted, so it is not known whether the partner created the order.</summary>
    public const string Interrupted = "Interrupted";

    /// <summary>The partner made nothing of what was sent; see the partner's message.</summary>
    public const string PartnerNothingToSend = "PartnerNothingToSend";

    /// <summary>The partner could not say which lines it makes, so nothing was sent.</summary>
    public const string ClaimFailed = "ClaimFailed";

    /// <summary>The partner kept asking to wait, so sending stopped. Arguments: how many times it was tried.</summary>
    public const string GaveUp = "GaveUp";
}
