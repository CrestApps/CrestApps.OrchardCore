namespace CrestApps.OrchardCore.Learning.AuditTrail;

/// <summary>
/// What a Learning audit event records. People are referred to by user id only.
/// </summary>
public sealed class LearningAuditData
{
    /// <summary>
    /// Gets or sets the enrollment.
    /// </summary>
    public string EnrollmentId { get; set; }

    /// <summary>
    /// Gets or sets the enrollment number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the user id of the learner.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the course.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the course item, for an item event.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the value before the change.
    /// </summary>
    public string From { get; set; }

    /// <summary>
    /// Gets or sets the value after the change.
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets why.
    /// </summary>
    public string Reason { get; set; }
}
