using CrestApps.Core.Models;
using CrestApps.OrchardCore.WorkQueues;

namespace CrestApps.OrchardCore.Learning.Certifications.Core.Services;

/// <summary>
/// A renewal to open on a day: the leased work item the upkeep claims when the window opens.
/// </summary>
public sealed class CertificationRenewalWork : CatalogItem, ILeasedWorkItem
{
    /// <summary>
    /// Gets or sets the progress whose renewal opens.
    /// </summary>
    public string ProgressId { get; set; }

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
}

/// <summary>
/// When the upkeep last looked for people who met a path's requirements, per path.
/// </summary>
public sealed class CertificationUpkeepState
{
    /// <summary>
    /// Gets or sets the document id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the last look per path.
    /// </summary>
    public Dictionary<string, DateTime> LastCheckedUtc { get; set; } = new(StringComparer.Ordinal);
}
