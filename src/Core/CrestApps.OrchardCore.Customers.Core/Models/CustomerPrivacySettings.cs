namespace CrestApps.OrchardCore.Customers.Core.Models;

/// <summary>
/// How long the store keeps guests' personal data, under Settings › Commerce › Customers. Zero keeps it.
/// </summary>
public sealed class CustomerPrivacySettings
{
    /// <summary>
    /// Gets or sets the months after a guest customer's last activity when their personal data is erased.
    /// </summary>
    public int InactiveGuestMonths { get; set; } = 24;

    /// <summary>
    /// Gets or sets the months after a guest's order was placed when its name, email, phone and billing address are
    /// removed. The lines, totals and dates stay.
    /// </summary>
    public int GuestOrderMonths { get; set; } = 12;
}
