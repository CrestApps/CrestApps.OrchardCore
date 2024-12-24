using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using CrestApps.OrchardCore.OpenAI.Core.Handlers;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;

public sealed class AzureOpenAIProfileWithAISearchHandler : AIChatProfileHandlerBase
{
    internal readonly IStringLocalizer S;

    public AzureOpenAIProfileWithAISearchHandler(IStringLocalizer<AzureOpenAIProfileWithAISearchHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingAIChatProfileContext context)
        => PopulateAsync(context.Profile, context.Data);

    public override Task UpdatingAsync(UpdatingAIChatProfileContext context)
        => PopulateAsync(context.Profile, context.Data);

    public override Task ValidatedAsync(ValidatedAIChatProfileContext context)
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

    private static Task PopulateAsync(AIChatProfile profile, JsonNode data)
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

        profile.Put(metadata);

        return Task.CompletedTask;
    }
}
