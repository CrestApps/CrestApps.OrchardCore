namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// The learner's courses: in progress, waiting for approval and completed.
/// </summary>
public class MyLearningViewModel
{
    public IList<LearnerCourseViewModel> Current { get; set; } = [];

    public IList<LearnerCourseViewModel> Completed { get; set; } = [];

    public IList<LearnerCourseViewModel> Other { get; set; } = [];
}
