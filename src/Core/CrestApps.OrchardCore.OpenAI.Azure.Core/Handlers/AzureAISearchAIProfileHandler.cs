using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;

public sealed class AzureAISearchAIProfileHandler : AIProfileHandlerBase
{
    internal readonly IStringLocalizer S;

    public AzureAISearchAIProfileHandler(IStringLocalizer<AzureAISearchAIProfileHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingAIProfileContext context)
        => PopulateAsync(context.Profile, context.Data);

    public override Task UpdatingAsync(UpdatingAIProfileContext context)
        => PopulateAsync(context.Profile, context.Data);

    public override Task ValidatedAsync(ValidatedAIProfileContext context)
    {
        if (context.Profile?.Source != AzureAISearchProfileSource.Key)
        {
            return Task.CompletedTask;
        }

        var metadata = context.Profile.As<AzureAIProfileAISearchMetadata>();

        if (string.IsNullOrWhiteSpace(metadata.IndexName))
        {
            context.Result.Fail(new ValidationResult(S["The Index is required."], [nameof(metadata.IndexName)]));
        }

        return Task.CompletedTask;
    }

    private static Task PopulateAsync(AIProfile profile, JsonNode data)
    {
        if (profile.Source != AzureAISearchProfileSource.Key)
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

        var includeContentItemCitations = metadataNode[nameof(metadata.IncludeContentItemCitations)]?.GetValue<bool?>();

        if (includeContentItemCitations.HasValue)
        {
            metadata.IncludeContentItemCitations = includeContentItemCitations.Value;
        }

        profile.Put(metadata);

        return Task.CompletedTask;
    }
}
