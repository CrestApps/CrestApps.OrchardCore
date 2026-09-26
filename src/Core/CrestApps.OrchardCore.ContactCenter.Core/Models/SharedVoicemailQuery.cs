using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Describes a page of messages to read from the queue shared voicemail boxes.
/// </summary>
public sealed class SharedVoicemailQuery
{
    /// <summary>
    /// Gets or sets the queues to read from, or <see langword="null"/> for every queue. An empty list reads nothing.
    /// </summary>
    public IReadOnlyCollection<string> QueueIds { get; set; }

    /// <summary>
    /// Gets or sets the one status to read, or <see langword="null"/> to read by <see cref="IncludeResolved"/>.
    /// </summary>
    public SharedVoicemailStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether messages already dealt with are read when no single
    /// <see cref="Status"/> is asked for. By default only the messages still waiting on the team are read.
    /// </summary>
    public bool IncludeResolved { get; set; }

    /// <summary>
    /// Gets or sets the one-based page number.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of messages on a page.
    /// </summary>
    public int PageSize { get; set; } = 20;
}
