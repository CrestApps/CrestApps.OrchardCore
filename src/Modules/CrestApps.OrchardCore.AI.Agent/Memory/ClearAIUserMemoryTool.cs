using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.ChatSessions;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.AI.Agent.Memory;

/// <summary>
/// Deletes everything the AI has remembered about one user, as the Clear AI memory action of a user's page does,
/// deleting each memory through the memory manager so it leaves the memory index too. Clearing another user's memory
/// needs the "for others" permission, checked on its own as well as through the memory permission handler.
/// </summary>
public sealed class ClearAIUserMemoryTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "clearAIUserMemory";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "userName": {
          "type": "string",
          "description": "The user name of the user whose AI memory is cleared. Leave out userName and userId to clear your own."
        },
        "userId": {
          "type": "string",
          "description": "The user ID of the user whose AI memory is cleared, instead of userName."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes everything the AI has remembered about one user, or about you when no user is given, and returns how many memories were removed. It cannot be undone. Another user's memory needs the permission to clear AI memory for others.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ClearAIMemory))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var callerId = RunningChat.GetCallerId(services);
        var hasUserName = arguments.TryGetFirstString("userName", out var userName) && !string.IsNullOrWhiteSpace(userName);
        var hasUserId = arguments.TryGetFirstString("userId", out var userId) && !string.IsNullOrWhiteSpace(userId);

        if (hasUserName && hasUserId)
        {
            return "Nothing was cleared: give userName or userId, not both.";
        }

        string targetName = null;

        if (hasUserName || hasUserId)
        {
            var userManager = services.GetRequiredService<UserManager<IUser>>();
            var user = hasUserName
                ? await userManager.FindByNameAsync(userName.Trim())
                : await userManager.FindByIdAsync(userId.Trim());

            if (user is null)
            {
                return $"Nothing was cleared: no user has the {(hasUserName ? "user name" : "ID")} '{(hasUserName ? userName.Trim() : userId.Trim())}'.";
            }

            userId = await userManager.GetUserIdAsync(user);
            targetName = await userManager.GetUserNameAsync(user);
        }
        else
        {
            userId = callerId;
        }

        if (string.IsNullOrEmpty(userId))
        {
            return "Nothing was cleared: the user could not be identified.";
        }

        var isOwnMemory = string.Equals(userId, callerId, StringComparison.Ordinal);

        // The memory permission handler answers with the user as the resource; a role that grants the own-memory
        // permission is not limited by the resource, so another user's memory is also checked for the "for others"
        // permission on its own.
        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ClearAIMemory, userId) ||
            (!isOwnMemory && !await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ClearAIMemoryForOthers)))
        {
            return isOwnMemory
                ? AIToolCallerAuthorization.NotAllowedMessage
                : "Nothing was cleared: clearing another user's AI memory needs the permission to clear AI memory for others.";
        }

        var result = await AIUserMemoryCleaner.ClearAsync(
            services.GetRequiredService<IAIMemoryStore>(),
            services.GetRequiredService<ICatalogManager<AIMemoryEntry>>(),
            userId);

        return AgentToolResults.Json(new
        {
            userId,
            userName = isOwnMemory ? AgentToolAuthorization.GetCaller(services)?.Identity?.Name : targetName,
            yours = isOwnMemory,
            found = result.Found,
            removed = result.Removed,
            note = result.Found == 0
                ? "No saved AI memory was found for this user."
                : result.Removed == result.Found
                    ? "All saved AI memory for this user has been cleared."
                    : "Some saved AI memory entries could not be removed.",
        });
    }
}
