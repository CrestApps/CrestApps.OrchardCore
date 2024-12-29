using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Core.Handlers;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;

public sealed class AzureOpenAIProfileHandler : AIChatProfileHandlerBase
{
    internal readonly IStringLocalizer S;

    public AzureOpenAIProfileHandler(IStringLocalizer<AzureOpenAIProfileHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingAIChatProfileContext context)
        => PopulateAsync(context.Profile, context.Data);

    public override Task UpdatingAsync(UpdatingAIChatProfileContext context)
        => PopulateAsync(context.Profile, context.Data);

    public override Task ValidatedAsync(ValidatedAIChatProfileContext context)
    {
        if (context.Profile.Source is null || !context.Profile.Source.StartsWith("Azure"))
        {
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    private static Task PopulateAsync(AIChatProfile profile, JsonNode data)
    {
        if (profile.Source is null || !profile.Source.StartsWith("Azure"))
        {
            return Task.CompletedTask;
        }

        var metadata = profile.As<AzureAIChatProfileMetadata>();

        var systemMessage = data[nameof(metadata.SystemMessage)]?.GetValue<string>();

        if (!string.IsNullOrEmpty(systemMessage))
        {
            metadata.SystemMessage = systemMessage;
        }

        var temperature = data[nameof(metadata.Temperature)]?.GetValue<float?>();

        if (temperature.HasValue)
        {
            metadata.Temperature = temperature;
        }

        var topP = data[nameof(metadata.TopP)]?.GetValue<float?>();

        if (topP.HasValue)
        {
            metadata.TopP = topP;
        }

        var frequencyPenalty = data[nameof(metadata.FrequencyPenalty)]?.GetValue<float?>();

        if (frequencyPenalty.HasValue)
        {
            metadata.FrequencyPenalty = frequencyPenalty;
        }

        var presencePenalty = data[nameof(metadata.PresencePenalty)]?.GetValue<float?>();

        if (frequencyPenalty.HasValue)
        {
            metadata.PresencePenalty = presencePenalty;
        }

        var maxTokens = data[nameof(metadata.MaxTokens)]?.GetValue<int?>();

        if (frequencyPenalty.HasValue)
        {
            metadata.MaxTokens = maxTokens;
        }

        var pastMessagesCount = data[nameof(metadata.PastMessagesCount)]?.GetValue<int?>();

        if (pastMessagesCount.HasValue)
        {
            metadata.PastMessagesCount = pastMessagesCount;
        }

        profile.Put(metadata);

        return Task.CompletedTask;
    }
}
