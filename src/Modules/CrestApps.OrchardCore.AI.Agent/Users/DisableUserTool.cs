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
/// Disables user accounts through the user service, as the Disable action and the Disable bulk action of the Users
/// admin page do, and reports the outcome for each user. It refuses the caller's own account, the site owner and the
/// only enabled administrator.
/// </summary>
public sealed class DisableUserTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "disableUser";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "userIds": {
          "type": "array",
          "description": "The IDs of the users to disable, from listUsers. At most 20.",
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
    public override string Description => "Disables one or more user accounts so they can no longer sign in. It refuses for the calling user's own account, the site super user and the only enabled administrator. It reports the outcome for each user.";

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
        var caller = AgentToolAuthorization.GetCaller(services);

        // A caller who may edit no user at all is refused before any account is looked up.
        if (caller is null || !await AgentToolAuthorization.IsAuthorizedAsync(services, UsersPermissions.EditUsers, new User()))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirst<string[]>("userIds", out var givenIds);

        var userIds = UserToolAccess.CleanIds(givenIds);

        if (userIds.Count == 0)
        {
            return "Nothing was changed: userIds must be a list of user IDs. Use listUsers to find them.";
        }

        if (userIds.Count > UserToolAccess.MaxUsers)
        {
            return $"Nothing was changed: one call disables at most {UserToolAccess.MaxUsers} users. Disable the users in several calls.";
        }

        var userManager = services.GetRequiredService<UserManager<IUser>>();
        var userService = services.GetRequiredService<IUserService>();
        var results = new List<object>();
        var disabledCount = 0;

        foreach (var userId in userIds)
        {
            // The check of the Disable action, on the user. An ID the caller may not edit gets the answer of an ID
            // that does not exist, so the tool does not tell which accounts exist.
            if (await userManager.FindByIdAsync(userId) is not User user ||
                !await AgentToolAuthorization.IsAuthorizedAsync(services, UsersPermissions.EditUsers, user))
            {
                results.Add(Row(userId, null, "notFound", "There is no user with this ID that you are allowed to edit."));

                continue;
            }

            // The page hides these actions or leaves them to the person; a chat refuses them.
            if (UserToolAccess.IsCaller(caller, user))
            {
                results.Add(Row(userId, user.UserName, "refused", "This is your own account. This tool does not disable the account of the person who asks."));

                continue;
            }

            if (await UserToolAccess.IsSiteOwnerAsync(services, user.UserId))
            {
                results.Add(Row(userId, user.UserName, "refused", "This is the site owner (the super user of the site). It cannot be disabled by this tool."));

                continue;
            }

            if (!user.IsEnabled)
            {
                results.Add(Row(userId, user.UserName, "alreadyDisabled"));

                continue;
            }

            if (await UserToolAccess.IsOnlyEnabledAdministratorAsync(services, user))
            {
                results.Add(Row(userId, user.UserName, "refused", "This is the only enabled administrator. Give the administrator role to another enabled user first."));

                continue;
            }

            if (await userService.DisableAsync(user))
            {
                disabledCount++;
                results.Add(Row(userId, user.UserName, "disabled"));
            }
            else
            {
                results.Add(Row(userId, user.UserName, "failed", "The user could not be disabled. The site keeps one enabled administrator; otherwise the site's log has the reason."));
            }
        }

        return AgentToolResults.Json(new
        {
            disabledCount,
            results,
            note = disabledCount > 0
                ? "A disabled user cannot sign in. Use enableUser to give access back."
                : null,
        });
    }

    private static object Row(string userId, string userName, string outcome, string reason = null)
        => new { userId, userName, outcome, reason };
}
