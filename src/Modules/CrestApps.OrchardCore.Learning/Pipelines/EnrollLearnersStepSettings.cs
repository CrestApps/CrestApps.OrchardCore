namespace CrestApps.OrchardCore.Learning.Pipelines;

/// <summary>
/// The settings of the Enroll learners step: which input fields hold the learner, the course and the dates.
/// </summary>
public sealed class EnrollLearnersStepSettings
{
    /// <summary>
    /// Gets or sets the input field that holds the user id of the learner, such as one joined from the Users data set.
    /// </summary>
    public string UserIdField { get; set; }

    /// <summary>
    /// Gets or sets the course every row is enrolled in, when no course field is set.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the input field that holds the content item id of the course, when rows name their course.
    /// </summary>
    public string CourseIdField { get; set; }

    /// <summary>
    /// Gets or sets the input field that holds the due date, optional.
    /// </summary>
    public string DueField { get; set; }

    /// <summary>
    /// Gets or sets the input field that holds the end of access, optional.
    /// </summary>
    public string AccessEndsField { get; set; }

    /// <summary>
    /// Gets or sets whether to enroll learners who have not completed the course's prerequisites.
    /// </summary>
    public bool IgnorePrerequisites { get; set; }
}
