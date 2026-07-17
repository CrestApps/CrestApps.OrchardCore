using System.Collections.Generic;

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
}
