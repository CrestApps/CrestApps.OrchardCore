using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;
using YSession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Agent.Users;

/// <summary>
/// Clears the lockout of a user, as the Unlock action of the Users admin page does: it resets the count of failed
/// sign-ins and removes the lockout end.
/// </summary>
public sealed class UnlockUserTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "unlockUser";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "userId": {
          "type": "string",
          "description": "The ID of the locked-out user, from listUsers or getUser."
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
    public override string Description => "Clears a user's lockout and failed sign-in count after too many failed attempts. Use it when getUser shows the user is locked out. It does not change the password.";

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

        arguments.TryGetFirstString("userId", out var userId);

        var (user, error) = await UserToolAccess.FindAsync(services, userId, UsersPermissions.EditUsers);

        if (error is not null)
        {
            return error;
        }

        var userManager = services.GetRequiredService<UserManager<IUser>>();
        var failedSignInCount = user.AccessFailedCount;

        // The admin page offers Unlock for a locked-out user only. For a user who is not locked out, the count of
        // failed sign-ins is reset and the lockout end is left alone: the user manager refuses to set it for an
        // account that cannot be locked out.
        if (!await userManager.IsLockedOutAsync(user))
        {
            await userManager.ResetAccessFailedCountAsync(user);

            return AgentToolResults.Json(new
            {
                unlocked = false,
                userId = user.UserId,
                userName = user.UserName,
                clearedFailedSignInCount = failedSignInCount,
                note = "The user is not locked out. The count of failed sign-ins was reset.",
            });
        }

        // The two calls of the Unlock action.
        await userManager.ResetAccessFailedCountAsync(user);

        var result = await userManager.SetLockoutEndDateAsync(user, null);

        if (!result.Succeeded)
        {
            // As the Unlock action does when it fails.
            await services.GetRequiredService<YSession>().CancelAsync();

            return $"The user was not unlocked: {string.Join(" ", result.Errors.Select(identityError => identityError.Description))}".TrimEnd();
        }

        return AgentToolResults.Json(new
        {
            unlocked = true,
            userId = user.UserId,
            userName = user.UserName,
            clearedFailedSignInCount = failedSignInCount,
            note = "The user can try to sign in again. The password did not change.",
        });
    }
}
