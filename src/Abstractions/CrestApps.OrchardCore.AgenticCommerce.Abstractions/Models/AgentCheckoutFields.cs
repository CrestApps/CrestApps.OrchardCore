namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// The protocol-neutral names of the fields an agent checkout message points at. Each protocol turns them into its own
/// JSON path.
/// </summary>
public static class AgentCheckoutFields
{
    /// <summary>
    /// The buyer's e-mail address.
    /// </summary>
    public const string BuyerEmail = "buyer.email";

    /// <summary>
    /// The buyer's name.
    /// </summary>
    public const string BuyerName = "buyer.name";

    /// <summary>
    /// The buyer's phone number.
    /// </summary>
    public const string BuyerPhone = "buyer.phone";

    /// <summary>
    /// Where to ship.
    /// </summary>
    public const string FulfillmentAddress = "fulfillment.address";

    /// <summary>
    /// How to ship.
    /// </summary>
    public const string FulfillmentOption = "fulfillment.option";

    /// <summary>
    /// Where to collect.
    /// </summary>
    public const string FulfillmentPickup = "fulfillment.pickup";

    /// <summary>
    /// The billing address.
    /// </summary>
    public const string BillingAddress = "payment.billing_address";

    /// <summary>
    /// The payment.
    /// </summary>
    public const string Payment = "payment";

    /// <summary>
    /// Returns the name of a line item, by its place in the agent's list.
    /// </summary>
    /// <param name="index">The line's place, from zero.</param>
    public static string LineItem(int index)
        => $"line_items[{index}]";

    /// <summary>
    /// Returns the name of a discount code, by its place in the agent's list.
    /// </summary>
    /// <param name="index">The code's place, from zero.</param>
    public static string DiscountCode(int index)
        => $"discounts.codes[{index}]";
}
