namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// My courses in the user menu: the learner's active courses.
/// </summary>
public class LearningUserMenuViewModel
{
    /// <summary>
    /// Gets or sets the active courses, newest enrollment first, at most the number the menu shows.
    /// </summary>
    public IList<LearningUserMenuCourse> Courses { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the learner has more active courses than the menu shows.
    /// </summary>
    public bool HasMore { get; set; }
}

/// <summary>
/// A course in My courses.
/// </summary>
public class LearningUserMenuCourse
{
    /// <summary>
    /// Gets or sets the course's content item id.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the course's title.
    /// </summary>
    public string Title { get; set; }
}
