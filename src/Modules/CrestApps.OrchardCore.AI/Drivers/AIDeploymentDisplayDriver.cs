using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Drivers;

internal sealed class AIDeploymentDisplayDriver : DisplayDriver<AIDeployment>
{
    private readonly AIOptions _aiOptions;
    private readonly INamedSourceCatalog<AIProviderConnection> _connectionsCatalog;
    private readonly INamedCatalog<AIDeployment> _deploymentsCatalog;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDeploymentDisplayDriver"/> class.
    /// </summary>
    /// <param name="deploymentCatalog">The catalog for retrieving AI deployments by name.</param>
    /// <param name="connectionsCatalog">The catalog for retrieving AI provider connections by name and source.</param>
    /// <param name="aiOptions">The AI configuration options.</param>
    /// <param name="stringLocalizer">The string localizer for this driver.</param>
    public AIDeploymentDisplayDriver(
        INamedCatalog<AIDeployment> deploymentCatalog,
        INamedSourceCatalog<AIProviderConnection> connectionsCatalog,
        IOptions<AIOptions> aiOptions,
        IStringLocalizer<AIDeploymentDisplayDriver> stringLocalizer)
    {
        _connectionsCatalog = connectionsCatalog;
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
        var results = new List<IDisplayResult>
        {
            Initialize<EditDeploymentViewModel>("AIDeploymentFields_Edit", model =>
            {
                model.Name = deployment.Name;
                model.ModelName = deployment.ModelName;
                model.SelectedTypes = deployment.Type.GetSupportedTypes().Select(static type => type.ToString()).ToArray();
                model.IsNew = context.IsNew;

                model.Types = Enum.GetValues<AIDeploymentType>()
                .Where(static type => type != AIDeploymentType.None)
                .Select(t => new SelectListItem(t.ToString(), t.ToString()))
                .ToList();
            }).Location("Content:1"),
        };

        if (!HasContainedConnection(deployment.ClientName))
        {
            results.Add(Initialize<EditDeploymentConnectionViewModel>("AIDeploymentConnectionName_Edit", async model =>
            {
                await PopulateConnectionEditorAsync(model, deployment);
            }).Location("Content:5"));
        }

        return Combine(results.ToArray());
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

        var modelName = model.ModelName?.Trim();

        if (string.IsNullOrEmpty(modelName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ModelName), S["Model name is required."]);
        }
        else
        {
            deployment.ModelName = modelName;
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
        }
        else
        {
            var connectionModel = new EditDeploymentConnectionViewModel();
            await context.Updater.TryUpdateModelAsync(connectionModel, Prefix);

            var connections = await _connectionsCatalog.GetAsync(deployment.ClientName);

            if (connections.Count == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(connectionModel.ConnectionName), S["There are no configured connection for the client name: {0}.", deployment.ClientName]);
            }
            else
            {
                if (string.IsNullOrEmpty(connectionModel.ConnectionName))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(connectionModel.ConnectionName), S["Connection name is required."]);
                }
                else
                {
                    var connection = await _connectionsCatalog.FindByConnectionNameAsync(deployment.ClientName, connectionModel.ConnectionName);

                    if (connection is null)
                    {
                        context.Updater.ModelState.AddModelError(Prefix, nameof(connectionModel.ConnectionName), S["Invalid connection name provided."]);
                    }
                    else
                    {
                        deployment.ConnectionName = string.IsNullOrWhiteSpace(connection.Name)
                            ? connection.ItemId
                            : connection.Name;
                    }
                }
            }
        }

        var anotherExists = (await _deploymentsCatalog.GetAllAsync())
            .Any(d =>
        d.ItemId != deployment.ItemId &&
            d.Name.Equals(deployment.Name, StringComparison.OrdinalIgnoreCase));

        if (anotherExists)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["A deployment with the specified technical name already exists."]);
        }

        return Edit(deployment, context);
    }

    private async Task PopulateConnectionEditorAsync(EditDeploymentConnectionViewModel model, AIDeployment deployment)
    {
        var selectedConnection = string.IsNullOrWhiteSpace(deployment.ConnectionName)
            ? null
            : await _connectionsCatalog.FindByConnectionNameAsync(deployment.ClientName, deployment.ConnectionName);
        var connections = await _connectionsCatalog.GetAsync(deployment.ClientName);

        model.ConnectionName = selectedConnection?.ItemId ?? deployment.ConnectionName;
        model.Connections = connections
            .OrderBy(connection => connection.GetDisplayName(), StringComparer.OrdinalIgnoreCase)
            .Select(connection => new SelectListItem(connection.GetDisplayName(), connection.ItemId))
            .ToArray();

        if (string.IsNullOrEmpty(model.ConnectionName) && model.Connections.Count == 1)
        {
            model.ConnectionName = model.Connections[0].Value;
        }
    }

    private bool HasContainedConnection(string providerName)
        => _aiOptions.Deployments.TryGetValue(providerName, out var entry) && entry.UseContainedConnection;

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
