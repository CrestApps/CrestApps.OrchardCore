namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// Tells the access and settings page of a survey owned by a course item whether that item counts toward the course
/// grade, and whether the survey is anonymous.
/// </summary>
public class SurveyCourseGradeViewModel
{
    /// <summary>
    /// Gets or sets whether the course item that owns the survey counts toward the course grade.
    /// </summary>
    public bool Gradable { get; set; }

    /// <summary>
    /// Gets or sets whether the survey is anonymous.
    /// </summary>
    public bool Anonymous { get; set; }
}
