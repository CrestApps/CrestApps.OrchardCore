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
/// Resets the second sign-in factors of a user who lost them: it switches off two-factor authentication, as the
/// "Disable two-factor authentication" action of the Users admin page does, and can also forget the user's
/// authenticator app key, as the user's own "Reset authenticator app" page does, so the codes of a lost device stop
/// working. Unlike the admin action it refuses the account of the caller. It reads and returns no key and no recovery
/// code. Passkeys are not offered: the Orchard Core version this module is built against has none.
/// </summary>
public sealed class ResetUserSignInFactorsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "resetUserSignInFactors";

    /// <summary>
    /// The factor that switches off two-factor authentication.
    /// </summary>
    public const string TwoFactorFactor = "twoFactor";

    /// <summary>
    /// The factor that forgets the authenticator app key.
    /// </summary>
    public const string AuthenticatorAppFactor = "authenticatorApp";

    private const string PasskeysFactor = "passkeys";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "userId": {
          "type": "string",
          "description": "The ID of the user who lost their second factor, from listUsers or getUser."
        },
        "factors": {
          "type": "array",
          "minItems": 1,
          "items": {
            "type": "string",
            "enum": [ "twoFactor", "authenticatorApp" ]
          },
          "description": "What to reset. twoFactor switches off two-factor authentication, so the user signs in with their password and can set it up again. authenticatorApp forgets the user's authenticator app key, so the codes of a lost or stolen device stop working and the user scans a new code when setting it up again; give it together with twoFactor, unless two-factor authentication is already off."
        }
      },
      "required": [
        "userId",
        "factors"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Resets the second sign-in factors of a user who lost them: switches off two-factor authentication, and can also forget their authenticator app key so a lost device's codes stop working. The user's other sessions are signed out. It refuses for the calling user's own account and never returns a key or a recovery code. Passkeys cannot be removed here: the Orchard Core version this site runs has no passkeys.";

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

        if (!arguments.TryGetFirstString("userId", out var userId) || string.IsNullOrWhiteSpace(userId))
        {
            return "Nothing was changed: give the userId of the user. Use listUsers to find it.";
        }

        if (!AgentToolArguments.TryReadStrings(arguments, "factors", out var requested) || requested is null || requested.Count == 0)
        {
            return $"Nothing was changed: give factors, a list with {TwoFactorFactor} and, for a lost device, {AuthenticatorAppFactor}.";
        }

        var factors = requested
            .Where(factor => !string.IsNullOrWhiteSpace(factor))
            .Select(factor => factor.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (factors.Contains(PasskeysFactor))
        {
            return "Nothing was changed: passkeys cannot be removed here, because the Orchard Core version this site runs has no passkeys. Reset twoFactor and authenticatorApp instead.";
        }

        var unknown = factors
            .Where(factor => !string.Equals(factor, TwoFactorFactor, StringComparison.OrdinalIgnoreCase) && !string.Equals(factor, AuthenticatorAppFactor, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (unknown.Count > 0)
        {
            return $"Nothing was changed: {string.Join(", ", unknown.Select(factor => $"'{factor}'"))} cannot be reset. The factors are {TwoFactorFactor} and {AuthenticatorAppFactor}.";
        }

        var resetTwoFactor = factors.Contains(TwoFactorFactor);
        var resetAuthenticator = factors.Contains(AuthenticatorAppFactor);
        var userManager = services.GetRequiredService<UserManager<IUser>>();

        if (await userManager.FindByIdAsync(userId.Trim()) is not User user)
        {
            return $"Nothing was changed: there is no user with the ID '{userId.Trim()}'. Use listUsers to find the ID.";
        }

        // Resetting the second factor of the account that asks would let one sentence weaken the caller's own sign-in.
        // A person does that from their own account.
        if (UserToolAccess.IsCaller(caller, user))
        {
            return "Nothing was changed: this is your own account. This tool does not reset the sign-in factors of the person who asks. Change them from your own account.";
        }

        var wasEnabled = await userManager.GetTwoFactorEnabledAsync(user);

        // A new authenticator key while two-factor authentication stays on would lock out a user whose only factor is
        // the app, until they use a recovery code.
        if (resetAuthenticator && wasEnabled && !resetTwoFactor)
        {
            return $"Nothing was changed: two-factor authentication is on for this user, so resetting only the authenticator app could lock them out. Give factors [\"{TwoFactorFactor}\", \"{AuthenticatorAppFactor}\"] to switch it off and forget the app's key together.";
        }

        if (resetAuthenticator && !userManager.SupportsUserAuthenticatorKey)
        {
            return "Nothing was changed: the user store of this site keeps no authenticator app keys.";
        }

        var twoFactorDisabled = false;

        if (resetTwoFactor && wasEnabled)
        {
            var result = await userManager.SetTwoFactorEnabledAsync(user, false);

            if (!result.Succeeded)
            {
                return $"Nothing was changed: {Describe(result)}".TrimEnd();
            }

            twoFactorDisabled = true;
        }

        var authenticatorReset = false;

        if (resetAuthenticator)
        {
            var result = await userManager.ResetAuthenticatorKeyAsync(user);

            if (!result.Succeeded)
            {
                return twoFactorDisabled
                    ? $"Two-factor authentication was switched off, but the authenticator app could not be reset: {Describe(result)}".TrimEnd()
                    : $"Nothing was changed: {Describe(result)}".TrimEnd();
            }

            authenticatorReset = true;
        }

        if (!twoFactorDisabled && !authenticatorReset)
        {
            return AgentToolResults.Json(new
            {
                changed = false,
                userId = user.UserId,
                userName = user.UserName,
                twoFactorDisabled = false,
                authenticatorAppReset = false,
                note = "Two-factor authentication is not enabled for this user, so there was nothing to reset.",
            });
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

        var notes = new List<string>();

        if (twoFactorDisabled)
        {
            notes.Add(required
                ? "The user signs in with their password. This site requires two-factor authentication for this user, so they are asked to set it up again at the next sign-in."
                : "The user signs in with their password and can set two-factor authentication up again from their own account.");
        }

        if (authenticatorReset)
        {
            notes.Add("The old authenticator app key no longer works; the user scans a new code when setting the app up again.");
        }

        notes.Add("The user's other signed-in sessions are signed out. Recovery codes were not read or returned.");

        return AgentToolResults.Json(new
        {
            changed = true,
            userId = user.UserId,
            userName = user.UserName,
            twoFactorDisabled,
            authenticatorAppReset = authenticatorReset,
            mustEnrollAgain = twoFactorDisabled && required,
            note = string.Join(" ", notes),
        });
    }

    private static string Describe(IdentityResult result)
        => string.Join(" ", result.Errors.Select(identityError => identityError.Description));
}
