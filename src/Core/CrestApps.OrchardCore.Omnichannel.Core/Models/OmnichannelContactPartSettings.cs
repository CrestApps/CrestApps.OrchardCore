namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the settings for <see cref="OmnichannelContactPart"/>.
/// </summary>
public sealed class OmnichannelContactPartSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether a time zone must be provided.
    /// </summary>
    public bool RequireTimeZone { get; set; } = true;

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
