namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// Marks a checkout as a sale rung up at a store's register. Staff run it, so it belongs to them rather than the
/// buyer: what follows the checkout's owner (their store credit, their points) doesn't apply to it.
/// </summary>
public sealed class CheckoutCounterSale
{
    public string RegisterId { get; set; }

    public string ShiftId { get; set; }

    public string LocationId { get; set; }
}
