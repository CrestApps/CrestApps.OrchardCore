using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Drivers;

public sealed class AIDeploymentDisplayDriver : DisplayDriver<AIDeployment>
{
    private readonly AIProviderOptions _providerOptions;
    private readonly IAIDeploymentStore _modelDeploymentStore;

    internal readonly IStringLocalizer S;

    public AIDeploymentDisplayDriver(
        IAIDeploymentStore modelDeploymentStore,
        IOptions<AIProviderOptions> providerOptions,
        IStringLocalizer<AIDeploymentDisplayDriver> stringLocalizer)
    {
        _providerOptions = providerOptions.Value;
        _modelDeploymentStore = modelDeploymentStore;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AIDeployment deployment, BuildDisplayContext context)
    {
        return CombineAsync(
            View("AIDeployment_Fields_SummaryAdmin", deployment).Location("Content:1"),
            View("AIDeployment_Buttons_SummaryAdmin", deployment).Location("Actions:5"),
            View("AIDeployment_DefaultTags_SummaryAdmin", deployment).Location("Tags:5"),
            View("AIDeployment_DefaultMeta_SummaryAdmin", deployment).Location("Meta:5")
        );
    }

    public override IDisplayResult Edit(AIDeployment deployment, BuildEditorContext context)
    {
        return Initialize<EditDeploymentViewModel>("AIDeploymentFields_Edit", model =>
        {
            model.Name = deployment.Name;
            model.ConnectionName = deployment.ConnectionName;
            model.IsNew = context.IsNew;

            if (_providerOptions.Providers.TryGetValue(deployment.ProviderName, out var providerOptions))
            {
                model.Connections = providerOptions.Connections.Select(x => new SelectListItem(x.Key, x.Key)).ToArray();

                if (string.IsNullOrEmpty(model.ConnectionName) && providerOptions.Connections.Count == 1)
                {
                    model.ConnectionName = providerOptions.Connections.First().Key;
                }
            }
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDeployment deployment, UpdateEditorContext context)
    {
        var model = new EditDeploymentViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (context.IsNew)
        {
            var name = model.Name?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Name is required."]);
            }

            deployment.Name = name;

            return Edit(deployment, context);
        }

        if (!_providerOptions.Providers.TryGetValue(deployment.ProviderName, out var provider))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConnectionName), S["There are no configured connection for the provider: {0}.", deployment.ProviderName]);
        }
        else
        {
            if (string.IsNullOrEmpty(model.ConnectionName))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConnectionName), S["Connection name is required."]);
            }
            else
            {
                if (!provider.Connections.TryGetValue(model.ConnectionName, out _))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConnectionName), S["Invalid connection name provided."]);
                }
                else
                {
                    deployment.ConnectionName = model.ConnectionName;
                }
            }
        }

        var anotherExists = (await _modelDeploymentStore.GetAllAsync())
            .Any(d => d.ProviderName.Equals(deployment.ProviderName, StringComparison.OrdinalIgnoreCase) &&
            d.ConnectionName.Equals(deployment.ConnectionName, StringComparison.OrdinalIgnoreCase) &&
            d.Name.Equals(deployment.Name, StringComparison.OrdinalIgnoreCase)
            && d.Id != deployment.Id);

        if (anotherExists)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConnectionName), S["The selected connection already has an existing deployment with the specified name."]);
        }

        return Edit(deployment, context);
    }
}
