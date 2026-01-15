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

namespace CrestApps.OrchardCore.OpenAI.Azure.Drivers;

public sealed class AzureOpenAISearchADataSourceDisplayDriver : DisplayDriver<AIDataSource>
{
    private readonly IIndexProfileStore _indexProfileStore;

    public AzureOpenAISearchADataSourceDisplayDriver(
        IIndexProfileStore indexProfileStore)
    {
        _indexProfileStore = indexProfileStore;
    }

    public override IDisplayResult Edit(AIDataSource dataSource, BuildEditorContext context)
    {
        if (dataSource.ProfileSource != AzureOpenAIConstants.ProviderName ||
            dataSource.Type != AzureOpenAIConstants.DataSourceTypes.AzureAISearch)
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
                var legacyMetadata = dataSource.As<AzureAIProfileAISearchMetadata>();
                model.IndexName = legacyMetadata?.IndexName;
            }
#pragma warning restore CS0618 // Type or member is obsolete

            model.IndexNames = (await _indexProfileStore.GetByProviderAsync(AzureAISearchConstants.ProviderName))
                .Select(i => new SelectListItem(i.Name, i.IndexName))
                .OrderBy(x => x.Text);

        }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDataSource dataSource, UpdateEditorContext context)
    {
        if (dataSource.ProfileSource != AzureOpenAIConstants.ProviderName ||
            dataSource.Type != AzureOpenAIConstants.DataSourceTypes.AzureAISearch)
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
