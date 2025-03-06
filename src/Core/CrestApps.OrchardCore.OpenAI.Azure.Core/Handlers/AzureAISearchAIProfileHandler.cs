using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;

public sealed class AzureAISearchAIProfileHandler : ModelHandlerBase<AIProfile>
{
    internal readonly IStringLocalizer S;

    public AzureAISearchAIProfileHandler(IStringLocalizer<AzureAISearchAIProfileHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<AIProfile> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<AIProfile> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task ValidatedAsync(ValidatedContext<AIProfile> context)
    {
        if (context.Model?.Source != AzureOpenAIConstants.AISearchImplementationName)
        {
            return Task.CompletedTask;
        }

        var metadata = context.Model.As<AzureAIProfileAISearchMetadata>();

        if (string.IsNullOrWhiteSpace(metadata.IndexName))
        {
            context.Result.Fail(new ValidationResult(S["The Index is required."], [nameof(metadata.IndexName)]));
        }

        return Task.CompletedTask;
    }

    private static Task PopulateAsync(AIProfile profile, JsonNode data)
    {
        if (profile.Source != AzureOpenAIConstants.AISearchImplementationName)
        {
            return Task.CompletedTask;
        }

        var metadataNode = data["Properties"]?[nameof(AzureAIProfileAISearchMetadata)]?.AsObject();

        if (metadataNode == null || metadataNode.Count == 0)
        {
            return Task.CompletedTask;
        }

        var metadata = profile.As<AzureAIProfileAISearchMetadata>();

        var indexName = metadataNode[nameof(metadata.IndexName)]?.GetValue<string>();

        if (!string.IsNullOrEmpty(indexName))
        {
            metadata.IndexName = indexName;
        }

        var strictness = metadataNode[nameof(metadata.Strictness)]?.GetValue<int?>();

        if (strictness.HasValue)
        {
            metadata.Strictness = strictness;
        }

        var topNDocuments = metadataNode[nameof(metadata.TopNDocuments)]?.GetValue<int?>();

        if (topNDocuments.HasValue)
        {
            metadata.TopNDocuments = topNDocuments;
        }

        profile.Put(metadata);

        return Task.CompletedTask;
    }
}
