using CrestApps.OrchardCore.OpenAI.Deployments.Steps;
using CrestApps.OrchardCore.OpenAI.Deployments.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.OpenAI.Deployments.Drivers;

internal sealed class OpenAIChatProfileDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, OpenAIChatProfileDeploymentStep>
{
    private readonly IOpenAIChatProfileStore _openAIChatProfileStore;

    internal readonly IStringLocalizer S;

    public OpenAIChatProfileDeploymentStepDisplayDriver(
        IOpenAIChatProfileStore openAIChatProfileStore,
        IStringLocalizer<OpenAIChatProfileDeploymentStepDisplayDriver> stringLocalizer)
    {
        _openAIChatProfileStore = openAIChatProfileStore;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(OpenAIChatProfileDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("OpenAIChatProfileDeploymentStep_Summary", step).Location("Summary", "Content"),
                View("OpenAIChatProfileDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(OpenAIChatProfileDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<OpenAIChatProfileDeploymentStepViewModel>("OpenAIChatProfileDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.ProfileNames = step.ProfileNames;
            model.AllProfileNames = (await _openAIChatProfileStore.GetAllAsync()).Select(x => x.Name).ToArray();
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(OpenAIChatProfileDeploymentStep step, UpdateEditorContext context)
    {
        var model = new OpenAIChatProfileDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix,
            p => p.IncludeAll,
            p => p.ProfileNames);

        if (model.IncludeAll)
        {
            step.IncludeAll = true;
            step.ProfileNames = [];
        }
        else
        {
            if (model.ProfileNames == null || model.ProfileNames.Length == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ProfileNames), S["At least one profile name is required."]);
            }

            step.IncludeAll = false;
            step.ProfileNames = model.ProfileNames;
        }


        return Edit(step, context);
    }
}
