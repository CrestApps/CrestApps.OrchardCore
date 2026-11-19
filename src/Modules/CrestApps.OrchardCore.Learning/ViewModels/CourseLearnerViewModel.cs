using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// The learner's block on a course page: where they stand, what they can do next, and the outline.
/// </summary>
public class CourseLearnerViewModel
{
    public CoursePart Part { get; set; }

    public string CourseId { get; set; }

    public bool SignedIn { get; set; }

    public bool CanSelfEnroll { get; set; }

    public bool IsStaff { get; set; }

    public Enrollment Enrollment { get; set; }

    public EnrollmentProgress Progress { get; set; }

    public CourseOutline Outline { get; set; }

    public IDictionary<string, string> KindIcons { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets where the course is between its start and end dates.
    /// </summary>
    public CourseSchedule Schedule { get; set; }

    /// <summary>
    /// Gets or sets the course's categories, each as its path of names, such as "Leadership > Coaching".
    /// </summary>
    public IList<string> Categories { get; set; } = [];
}
