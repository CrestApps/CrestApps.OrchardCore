namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// The names of the emails about orders a store can write its own templates of. Each is registered as a commerce email
/// kind by the feature that sends it.
/// </summary>
public static class OrderEmailKinds
{
    public const string Confirmation = "order-confirmation";

    public const string Invoice = "order-invoice";

    public const string Quote = "order-quote";

    public const string Shipped = "order-shipped";

    public const string Return = "order-return";

    public const string GiftCard = "gift-card";

    public const string Recovery = "checkout-recovery";
}
