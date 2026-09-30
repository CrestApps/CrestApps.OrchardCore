namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Says whether the CRM feature (leads, accounts and opportunities) is enabled. It is switched on by that feature,
/// so code outside it can tell leads from contacts only when leads exist as a concept. While it is off, a content
/// type carrying <see cref="LeadPart"/> is treated as a plain contact type, exactly as before the feature existed.
/// </summary>
public sealed class OmnichannelCrmOptions
{
    /// <summary>
    /// Gets or sets whether the CRM feature is enabled.
    /// </summary>
    public bool Enabled { get; set; }
}
