using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;
using YSession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Agent.Users;

/// <summary>
/// Deletes a user account through the user manager, as the Delete action of the Users admin page does. That action
/// leaves three cases to the person, and the tool refuses them: the caller's own account, the site owner and the only
/// enabled administrator.
/// </summary>
public sealed class DeleteUserTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteUser";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "userId": {
          "type": "string",
          "description": "The ID of the user to delete, from listUsers or getUser."
        },
        "userName": {
          "type": "string",
          "description": "The user name of that user, exactly as getUser returns it. It is checked against the ID so the wrong account is not deleted, and it shows the person who approves which account goes."
        }
      },
      "required": [
        "userId",
        "userName"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Permanently deletes a user account; this cannot be undone. It refuses for the calling user's own account, the site super user and the only enabled administrator; suggest setUserAccountState with action disable when the admin only wants to block access.";

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

        if (caller is null)
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("userId", out var userId);

        var (user, error) = await UserToolAccess.FindAsync(services, userId, UsersPermissions.DeleteUsers);

        if (error is not null)
        {
            return error;
        }

        if (!arguments.TryGetFirstString("userName", out var userName) ||
            !string.Equals(userName.Trim(), user.UserName, StringComparison.OrdinalIgnoreCase))
        {
            return "Nothing was deleted: userName does not match the user with this ID. Read the user with getUser and give its user name.";
        }

        // The page hides the Delete link for the own account and has no guard for the other two cases.
        if (UserToolAccess.IsCaller(caller, user))
        {
            return "Nothing was deleted: this is your own account. This tool does not delete the account of the person who asks.";
        }

        if (await UserToolAccess.IsSiteOwnerAsync(services, user.UserId))
        {
            return $"Nothing was deleted: '{user.UserName}' is the site owner (the super user of the site) and cannot be deleted by this tool.";
        }

        if (await UserToolAccess.IsOnlyEnabledAdministratorAsync(services, user))
        {
            return $"Nothing was deleted: '{user.UserName}' is the only enabled administrator. Give the administrator role to another enabled user first.";
        }

        var deleted = new
        {
            userId = user.UserId,
            userName = user.UserName,
            email = user.Email,
        };

        var result = await services.GetRequiredService<UserManager<IUser>>().DeleteAsync(user);

        if (!result.Succeeded)
        {
            // As the Delete action does when it fails.
            await services.GetRequiredService<YSession>().CancelAsync();

            return $"Nothing was deleted: {string.Join(" ", result.Errors.Select(identityError => identityError.Description))}".TrimEnd();
        }

        return AgentToolResults.Json(new
        {
            deleted = true,
            user = deleted,
            note = "The account cannot be restored. Content the user owned keeps its owner ID.",
        });
    }
}
