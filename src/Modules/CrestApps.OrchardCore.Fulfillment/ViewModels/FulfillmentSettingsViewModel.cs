namespace CrestApps.OrchardCore.Fulfillment.ViewModels;

/// <summary>
/// The fulfillment settings editor.
/// </summary>
public class FulfillmentSettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether paid orders are sent to the partners by themselves.
    /// </summary>
    public bool SendAutomatically { get; set; }
}
