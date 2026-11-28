using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.AI;

/// <summary>
/// Sets whether the calling user can be assigned new messaging conversations, as the workspace's availability toggle does.
/// </summary>
internal sealed class SetMessagingAvailabilityTool : MessagingAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setMessagingAvailability";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "available": {
          "type": "boolean",
          "description": "true to be assigned new messaging conversations, false to stop."
        }
      },
      "required": ["available"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Sets whether the calling user is available to be assigned new messaging conversations.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!arguments.TryGetFirst<bool>("available", out var available))
        {
            return "Nothing was changed: say with available whether you want new conversations (true) or not (false).";
        }

        var agent = await services.GetRequiredService<MessagingWorkspaceBuilder>().GetCurrentAgentAsync(user);

        if (agent is null)
        {
            return "Nothing was changed: you have no agent profile.";
        }

        var availability = await services.GetRequiredService<IMessagingAvailabilityService>().SetAvailableAsync(agent, available, cancellationToken);

        return Json(new
        {
            available = availability.Available,
        });
    }
}
