namespace CrestApps.OrchardCore.PayLater.ViewModels;

/// <summary>
/// The editor view model for the Pay Later settings.
/// </summary>
public class PayLaterSettingsViewModel
{
    /// <summary>
    /// Gets or sets the number of days a Pay Later balance is allowed before it is due.
    /// </summary>
    public int NetTermDays { get; set; }
}
