using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Deployments.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Deployments.Drivers;

internal sealed class AIProviderConnectionDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, AIProviderConnectionDeploymentStep>
{
    private readonly INamedModelStore<AIProviderConnection> _store;

    internal readonly IStringLocalizer S;

    public AIProviderConnectionDeploymentStepDisplayDriver(
        INamedModelStore<AIProviderConnection> store,
        IStringLocalizer<AIProviderConnectionDeploymentStepDisplayDriver> stringLocalizer)
    {
        _store = store;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AIProviderConnectionDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("AIProviderConnectionDeploymentStep_Summary", step).Location("Summary", "Content"),
                View("AIProviderConnectionDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(AIProviderConnectionDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<AIProviderConnectionDeploymentStepViewModel>("AIProviderConnectionDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.Connections = (await _store.GetAllAsync()).Select(x => new AIProviderConnectionEntryViewModel
            {
                Id = x.Id,
                DisplayText = x.DisplayText,
                IsSelected = step.ConnectionIds?.Contains(x.Id) ?? false
            }).OrderBy(x => x.DisplayText)
            .ToArray();
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProviderConnectionDeploymentStep step, UpdateEditorContext context)
    {
        var model = new AIProviderConnectionDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix,
            p => p.IncludeAll,
            p => p.Connections);

        if (model.IncludeAll)
        {
            step.IncludeAll = true;
            step.ConnectionIds = [];
        }
        else
        {
            if (model.Connections == null || model.Connections.Length == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Connections), S["At least one connection is required."]);
            }

            step.IncludeAll = false;
            step.ConnectionIds = model.Connections.Where(x => x.IsSelected).Select(x => x.Id).ToArray();
        }

        return Edit(step, context);
    }
}
