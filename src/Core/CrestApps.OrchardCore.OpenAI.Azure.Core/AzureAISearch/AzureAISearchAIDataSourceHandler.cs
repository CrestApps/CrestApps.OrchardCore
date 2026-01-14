using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;

public sealed class AzureAISearchAIDataSourceHandler : CatalogEntryHandlerBase<AIDataSource>
{
    private readonly IODataFilterValidator _odataFilterValidator;

    internal readonly IStringLocalizer S;

    public AzureAISearchAIDataSourceHandler(
        IODataFilterValidator odataFilterValidator,
        IStringLocalizer<AzureAISearchAIDataSourceHandler> stringLocalizer)
    {
        _odataFilterValidator = odataFilterValidator;
        S = stringLocalizer;
    }

    public override Task ValidatedAsync(ValidatedContext<AIDataSource> context)
    {
        if (context.Model.ProfileSource != AzureOpenAIConstants.ProviderName ||
            context.Model.Type != AzureOpenAIConstants.DataSourceTypes.AzureAISearch)
        {
            return Task.CompletedTask;
        }

        var metadata = context.Model.As<AzureAIProfileAISearchMetadata>();

        if (string.IsNullOrWhiteSpace(metadata.IndexName))
        {
            context.Result.Fail(new ValidationResult(S["The Index is required."], [nameof(metadata.IndexName)]));
        }

        if (!string.IsNullOrWhiteSpace(metadata.Filter) && !_odataFilterValidator.IsValid(metadata.Filter))
        {
            context.Result.Fail(new ValidationResult(S["The Filter must be a valid OData filter expression."], [nameof(metadata.Filter)]));
        }

        return Task.CompletedTask;
    }
}
