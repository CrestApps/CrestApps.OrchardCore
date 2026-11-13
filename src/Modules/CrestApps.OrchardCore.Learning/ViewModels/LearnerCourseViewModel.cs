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
}
