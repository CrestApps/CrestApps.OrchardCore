namespace CrestApps.OrchardCore.Customers.Core.Models;

/// <summary>
/// One thing done with a customer's personal data, kept for accountability.
/// </summary>
public sealed class CustomerPrivacyEntry
{
    public DateTime Utc { get; set; }

    public CustomerPrivacyAction Action { get; set; }

    /// <summary>
    /// Gets or sets who did it: an administrator's user name, or empty for the retention task.
    /// </summary>
    public string By { get; set; }
}

public enum CustomerPrivacyAction
{
    /// <summary>
    /// An administrator downloaded everything the store holds on the customer.
    /// </summary>
    Exported,

    /// <summary>
    /// An administrator erased the customer's personal data.
    /// </summary>
    Erased,

    /// <summary>
    /// The retention task erased a guest's personal data after the retention window.
    /// </summary>
    Expired,
}
