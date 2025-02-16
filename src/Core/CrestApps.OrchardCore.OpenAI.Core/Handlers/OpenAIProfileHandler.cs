using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;
using OrchardCore.Liquid;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.OpenAI.Core.Handlers;

public sealed class OpenAIProfileHandler : AIProfileHandlerBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAIProfileStore _profileStore;
    private readonly IAIDeploymentStore _deploymentStore;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public OpenAIProfileHandler(
        IHttpContextAccessor httpContextAccessor,
        IAIProfileStore profileStore,
        IAIDeploymentStore deploymentStore,
        ILiquidTemplateManager liquidTemplateManager,
        IClock clock,
        IStringLocalizer<OpenAIProfileHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _profileStore = profileStore;
        _deploymentStore = deploymentStore;
        _liquidTemplateManager = liquidTemplateManager;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingAIProfileContext context)
        => PopulateAsync(context.Profile, context.Data);

    public override Task UpdatingAsync(UpdatingAIProfileContext context)
        => PopulateAsync(context.Profile, context.Data);

    private static Task PopulateAsync(AIProfile profile, JsonNode data)
    {
        var metadataNode = data["Properties"]?[nameof(AIProfileMetadata)]?.AsObject();

        if (metadataNode == null || metadataNode.Count == 0)
        {
            return Task.CompletedTask;
        }

        var metadata = profile.As<AIProfileMetadata>();

        var settings = profile.GetSettings<AIProfileSettings>();

        if (!settings.LockSystemMessage)
        {
            var systemMessage = data[nameof(AIProfileMetadata.SystemMessage)]?.GetValue<string>()?.Trim();

            if (!string.IsNullOrEmpty(systemMessage))
            {
                metadata.SystemMessage = systemMessage;
            }
        }

        var temperature = metadataNode[nameof(metadata.Temperature)]?.GetValue<float?>();

        if (temperature.HasValue)
        {
            metadata.Temperature = temperature;
        }

        var topP = metadataNode[nameof(metadata.TopP)]?.GetValue<float?>();

        if (topP.HasValue)
        {
            metadata.TopP = topP;
        }

        var frequencyPenalty = metadataNode[nameof(metadata.FrequencyPenalty)]?.GetValue<float?>();

        if (frequencyPenalty.HasValue)
        {
            metadata.FrequencyPenalty = frequencyPenalty;
        }

        var presencePenalty = metadataNode[nameof(metadata.PresencePenalty)]?.GetValue<float?>();

        if (frequencyPenalty.HasValue)
        {
            metadata.PresencePenalty = presencePenalty;
        }

        var maxTokens = metadataNode[nameof(metadata.MaxTokens)]?.GetValue<int?>();

        if (frequencyPenalty.HasValue)
        {
            metadata.MaxTokens = maxTokens;
        }

        var pastMessagesCount = metadataNode[nameof(metadata.PastMessagesCount)]?.GetValue<int?>();

        if (pastMessagesCount.HasValue)
        {
            metadata.PastMessagesCount = pastMessagesCount;
        }

        var useCaching = metadataNode[nameof(metadata.UseCaching)]?.GetValue<bool?>();

        if (useCaching.HasValue)
        {
            metadata.UseCaching = useCaching.Value;
        }

        profile.Put(metadata);

        return Task.CompletedTask;
    }
}
