using CrestApps.OrchardCore.AI.Deployments.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.DataSources.Deployments;

internal sealed class AIDataSourceDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, AIDataSourceDeploymentStep>
{
    private readonly ICatalog<AIDataSource> _store;

    internal readonly IStringLocalizer S;

    public AIDataSourceDeploymentStepDisplayDriver(
        ICatalog<AIDataSource> store,
        IStringLocalizer<AIDataSourceDeploymentStepDisplayDriver> stringLocalizer)
    {
        _store = store;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AIDataSourceDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("AIDataSourceDeploymentStep_Summary", step).Location("Summary", "Content"),
                View("AIDataSourceDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(AIDataSourceDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<AIDataSourceDeploymentStepViewModel>("AIDataSourceDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.DataSources = (await _store.GetAllAsync()).Select(x => new AIDataSourceEntryViewModel
            {
                ItemId = x.ItemId,
                DisplayText = x.DisplayText,
                IsSelected = step.SourceIds?.Contains(x.ItemId) ?? false
            }).OrderBy(x => x.DisplayText)
            .ToArray();
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDataSourceDeploymentStep step, UpdateEditorContext context)
    {
        var model = new AIDataSourceDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix,
            p => p.IncludeAll,
            p => p.DataSources);

        if (model.IncludeAll)
        {
            step.IncludeAll = true;
            step.SourceIds = [];
        }
        else
        {
            if (model.DataSources == null || model.DataSources.Length == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.DataSources), S["At least one data-source is required."]);
            }

            step.IncludeAll = false;
            step.SourceIds = model.DataSources.Where(x => x.IsSelected).Select(x => x.ItemId).ToArray();
        }

        return Edit(step, context);
    }
}
