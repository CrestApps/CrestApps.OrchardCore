using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;

public sealed class AzureAIDataSourceHandler : CatalogEntryHandlerBase<AIDataSource>
{
    internal readonly IStringLocalizer S;

    public AzureAIDataSourceHandler(
        IStringLocalizer<AzureAIDataSourceHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task ValidatedAsync(ValidatedContext<AIDataSource> context)
    {
        if (context.Model.ProfileSource != AzureOpenAIConstants.ProviderName)
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
