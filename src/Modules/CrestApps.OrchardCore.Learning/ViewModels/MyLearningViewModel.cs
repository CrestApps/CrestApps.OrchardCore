namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// My learning: the learner's courses as a course overview, with the state filter, the search and the view chosen.
/// </summary>
public class MyLearningViewModel
{
    /// <summary>
    /// Gets or sets the courses shown, after the filter and the search.
    /// </summary>
    public IList<LearnerCourseViewModel> Courses { get; set; } = [];

    /// <summary>
    /// Gets or sets how many of the learner's courses are in each state (the keys of <see cref="LearnerCourseStates"/>),
    /// and in all of them under <c>all</c>.
    /// </summary>
    public Dictionary<string, int> Counts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the state filter: <c>all</c>, <c>inprogress</c>, <c>notstarted</c> or <c>completed</c>.
    /// </summary>
    public string Status { get; set; } = LearnerCourseStates.All;

    /// <summary>
    /// Gets or sets the text the course titles and codes are searched for.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the view: <c>cards</c> or <c>list</c>.
    /// </summary>
    public string View { get; set; } = "cards";
}
