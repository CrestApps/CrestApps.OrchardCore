using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.AI.Agent.Users;

/// <summary>
/// Enables disabled user accounts through the user service, as the Enable action and the Enable bulk action of the
/// Users admin page do, and reports the outcome for each user.
/// </summary>
public sealed class EnableUserTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "enableUser";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "userIds": {
          "type": "array",
          "description": "The IDs of the users to enable, from listUsers. At most 20.",
          "items": {
            "type": "string"
          },
          "minItems": 1,
          "maxItems": 20
        }
      },
      "required": [
        "userIds"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Enables one or more disabled user accounts so they can sign in again. Use it to approve users waiting for moderation. It reports the outcome for each user.";

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

        // A caller who may edit no user at all is refused before any account is looked up.
        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, UsersPermissions.EditUsers, new User()))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirst<string[]>("userIds", out var givenIds);

        var userIds = UserToolAccess.CleanIds(givenIds);

        if (userIds.Count == 0)
        {
            return "Nothing was changed: userIds must be a list of user IDs. Use listUsers with status disabled to find them.";
        }

        if (userIds.Count > UserToolAccess.MaxUsers)
        {
            return $"Nothing was changed: one call enables at most {UserToolAccess.MaxUsers} users. Enable the users in several calls.";
        }

        var userManager = services.GetRequiredService<UserManager<IUser>>();
        var userService = services.GetRequiredService<IUserService>();
        var results = new List<object>();
        var enabledCount = 0;

        foreach (var userId in userIds)
        {
            // The check of the Enable action, on the user. An ID the caller may not edit gets the answer of an ID that
            // does not exist, so the tool does not tell which accounts exist.
            if (await userManager.FindByIdAsync(userId) is not User user ||
                !await AgentToolAuthorization.IsAuthorizedAsync(services, UsersPermissions.EditUsers, user))
            {
                results.Add(Row(userId, null, "notFound", "There is no user with this ID that you are allowed to edit."));

                continue;
            }

            if (user.IsEnabled)
            {
                results.Add(Row(userId, user.UserName, "alreadyEnabled"));

                continue;
            }

            if (await userService.EnableAsync(user))
            {
                enabledCount++;
                results.Add(Row(userId, user.UserName, "enabled"));
            }
            else
            {
                results.Add(Row(userId, user.UserName, "failed", "The user could not be enabled. The site's log has the reason."));
            }
        }

        return AgentToolResults.Json(new
        {
            enabledCount,
            results,
        });
    }

    private static object Row(string userId, string userName, string outcome, string reason = null)
        => new { userId, userName, outcome, reason };
}
