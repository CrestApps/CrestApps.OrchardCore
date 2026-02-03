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

internal sealed class McpPromptDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, McpPromptDeploymentStep>
{
    private readonly ICatalog<McpPrompt> _store;

    internal readonly IStringLocalizer S;

    public McpPromptDeploymentStepDisplayDriver(
        ICatalog<McpPrompt> store,
        IStringLocalizer<McpPromptDeploymentStepDisplayDriver> stringLocalizer)
    {
        _store = store;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(McpPromptDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                Initialize<DisplayMcpPromptDeploymentStepViewModel>("McpPromptDeploymentStep_Summary", async model =>
                {
                    if (step.IncludeAll)
                    {
                        model.IncludeAll = true;
                        model.Names = [];
                    }
                    else
                    {
                        model.Names = (await _store.GetAllAsync())
                        .Where(x => step.PromptIds.Contains(x.ItemId))
                        .Select(x => x.DisplayText);
                    }
                }).Location("Summary", "Content"),
                View("McpPromptDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(McpPromptDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<McpPromptStepViewModel>("McpPromptDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.Prompts = (await _store.GetAllAsync())
            .Select(prompt => new SelectListItem(prompt.DisplayText, prompt.ItemId)
            {
                Selected = step.PromptIds is not null && step.PromptIds.Contains(prompt.ItemId),
            }).ToArray();
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(McpPromptDeploymentStep step, UpdateEditorContext context)
    {
        var model = new McpPromptStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix,
            p => p.IncludeAll,
            p => p.Prompts);

        if (model.IncludeAll)
        {
            step.IncludeAll = true;
            step.PromptIds = [];
        }
        else
        {
            if (model.Prompts == null || !model.Prompts.Any(x => x.Selected))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Prompts), S["At least one prompt is required."]);
            }

            step.IncludeAll = false;
            step.PromptIds = model.Prompts.Where(x => x.Selected).Select(x => x.Value).ToArray();
        }

        return Edit(step, context);
    }
}
