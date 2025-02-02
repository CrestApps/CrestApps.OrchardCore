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

    internal readonly IStringLocalizer S;

    public AIDeploymentDisplayDriver(
        IOptions<AIProviderOptions> providerOptions,
        IStringLocalizer<AIDeploymentDisplayDriver> stringLocalizer)
    {
        _providerOptions = providerOptions.Value;
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

            if (_providerOptions.Providers.TryGetValue(deployment.Source, out var providerOptions))
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
        if (!context.IsNew)
        {
            return Edit(deployment, context);
        }

        var model = new EditDeploymentViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var name = model.Name?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Name is required."]);
        }

        if (!_providerOptions.Providers.TryGetValue(deployment.Source, out var providerOptions))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConnectionName), S["There are no configured connection for the source: {0}.", deployment.Source]);
        }
        else
        {
            if (string.IsNullOrEmpty(model.ConnectionName))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConnectionName), S["Connection name is required."]);
            }
            else
            {
                var connection = providerOptions.Connections.FirstOrDefault(x => x.Key != null && x.Key.Equals(model.ConnectionName, StringComparison.OrdinalIgnoreCase));

                if (connection.Value == null)
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConnectionName), S["Invalid connection name provided."]);
                }
                else
                {
                    deployment.ConnectionName = connection.Key;
                }
            }
        }

        deployment.Name = name;

        return Edit(deployment, context);
    }
}
