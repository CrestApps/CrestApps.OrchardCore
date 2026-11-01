namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A worker's portal settings, kept in the worker's properties: which channels they hear on, and the secret of their
/// calendar feed.
/// </summary>
public sealed class WorkerPortalSettings
{
    /// <summary>
    /// Gets or sets the channels the worker turned off, by name.
    /// </summary>
    public IList<string> DisabledChannels { get; set; } = [];

    /// <summary>
    /// Gets or sets the kinds of notification the worker turned off.
    /// </summary>
    public IList<WorkforceNotificationKind> DisabledKinds { get; set; } = [];

    /// <summary>
    /// Gets or sets the secret in the worker's calendar feed address; empty until they ask for one.
    /// </summary>
    public string FeedToken { get; set; }
}
