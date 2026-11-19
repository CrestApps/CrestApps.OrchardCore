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
}
