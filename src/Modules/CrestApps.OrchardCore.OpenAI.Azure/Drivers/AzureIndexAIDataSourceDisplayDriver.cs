using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Indexing.Core.Indexes;
using OrchardCore.Indexing.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.OpenAI.Azure.Drivers;

public sealed class AzureIndexAIDataSourceDisplayDriver : DisplayDriver<AIDataSource>
{
    private readonly ISession _session;
    private readonly AIOptions _aiOptions;

    public AzureIndexAIDataSourceDisplayDriver(
        ISession session,
        IOptions<AIOptions> aiOptions)
    {
        _session = session;
        _aiOptions = aiOptions.Value;
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

            var indexProfileSources = _aiOptions.DataSources.Keys
                .Where(x => x.ProfileSource == AzureOpenAIConstants.ProviderName)
                .Select(x => x.Type)
                .ToArray();

            if (indexProfileSources.Length > 0)
            {
                var indexes = await _session.Query<IndexProfile, IndexProfileIndex>()
                    .Where(ip => ip.ProviderName.IsIn(indexProfileSources))
                    .ListAsync();

                model.IndexNames = indexes
                    .Select(i => new SelectListItem(i.Name, i.Name))
                    .OrderBy(x => x.Text);
            }
            else
            {
                model.IndexNames = [];
            }

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
