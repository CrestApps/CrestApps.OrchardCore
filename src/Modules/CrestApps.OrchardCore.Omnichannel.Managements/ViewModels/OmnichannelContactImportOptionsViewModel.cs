namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// View model for the omnichannel contact import options UI.
/// </summary>
public class OmnichannelContactImportOptionsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether to ignore duplicate contacts based on phone number.
    /// </summary>
    public bool IgnoreDuplicateByPhoneNumber { get; set; } = true;
}
