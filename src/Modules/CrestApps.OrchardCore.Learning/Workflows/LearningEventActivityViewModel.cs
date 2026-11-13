using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Learning.Workflows;

/// <summary>
/// The editor of a Learning workflow event.
/// </summary>
public class LearningEventActivityViewModel
{
    public string[] CourseIds { get; set; } = [];

    public bool IncludeWorkflowChanges { get; set; }

    [BindNever]
    public IList<SelectListItem> CourseOptions { get; set; } = [];
}
