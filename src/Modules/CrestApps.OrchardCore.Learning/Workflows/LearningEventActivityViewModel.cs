using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Learning.Workflows;

/// <summary>
/// The editor of a Learning workflow event.
/// </summary>
public class LearningEventActivityViewModel
{
    public string[] CourseIds { get; set; } = [];

    public bool IncludeWorkflowChanges { get; set; }

    /// <summary>
    /// Gets or sets the chosen courses, as the <c>[{ value, text }]</c> JSON the course picker starts with.
    /// </summary>
    [BindNever]
    public string CoursesJson { get; set; } = "[]";
}
