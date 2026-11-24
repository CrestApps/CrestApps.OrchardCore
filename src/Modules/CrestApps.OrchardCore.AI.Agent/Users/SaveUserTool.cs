using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.AI.Agent.Users;

/// <summary>
/// Updates the user name, email and phone number of a user, the way the Edit page of the Users admin area does: the
/// same permission on the user, the same site settings that lock a field, the same phone number check, and the user
/// manager to validate and store. It takes no password and no role, and it does not create a user.
/// </summary>
public sealed class SaveUserTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveUser";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "userId": {
          "type": "string",
          "description": "The ID of the user to update, from listUsers or getUser."
        },
        "userName": {
          "type": "string",
          "description": "The new user name. Leave it out to keep the user name. The site can lock user names in Settings > Security > User Login."
        },
        "email": {
          "type": "string",
          "description": "The new email address. Leave it out to keep the email. The site can lock emails in Settings > Security > User Login."
        },
        "phoneNumber": {
          "type": "string",
          "description": "The new phone number, or an empty text to remove it. Leave it out to keep the phone number. The site can lock phone numbers in Settings > Security > User Login."
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
    public override string Description => "Updates a user's user name, email and phone number. It cannot create a user and it cannot set a password: a person does both on the Users page. Roles are changed with setUserRoles.";

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

        if (!arguments.TryGetFirstString("userId", out var userId))
        {
            return "Nothing was saved: give the userId of the user to update. This tool does not create a user: a person creates one, with a password, on the Users page (Access Control > Users > Add User).";
        }

        var (user, error) = await UserToolAccess.FindAsync(services, userId, UsersPermissions.EditUsers);

        if (error is not null)
        {
            return error;
        }

        var hasUserName = arguments.TryGetFirstString("userName", out var userName);
        var hasEmail = arguments.TryGetFirstString("email", out var email);
        var hasPhoneNumber = arguments.TryGetFirstString("phoneNumber", true, out var phoneNumber);

        userName = userName?.Trim();
        email = email?.Trim();
        phoneNumber = phoneNumber?.Trim();

        // What the call asks to change, compared with what is stored.
        var changesUserName = hasUserName && !string.Equals(userName, user.UserName, StringComparison.Ordinal);
        var changesEmail = hasEmail && !string.Equals(email, user.Email, StringComparison.Ordinal);
        var changesPhoneNumber = hasPhoneNumber && !string.Equals(phoneNumber, user.PhoneNumber ?? string.Empty, StringComparison.Ordinal);

        if (!hasUserName && !hasEmail && !hasPhoneNumber)
        {
            return "Nothing was saved: give userName, email or phoneNumber to change the user.";
        }

        if (!changesUserName && !changesEmail && !changesPhoneNumber)
        {
            return "Nothing was saved: the user already has these values.";
        }

        if ((hasUserName && string.IsNullOrEmpty(userName)) || (hasEmail && string.IsNullOrEmpty(email)))
        {
            return "Nothing was saved: the user name and the email of a user cannot be empty.";
        }

        var isOwnAccount = UserToolAccess.IsCaller(caller, user);

        // As on the Edit page, a person changes their own user information only with the permission for it.
        if (isOwnAccount && !await AgentToolAuthorization.IsAuthorizedAsync(services, UsersPermissions.EditOwnUser))
        {
            return "Nothing was saved: you are not allowed to change your own user information.";
        }

        // The Edit page shows a locked field as read-only, to administrators too. A locked field is refused, not skipped.
        var loginSettings = await services.GetRequiredService<ISiteService>().GetSettingsAsync<LoginSettings>();

        if (changesUserName && !loginSettings.AllowChangingUsername)
        {
            return "Nothing was saved: this site does not allow changing a user name. An administrator can allow it in Settings > Security > User Login.";
        }

        if (changesEmail && !loginSettings.AllowChangingEmail)
        {
            return "Nothing was saved: this site does not allow changing an email address. An administrator can allow it in Settings > Security > User Login.";
        }

        if (changesPhoneNumber && !loginSettings.AllowChangingPhoneNumber)
        {
            return "Nothing was saved: this site does not allow changing a phone number. An administrator can allow it in Settings > Security > User Login.";
        }

        if (changesPhoneNumber && !string.IsNullOrEmpty(phoneNumber) && !services.GetRequiredService<IPhoneFormatValidator>().IsValid(phoneNumber))
        {
            return $"Nothing was saved: '{phoneNumber}' is not a valid phone number. Give it in the international format, for example +14155550100.";
        }

        var previous = (user.UserName, user.Email, user.PhoneNumber);
        var changed = new List<string>();

        if (changesUserName)
        {
            user.UserName = userName;
            changed.Add("userName");
        }

        if (changesEmail)
        {
            user.Email = email;
            changed.Add("email");
        }

        if (changesPhoneNumber)
        {
            user.PhoneNumber = phoneNumber;
            changed.Add("phoneNumber");
        }

        // The user manager validates the user (a user name or an email that is taken or not valid) and stores it.
        var result = await services.GetRequiredService<UserManager<IUser>>().UpdateAsync(user);

        if (!result.Succeeded)
        {
            // The loaded user is shared with the rest of the request, so the refused values are taken back.
            (user.UserName, user.Email, user.PhoneNumber) = previous;

            return $"Nothing was saved: {string.Join(" ", result.Errors.Select(identityError => identityError.Description))}".TrimEnd();
        }

        string note = null;

        if (isOwnAccount)
        {
            note = await RefreshSignInAsync(services, user)
                ? "Your sign-in was refreshed with the new values."
                : "Sign out and sign in again to see the new values in your own session.";
        }
        else if (changesEmail)
        {
            note = "Whether the email is confirmed did not change. Use setUserAccountState with action confirmEmail only when the new address is known to be verified.";
        }

        return AgentToolResults.Json(new
        {
            saved = true,
            changed,
            user = UserToolProjection.Summarize(user),
            adminUrl = UserToolAccess.GetAdminUrl(services, "Edit", user.UserId),
            note,
        });
    }

    private static async Task<bool> RefreshSignInAsync(IServiceProvider services, User user)
    {
        // The Edit page refreshes the sign-in of a person who changed their own account. A chat answer that is already
        // being streamed cannot set the sign-in cookie any more, so the tool says what to do instead.
        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;
        var signInManager = services.GetService<SignInManager<IUser>>();

        if (httpContext is null || httpContext.Response.HasStarted || signInManager is null)
        {
            return false;
        }

        await signInManager.RefreshSignInAsync(user);

        return true;
    }
}
