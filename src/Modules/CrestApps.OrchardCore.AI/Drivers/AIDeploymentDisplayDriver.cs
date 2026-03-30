using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.AI.Drivers;

internal sealed class AIDeploymentDisplayDriver : DisplayDriver<AIDeployment>
{
    private readonly AIProviderOptions _providerOptions;
    private readonly AIOptions _aiOptions;
    private readonly INamedCatalog<AIDeployment> _deploymentsCatalog;

    internal readonly IStringLocalizer S;

    public AIDeploymentDisplayDriver(
        INamedCatalog<AIDeployment> deploymentCatalog,
        IOptions<AIProviderOptions> providerOptions,
        IOptions<AIOptions> aiOptions,
        IStringLocalizer<AIDeploymentDisplayDriver> stringLocalizer)
    {
        _providerOptions = providerOptions.Value;
        _aiOptions = aiOptions.Value;
        _deploymentsCatalog = deploymentCatalog;
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
            model.SelectedTypes = deployment.Type.GetSupportedTypes().Select(static type => type.ToString()).ToArray();
            model.IsNew = context.IsNew;
            model.HasContainedConnection = HasContainedConnection(deployment.ClientName);

            model.Types = Enum.GetValues<AIDeploymentType>()
                .Where(static type => type != AIDeploymentType.None)
                .Select(t => new SelectListItem(t.ToString(), t.ToString()))
                .ToList();

            if (!model.HasContainedConnection &&
                _providerOptions.Providers.TryGetValue(deployment.ClientName, out var providerOptions))
            {
                model.Connections = providerOptions.Connections.Select(x => new SelectListItem(x.Value.GetValue<string>("ConnectionNameAlias") ?? x.Key, x.Key)).ToArray();

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
        }

        if (!TryGetSelectedTypes(model.SelectedTypes, out var deploymentTypes))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SelectedTypes), S["At least one deployment type is required."]);
        }
        else
        {
            deployment.Type = deploymentTypes;
        }

        if (HasContainedConnection(deployment.ClientName))
        {
            // Contained-connection providers manage their own connection parameters
            // in the deployment's Properties via their own display driver.
            deployment.ConnectionName = null;
            deployment.ConnectionNameAlias = null;
        }
        else if (!_providerOptions.Providers.TryGetValue(deployment.ClientName, out var provider))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConnectionName), S["There are no configured connection for the client name: {0}.", deployment.ClientName]);
        }
        else
        {
            if (string.IsNullOrEmpty(model.ConnectionName))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConnectionName), S["Connection name is required."]);
            }
            else
            {
                if (!provider.Connections.TryGetValue(model.ConnectionName, out var connection))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConnectionName), S["Invalid connection name provided."]);
                }
                else
                {
                    deployment.ConnectionName = model.ConnectionName;
                    deployment.ConnectionNameAlias = connection.GetValue<string>("ConnectionNameAlias");
                }
            }
        }

        var anotherExists = (await _deploymentsCatalog.GetAllAsync())
            .Any(d => d.ClientName == deployment.ClientName &&
            d.ConnectionName == deployment.ConnectionName &&
            d.Name.Equals(deployment.Name, StringComparison.OrdinalIgnoreCase)
            && d.ItemId != deployment.ItemId);

        if (anotherExists)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["The selected connection already has an existing deployment with the specified name."]);
        }

        return Edit(deployment, context);
    }

    private bool HasContainedConnection(string providerName)
        => _aiOptions.Deployments.TryGetValue(providerName, out var entry) && entry.SupportsContainedConnection;

    private static bool TryGetSelectedTypes(IEnumerable<string> selectedTypes, out AIDeploymentType deploymentTypes)
    {
        deploymentTypes = AIDeploymentType.None;

        if (selectedTypes is null)
        {
            return false;
        }

        foreach (var typeName in selectedTypes.Where(static value => !string.IsNullOrWhiteSpace(value)))
        {
            if (!Enum.TryParse<AIDeploymentType>(typeName, ignoreCase: true, out var parsedType) ||
                parsedType == AIDeploymentType.None)
            {
                deploymentTypes = AIDeploymentType.None;
                return false;
            }

            deploymentTypes |= parsedType;
        }

        return deploymentTypes.IsValidSelection();
    }
}
