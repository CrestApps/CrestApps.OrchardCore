namespace CrestApps.OrchardCore.Banking.ViewModels;

/// <summary>
/// The page that shows a full account number once.
/// </summary>
public class BankAccountRevealViewModel
{
    /// <summary>
    /// Gets or sets the account's id.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the account's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the routing number.
    /// </summary>
    public string RoutingNumber { get; set; }

    /// <summary>
    /// Gets or sets the full account number.
    /// </summary>
    public string AccountNumber { get; set; }
}
