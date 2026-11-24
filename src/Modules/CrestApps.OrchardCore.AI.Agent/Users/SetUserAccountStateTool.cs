using System.Security.Claims;
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
/// Enables, disables or unlocks user accounts, or confirms their email, as the Enable, Disable, Unlock and Confirm
/// email actions (and the bulk actions) of the Users admin page do, and reports the outcome for each user. Disabling
/// refuses the caller's own account, the site owner and the only enabled administrator.
/// </summary>
internal sealed class SetUserAccountStateTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setUserAccountState";

    /// <summary>
    /// The action that enables disabled accounts.
    /// </summary>
    public const string EnableAction = "enable";

    /// <summary>
    /// The action that disables accounts.
    /// </summary>
    public const string DisableAction = "disable";

    /// <summary>
    /// The action that clears a lockout and the count of failed sign-ins.
    /// </summary>
    public const string UnlockAction = "unlock";

    /// <summary>
    /// The action that marks the email address as confirmed.
    /// </summary>
    public const string ConfirmEmailAction = "confirmEmail";

    private static readonly string[] _actions = [EnableAction, DisableAction, UnlockAction, ConfirmEmailAction];

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "userIds": {
          "type": "array",
          "description": "The IDs of the users, from listUsers or getUser. At most 20.",
          "items": {
            "type": "string"
          },
          "minItems": 1,
          "maxItems": 20
        },
        "action": {
          "type": "string",
          "enum": [
            "enable",
            "disable",
            "unlock",
            "confirmEmail"
          ],
          "description": "enable: let disabled accounts sign in again (also approves users waiting for moderation). disable: stop the accounts from signing in. unlock: clear the lockout and the failed sign-in count after too many failed attempts; the password does not change. confirmEmail: mark the email address as confirmed without sending an email, only when the admin says it is already verified."
        }
      },
      "required": [
        "userIds",
        "action"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Enables, disables or unlocks one or more user accounts, or marks their email as confirmed without sending an email. Disabling refuses the calling user's own account, the site super user and the only enabled administrator. It reports the outcome for each user.";

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

        arguments.TryGetFirstString("action", out var givenAction);

        var action = _actions.FirstOrDefault(name => string.Equals(name, givenAction?.Trim(), StringComparison.OrdinalIgnoreCase));

        if (action is null)
        {
            return "Nothing was changed: action must be enable, disable, unlock or confirmEmail.";
        }

        arguments.TryGetFirst<string[]>("userIds", out var givenIds);

        var userIds = UserToolAccess.CleanIds(givenIds);

        if (userIds.Count == 0)
        {
            return "Nothing was changed: userIds must be a list of user IDs. Use listUsers to find them.";
        }

        if (userIds.Count > UserToolAccess.MaxUsers)
        {
            return $"Nothing was changed: one call acts on at most {UserToolAccess.MaxUsers} users. Change the users in several calls.";
        }

        var userManager = services.GetRequiredService<UserManager<IUser>>();
        var results = new List<object>();
        var changedCount = 0;

        foreach (var userId in userIds)
        {
            // The check of the admin action, on the user. An ID the caller may not edit gets the answer of an ID that
            // does not exist, so the tool does not tell which accounts exist.
            if (await userManager.FindByIdAsync(userId) is not User user ||
                !await AgentToolAuthorization.IsAuthorizedAsync(services, UsersPermissions.EditUsers, user))
            {
                results.Add(Row(userId, null, "notFound", "There is no user with this ID that you are allowed to edit."));

                continue;
            }

            var row = action switch
            {
                EnableAction => await EnableAsync(services, user),
                DisableAction => await DisableAsync(services, caller, user),
                UnlockAction => await UnlockAsync(userManager, user),
                _ => await ConfirmEmailAsync(userManager, user),
            };

            if (row.Changed)
            {
                changedCount++;
            }

            results.Add(row.Result);
        }

        return AgentToolResults.Json(new
        {
            action,
            changedCount,
            results,
            note = changedCount == 0
                ? null
                : action switch
                {
                    DisableAction => "A disabled user cannot sign in. Use setUserAccountState with action enable to give access back.",
                    UnlockAction => "The unlocked users can try to sign in again. Their passwords did not change.",
                    ConfirmEmailAction => "No email was sent. The addresses are now treated as verified.",
                    _ => null,
                },
        });
    }

    private static async Task<(bool Changed, object Result)> EnableAsync(IServiceProvider services, User user)
    {
        if (user.IsEnabled)
        {
            return (false, Row(user.UserId, user.UserName, "alreadyEnabled"));
        }

        return await services.GetRequiredService<IUserService>().EnableAsync(user)
            ? (true, Row(user.UserId, user.UserName, "enabled"))
            : (false, Row(user.UserId, user.UserName, "failed", "The user could not be enabled. The site's log has the reason."));
    }

    private static async Task<(bool Changed, object Result)> DisableAsync(IServiceProvider services, ClaimsPrincipal caller, User user)
    {
        // The page hides these actions or leaves them to the person; a chat refuses them.
        if (UserToolAccess.IsCaller(caller, user))
        {
            return (false, Row(user.UserId, user.UserName, "refused", "This is your own account. This tool does not disable the account of the person who asks."));
        }

        if (await UserToolAccess.IsSiteOwnerAsync(services, user.UserId))
        {
            return (false, Row(user.UserId, user.UserName, "refused", "This is the site owner (the super user of the site). It cannot be disabled by this tool."));
        }

        if (!user.IsEnabled)
        {
            return (false, Row(user.UserId, user.UserName, "alreadyDisabled"));
        }

        if (await UserToolAccess.IsOnlyEnabledAdministratorAsync(services, user))
        {
            return (false, Row(user.UserId, user.UserName, "refused", "This is the only enabled administrator. Give the administrator role to another enabled user first."));
        }

        return await services.GetRequiredService<IUserService>().DisableAsync(user)
            ? (true, Row(user.UserId, user.UserName, "disabled"))
            : (false, Row(user.UserId, user.UserName, "failed", "The user could not be disabled. The site keeps one enabled administrator; otherwise the site's log has the reason."));
    }

    private static async Task<(bool Changed, object Result)> UnlockAsync(UserManager<IUser> userManager, User user)
    {
        var failedSignInCount = user.AccessFailedCount;

        // The admin page offers Unlock for a locked-out user only. For a user who is not locked out, the count of
        // failed sign-ins is reset and the lockout end is left alone: the user manager refuses to set it for an
        // account that cannot be locked out.
        if (!await userManager.IsLockedOutAsync(user))
        {
            await userManager.ResetAccessFailedCountAsync(user);

            return (false, new
            {
                userId = user.UserId,
                userName = user.UserName,
                outcome = "notLockedOut",
                clearedFailedSignInCount = failedSignInCount,
                reason = "The user is not locked out. The count of failed sign-ins was reset.",
            });
        }

        // The two calls of the Unlock action. Unlike that action, a failure does not cancel the session, because the
        // changes made for the other users of the call must be kept.
        await userManager.ResetAccessFailedCountAsync(user);

        var result = await userManager.SetLockoutEndDateAsync(user, null);

        if (!result.Succeeded)
        {
            return (false, Row(user.UserId, user.UserName, "failed", $"The user was not unlocked: {string.Join(" ", result.Errors.Select(identityError => identityError.Description))}".TrimEnd()));
        }

        return (true, new
        {
            userId = user.UserId,
            userName = user.UserName,
            outcome = "unlocked",
            clearedFailedSignInCount = failedSignInCount,
            reason = (string)null,
        });
    }

    private static async Task<(bool Changed, object Result)> ConfirmEmailAsync(UserManager<IUser> userManager, User user)
    {
        if (await userManager.IsEmailConfirmedAsync(user))
        {
            return (false, Row(user.UserId, user.UserName, "alreadyConfirmed"));
        }

        if (string.IsNullOrEmpty(user.Email))
        {
            return (false, Row(user.UserId, user.UserName, "refused", "The user has no email address."));
        }

        // As the Confirm email action does: the token is made and spent here, and is not part of the result.
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var result = await userManager.ConfirmEmailAsync(user, token);

        return result.Succeeded
            ? (true, Row(user.UserId, user.UserName, "confirmed"))
            : (false, Row(user.UserId, user.UserName, "failed", $"The email was not confirmed: {string.Join(" ", result.Errors.Select(identityError => identityError.Description))}".TrimEnd()));
    }

    private static object Row(string userId, string userName, string outcome, string reason = null)
        => new { userId, userName, outcome, reason };
}
