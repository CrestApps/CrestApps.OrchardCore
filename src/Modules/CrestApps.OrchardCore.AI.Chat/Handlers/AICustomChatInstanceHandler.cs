using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using Microsoft.AspNetCore.Http;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Chat.Handlers;

public sealed class AICustomChatInstanceHandler : CatalogEntryHandlerBase<AICustomChatInstance>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    public AICustomChatInstanceHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
    }

    public override Task InitializingAsync(InitializingContext<AICustomChatInstance> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<AICustomChatInstance> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task InitializedAsync(InitializedContext<AICustomChatInstance> context)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        var user = _httpContextAccessor.HttpContext?.User;

        if (user != null)
        {
            context.Model.UserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        return Task.CompletedTask;
    }

    private static Task PopulateAsync(AICustomChatInstance instance, JsonNode data)
    {
        if (data == null)
        {
            return Task.CompletedTask;
        }

        var displayText = data[nameof(AICustomChatInstance.DisplayText)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(displayText))
        {
            instance.DisplayText = displayText;
        }

        var connectionName = data[nameof(AICustomChatInstance.ConnectionName)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(connectionName))
        {
            instance.ConnectionName = connectionName;
        }

        var deploymentId = data[nameof(AICustomChatInstance.DeploymentId)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(deploymentId))
        {
            instance.DeploymentId = deploymentId;
        }

        var systemMessage = data[nameof(AICustomChatInstance.SystemMessage)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(systemMessage))
        {
            instance.SystemMessage = systemMessage;
        }

        var maxTokens = data[nameof(AICustomChatInstance.MaxTokens)]?.GetValue<int?>();

        if (maxTokens.HasValue)
        {
            instance.MaxTokens = maxTokens.Value;
        }

        var temperature = data[nameof(AICustomChatInstance.Temperature)]?.GetValue<float?>();

        if (temperature.HasValue)
        {
            instance.Temperature = temperature.Value;
        }

        var topP = data[nameof(AICustomChatInstance.TopP)]?.GetValue<float?>();

        if (topP.HasValue)
        {
            instance.TopP = topP.Value;
        }

        var frequencyPenalty = data[nameof(AICustomChatInstance.FrequencyPenalty)]?.GetValue<float?>();

        if (frequencyPenalty.HasValue)
        {
            instance.FrequencyPenalty = frequencyPenalty.Value;
        }

        var presencePenalty = data[nameof(AICustomChatInstance.PresencePenalty)]?.GetValue<float?>();

        if (presencePenalty.HasValue)
        {
            instance.PresencePenalty = presencePenalty.Value;
        }

        var pastMessagesCount = data[nameof(AICustomChatInstance.PastMessagesCount)]?.GetValue<int?>();

        if (pastMessagesCount.HasValue)
        {
            instance.PastMessagesCount = pastMessagesCount.Value;
        }

        var toolNames = data[nameof(AICustomChatInstance.ToolNames)]?.AsArray();

        if (toolNames != null)
        {
            instance.ToolNames = toolNames
                .Select(x => x?.GetValue<string>())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();
        }

        return Task.CompletedTask;
    }
}
