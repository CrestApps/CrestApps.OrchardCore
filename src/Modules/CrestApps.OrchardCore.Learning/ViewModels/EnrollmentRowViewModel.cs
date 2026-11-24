using CrestApps.OrchardCore.Learning.Models;

namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// A row of the enrollment list.
/// </summary>
public class EnrollmentRowViewModel
{
    public Enrollment Enrollment { get; set; }

    public string LearnerName { get; set; }

    public string CourseTitle { get; set; }
}
