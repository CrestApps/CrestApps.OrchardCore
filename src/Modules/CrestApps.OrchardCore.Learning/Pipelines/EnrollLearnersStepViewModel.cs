using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Learning.Pipelines;

/// <summary>
/// The designer editor of the Enroll learners step.
/// </summary>
public class EnrollLearnersStepViewModel
{
    public string UserIdField { get; set; }

    public string CourseId { get; set; }

    public string CourseIdField { get; set; }

    public string DueField { get; set; }

    public string AccessEndsField { get; set; }

    public bool IgnorePrerequisites { get; set; }

    [BindNever]
    public string CourseTitle { get; set; }

    [BindNever]
    public IList<SelectListItem> CourseOptions { get; set; } = [];
}
