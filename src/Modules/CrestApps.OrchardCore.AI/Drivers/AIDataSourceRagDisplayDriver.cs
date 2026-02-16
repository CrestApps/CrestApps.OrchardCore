using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Indexing;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Display driver for RAG query parameters on AIProfile.
/// Allows users to customize query-time parameters like Filter, Strictness, and TopNDocuments
/// per AI profile.
/// </summary>
public sealed class AIDataSourceRagDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly ICatalog<AIDataSource> _dataSourceStore;
    private readonly IIndexProfileStore _indexProfileStore;

    internal readonly IStringLocalizer S;

    public AIDataSourceRagDisplayDriver(
        ICatalog<AIDataSource> dataSourceStore,
        IIndexProfileStore indexProfileStore,
        IStringLocalizer<AIDataSourceRagDisplayDriver> stringLocalizer)
    {
        _dataSourceStore = dataSourceStore;
        _indexProfileStore = indexProfileStore;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        var dataSourceMetadata = profile.As<AIProfileDataSourceMetadata>();

        if (string.IsNullOrEmpty(dataSourceMetadata?.DataSourceId))
        {
            return null;
        }

        return Initialize<EditAIDataSourceRagViewModel>("AIDataSourceRag_Edit", async model =>
        {
            var ragMetadata = profile.As<AIDataSourceRagMetadata>();

            model.Strictness = ragMetadata?.Strictness;
            model.TopNDocuments = ragMetadata?.TopNDocuments;
            model.IsInScope = ragMetadata?.IsInScope ?? true;
            model.Filter = ragMetadata?.Filter;

            // Determine the source index provider for filter format hints.
            var dataSource = await _dataSourceStore.FindByIdAsync(dataSourceMetadata.DataSourceId);
            if (dataSource != null)
            {
                var indexMetadata = dataSource.As<AIDataSourceIndexMetadata>();
                if (!string.IsNullOrEmpty(indexMetadata.IndexName))
                {
                    var sourceProfile = await _indexProfileStore.FindByNameAsync(indexMetadata.IndexName);
                    model.SourceProviderName = sourceProfile?.ProviderName;
                }
            }
        }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var dataSourceMetadata = profile.As<AIProfileDataSourceMetadata>();
        if (string.IsNullOrEmpty(dataSourceMetadata?.DataSourceId))
        {
            return null;
        }

        var model = new EditAIDataSourceRagViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // Validate filter if provided.
        if (!string.IsNullOrWhiteSpace(model.Filter))
        {
            var dataSource = await _dataSourceStore.FindByIdAsync(dataSourceMetadata.DataSourceId);

            if (dataSource != null)
            {
                var indexMetadata = dataSource.As<AIDataSourceIndexMetadata>();
                if (!string.IsNullOrEmpty(indexMetadata.IndexName))
                {
                    var sourceProfile = await _indexProfileStore.FindByNameAsync(indexMetadata.IndexName);
                    if (sourceProfile != null)
                    {
                        ValidateFilter(model.Filter, sourceProfile.ProviderName, context);
                    }
                }
            }
        }

        profile.Put(new AIDataSourceRagMetadata
        {
            Strictness = model.Strictness,
            TopNDocuments = model.TopNDocuments,
            IsInScope = model.IsInScope,
            Filter = model.Filter,
        });

        return Edit(profile, context);
    }

    private void ValidateFilter(string filter, string providerName, UpdateEditorContext context)
    {
        if (string.Equals(providerName, "Elasticsearch", StringComparison.OrdinalIgnoreCase))
        {
            // Elasticsearch filters must be valid JSON (DSL query).
            try
            {
                JsonDocument.Parse(filter);
            }
            catch (JsonException)
            {
                context.Updater.ModelState.AddModelError(Prefix + "." + nameof(EditAIDataSourceRagViewModel.Filter),
                    S["The filter must be a valid Elasticsearch DSL query in JSON format."]);
            }
        }
        else if (string.Equals(providerName, "AzureAISearch", StringComparison.OrdinalIgnoreCase))
        {
            // Azure AI Search filters must be valid OData expressions.
            // Basic validation: check it's not empty and doesn't contain obvious JSON.
            if (filter.TrimStart().StartsWith('{') || filter.TrimStart().StartsWith('['))
            {
                context.Updater.ModelState.AddModelError(Prefix + "." + nameof(EditAIDataSourceRagViewModel.Filter),
                    S["The filter must be a valid OData filter expression, not JSON. Example: status eq 'active' and category eq 'docs'"]);
            }
        }
    }
}
