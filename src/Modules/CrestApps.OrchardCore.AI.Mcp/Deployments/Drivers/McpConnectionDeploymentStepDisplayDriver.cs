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

internal sealed class McpConnectionDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, McpConnectionDeploymentStep>
{
    private readonly ISourceCatalog<McpConnection> _store;

    internal readonly IStringLocalizer S;

    public McpConnectionDeploymentStepDisplayDriver(
        ISourceCatalog<McpConnection> store,
        IStringLocalizer<McpConnectionDeploymentStepDisplayDriver> stringLocalizer)
    {
        _store = store;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(McpConnectionDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                Initialize<DisplayMcpConnectionDeploymentStepViewModel>("McpConnectionDeploymentStep_Summary", async model =>
                {
                    if (step.IncludeAll)
                    {
                        model.IncludeAll = true;
                        model.Names = [];
                    }
                    else
                    {
                        model.Names = (await _store.GetAllAsync())
                        .Where(x => step.ConnectionIds.Contains(x.Id))
                        .Select(x => x.DisplayText);
                    }
                }).Location("Summary", "Content"),
                View("McpConnectionDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(McpConnectionDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<McpConnectionStepViewModel>("McpConnectionDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.Connections = (await _store.GetAllAsync())
            .Select(connection => new SelectListItem(connection.DisplayText, connection.Id)
            {
                Selected = step.ConnectionIds is not null && step.ConnectionIds.Contains(connection.Id),
            }).ToArray();
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(McpConnectionDeploymentStep step, UpdateEditorContext context)
    {
        var model = new McpConnectionStepViewModel();

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
            if (model.Connections == null || !model.Connections.Any(x => x.Selected))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Connections), S["At least one connection is required."]);
            }

            step.IncludeAll = false;
            step.ConnectionIds = model.Connections.Where(x => x.Selected).Select(x => x.Value).ToArray();
        }

        return Edit(step, context);
    }
}
