using CrestApps.AI.Deployments;
using CrestApps.AI.Memory;
using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.Memory.Models;
using CrestApps.OrchardCore.AI.Memory.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Memory.Drivers;

public sealed class AIMemoryIndexProfileDisplayDriver : DisplayDriver<IndexProfile>
{
    private const char Separator = '|';
    private const int ExpectedPartsCount = 3;

    private readonly IAIDeploymentManager _deploymentManager;
    private readonly AIProviderOptions _providerOptions;

    internal readonly IStringLocalizer S;

    public AIMemoryIndexProfileDisplayDriver(
        IAIDeploymentManager deploymentManager,
        IOptions<AIProviderOptions> providerOptions,
        IStringLocalizer<AIMemoryIndexProfileDisplayDriver> stringLocalizer)
    {
        _deploymentManager = deploymentManager;
        _providerOptions = providerOptions.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(IndexProfile indexProfile, BuildEditorContext context)
    {
        if (!CanHandle(indexProfile))
        {
            return null;
        }

        return Initialize<AIMemoryIndexProfileViewModel>("AIMemoryIndexProfile_Edit", async model =>
        {
            var metadata = indexProfile.As<AIMemoryIndexProfileMetadata>();
            var embeddingConnections = new List<SelectListItem>();

            foreach (var (providerName, provider) in _providerOptions.Providers)
            {
                foreach (var (connectionName, connection) in provider.Connections)
                {
                    var embeddingDeployment = await _deploymentManager.GetDefaultAsync(providerName, connectionName, AIDeploymentType.Embedding);
                    var embeddingDeploymentName = embeddingDeployment?.Name;

                    if (string.IsNullOrEmpty(embeddingDeploymentName))
                    {
                        continue;
                    }

                    var key = string.Join(Separator, providerName, connectionName, embeddingDeploymentName);
                    var displayName = connection.TryGetValue("ConnectionNameAlias", out var alias)
                    ? $"{alias} ({providerName})"
                    : $"{connectionName} ({providerName})";

                    var isSelected = metadata.EmbeddingProviderName == providerName &&
                        metadata.EmbeddingConnectionName == connectionName &&
                            metadata.EmbeddingDeploymentName == embeddingDeploymentName;

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

        var model = new AIMemoryIndexProfileViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var metadata = indexProfile.As<AIMemoryIndexProfileMetadata>();
        var isSet = false;

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
        => string.Equals(indexProfile.Type, MemoryConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase);
}
