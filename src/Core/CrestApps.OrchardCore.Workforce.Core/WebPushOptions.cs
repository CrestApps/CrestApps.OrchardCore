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
    /// The browsers' push services, accepted when <see cref="AllowedHosts"/> is empty: Firebase Cloud Messaging
    /// (Chrome and other Chromium browsers), Mozilla's push service (Firefox), Apple's web push service (Safari, which
    /// Apple asks servers to allow as <c>*.push.apple.com</c>) and the Windows Push Notification Services (Edge, whose
    /// channels Microsoft keeps on <c>*.notify.windows.com</c>).
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultAllowedHosts =
    [
        "fcm.googleapis.com",
        "updates.push.services.mozilla.com",
        "*.push.apple.com",
        "*.notify.windows.com",
    ];

    /// <summary>
    /// Gets or sets the push services a device may name, by host (<c>fcm.googleapis.com</c>) or by domain
    /// (<c>*.push.apple.com</c>). Setting it replaces <see cref="DefaultAllowedHosts"/>; empty accepts those.
    /// </summary>
    public IList<string> AllowedHosts { get; set; } = [];

    /// <summary>
    /// Gets or sets whether a device may name any public HTTPS host, such as a self-hosted push service. Off by
    /// default, so the site posts only to the push services it knows.
    /// </summary>
    public bool AllowAnyHost { get; set; }
}
