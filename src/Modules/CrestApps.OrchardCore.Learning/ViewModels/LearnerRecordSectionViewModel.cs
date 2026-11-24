using CrestApps.OrchardCore.Learning.Models;

namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// The Learning section of the user editor and the staff profile.
/// </summary>
public class LearnerRecordSectionViewModel
{
    /// <summary>
    /// Gets or sets the user id.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets how many enrollments the person has.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Gets or sets how many are completed.
    /// </summary>
    public int Completed { get; set; }

    /// <summary>
    /// Gets or sets how many are open.
    /// </summary>
    public int Open { get; set; }

    /// <summary>
    /// Gets the newest enrollments.
    /// </summary>
    public IList<LearnerRecordRowViewModel> Rows { get; } = [];
}

/// <summary>
/// One enrollment in the Learning section.
/// </summary>
public class LearnerRecordRowViewModel
{
    /// <summary>
    /// Gets or sets the enrollment id.
    /// </summary>
    public string EnrollmentId { get; set; }

    /// <summary>
    /// Gets or sets the enrollment number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the course title.
    /// </summary>
    public string CourseTitle { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public EnrollmentStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the progress, from 0 to 100.
    /// </summary>
    public int ProgressPercent { get; set; }

    /// <summary>
    /// Gets or sets the due date, in the viewer's time zone.
    /// </summary>
    public string Due { get; set; }

    /// <summary>
    /// Gets or sets the completion date, in the viewer's time zone.
    /// </summary>
    public string Completed { get; set; }
}
