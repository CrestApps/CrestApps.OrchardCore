using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using CrestApps.OrchardCore.OpenAI.Core.Handlers;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;

public sealed class AzureOpenAIChatProfileWithAISearchHandler : OpenAIChatProfileHandlerBase
{
    internal readonly IStringLocalizer S;

    public AzureOpenAIChatProfileWithAISearchHandler(IStringLocalizer<AzureOpenAIChatProfileWithAISearchHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingOpenAIChatProfileContext context)
        => PopulateAsync(context.Profile, context.Data);

    public override Task UpdatingAsync(UpdatingOpenAIChatProfileContext context)
        => PopulateAsync(context.Profile, context.Data);

    public override Task ValidatedAsync(ValidatedOpenAIChatProfileContext context)
    {
        if (context.Profile?.Source != AzureWithAzureAISearchProfileSource.Key)
        {
            return Task.CompletedTask;
        }

        var metadata = context.Profile.As<AzureAIChatProfileAISearchMetadata>();

        if (string.IsNullOrWhiteSpace(metadata.IndexName))
        {
            context.Result.Fail(new ValidationResult(S["The Index is required."], [nameof(metadata.IndexName)]));
        }

        return Task.CompletedTask;
    }

    private static Task PopulateAsync(OpenAIChatProfile profile, JsonNode data)
    {
        if (profile.Source != AzureWithAzureAISearchProfileSource.Key)
        {
            return Task.CompletedTask;
        }

        var metadata = profile.As<AzureAIChatProfileAISearchMetadata>();

        var indexName = data[nameof(metadata.IndexName)]?.GetValue<string>();

        if (!string.IsNullOrEmpty(indexName))
        {
            metadata.IndexName = indexName;
        }

        var strictness = data[nameof(metadata.Strictness)]?.GetValue<int?>();

        if (strictness.HasValue)
        {
            metadata.Strictness = strictness;
        }

        var topNDocuments = data[nameof(metadata.TopNDocuments)]?.GetValue<int?>();

        if (topNDocuments.HasValue)
        {
            metadata.TopNDocuments = topNDocuments;
        }

        profile.Put(metadata);

        return Task.CompletedTask;
    }
}
