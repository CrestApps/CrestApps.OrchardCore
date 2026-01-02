using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Elasticsearch;

public sealed class ElasticsearchAIDataSourceHandler : CatalogEntryHandlerBase<AIDataSource>
{
    internal readonly IStringLocalizer S;

    public ElasticsearchAIDataSourceHandler(IStringLocalizer<ElasticsearchAIDataSourceHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task ValidatedAsync(ValidatedContext<AIDataSource> context)
    {
        if (context.Model.ProfileSource != AzureOpenAIConstants.AzureOpenAIOwnData ||
            context.Model.Type != AzureOpenAIConstants.DataSourceTypes.Elasticsearch)
        {
            return Task.CompletedTask;
        }

        var metadata = context.Model.As<AzureAIProfileElasticsearchMetadata>();

        if (string.IsNullOrWhiteSpace(metadata.IndexName))
        {
            context.Result.Fail(new ValidationResult(S["The Index is required."], [nameof(metadata.IndexName)]));
        }

        return Task.CompletedTask;
    }
}
