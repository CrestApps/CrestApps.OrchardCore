using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Certifications.Models;

/// <summary>
/// Learning a person did elsewhere and recorded for education credits, with evidence; credits are posted when a
/// person with the permission accepts it.
/// </summary>
public sealed class ExternalLearningSubmission : CatalogItem
{
    /// <summary>
    /// Gets or sets the person.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets what the learning was.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets who provided it.
    /// </summary>
    public string Provider { get; set; }

    /// <summary>
    /// Gets or sets the day it was completed.
    /// </summary>
    public DateOnly CompletedOn { get; set; }

    /// <summary>
    /// Gets or sets the credit type.
    /// </summary>
    public string CreditTypeId { get; set; }

    /// <summary>
    /// Gets or sets the amount claimed.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the protected file of the evidence.
    /// </summary>
    public string EvidenceFileId { get; set; }

    /// <summary>
    /// Gets or sets the name of the evidence file.
    /// </summary>
    public string EvidenceFileName { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public ExternalLearningState State { get; set; }

    /// <summary>
    /// Gets or sets when it was submitted.
    /// </summary>
    public DateTime SubmittedUtc { get; set; }

    /// <summary>
    /// Gets or sets who accepted or rejected it.
    /// </summary>
    public string ReviewedBy { get; set; }

    /// <summary>
    /// Gets or sets when.
    /// </summary>
    public DateTime? ReviewedUtc { get; set; }

    /// <summary>
    /// Gets or sets the note of the review.
    /// </summary>
    public string ReviewNote { get; set; }

    /// <summary>
    /// Gets or sets the key of the row an import recorded it from, so importing the same row again records nothing.
    /// </summary>
    public string RowKey { get; set; }
}

/// <summary>
/// The state of external learning.
/// </summary>
public enum ExternalLearningState
{
    /// <summary>
    /// Waiting for a review.
    /// </summary>
    Submitted,

    /// <summary>
    /// Accepted: its credits are posted.
    /// </summary>
    Accepted,

    /// <summary>
    /// Rejected.
    /// </summary>
    Rejected,
}
