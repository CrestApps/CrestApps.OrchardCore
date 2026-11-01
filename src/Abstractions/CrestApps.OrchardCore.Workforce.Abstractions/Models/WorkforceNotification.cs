namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Something a worker should hear about, such as a published schedule or approved time off.
/// </summary>
public sealed class WorkforceNotification
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets what it is about.
    /// </summary>
    public WorkforceNotificationKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the text.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the page of the portal it opens, relative to the site, such as <c>my-work/schedule</c>.
    /// </summary>
    public string Url { get; set; }
}
