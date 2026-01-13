using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Elasticsearch;
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
        if (dataSource.ProfileSource != AzureOpenAIConstants.AzureOpenAIOwnData ||
            dataSource.Type != AzureOpenAIConstants.DataSourceTypes.Elasticsearch)
        {
            return null;
        }

        return Initialize<AzureDataSourceElasticsearchViewModel>("AzureOpenAIDataSourceElasticsearch_Edit", async model =>
        {
            var metadata = dataSource.As<AzureAIProfileElasticsearchMetadata>();

            model.IndexName = metadata.IndexName;

            var indexProfiles = await _indexProfileStore.GetByProviderAsync(ElasticsearchConstants.ProviderName);

            model.IndexNames = indexProfiles.Select(i => new SelectListItem(i.Name, i.IndexName));
        }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDataSource dataSource, UpdateEditorContext context)
    {
        if (dataSource.ProfileSource != AzureOpenAIConstants.AzureOpenAIOwnData ||
            dataSource.Type != AzureOpenAIConstants.DataSourceTypes.Elasticsearch)
        {
            return null;
        }

        var model = new AzureDataSourceElasticsearchViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);


        if (string.IsNullOrEmpty(model.IndexName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.IndexName), S["Invalid index name is required."]);
        }
        else if (await _indexProfileStore.FindByIndexNameAndProviderAsync(model.IndexName, ElasticsearchConstants.ProviderName) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.IndexName), S["Invalid index name."]);
        }

        dataSource.Put(new AzureAIProfileElasticsearchMetadata
        {
            IndexName = model.IndexName,
        });

        return Edit(dataSource, context);
    }
}

public sealed class AzureOpenAIElasticsearchAIProfileDisplayDriver : DisplayDriver<AIProfile>
{
    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        if (profile.Source != AzureOpenAIConstants.DataSourceTypes.Elasticsearch)
        {
            return null;
        }

        return Initialize<AzureProfileElasticsearchViewModel>("AzureOpenAIProfileElasticsearch_Edit", async model =>
        {
            var metadata = profile.As<AzureOpenAIProfileElasticsearchMetadata>();

            model.Strictness = metadata.Strictness;
            model.TopNDocuments = metadata.TopNDocuments;
            model.Filter = metadata.Filter;

        }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        if (profile.Source != AzureOpenAIConstants.DataSourceTypes.Elasticsearch)
        {
            return null;
        }

        var model = new AzureProfileElasticsearchViewModel();

        profile.Put(new AzureOpenAIProfileElasticsearchMetadata
        {
            Strictness = model.Strictness,
            TopNDocuments = model.TopNDocuments,
            Filter = model.Filter,
        });

        return Edit(profile, context);
    }
}
