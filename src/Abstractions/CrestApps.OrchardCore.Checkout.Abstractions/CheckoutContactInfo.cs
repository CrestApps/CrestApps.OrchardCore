namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// A provider-neutral snapshot of who to contact about a checkout. It is stored in the checkout session
/// property bag so a guest buyer, who has no user account, can still be reached for receipts and
/// outstanding-payment reminders. For an authenticated buyer this is optional because their contact
/// details resolve from their account.
/// </summary>
public sealed class CheckoutContactInfo
{
    /// <summary>
    /// Gets or sets the buyer's display name, when it was collected.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the buyer's email address, when it was collected.
    /// </summary>
    public string Email { get; set; }
}
