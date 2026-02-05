using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Deployments.Steps;
using CrestApps.OrchardCore.AI.Mcp.Deployments.ViewModels;
using CrestApps.OrchardCore.AI.Mcp.ViewModels;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Mcp.Deployments.Drivers;

internal sealed class McpResourceDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, McpResourceDeploymentStep>
{
    private readonly ISourceCatalog<McpResource> _store;

    internal readonly IStringLocalizer S;

    public McpResourceDeploymentStepDisplayDriver(
        ISourceCatalog<McpResource> store,
        IStringLocalizer<McpResourceDeploymentStepDisplayDriver> stringLocalizer)
    {
        _store = store;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(McpResourceDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                Initialize<DisplayMcpResourceDeploymentStepViewModel>("McpResourceDeploymentStep_Summary", async model =>
                {
                    if (step.IncludeAll)
                    {
                        model.IncludeAll = true;
                        model.Names = [];
                    }
                    else
                    {
                        model.Names = (await _store.GetAllAsync())
                        .Where(x => step.ResourceIds.Contains(x.ItemId))
                        .Select(x => x.DisplayText);
                    }
                }).Location("Summary", "Content"),
                View("McpResourceDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(McpResourceDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<McpResourceStepViewModel>("McpResourceDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.Resources = (await _store.GetAllAsync())
            .Select(resource => new SelectListItem(resource.DisplayText, resource.ItemId)
            {
                Selected = step.ResourceIds is not null && step.ResourceIds.Contains(resource.ItemId),
            }).ToArray();
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(McpResourceDeploymentStep step, UpdateEditorContext context)
    {
        var model = new McpResourceStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix,
            p => p.IncludeAll,
            p => p.Resources);

        if (model.IncludeAll)
        {
            step.IncludeAll = true;
            step.ResourceIds = [];
        }
        else
        {
            if (model.Resources == null || !model.Resources.Any(x => x.Selected))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Resources), S["At least one resource is required."]);
            }

            step.IncludeAll = false;
            step.ResourceIds = model.Resources.Where(x => x.Selected).Select(x => x.Value).ToArray();
        }

        return Edit(step, context);
    }
}
