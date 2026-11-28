using CrestApps.Core.Models;
using CrestApps.OrchardCore.WorkQueues;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// A response to submit at its deadline, on the leased work queue so a restart or a second node never loses or doubles
/// one.
/// </summary>
public sealed class SurveyDeadlineWork : CatalogItem, ILeasedWorkItem
{
    /// <summary>
    /// The YesSql collection of the deadline work.
    /// </summary>
    public const string CollectionName = "SurveyDeadlines";

    /// <summary>
    /// Gets or sets the id of the response.
    /// </summary>
    public string ResponseId { get; set; }

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
