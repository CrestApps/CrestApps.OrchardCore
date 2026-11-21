using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Surveys.Deployments;

/// <summary>
/// Shows and edits the survey templates deployment step.
/// </summary>
internal sealed class SurveyTemplatesDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, SurveyTemplatesDeploymentStep>
{
    private readonly SurveyTemplateStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyTemplatesDeploymentStepDisplayDriver"/> class.
    /// </summary>
    /// <param name="store">The template store.</param>
    public SurveyTemplatesDeploymentStepDisplayDriver(SurveyTemplateStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public override IDisplayResult Display(SurveyTemplatesDeploymentStep step, BuildDisplayContext context)
        => Combine(
            View("SurveyTemplatesDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("SurveyTemplatesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    /// <inheritdoc />
    public override IDisplayResult Edit(SurveyTemplatesDeploymentStep step, BuildEditorContext context)
        => Initialize<SurveysDeploymentStepViewModel>("SurveyTemplatesDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.Names = step.TemplateNames ?? [];
            model.Options = (await _store.ListOrderedAsync())
                .Select(template => new SelectListItem(template.Title, template.Name, model.Names.Contains(template.Name, StringComparer.OrdinalIgnoreCase)))
                .ToList();
        }).Location("Content");

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(SurveyTemplatesDeploymentStep step, UpdateEditorContext context)
    {
        var model = new SurveysDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, value => value.IncludeAll, value => value.Names);

        step.IncludeAll = model.IncludeAll;
        step.TemplateNames = model.IncludeAll ? [] : (model.Names ?? []).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        return Edit(step, context);
    }
}
