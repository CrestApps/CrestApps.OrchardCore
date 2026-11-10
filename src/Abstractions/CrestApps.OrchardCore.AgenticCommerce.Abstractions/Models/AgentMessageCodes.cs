namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// The codes of agent checkout messages. Each protocol passes them on, or maps them to its own.
/// </summary>
public static class AgentMessageCodes
{
    /// <summary>
    /// A required value is missing.
    /// </summary>
    public const string Missing = "missing";

    /// <summary>
    /// A value is not valid.
    /// </summary>
    public const string Invalid = "invalid";

    /// <summary>
    /// An item is out of stock, or not in the quantity asked.
    /// </summary>
    public const string OutOfStock = "out_of_stock";

    /// <summary>
    /// An item can't be bought.
    /// </summary>
    public const string ItemUnavailable = "item_unavailable";

    /// <summary>
    /// An item, or the checkout, doesn't exist.
    /// </summary>
    public const string NotFound = "not_found";

    /// <summary>
    /// Nothing ships to the address.
    /// </summary>
    public const string AddressUndeliverable = "address_undeliverable";

    /// <summary>
    /// The payment failed.
    /// </summary>
    public const string PaymentFailed = "payment_failed";

    /// <summary>
    /// The payment was declined.
    /// </summary>
    public const string PaymentDeclined = "payment_declined";

    /// <summary>
    /// The buyer's bank asks the buyer to approve the payment.
    /// </summary>
    public const string Requires3ds = "requires_3ds";

    /// <summary>
    /// A discount code doesn't apply.
    /// </summary>
    public const string DiscountCodeInvalid = "discount_code_invalid";

    /// <summary>
    /// The buyer has to give something the agent can't, in the store's own checkout.
    /// </summary>
    public const string RequiresBuyerInput = "requires_buyer_input";

    /// <summary>
    /// The checkout can't do that in its state.
    /// </summary>
    public const string InvalidState = "invalid_state";

    /// <summary>
    /// A price changed since the agent last saw it.
    /// </summary>
    public const string PriceChanged = "price_changed";
}
