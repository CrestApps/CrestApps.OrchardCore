using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Elasticsearch;

public sealed class ElasticsearchAIDataSourceHandler : CatalogEntryHandlerBase<AIDataSource>
{
    private readonly IODataFilterValidator _filterValidator;

    internal readonly IStringLocalizer S;

    public ElasticsearchAIDataSourceHandler(
        IODataFilterValidator filterValidator,
        IStringLocalizer<ElasticsearchAIDataSourceHandler> stringLocalizer)
    {
        _filterValidator = filterValidator;
        S = stringLocalizer;
    }

    public override Task ValidatedAsync(ValidatedContext<AIDataSource> context)
    {
        if (context.Model.ProfileSource != AzureOpenAIConstants.ProviderName ||
            context.Model.Type != AzureOpenAIConstants.DataSourceTypes.Elasticsearch)
        {
            return Task.CompletedTask;
        }

        var indexMetadata = context.Model.As<AzureAIDataSourceIndexMetadata>();

        if (string.IsNullOrWhiteSpace(indexMetadata?.IndexName))
        {
            context.Result.Fail(new ValidationResult(S["The Index is required."], [nameof(AzureAIDataSourceIndexMetadata.IndexName)]));
        }

        return Task.CompletedTask;
    }
}
