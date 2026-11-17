namespace CrestApps.OrchardCore.Fulfillment.ViewModels;

/// <summary>
/// One fulfillment partner on the settings page.
/// </summary>
public class FulfillmentPartnerEntry
{
    /// <summary>
    /// Gets or sets the name shown to people.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the store saved credentials and chose an account for the partner.
    /// </summary>
    public bool IsConnected { get; set; }
}
