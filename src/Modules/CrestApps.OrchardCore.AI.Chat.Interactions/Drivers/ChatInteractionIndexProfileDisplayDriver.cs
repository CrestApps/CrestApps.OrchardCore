using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Indexing.Models;
using OrchardCore.Search.Elasticsearch;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

/// <summary>
/// Display driver for Chat Interaction Index Profile that adds embedding connection selection.
/// Only applies when IndexProfile.Type is ChatInteractionsConstants.IndexingTaskType
/// and ProviderName is ElasticsearchConstants.ProviderName.
/// </summary>
public sealed class ChatInteractionIndexProfileDisplayDriver : DisplayDriver<IndexProfile>
{
    private readonly AIProviderOptions _providerOptions;

    internal readonly IStringLocalizer S;

    public ChatInteractionIndexProfileDisplayDriver(
        IOptions<AIProviderOptions> providerOptions,
        IStringLocalizer<ChatInteractionIndexProfileDisplayDriver> stringLocalizer)
    {
        _providerOptions = providerOptions.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(IndexProfile indexProfile, BuildEditorContext context)
    {
        if (!CanHandle(indexProfile))
        {
            return null;
        }

        return Initialize<EditChatInteractionIndexProfileViewModel>("ChatInteractionIndexProfile_Edit", model =>
        {
            var metadata = indexProfile.As<ChatInteractionIndexProfileMetadata>();

            model.EmbeddingProviderName = metadata.EmbeddingProviderName;
            model.EmbeddingConnectionName = metadata.EmbeddingConnectionName;
            model.EmbeddingDeploymentName = metadata.EmbeddingDeploymentName;

            // Build the list of available embedding connections across all providers
            var embeddingConnections = new List<SelectListItem>
            {
                new(S["-- Select an embedding connection --"], string.Empty)
            };

            foreach (var (providerName, provider) in _providerOptions.Providers)
            {
                foreach (var (connectionName, connection) in provider.Connections)
                {
                    // Check if this connection has embedding deployments configured
                    var embeddingDeploymentName = connection.GetDefaultEmbeddingDeploymentName(false);

                    if (string.IsNullOrEmpty(embeddingDeploymentName))
                    {
                        continue;
                    }

                    // Create a unique key combining provider, connection, and deployment
                    var key = $"{providerName}|{connectionName}|{embeddingDeploymentName}";

                    // Display name: Connection alias or name (Provider)
                    var displayName = connection.TryGetValue("ConnectionNameAlias", out var alias)
                        ? $"{alias} ({providerName})"
                        : $"{connectionName} ({providerName})";

                    embeddingConnections.Add(new SelectListItem(displayName, key));
                }
            }

            model.EmbeddingConnections = embeddingConnections;
        }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(IndexProfile indexProfile, UpdateEditorContext context)
    {
        if (!CanHandle(indexProfile))
        {
            return null;
        }

        var model = new EditChatInteractionIndexProfileViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var metadata = indexProfile.As<ChatInteractionIndexProfileMetadata>();

        // Parse the selected embedding connection (format: providerName|connectionName|deploymentName)
        if (!string.IsNullOrEmpty(model.EmbeddingConnectionName))
        {
            var parts = model.EmbeddingConnectionName.Split('|');

            if (parts.Length == 3)
            {
                metadata.EmbeddingProviderName = parts[0];
                metadata.EmbeddingConnectionName = parts[1];
                metadata.EmbeddingDeploymentName = parts[2];
            }
        }
        else
        {
            // Clear the metadata if no connection is selected
            metadata.EmbeddingProviderName = null;
            metadata.EmbeddingConnectionName = null;
            metadata.EmbeddingDeploymentName = null;
        }

        indexProfile.Put(metadata);

        return Edit(indexProfile, context);
    }

    private static bool CanHandle(IndexProfile indexProfile)
    {
        return string.Equals(ElasticsearchConstants.ProviderName, indexProfile.ProviderName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(ChatInteractionsConstants.IndexingTaskType, indexProfile.Type, StringComparison.OrdinalIgnoreCase);
    }
}
