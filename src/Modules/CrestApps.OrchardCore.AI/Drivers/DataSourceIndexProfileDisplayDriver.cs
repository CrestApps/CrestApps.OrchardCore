using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Display driver for DataSource knowledge base index profiles.
/// Handles embedding connection selection and default field configuration.
/// </summary>
public sealed class DataSourceIndexProfileDisplayDriver : DisplayDriver<IndexProfile>
{
    private const char Separator = '|';
    private const int ExpectedPartsCount = 3;

    private readonly AIProviderOptions _providerOptions;

    internal readonly IStringLocalizer S;

    public DataSourceIndexProfileDisplayDriver(
        IOptions<AIProviderOptions> providerOptions,
        IStringLocalizer<DataSourceIndexProfileDisplayDriver> stringLocalizer)
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

        return Initialize<EditDataSourceIndexProfileViewModel>("DataSourceIndexProfile_Edit", model =>
        {
            var metadata = indexProfile.As<DataSourceIndexProfileMetadata>();

            // Build the list of available embedding connections across all providers.
            var embeddingConnections = new List<SelectListItem>();

            foreach (var (providerName, provider) in _providerOptions.Providers)
            {
                foreach (var (connectionName, connection) in provider.Connections)
                {
                    var embeddingDeploymentName = connection.GetDefaultEmbeddingDeploymentName(false);

                    if (string.IsNullOrEmpty(embeddingDeploymentName))
                    {
                        continue;
                    }

                    var key = string.Join(Separator, providerName, connectionName, embeddingDeploymentName);

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

            // Make read-only once the index has been created.
            model.IsLocked = !string.IsNullOrEmpty(metadata.EmbeddingProviderName) &&
                             !string.IsNullOrEmpty(indexProfile.IndexFullName);
        }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(IndexProfile indexProfile, UpdateEditorContext context)
    {
        if (!CanHandle(indexProfile))
        {
            return null;
        }

        var model = new EditDataSourceIndexProfileViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var metadata = indexProfile.As<DataSourceIndexProfileMetadata>();

        // Don't allow changes if already locked.
        if (!string.IsNullOrEmpty(metadata.EmbeddingProviderName) &&
            !string.IsNullOrEmpty(indexProfile.IndexFullName))
        {
            return Edit(indexProfile, context);
        }

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
    {
        return string.Equals(DataSourceConstants.IndexingTaskType, indexProfile.Type, StringComparison.OrdinalIgnoreCase);
    }
}
