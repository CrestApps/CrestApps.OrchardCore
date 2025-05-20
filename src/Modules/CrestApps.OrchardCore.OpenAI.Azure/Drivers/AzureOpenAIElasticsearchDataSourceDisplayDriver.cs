using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Documents;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Search.Elasticsearch.Core.Models;

namespace CrestApps.OrchardCore.OpenAI.Azure.Drivers;

public sealed class AzureOpenAIElasticsearchDataSourceDisplayDriver : DisplayDriver<AIDataSource>
{
    private readonly IDocumentManager<ElasticIndexSettingsDocument> _documentManager;

    internal readonly IStringLocalizer S;

    public AzureOpenAIElasticsearchDataSourceDisplayDriver(
        IDocumentManager<ElasticIndexSettingsDocument> documentManager,
        IStringLocalizer<AzureOpenAIElasticsearchDataSourceDisplayDriver> stringLocalizer)
    {
        _documentManager = documentManager;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIDataSource dataSource, BuildEditorContext context)
    {
        if (dataSource.ProfileSource != AzureOpenAIConstants.AzureOpenAIOwnData ||
            dataSource.Type != AzureOpenAIConstants.DataSourceTypes.Elasticsearch)
        {
            return null;
        }

        return Initialize<AzureProfileElasticsearchViewModel>("AzureOpenAIProfileElasticsearch_Edit", async model =>
        {
            var metadata = dataSource.As<AzureAIProfileElasticsearchMetadata>();

            model.Strictness = metadata.Strictness;
            model.TopNDocuments = metadata.TopNDocuments;
            model.IndexName = metadata.IndexName;

            var document = await _documentManager.GetOrCreateImmutableAsync();

            model.IndexNames = document.ElasticIndexSettings.Values.Select(i => new SelectListItem(i.IndexName, i.IndexName));
        }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDataSource dataSource, UpdateEditorContext context)
    {
        if (dataSource.ProfileSource != AzureOpenAIConstants.AzureOpenAIOwnData ||
            dataSource.Type != AzureOpenAIConstants.DataSourceTypes.Elasticsearch)
        {
            return null;
        }

        var model = new AzureProfileElasticsearchViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var document = await _documentManager.GetOrCreateImmutableAsync();

        if (string.IsNullOrEmpty(model.IndexName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.IndexName), S["Invalid index name is required."]);
        }
        else if (!document.ElasticIndexSettings.Values.Any(x => x.IndexName == model.IndexName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.IndexName), S["Invalid index name."]);
        }

        dataSource.Put(new AzureAIProfileElasticsearchMetadata
        {
            IndexName = model.IndexName,
            Strictness = model.Strictness,
            TopNDocuments = model.TopNDocuments,
        });

        return Edit(dataSource, context);
    }
}
