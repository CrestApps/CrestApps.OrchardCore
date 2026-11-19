namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// The learner's courses: in progress, waiting for approval and completed.
/// </summary>
public class MyLearningViewModel
{
    public IList<LearnerCourseViewModel> Current { get; set; } = [];

    public IList<LearnerCourseViewModel> Completed { get; set; } = [];

    /// <summary>
    /// Gets or sets the open enrollments in courses whose end date has passed, which stay open.
    /// </summary>
    public IList<LearnerCourseViewModel> Past { get; set; } = [];

    public IList<LearnerCourseViewModel> Other { get; set; } = [];
}
