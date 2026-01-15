using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Elasticsearch;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Search.Elasticsearch;

namespace CrestApps.OrchardCore.OpenAI.Azure.Drivers;

public sealed class AzureOpenAIElasticsearchDataSourceDisplayDriver : DisplayDriver<AIDataSource>
{
    private readonly IIndexProfileStore _indexProfileStore;

    internal readonly IStringLocalizer S;

    public AzureOpenAIElasticsearchDataSourceDisplayDriver(
        IIndexProfileStore indexProfileStore,
        IStringLocalizer<AzureOpenAIElasticsearchDataSourceDisplayDriver> stringLocalizer)
    {
        _indexProfileStore = indexProfileStore;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIDataSource dataSource, BuildEditorContext context)
    {
        if (dataSource.ProfileSource != AzureOpenAIConstants.ProviderName ||
            dataSource.Type != AzureOpenAIConstants.DataSourceTypes.Elasticsearch)
        {
            return null;
        }

        return Initialize<AzureDataSourceIndexViewModel>("AzureOpenAIDataSourceIndex_Edit", async model =>
        {
            // Try the new metadata first, fall back to legacy for backward compatibility
            var indexMetadata = dataSource.As<AzureAIDataSourceIndexMetadata>();
            model.IndexName = indexMetadata?.IndexName;

#pragma warning disable CS0618 // Type or member is obsolete
            if (string.IsNullOrEmpty(model.IndexName))
            {
                var legacyMetadata = dataSource.As<AzureAIProfileElasticsearchMetadata>();
                model.IndexName = legacyMetadata?.IndexName;
            }
#pragma warning restore CS0618 // Type or member is obsolete

            var indexProfiles = await _indexProfileStore.GetByProviderAsync(ElasticsearchConstants.ProviderName);

            model.IndexNames = indexProfiles.Select(i => new SelectListItem(i.Name, i.IndexName));
        }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDataSource dataSource, UpdateEditorContext context)
    {
        if (dataSource.ProfileSource != AzureOpenAIConstants.ProviderName ||
            dataSource.Type != AzureOpenAIConstants.DataSourceTypes.Elasticsearch)
        {
            return null;
        }

        var model = new AzureDataSourceIndexViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);


        if (string.IsNullOrEmpty(model.IndexName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.IndexName), S["Invalid index name is required."]);
        }
        else if (await _indexProfileStore.FindByIndexNameAndProviderAsync(model.IndexName, ElasticsearchConstants.ProviderName) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.IndexName), S["Invalid index name."]);
        }

        // Store only index-level configuration on the data source
        dataSource.Put(new AzureAIDataSourceIndexMetadata
        {
            IndexName = model.IndexName,
        });

        return Edit(dataSource, context);
    }
}
