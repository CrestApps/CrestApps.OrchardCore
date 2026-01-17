using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Search.AzureAI;
using OrchardCore.Search.Elasticsearch;

namespace CrestApps.OrchardCore.OpenAI.Azure.Drivers;

public sealed class AzureIndexAIDataSourceDisplayDriver : DisplayDriver<AIDataSource>
{
    private readonly IIndexProfileStore _indexProfileStore;

    public AzureIndexAIDataSourceDisplayDriver(IIndexProfileStore indexProfileStore)
    {
        _indexProfileStore = indexProfileStore;
    }

    public override IDisplayResult Edit(AIDataSource dataSource, BuildEditorContext context)
    {
        if (dataSource.ProfileSource != AzureOpenAIConstants.ProviderName)
        {
            return null;
        }

        return Initialize<AzureDataSourceIndexViewModel>("AzureIndexAIDataSource_Edit", async model =>
        {
            var indexMetadata = dataSource.As<AzureAIDataSourceIndexMetadata>();
            model.IndexName = indexMetadata.IndexName;

            var indexProviderName = dataSource.Type switch
            {
                AzureOpenAIConstants.DataSourceTypes.AzureAISearch => AzureAISearchConstants.ProviderName,
                AzureOpenAIConstants.DataSourceTypes.Elasticsearch => ElasticsearchConstants.ProviderName,
                AzureOpenAIConstants.DataSourceTypes.MongoDB => "MongoDB",
                _ => dataSource.Type
            };

            var indexes = await _indexProfileStore.GetByProviderAsync(indexProviderName);

            model.IndexNames = indexes
                .Select(i => new SelectListItem(i.Name, i.Name))
                .OrderBy(x => x.Text);
        }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDataSource dataSource, UpdateEditorContext context)
    {
        if (dataSource.ProfileSource != AzureOpenAIConstants.ProviderName)
        {
            return null;
        }

        var model = new AzureDataSourceIndexViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // Store only index-level configuration on the data source
        dataSource.Put(new AzureAIDataSourceIndexMetadata
        {
            IndexName = model.IndexName,
        });

        return Edit(dataSource, context);
    }
}
