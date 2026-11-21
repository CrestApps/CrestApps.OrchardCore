using CrestApps.OrchardCore.Learning.Models;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// One of the learner's enrollments, with the course as it is now or as the completion record kept it.
/// </summary>
public class LearnerCourseViewModel
{
    public Enrollment Enrollment { get; set; }

    public string Title { get; set; }

    public string Code { get; set; }

    public RouteValueDictionary CourseRouteValues { get; set; }

    public bool CourseAvailable { get; set; }

    /// <summary>
    /// Gets or sets when the course starts, in UTC.
    /// </summary>
    public DateTime? StartsUtc { get; set; }

    /// <summary>
    /// Gets or sets when the course ends, in UTC.
    /// </summary>
    public DateTime? EndsUtc { get; set; }

    /// <summary>
    /// Gets or sets where the course is between its dates.
    /// </summary>
    public CourseSchedule Schedule { get; set; }

    /// <summary>
    /// Gets or sets the public address of the course's cover picture, or <see langword="null"/> without one.
    /// </summary>
    public string CoverUrl { get; set; }

    /// <summary>
    /// Gets or sets the course's first category, as its path of names.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets how many items count toward completion.
    /// </summary>
    public int RequiredCount { get; set; }

    /// <summary>
    /// Gets or sets how many of the items that count toward completion the learner completed.
    /// </summary>
    public int CompletedCount { get; set; }

    /// <summary>
    /// Gets or sets the state My learning filters by, one of <see cref="LearnerCourseStates"/>.
    /// </summary>
    public string State { get; set; }
}
