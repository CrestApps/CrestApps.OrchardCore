namespace CrestApps.OrchardCore.Telephony.Sms.ViewModels;

/// <summary>
/// The edit view model for the SMS portal site settings.
/// </summary>
public class SmsPortalSettingsViewModel
{
    /// <summary>
    /// Gets or sets the technical name of the default SMS provider used when a number does not pin one.
    /// </summary>
    public string DefaultProviderName { get; set; }
}
