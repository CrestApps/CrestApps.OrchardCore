namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// Who is paying, as the card-testing protection counts them: the network the payment was begun from, stored on the
/// checkout when a payment begins, so a decline reported later (by a webhook or the reconciliation sweep) is counted
/// against the same network.
/// </summary>
public sealed class CheckoutClientInfo
{
    /// <summary>
    /// Gets or sets the client's network key: its IPv4 address, or the /64 of its IPv6 address. Never shown to anyone.
    /// </summary>
    public string NetworkKey { get; set; }
}
