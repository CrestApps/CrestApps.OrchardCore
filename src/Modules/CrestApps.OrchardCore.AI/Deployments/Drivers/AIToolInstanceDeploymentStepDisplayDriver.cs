using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Deployments.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Deployments.Drivers;

internal sealed class AIToolInstanceDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, AIToolInstanceDeploymentStep>
{
    private readonly INamedCatalog<AIToolInstance> _instancesCatalog;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolInstanceDeploymentStepDisplayDriver"/> class.
    /// </summary>
    /// <param name="instancesCatalog">The tool instances catalog.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolInstanceDeploymentStepDisplayDriver(
        INamedCatalog<AIToolInstance> instancesCatalog,
        IStringLocalizer<AIToolInstanceDeploymentStepDisplayDriver> stringLocalizer)
    {
        _instancesCatalog = instancesCatalog;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AIToolInstanceDeploymentStep step, BuildDisplayContext context)
    {
        return
        CombineAsync(
            View("AIToolInstanceDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("AIToolInstanceDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
        );
    }

    public override IDisplayResult Edit(AIToolInstanceDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<AIToolInstanceDeploymentStepViewModel>("AIToolInstanceDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.Instances = (await _instancesCatalog.GetAllAsync()).Select(x => new AIToolInstanceEntryViewModel
            {
                ItemId = x.ItemId,
                DisplayText = x.Name,
                IsSelected = step.InstanceIds?.Contains(x.ItemId) ?? false,
            }).OrderBy(x => x.DisplayText)
            .ToArray();
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIToolInstanceDeploymentStep step, UpdateEditorContext context)
    {
        var model = new AIToolInstanceDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix,
        p => p.IncludeAll,
        p => p.Instances);

        if (model.IncludeAll)
        {
            step.IncludeAll = true;
            step.InstanceIds = [];
        }
        else
        {
            if (model.Instances == null || model.Instances.Length == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Instances), S["At least one tool instance is required."]);
            }

            step.IncludeAll = false;
            step.InstanceIds = model.Instances.Where(x => x.IsSelected).Select(x => x.ItemId).ToArray();
        }

        return Edit(step, context);
    }
}
