namespace CrestApps.OrchardCore.Transactions.Models;

/// <summary>
/// The kinds of <see cref="PaymentIssue"/> the commerce modules record. The kind is an open string, so another module
/// may record its own.
/// </summary>
public static class PaymentIssueKinds
{
    /// <summary>
    /// A payment had to be refunded, such as for a checkout that could not complete, and the refund could not be issued.
    /// </summary>
    public const string RefundFailed = "RefundFailed";

    /// <summary>
    /// A subscription was canceled, paused or resumed here, and the payment provider refused to do the same, so the
    /// customer may still be billed.
    /// </summary>
    public const string GatewaySubscriptionChangeFailed = "GatewaySubscriptionChangeFailed";

    /// <summary>
    /// A payment was settled and a discount provider could not record the redemption of its code.
    /// </summary>
    public const string DiscountRedemptionFailed = "DiscountRedemptionFailed";
}
