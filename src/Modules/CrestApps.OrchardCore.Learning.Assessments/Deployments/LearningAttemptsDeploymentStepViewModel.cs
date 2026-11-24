using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Learning.Assessments.Deployments;

public class LearningAttemptsDeploymentStepViewModel
{
    public string QuizId { get; set; }

    public bool IncludeOverrides { get; set; }

    [BindNever]
    public IList<SelectListItem> QuizOptions { get; set; } = [];
}
