using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContentAccessControl.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.ContentAccessControl.AI;

/// <summary>
/// Tells which roles may view a content item, read the way the content access authorization handler reads them, and
/// whether its content type restricts access at all. It needs <c>EditContent</c> on the item, as its editor does.
/// </summary>
internal sealed class GetContentItemAccessRolesTool : ContentAccessControlAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getContentItemAccessRoles";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The id of the content item."
        }
      },
      "required": ["contentItemId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Tells which roles may view a content item and whether its content type restricts access at all (role picker parts with \"Restrict content\" on, including named parts). Use it before changing who can see an item.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var contentItemId = ReadString(arguments, "contentItemId");
        var contentManager = services.GetRequiredService<IContentManager>();
        var latest = contentItemId is null ? null : await contentManager.GetAsync(contentItemId, VersionOptions.Latest);

        if (latest is null)
        {
            return $"No content item has the id '{contentItemId}'.";
        }

        if (!await IsAuthorizedAsync(services, caller, CommonPermissions.EditContent, latest))
        {
            return NotAllowedMessage;
        }

        var definition = await services.GetRequiredService<IContentDefinitionManager>().GetTypeDefinitionAsync(latest.ContentType);

        if (definition is null)
        {
            return $"The content type '{latest.ContentType}' of this item no longer exists.";
        }

        var restrictingParts = ContentAccessRestriction.GetRestrictingParts(definition);
        var published = latest.Published ? latest : await contentManager.GetAsync(contentItemId, VersionOptions.Published);

        if (restrictingParts.Count == 0)
        {
            return Json(new
            {
                contentItemId,
                displayText = latest.DisplayText,
                contentType = latest.ContentType,
                restrictable = false,
                restricted = false,
                message = $"The content type '{latest.ContentType}' does not restrict access by role: it has no Role Picker part with \"Restrict content\" on. Everyone who may view content can view this item. Turning restriction on is a change to the content type definition.",
            });
        }

        var roleNames = ContentAccessRoleTools.GetRoleNames(services);
        HashSet<string> effective = published is null ? [] : ContentAccessRestriction.GetRoleNames(definition, published);

        return Json(new
        {
            contentItemId,
            displayText = latest.DisplayText,
            contentType = latest.ContentType,
            restrictable = true,
            restricted = effective.Count > 0,
            published = published is not null,
            hasDraft = published is null || published.ContentItemVersionId != latest.ContentItemVersionId,
            visibleToRoles = effective.Order(StringComparer.OrdinalIgnoreCase).ToList(),
            parts = restrictingParts.Select(part => ContentAccessRoleTools.DescribePart(part, published, latest, roleNames)).ToList(),
            message = effective.Count > 0
                ? "Only people in one of visibleToRoles, and administrators, may view the published item, and only if they may view content."
                : published is null
                    ? "The item is not published. When it is published, the roles of its parts decide who may view it."
                    : "No role is set, so everyone who may view content can view this item.",
        });
    }
}
