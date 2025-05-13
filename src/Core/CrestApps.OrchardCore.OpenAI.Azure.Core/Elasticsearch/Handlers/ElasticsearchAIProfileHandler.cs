using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Elasticsearch.Handlers;

public sealed class ElasticsearchAIProfileHandler : ModelHandlerBase<AIProfile>
{
    internal readonly IStringLocalizer S;

    public ElasticsearchAIProfileHandler(IStringLocalizer<ElasticsearchAIProfileHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task ValidatedAsync(ValidatedContext<AIProfile> context)
    {
        if (context.Model?.Source != AzureOpenAIConstants.AISearchImplementationName)
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
