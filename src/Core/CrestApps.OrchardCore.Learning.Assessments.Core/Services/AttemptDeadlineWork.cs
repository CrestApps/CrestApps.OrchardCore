using CrestApps.Core.Models;
using CrestApps.OrchardCore.WorkQueues;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// The leased work of submitting an attempt at its deadline. It is due at the deadline, and survives restarts and runs
/// on one node only.
/// </summary>
public sealed class AttemptDeadlineWork : CatalogItem, ILeasedWorkItem
{
    /// <summary>
    /// The collection of the deadline queue.
    /// </summary>
    public const string CollectionName = "LearningAttemptDeadlines";

    /// <summary>
    /// Gets or sets the attempt.
    /// </summary>
    public string AttemptId { get; set; }

    /// <inheritdoc />
    public LeasedWorkItemStatus Status { get; set; }

    /// <inheritdoc />
    public string OwnerToken { get; set; }

    /// <inheritdoc />
    public long FenceToken { get; set; }

    /// <inheritdoc />
    public int AttemptCount { get; set; }

    /// <inheritdoc />
    public DateTime NextAttemptUtc { get; set; }

    /// <inheritdoc />
    public string LastError { get; set; }
}
