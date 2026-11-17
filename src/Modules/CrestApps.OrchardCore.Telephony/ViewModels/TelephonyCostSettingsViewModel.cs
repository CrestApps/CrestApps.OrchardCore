namespace CrestApps.OrchardCore.Telephony.ViewModels;

/// <summary>
/// View model for editing what the phone carrier charges.
/// </summary>
public class TelephonyCostSettingsViewModel
{
    /// <summary>
    /// Gets or sets the currency the carrier's prices are in.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets what a minute of a phone call costs with the carrier.
    /// </summary>
    public decimal PerMinute { get; set; }
}
