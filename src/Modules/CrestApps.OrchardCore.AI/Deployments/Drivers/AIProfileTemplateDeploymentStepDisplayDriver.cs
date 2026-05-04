using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Deployments.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Deployments.Drivers;

internal sealed class AIProfileTemplateDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, AIProfileTemplateDeploymentStep>
{
    private readonly INamedCatalog<AIProfileTemplate> _templatesCatalog;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplateDeploymentStepDisplayDriver"/> class.
    /// </summary>
    /// <param name="templatesCatalog">The templates catalog.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileTemplateDeploymentStepDisplayDriver(
        INamedCatalog<AIProfileTemplate> templatesCatalog,
        IStringLocalizer<AIProfileTemplateDeploymentStepDisplayDriver> stringLocalizer)
    {
        _templatesCatalog = templatesCatalog;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AIProfileTemplateDeploymentStep step, BuildDisplayContext context)
    {
        return
        CombineAsync(
            View("AIProfileTemplateDeploymentStep_Summary", step).Location("Summary", "Content"),
        View("AIProfileTemplateDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
        );
    }

    public override IDisplayResult Edit(AIProfileTemplateDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<AIProfileTemplateDeploymentStepViewModel>("AIProfileTemplateDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.TemplateNames = step.TemplateNames;
            model.AllTemplateNames = (await _templatesCatalog.GetAllAsync()).Select(x => x.Name).Order().ToArray();
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplateDeploymentStep step, UpdateEditorContext context)
    {
        var model = new AIProfileTemplateDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix,
        p => p.IncludeAll,
        p => p.TemplateNames);

        if (model.IncludeAll)
        {
            step.IncludeAll = true;
            step.TemplateNames = [];
        }
        else
        {
            if (model.TemplateNames == null || model.TemplateNames.Length == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.TemplateNames), S["At least one template name is required."]);
            }

            step.IncludeAll = false;
            step.TemplateNames = model.TemplateNames;
        }

        return Edit(step, context);
    }
}
