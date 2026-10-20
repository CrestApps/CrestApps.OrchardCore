namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// The number the buyer knows what a checkout pays for by, such as an order's number, kept on the checkout so a client
/// that can't read the order (a guest over the commerce API) can still show it.
/// </summary>
public sealed class CheckoutReferenceNumber
{
    public string Number { get; set; }
}
