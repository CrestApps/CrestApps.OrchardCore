namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for editing omnichannel contact part settings.
/// </summary>
public class OmnichannelContactPartSettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the contact time zone is required.
    /// </summary>
    public bool RequireTimeZone { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Do not call preference is available.
    /// </summary>
    public bool UseDoNotCall { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the Do not SMS preference is available.
    /// </summary>
    public bool UseDoNotSms { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Do not chat preference is available.
    /// </summary>
    public bool UseDoNotChat { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Do not email preference is available.
    /// </summary>
    public bool UseDoNotEmail { get; set; }
}
