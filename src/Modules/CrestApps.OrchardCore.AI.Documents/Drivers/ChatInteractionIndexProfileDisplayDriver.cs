using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Documents.Drivers;

public sealed class ChatInteractionIndexProfileDisplayDriver : DisplayDriver<IndexProfile>
{
    private const char Separator = '|';
    private const int ExpectedPartsCount = 3;

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

        return Initialize<ChatInteractionIndexProfileViewModel>("ChatInteractionIndexProfile_Edit", model =>
        {
            var metadata = indexProfile.As<ChatInteractionIndexProfileMetadata>();

            // Build the list of available embedding connections across all providers
            var embeddingConnections = new List<SelectListItem>();

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
                    var key = string.Join(Separator, providerName, connectionName, embeddingDeploymentName);

                    // Display name: Connection alias or name (Provider)
                    var displayName = connection.TryGetValue("ConnectionNameAlias", out var alias)
                        ? $"{alias} ({providerName})"
                        : $"{connectionName} ({providerName})";

                    var isSelected = metadata.EmbeddingProviderName == providerName &&
                                metadata.EmbeddingDeploymentName == embeddingDeploymentName &&
                                metadata.EmbeddingConnectionName == connectionName;

                    if (isSelected)
                    {
                        model.EmbeddingConnection = key;
                    }

                    embeddingConnections.Add(new SelectListItem(displayName, key, isSelected));
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

        var model = new ChatInteractionIndexProfileViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var metadata = indexProfile.As<ChatInteractionIndexProfileMetadata>();

        var isSet = false;

        // Parse the selected embedding connection (format: providerName|connectionName|deploymentName)
        if (!string.IsNullOrEmpty(model.EmbeddingConnection))
        {
            var parts = model.EmbeddingConnection.Split(Separator);

            if (parts.Length == ExpectedPartsCount)
            {
                metadata.EmbeddingProviderName = parts[0];
                metadata.EmbeddingConnectionName = parts[1];
                metadata.EmbeddingDeploymentName = parts[2];

                isSet = true;
            }
        }

        if (!isSet)
        {
            context.Updater.ModelState.AddModelError(Prefix, S["Embedding connection is required."]);
        }

        indexProfile.Put(metadata);

        return Edit(indexProfile, context);
    }

    private static bool CanHandle(IndexProfile indexProfile)
    {
        return string.Equals(AIConstants.AIDocumentsIndexingTaskType, indexProfile.Type, StringComparison.OrdinalIgnoreCase);
    }
}
