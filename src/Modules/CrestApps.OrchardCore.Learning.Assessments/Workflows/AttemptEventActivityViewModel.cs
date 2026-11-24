using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Learning.Assessments.Workflows;

public class AttemptEventActivityViewModel
{
    public string[] QuizIds { get; set; } = [];

    public bool IncludeWorkflowChanges { get; set; }

    [BindNever]
    public IList<SelectListItem> QuizOptions { get; set; } = [];
}
