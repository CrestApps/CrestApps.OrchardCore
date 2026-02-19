using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

public sealed class ChatInteractionConnectionDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly AIOptions _aiOptions;
    private readonly AIProviderOptions _providerOptions;

    internal readonly IStringLocalizer S;

    public ChatInteractionConnectionDisplayDriver(
        IAIDeploymentManager deploymentManager,
        IOptions<AIOptions> aiOptions,
        IOptions<AIProviderOptions> providerOptions,
        IStringLocalizer<ChatInteractionConnectionDisplayDriver> stringLocalizer)
    {
        _deploymentManager = deploymentManager;
        _aiOptions = aiOptions.Value;
        _providerOptions = providerOptions.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ChatInteraction interaction, BuildEditorContext context)
    {
        var connectionResult = Initialize<EditChatInteractionConnectionViewModel>("ChatInteractionConnection_Edit", async model =>
        {
            if (!_aiOptions.ProfileSources.TryGetValue(interaction.Source, out var profileSource))
            {
                return;
            }

            model.ProviderName = profileSource.ProviderName;
            model.DeploymentId = interaction.DeploymentId;

            if (profileSource is not null && _providerOptions.Providers.TryGetValue(profileSource.ProviderName, out var provider))
            {
                if (provider.Connections.Count == 1)
                {
                    // If there's only one connection, use it automatically
                    var connection = provider.Connections.First();
                    model.ConnectionName = connection.Key;
                }
                else
                {
                    model.ConnectionName = interaction.ConnectionName;
                }

                model.ConnectionNames = provider.Connections.Select(x => new SelectListItem(
                    x.Value.TryGetValue("ConnectionNameAlias", out var alias) ? alias.ToString() : x.Key,
                    x.Key)).ToArray();
            }
            else
            {
                model.ConnectionNames = [];
            }

            // Load deployments based on connection name
            if (!string.IsNullOrEmpty(interaction.DeploymentId))
            {
                var deployment = await _deploymentManager.FindByIdAsync(interaction.DeploymentId);

                if (deployment is not null)
                {
                    model.Deployments = (await _deploymentManager.GetAllAsync(profileSource.ProviderName, deployment.ConnectionName))
                        .Select(x => new SelectListItem(x.Name, x.ItemId));
                }
            }

            if (model.Deployments is null || !model.Deployments.Any())
            {
                var connectionName = interaction.ConnectionName;

                if (string.IsNullOrEmpty(connectionName) && _providerOptions.Providers.TryGetValue(profileSource.ProviderName, out var prov))
                {
                    connectionName = prov.DefaultConnectionName;
                }

                if (!string.IsNullOrEmpty(connectionName))
                {
                    model.Deployments = (await _deploymentManager.GetAllAsync(profileSource.ProviderName, connectionName))
                        .Select(x => new SelectListItem(x.Name, x.ItemId));
                }
            }

        }).Location("Parameters:1#Settings;2");

        return connectionResult;
    }

    public override async Task<IDisplayResult> UpdateAsync(ChatInteraction interaction, UpdateEditorContext context)
    {
        if (!_aiOptions.ProfileSources.TryGetValue(interaction.Source, out _))
        {
            return null;
        }

        var model = new EditChatInteractionConnectionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        interaction.ConnectionName = model.ConnectionName;
        interaction.DeploymentId = model.DeploymentId;

        return Edit(interaction, context);
    }
}
