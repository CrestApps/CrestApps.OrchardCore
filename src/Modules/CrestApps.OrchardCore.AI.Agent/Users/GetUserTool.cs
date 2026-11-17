using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.AI.Agent.Users;

/// <summary>
/// Returns one user, as the View page of the Users admin area does: profile, roles and sign-in state, built from an
/// explicit list of fields. It never returns a password hash, a security stamp, a token or the key of an external login.
/// </summary>
public sealed class GetUserTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getUser";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "userId": {
          "type": "string",
          "description": "The ID of the user, from listUsers. Give this, userName or email."
        },
        "userName": {
          "type": "string",
          "description": "The exact user name of the user. Used when userId is not given."
        },
        "email": {
          "type": "string",
          "description": "The exact email address of the user. Used when userId and userName are not given."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Gets one user by id, user name or email: profile, roles, enabled, email confirmed, whether a password is set, locked out until, failed sign-in count, two-factor state, external login provider names, time zone, culture and custom user settings. Call it before changing a user. It never returns a password, a token or a key.";

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

        var userManager = services.GetRequiredService<UserManager<IUser>>();
        IUser found;
        string searched;

        if (arguments.TryGetFirstString("userId", out var userId) && !string.IsNullOrWhiteSpace(userId))
        {
            found = await userManager.FindByIdAsync(userId.Trim());
            searched = $"the ID '{userId}'";
        }
        else if (arguments.TryGetFirstString("userName", out var userName) && !string.IsNullOrWhiteSpace(userName))
        {
            found = await userManager.FindByNameAsync(userName.Trim());
            searched = $"the user name '{userName}'";
        }
        else if (arguments.TryGetFirstString("email", out var email) && !string.IsNullOrWhiteSpace(email))
        {
            found = await userManager.FindByEmailAsync(email.Trim());
            searched = $"the email '{email}'";
        }
        else
        {
            return "Give userId, userName or email. Use listUsers to find a user.";
        }

        if (found is not User user)
        {
            return await UserToolAccess.GetNotFoundMessageAsync(
                services,
                UsersPermissions.ViewUsers,
                $"There is no user with {searched}. Use listUsers to find the user.");
        }

        // The View page checks the permission on the user. A person may always read their own account.
        if (!UserToolAccess.IsCaller(caller, user) &&
            !await AgentToolAuthorization.IsAuthorizedAsync(services, UsersPermissions.ViewUsers, user))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        // The user editor shows a custom user settings type to a caller who holds the permission of that type.
        var customSettings = await UserToolProjection.ReadCustomSettingsAsync(
            services,
            user,
            settingsType => AIToolCallerAuthorization.IsAuthorizedAsync(services, OrchardCorePermissions.CreateManageOwnCustomUserSettings(settingsType)));

        return AgentToolResults.Json(new
        {
            user = await UserToolProjection.DescribeAsync(services, user, customSettings),
        });
    }
}
