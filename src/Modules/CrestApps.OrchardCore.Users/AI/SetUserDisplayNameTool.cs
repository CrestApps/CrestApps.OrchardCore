using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Users.Core.Models;
using CrestApps.OrchardCore.Users.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Entities;
using OrchardCore.Settings;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Users.AI;

/// <summary>
/// Sets a user's first, middle, last and display name, as the name card of the user editor does: it needs
/// <c>EditUsers</c> on that user and applies the required-name rules of the User Display Name settings. The user is saved
/// through the user manager, so the user event handlers and the name index run.
/// </summary>
internal sealed class SetUserDisplayNameTool : UsersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setUserDisplayName";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "userId": {
          "type": "string",
          "description": "The id of the user, from searchForUsers. A user name or email is accepted too."
        },
        "displayName": {
          "type": "string",
          "description": "The display name. An empty text clears it; leave it out to keep it."
        },
        "firstName": {
          "type": "string",
          "description": "The first name. An empty text clears it; leave it out to keep it."
        },
        "middleName": {
          "type": "string",
          "description": "The middle name. An empty text clears it; leave it out to keep it."
        },
        "lastName": {
          "type": "string",
          "description": "The last name. An empty text clears it; leave it out to keep it."
        }
      },
      "required": ["userId"],
      "additionalProperties": false
    }
    """);

    private static readonly Dictionary<string, string> _argumentNames = new(StringComparer.Ordinal)
    {
        [nameof(UserFullNamePart.DisplayName)] = "displayName",
        [nameof(UserFullNamePart.FirstName)] = "firstName",
        [nameof(UserFullNamePart.MiddleName)] = "middleName",
        [nameof(UserFullNamePart.LastName)] = "lastName",
    };

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Sets a user's first, middle, last and display name. Use it after searchForUsers; it applies the required-field rules from the User Display Name settings and fails when a required part is missing.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var userId = ReadString(arguments, "userId");
        var user = await FindUserAsync(services, userId);

        if (user is null)
        {
            // A caller who may not edit users learns nothing about which users exist.
            return await IsAuthorizedAsync(services, caller, UsersPermissions.EditUsers)
                ? $"No user has the id '{userId}'. Call searchForUsers to find it."
                : NotAllowedMessage;
        }

        if (!await IsAuthorizedAsync(services, caller, UsersPermissions.EditUsers, user))
        {
            return NotAllowedMessage;
        }

        if (!_argumentNames.Values.Any(name => HasArgument(arguments, name)))
        {
            return "Nothing was changed: give at least one of displayName, firstName, middleName and lastName.";
        }

        var part = user.GetOrCreate<UserFullNamePart>();

        part.DisplayName = Read(arguments, "displayName", part.DisplayName);
        part.FirstName = Read(arguments, "firstName", part.FirstName);
        part.MiddleName = Read(arguments, "middleName", part.MiddleName);
        part.LastName = Read(arguments, "lastName", part.LastName);

        var settings = await services.GetRequiredService<ISiteService>().GetSettingsAsync<DisplayNameSettings>();
        var missing = UserFullNameRules.GetMissingRequired(settings, part.DisplayName, part.FirstName, part.MiddleName, part.LastName);

        if (missing.Count > 0)
        {
            return $"Nothing was changed: the User Display Name settings require {string.Join(", ", missing.Select(name => _argumentNames[name]))}, which would be empty.";
        }

        user.Put(part);

        var errors = await UpdateUserAsync(services, user);

        if (errors.Count > 0)
        {
            return $"The user was not saved: {string.Join(" ", errors)}";
        }

        return Json(new
        {
            userId = user.UserId,
            userName = user.UserName,
            displayName = part.DisplayName,
            firstName = part.FirstName,
            middleName = part.MiddleName,
            lastName = part.LastName,
            saved = true,
        });
    }

    private static string Read(AIFunctionArguments arguments, string name, string current)
        => HasArgument(arguments, name)
            ? ReadString(arguments, name)
            : current;
}
