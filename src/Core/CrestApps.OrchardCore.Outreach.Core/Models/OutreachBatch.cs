using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.WorkQueues;

namespace CrestApps.OrchardCore.Outreach.Core.Models;

/// <summary>
/// One page of an outreach run, on the leased work queue. A batch keeps the keys of the recipients it has handled, so a
/// retried batch never sends twice to the same recipient.
/// </summary>
public sealed class OutreachBatch : CatalogItem, IModifiedUtcAwareModel, ILeasedWorkItem
{
    /// <summary>
    /// Gets or sets the run the batch belongs to.
    /// </summary>
    public string RunId { get; set; }

    /// <summary>
    /// Gets or sets the number of the batch within its run, starting at 1.
    /// </summary>
    public int Sequence { get; set; }

    /// <summary>
    /// Gets or sets the audience cursor the batch's page starts at, or <see langword="null"/> for the first page.
    /// </summary>
    public string Cursor { get; set; }

    /// <summary>
    /// Gets or sets the cursor of the page after this one, once the batch has read its page.
    /// </summary>
    public string NextCursor { get; set; }

    /// <summary>
    /// Gets or sets the keys of the recipients the batch has handled.
    /// </summary>
    public List<string> ProcessedKeys { get; set; } = [];

    /// <summary>
    /// Gets or sets how many messages the batch sent.
    /// </summary>
    public int Sent { get; set; }

    /// <summary>
    /// Gets or sets how many recipients the batch skipped.
    /// </summary>
    public int Skipped { get; set; }

    /// <summary>
    /// Gets or sets how many of the batch's sends failed.
    /// </summary>
    public int Failed { get; set; }

    /// <inheritdoc/>
    public LeasedWorkItemStatus Status { get; set; }

    /// <inheritdoc/>
    public string OwnerToken { get; set; }

    /// <inheritdoc/>
    public long FenceToken { get; set; }

    /// <inheritdoc/>
    public int AttemptCount { get; set; }

    /// <inheritdoc/>
    public DateTime NextAttemptUtc { get; set; }

    /// <inheritdoc/>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the batch was queued.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <inheritdoc/>
    public DateTime? ModifiedUtc { get; set; }
}
