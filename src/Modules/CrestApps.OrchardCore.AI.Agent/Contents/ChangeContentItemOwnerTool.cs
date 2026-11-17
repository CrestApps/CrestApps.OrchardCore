using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

/// <summary>
/// Changes the owner of a content item. The admin sets the owner in the owner editor of the content item; this tool sets
/// the same value for a caller who holds the same permission.
/// </summary>
public sealed class ChangeContentItemOwnerTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "changeContentItemOwner";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The ID of the content item (ContentItemId) whose owner changes."
        },
        "userId": {
          "type": "string",
          "description": "The user ID of the new owner. Give one of userId, userName or email."
        },
        "userName": {
          "type": "string",
          "description": "The user name of the new owner. Give one of userId, userName or email."
        },
        "email": {
          "type": "string",
          "description": "The email address of the new owner. Give one of userId, userName or email."
        }
      },
      "required": [
        "contentItemId"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Changes the owner of a content item to another user, given by user name, user ID or email. Ownership decides who can edit, publish or delete the item under the \"own content\" permissions. The author of the item is not changed.";

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

        if (AgentToolAuthorization.GetCaller(services) is null)
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("contentItemId", out var contentItemId))
        {
            return "The contentItemId argument is required.";
        }

        var contentManager = services.GetRequiredService<IContentManager>();
        var contentItem = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);

        if (contentItem is null)
        {
            return $"No content item with the ID '{contentItemId}' was found.";
        }

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.EditContentOwner, contentItem))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var userManager = services.GetService<UserManager<IUser>>();

        if (userManager is null)
        {
            return "Nothing was changed: the Users feature is not enabled, so the new owner cannot be found.";
        }

        IUser user = null;

        if (arguments.TryGetFirstString("userId", out var userId))
        {
            user = await userManager.FindByIdAsync(userId);
        }
        else if (arguments.TryGetFirstString("userName", out var userName))
        {
            user = await userManager.FindByNameAsync(userName);
        }
        else if (arguments.TryGetFirstString("email", out var email))
        {
            user = await userManager.FindByEmailAsync(email);
        }
        else
        {
            return "Nothing was changed: give the new owner as userId, userName or email.";
        }

        if (user is null)
        {
            return "Nothing was changed: no user matches the given userId, userName or email.";
        }

        var newOwner = await userManager.GetUserIdAsync(user);
        var previousOwner = contentItem.Owner;

        if (string.Equals(previousOwner, newOwner, StringComparison.Ordinal))
        {
            return $"Nothing was changed: '{user.UserName}' already owns content item '{contentItemId}'.";
        }

        var versionIds = new List<string>();

        contentItem.Owner = newOwner;
        await contentManager.UpdateAsync(contentItem);
        versionIds.Add(contentItem.ContentItemVersionId);

        if (!contentItem.Published)
        {
            // The latest version is a draft. The published version, when there is one, gets the same owner.
            var published = await contentManager.GetAsync(contentItemId, VersionOptions.Published);

            if (published is not null && published.ContentItemVersionId != contentItem.ContentItemVersionId)
            {
                published.Owner = newOwner;
                await contentManager.UpdateAsync(published);
                versionIds.Add(published.ContentItemVersionId);
            }
        }

        return AgentToolResults.Json(new
        {
            changed = true,
            contentItemId,
            displayText = contentItem.DisplayText,
            previousOwnerUserId = previousOwner,
            ownerUserId = newOwner,
            ownerUserName = user.UserName,
            updatedVersionIds = versionIds,
        });
    }
}
