namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// View model for the Contact Center external transfer settings page.
/// </summary>
public class ContactCenterExternalTransferSettingsViewModel
{
    /// <summary>
    /// Gets or sets the list of approved external transfer destinations shown and edited on
    /// the settings page.
    /// </summary>
    public List<ContactCenterExternalDestinationViewModel> Destinations { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether agents may transfer to numbers that are not in <see cref="Destinations"/>.
    /// </summary>
    public bool AllowUnlistedNumbers { get; set; }
}
