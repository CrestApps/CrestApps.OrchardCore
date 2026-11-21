using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Learning.Deployments;

/// <summary>
/// The editor of the enrollments deployment step.
/// </summary>
public class LearningEnrollmentsDeploymentStepViewModel
{
    public string CourseId { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public bool IncludeProgress { get; set; } = true;

    [BindNever]
    public IList<SelectListItem> CourseOptions { get; set; } = [];
}
