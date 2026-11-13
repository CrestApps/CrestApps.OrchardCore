namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// One change of an enrollment's state, kept on the enrollment with who made it and why.
/// </summary>
public sealed class EnrollmentStatusChange
{
    /// <summary>
    /// Gets or sets the state before the change.
    /// </summary>
    public EnrollmentStatus From { get; set; }

    /// <summary>
    /// Gets or sets the state after the change.
    /// </summary>
    public EnrollmentStatus To { get; set; }

    /// <summary>
    /// Gets or sets when the change was made, in UTC.
    /// </summary>
    public DateTime ChangedUtc { get; set; }

    /// <summary>
    /// Gets or sets the user id of the person who made the change, or the name of the process that made it.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets why the change was made.
    /// </summary>
    public string Reason { get; set; }
}
