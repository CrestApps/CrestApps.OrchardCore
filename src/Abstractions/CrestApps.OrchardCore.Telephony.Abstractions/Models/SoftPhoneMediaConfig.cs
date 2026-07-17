namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Describes media preferences for the browser soft phone.
/// </summary>
public sealed class SoftPhoneMediaConfig
{
    /// <summary>
    /// Gets or sets the preferred audio codecs.
    /// </summary>
    public IList<string> Codecs { get; set; } = [];
}
