using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Users;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.AI.Agent.Users;

/// <summary>
/// Sets the values of one custom user settings type on a user. The values live inside the user, not in a content
/// item, so the content tools do not reach them. The tool reads the stored settings with the custom user settings
/// service, merges the given parts and fields the way createOrUpdateContentItem does, validates the result with the
/// content manager, and stores the user with the user manager, as the user editor does.
/// </summary>
public sealed class SaveUserCustomSettingsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveUserCustomSettings";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "userId": {
          "type": "string",
          "description": "The ID of the user, from listUsers or getUser."
        },
        "settingsType": {
          "type": "string",
          "description": "The technical name of the custom user settings type: a content type with the CustomUserSettings stereotype. getUser returns the types that have values."
        },
        "settings": {
          "type": "object",
          "description": "The parts and fields to change, in the JSON structure of a content item of that type, for example { \"UserProfile\": { \"Department\": { \"Text\": \"Finance\" } } }. Parts and fields that are not given keep their value; a list that is given replaces the stored list.",
          "additionalProperties": true
        }
      },
      "required": [
        "userId",
        "settingsType",
        "settings"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Sets the values of one custom user settings type for a user, given the settings type name and the parts and fields to change. Read the current values with getUser and the type's shape with the content type tools first.";

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

        // The first check of the user editor: the permission to edit the user.
        var (user, error) = await UserToolAccess.FindAsync(services, userId, UsersPermissions.EditUsers);

        if (error is not null)
        {
            return error;
        }

        if (!arguments.TryGetFirstString("settingsType", out var settingsTypeName))
        {
            return "Nothing was saved: give settingsType, the technical name of the custom user settings type.";
        }

        if (!AgentToolArguments.TryReadJsonObject(arguments, "settings", out var settings) || settings is null)
        {
            return "Nothing was saved: settings must be a JSON object with the parts and fields to change.";
        }

        var customUserSettingsService = services.GetRequiredService<CustomUserSettingsService>();
        var settingsType = await FindSettingsTypeAsync(customUserSettingsService, settingsTypeName.Trim());

        if (settingsType is null)
        {
            var names = (await customUserSettingsService.GetAllSettingsTypeNamesAsync()).Order(StringComparer.OrdinalIgnoreCase).ToList();

            return names.Count == 0
                ? "Nothing was saved: this site has no custom user settings type. A content type becomes one when it has the CustomUserSettings stereotype."
                : $"Nothing was saved: there is no custom user settings type named '{settingsTypeName}'. The types are: {string.Join(", ", names)}.";
        }

        // The second check of the user editor: the permission of the settings type, for whichever user is edited.
        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, OrchardCorePermissions.CreateManageOwnCustomUserSettings(settingsType.Name)))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        // The settings item belongs to the user: it has no ID of its own to address and its type is the settings type.
        RemoveProperty(settings, nameof(ContentItem.ContentItemId));
        RemoveProperty(settings, nameof(ContentItem.ContentItemVersionId));
        RemoveProperty(settings, nameof(ContentItem.ContentType));

        if (settings.Count == 0)
        {
            return "Nothing was saved: settings has no part to change.";
        }

        error = await ContentItemPayloadCheck.GetErrorAsync(services, settingsType, settings, "custom user settings", "settings", cancellationToken);

        if (error is not null)
        {
            return error;
        }

        settings[nameof(ContentItem.ContentType)] = settingsType.Name;

        // The stored settings, or a new item of the type when the user has none yet, as the user editor reads them.
        var current = await customUserSettingsService.GetSettingsAsync(user, settingsType);
        var contentItem = await services.GetRequiredService<ContentItemPreparationService>().PrepareAsync(
            settingsType,
            settings,
            current,
            cancellationToken);

        var validation = await services.GetRequiredService<IContentManager>().ValidateAsync(contentItem);

        if (!validation.Succeeded)
        {
            return $"Nothing was saved: the settings are not valid. {string.Join(" ", validation.Errors.Select(validationError => validationError.ErrorMessage))}".TrimEnd();
        }

        user.Properties.TryGetPropertyValue(settingsType.Name, out var previous);

        var stored = JObject.FromObject(contentItem);

        user.Properties[settingsType.Name] = stored;

        var result = await services.GetRequiredService<UserManager<IUser>>().UpdateAsync(user);

        if (!result.Succeeded)
        {
            // The loaded user is shared with the rest of the request, so the refused settings are taken back.
            user.Properties.Remove(settingsType.Name);

            if (previous is not null)
            {
                user.Properties[settingsType.Name] = previous;
            }

            return $"Nothing was saved: {string.Join(" ", result.Errors.Select(identityError => identityError.Description))}".TrimEnd();
        }

        return AgentToolResults.Json(new
        {
            saved = true,
            userId = user.UserId,
            userName = user.UserName,
            settingsType = settingsType.Name,
            settings = UserToolProjection.GetParts(stored),
        });
    }

    private static async Task<ContentTypeDefinition> FindSettingsTypeAsync(CustomUserSettingsService customUserSettingsService, string name)
    {
        var settingsType = await customUserSettingsService.GetSettingsTypeAsync(name);

        if (settingsType is not null)
        {
            return settingsType;
        }

        return (await customUserSettingsService.GetAllSettingsTypesAsync())
            .FirstOrDefault(type => string.Equals(type.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static void RemoveProperty(JsonObject node, string name)
    {
        var key = node
            .Select(property => property.Key)
            .FirstOrDefault(propertyName => string.Equals(propertyName, name, StringComparison.OrdinalIgnoreCase));

        if (key is not null)
        {
            node.Remove(key);
        }
    }
}
