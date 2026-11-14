namespace CrestApps.OrchardCore.Workforce.Core;

/// <summary>
/// Limits on the devices workers turn push notifications on for. Read from the tenant's
/// <c>CrestApps_Workforce:WebPush</c> configuration section.
/// </summary>
public sealed class WebPushOptions
{
    /// <summary>
    /// The configuration section the options are read from.
    /// </summary>
    public const string SectionName = "CrestApps_Workforce:WebPush";

    /// <summary>
    /// Gets or sets how many devices one worker may have. Turning push on for one more replaces the oldest.
    /// </summary>
    public int MaxSubscriptionsPerWorker { get; set; } = 10;

    /// <summary>
    /// Gets or sets the push services a device may name, by host (<c>fcm.googleapis.com</c>) or by domain
    /// (<c>*.push.apple.com</c>). Empty accepts any public HTTPS host.
    /// </summary>
    public IList<string> AllowedHosts { get; set; } = [];
}
