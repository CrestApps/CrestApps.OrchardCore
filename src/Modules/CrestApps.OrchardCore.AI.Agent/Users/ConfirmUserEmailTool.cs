using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.AI.Agent.Users;

/// <summary>
/// Marks the email of a user as confirmed, as the Confirm email action of the Users admin page does. The confirmation
/// token is created and used inside the tool and never leaves the server.
/// </summary>
public sealed class ConfirmUserEmailTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "confirmUserEmail";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "userId": {
          "type": "string",
          "description": "The ID of the user whose email is confirmed, from listUsers or getUser."
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
    public override string Description => "Marks a user's email address as confirmed without sending an email. Use it only when the admin says the address is already verified.";

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

        if (await userManager.IsEmailConfirmedAsync(user))
        {
            return AgentToolResults.Json(new
            {
                confirmed = false,
                userId = user.UserId,
                userName = user.UserName,
                email = user.Email,
                note = "The email of this user is already confirmed.",
            });
        }

        if (string.IsNullOrEmpty(user.Email))
        {
            return "Nothing was confirmed: the user has no email address.";
        }

        // As the Confirm email action does: the token is made and spent here, and is not part of the result.
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var result = await userManager.ConfirmEmailAsync(user, token);

        if (!result.Succeeded)
        {
            return $"Nothing was confirmed: {string.Join(" ", result.Errors.Select(identityError => identityError.Description))}".TrimEnd();
        }

        return AgentToolResults.Json(new
        {
            confirmed = true,
            userId = user.UserId,
            userName = user.UserName,
            email = user.Email,
            note = "No email was sent. The address is now treated as verified.",
        });
    }
}
