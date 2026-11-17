using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;
using OrchardCore.Users.Events;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.AI.Agent.Users;

/// <summary>
/// Switches off two-factor authentication for a user, as the "Disable two-factor authentication" action of the Users
/// admin page does. Unlike that action it refuses the account of the caller. It reads and returns no authenticator
/// key and no recovery code.
/// </summary>
public sealed class DisableUserTwoFactorTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "disableUserTwoFactor";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "userId": {
          "type": "string",
          "description": "The ID of the user who lost their second factor, from listUsers or getUser."
        }
      },
      "required": [
        "userId"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Switches off two-factor authentication for a user who lost their second factor, so they can sign in with their password and set it up again. It refuses for the calling user's own account.";

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

        // The check of the admin action, which passes no user.
        if (caller is null || !await AgentToolAuthorization.IsAuthorizedAsync(services, UsersPermissions.DisableTwoFactorAuthenticationForUsers))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("userId", out var userId))
        {
            return "Nothing was changed: give the userId of the user. Use listUsers to find it.";
        }

        var userManager = services.GetRequiredService<UserManager<IUser>>();

        if (await userManager.FindByIdAsync(userId.Trim()) is not User user)
        {
            return $"Nothing was changed: there is no user with the ID '{userId}'. Use listUsers to find the ID.";
        }

        // Switching off the second factor of the account that asks would let one sentence weaken the caller's own
        // sign-in. A person does that from their own account.
        if (UserToolAccess.IsCaller(caller, user))
        {
            return "Nothing was changed: this is your own account. This tool does not switch off the two-factor authentication of the person who asks. Switch it off from your own account.";
        }

        if (!await userManager.GetTwoFactorEnabledAsync(user))
        {
            return AgentToolResults.Json(new
            {
                disabled = false,
                userId = user.UserId,
                userName = user.UserName,
                note = "Two-factor authentication is not enabled for this user.",
            });
        }

        var result = await userManager.SetTwoFactorEnabledAsync(user, false);

        if (!result.Succeeded)
        {
            return $"Nothing was changed: {string.Join(" ", result.Errors.Select(identityError => identityError.Description))}".TrimEnd();
        }

        // The site, or a rule for the roles of the user, can require two-factor authentication. The handlers are asked
        // directly: their coordinator keeps the answer of the first user it was asked about for the whole request.
        var required = false;

        foreach (var handler in services.GetServices<ITwoFactorAuthenticationHandler>())
        {
            if (await handler.IsRequiredAsync(user))
            {
                required = true;

                break;
            }
        }

        return AgentToolResults.Json(new
        {
            disabled = true,
            userId = user.UserId,
            userName = user.UserName,
            mustEnrollAgain = required,
            note = required
                ? "The user signs in with their password. This site requires two-factor authentication for this user, so they are asked to set it up again at the next sign-in."
                : "The user signs in with their password and can set two-factor authentication up again from their own account.",
        });
    }
}
