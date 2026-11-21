namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// A completed enrollment that needs a person to look at it, because a result it rests on changed after completion,
/// such as an exam attempt voided for cheating or regraded below the pass mark. The enrollment stays Completed; the
/// reviewer decides what follows (a retake, or in the certification module, revoking the certificate).
/// </summary>
public sealed class EnrollmentReview
{
    /// <summary>
    /// Gets or sets why the enrollment needs review, a code such as <c>ResultChangedAfterCompletion</c>.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the course item whose result changed.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the score the item had, or <see langword="null"/>.
    /// </summary>
    public decimal? PreviousScore { get; set; }

    /// <summary>
    /// Gets or sets the score the item has now, or <see langword="null"/> when it was voided.
    /// </summary>
    public decimal? NewScore { get; set; }

    /// <summary>
    /// Gets or sets when the enrollment was flagged, in UTC.
    /// </summary>
    public DateTime FlaggedUtc { get; set; }

    /// <summary>
    /// Gets or sets when a person cleared the review, in UTC, or <see langword="null"/> while it is open.
    /// </summary>
    public DateTime? ClearedUtc { get; set; }

    /// <summary>
    /// Gets or sets who cleared the review.
    /// </summary>
    public string ClearedBy { get; set; }

    /// <summary>
    /// Gets or sets what the reviewer decided.
    /// </summary>
    public string ClearNote { get; set; }
}
