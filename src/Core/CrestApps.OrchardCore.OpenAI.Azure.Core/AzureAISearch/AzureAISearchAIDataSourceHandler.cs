using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;

public sealed class AzureAISearchAIDataSourceHandler : CatalogEntryHandlerBase<AIDataSource>
{
    private readonly IODataFilterValidator _filterValidator;

    internal readonly IStringLocalizer S;

    public AzureAISearchAIDataSourceHandler(
        IODataFilterValidator filterValidator,
        IStringLocalizer<AzureAISearchAIDataSourceHandler> stringLocalizer)
    {
        _filterValidator = filterValidator;
        S = stringLocalizer;
    }

    public override Task ValidatedAsync(ValidatedContext<AIDataSource> context)
    {
        if (context.Model.ProfileSource != AzureOpenAIConstants.ProviderName ||
            context.Model.Type != AzureOpenAIConstants.DataSourceTypes.AzureAISearch)
        {
            return Task.CompletedTask;
        }

        // Try the new metadata first, fall back to legacy for backward compatibility
        var indexMetadata = context.Model.As<AzureAIDataSourceIndexMetadata>();
        var indexName = indexMetadata?.IndexName;

        // Fall back to legacy metadata if new metadata doesn't have index name
#pragma warning disable CS0618 // Type or member is obsolete
        if (string.IsNullOrWhiteSpace(indexName))
        {
            var legacyMetadata = context.Model.As<AzureAIProfileAISearchMetadata>();
            indexName = legacyMetadata?.IndexName;
        }
#pragma warning restore CS0618 // Type or member is obsolete

        if (string.IsNullOrWhiteSpace(indexName))
        {
            context.Result.Fail(new ValidationResult(S["The Index is required."], [nameof(AzureAIDataSourceIndexMetadata.IndexName)]));
        }

        return Task.CompletedTask;
    }
}
