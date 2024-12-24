using System.ComponentModel.DataAnnotations;
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

        var metadata = context.Profile.As<AzureAIChatProfileMetadata>();

        if (string.IsNullOrWhiteSpace(metadata.DeploymentName))
        {
            context.Result.Fail(new ValidationResult(S["The Deployment is required."], [nameof(metadata.DeploymentName)]));
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

        var deploymentName = data[nameof(metadata.DeploymentName)]?.GetValue<string>();

        if (!string.IsNullOrEmpty(deploymentName))
        {
            metadata.DeploymentName = deploymentName;
        }

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

        var tokenLength = data[nameof(metadata.TokenLength)]?.GetValue<int?>();

        if (frequencyPenalty.HasValue)
        {
            metadata.TokenLength = tokenLength;
        }

        var pastMessagesCount = data[nameof(metadata.PastMessagesCount)]?.GetValue<int?>();

        if (pastMessagesCount.HasValue)
        {
            metadata.PastMessagesCount = pastMessagesCount;
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
