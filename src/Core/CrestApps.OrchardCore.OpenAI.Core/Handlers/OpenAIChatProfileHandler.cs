using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;
using OrchardCore.Liquid;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.OpenAI.Core.Handlers;

public sealed class OpenAIChatProfileHandler : AIChatProfileHandlerBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAIChatProfileStore _profileStore;
    private readonly IAIDeploymentStore _modelDeploymentStore;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public OpenAIChatProfileHandler(
        IHttpContextAccessor httpContextAccessor,
        IAIChatProfileStore profileStore,
        IAIDeploymentStore modelDeploymentStore,
        ILiquidTemplateManager liquidTemplateManager,
        IClock clock,
        IStringLocalizer<OpenAIChatProfileHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _profileStore = profileStore;
        _modelDeploymentStore = modelDeploymentStore;
        _liquidTemplateManager = liquidTemplateManager;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingAIChatProfileContext context)
        => PopulateAsync(context.Profile, context.Data);

    public override Task UpdatingAsync(UpdatingAIChatProfileContext context)
        => PopulateAsync(context.Profile, context.Data);

    private static Task PopulateAsync(AIChatProfile profile, JsonNode data)
    {
        var metadataNode = data["Properties"]?[nameof(OpenAIChatProfileMetadata)]?.AsObject();

        if (metadataNode == null || metadataNode.Count == 0)
        {
            return Task.CompletedTask;
        }

        var metadata = profile.As<OpenAIChatProfileMetadata>();

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

        profile.Put(metadata);

        return Task.CompletedTask;
    }
}
