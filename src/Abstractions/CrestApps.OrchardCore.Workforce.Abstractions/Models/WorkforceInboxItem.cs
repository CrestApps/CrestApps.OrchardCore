using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A notification kept in the worker's portal inbox.
/// </summary>
public sealed class WorkforceInboxItem : CatalogItem
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
    /// Gets or sets the page it opens.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it was sent.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the worker read it.
    /// </summary>
    public DateTime? ReadUtc { get; set; }
}
