using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContentAccessControl.Services;
using CrestApps.OrchardCore.Roles.Core;
using CrestApps.OrchardCore.Roles.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.ContentAccessControl.AI;

/// <summary>
/// Sets the roles that may view a content item, in a Role Picker part with "Restrict content" on, with the rules of the
/// part's editor (Required, Allow select multiple, Excluded roles). It needs <c>EditContent</c> on the item, and
/// <c>PublishContent</c> to publish the change, as the content editor's buttons do. A published item with no pending
/// draft is published again with the new roles; a pending draft is never published by this tool, because it would ship
/// someone's unrelated edits.
/// </summary>
internal sealed class SetContentItemAccessRolesTool : ContentAccessControlAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setContentItemAccessRoles";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The id of the content item."
        },
        "roles": {
          "type": "array",
          "items": { "type": "string" },
          "description": "The roles that may view the item, from getContentItemAccessRoles. An empty list makes it visible to everyone who may view content."
        },
        "partName": {
          "type": "string",
          "description": "The name of the restricting Role Picker part, from getContentItemAccessRoles. Required only when the content type has more than one."
        },
        "saveAsDraft": {
          "type": "boolean",
          "description": "true to save the roles in a draft without publishing them. Required when the item has unpublished changes; the published item keeps its roles until the draft is published."
        }
      },
      "required": ["contentItemId", "roles"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Sets the roles that may view a content item on a content type that restricts access by role. An empty list makes the item visible to everyone who can view content; it fails when the type has no restricting role picker part.";

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

        var requested = ReadStringList(arguments, "roles");

        if (requested is null)
        {
            return "Nothing was changed: give the roles in roles, or an empty list to make the item visible to everyone who may view content.";
        }

        var definition = await services.GetRequiredService<IContentDefinitionManager>().GetTypeDefinitionAsync(latest.ContentType);
        var restrictingParts = ContentAccessRestriction.GetRestrictingParts(definition);

        if (restrictingParts.Count == 0)
        {
            return $"Nothing was changed: the content type '{latest.ContentType}' does not restrict access by role. It needs a Role Picker part with \"Restrict content\" on, which is a change to the content type definition.";
        }

        var partName = ReadString(arguments, "partName");
        var partDefinition = partName is null
            ? (restrictingParts.Count == 1 ? restrictingParts[0] : null)
            : restrictingParts.FirstOrDefault(part => string.Equals(part.Name, partName, StringComparison.OrdinalIgnoreCase));

        if (partDefinition is null)
        {
            return partName is null
                ? $"Nothing was changed: the content type has several restricting Role Picker parts. Give partName, one of: {string.Join(", ", restrictingParts.Select(part => part.Name))}."
                : $"Nothing was changed: '{partName}' is not a restricting Role Picker part of this content type. Use one of: {string.Join(", ", restrictingParts.Select(part => part.Name))}.";
        }

        // The editor lists the site's roles without the excluded ones; a role it would not offer is refused, not dropped.
        var settings = partDefinition.GetSettings<RolePickerPartSettings>();
        var siteRoles = ContentAccessRoleTools.GetRoleNames(services);
        var roles = new List<string>();

        foreach (var role in requested.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var match = siteRoles.FirstOrDefault(name => string.Equals(name, role, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                return $"Nothing was changed: there is no role named '{role}'. Call getContentItemAccessRoles for the roles that can be used.";
            }

            if ((settings.ExcludedRoles ?? []).Contains(match, StringComparer.OrdinalIgnoreCase))
            {
                return $"Nothing was changed: the role '{match}' is excluded from this Role Picker part.";
            }

            roles.Add(match);
        }

        var selected = RolePickerRoleSelection.Select(settings, roles);

        switch (RolePickerRoleSelection.Check(settings, selected))
        {
            case RolePickerSelectionProblem.RoleRequired:
                return "Nothing was changed: this Role Picker part requires a role.";
            case RolePickerSelectionProblem.AtLeastOneRoleRequired:
                return "Nothing was changed: this Role Picker part requires at least one role.";
            case RolePickerSelectionProblem.OnlyOneRoleAllowed:
                return "Nothing was changed: this Role Picker part allows only one role.";
        }

        var published = latest.Published ? latest : await contentManager.GetAsync(contentItemId, VersionOptions.Published);
        var hasPendingDraft = published is not null && published.ContentItemVersionId != latest.ContentItemVersionId;
        var saveAsDraft = ReadBool(arguments, "saveAsDraft") == true || published is null;

        if (hasPendingDraft && !saveAsDraft)
        {
            return "Nothing was changed: the item has unpublished changes, and publishing the new roles would publish them too. Publish or discard the draft first, or pass saveAsDraft: true to save the roles in the draft; the published item keeps its current roles until the draft is published.";
        }

        if (!saveAsDraft && !await IsAuthorizedAsync(services, caller, CommonPermissions.PublishContent, latest))
        {
            return "Nothing was changed: publishing the new roles needs the permission to publish this item. Pass saveAsDraft: true to save them in a draft that a publisher can publish.";
        }

        var item = await contentManager.GetAsync(contentItemId, VersionOptions.DraftRequired);

        if (partDefinition.IsNamedPart())
        {
            item.Alter<RolePickerPart>(partDefinition.Name, part => part.RoleNames = selected);
        }
        else
        {
            item.Alter<RolePickerPart>(part => part.RoleNames = selected);
        }

        await contentManager.UpdateAsync(item);

        var validation = await contentManager.ValidateAsync(item);

        if (!validation.Succeeded)
        {
            return $"Nothing was saved: the content item is not valid. {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))}";
        }

        if (saveAsDraft)
        {
            await contentManager.SaveDraftAsync(item);
        }
        else if (!await contentManager.PublishAsync(item))
        {
            return "The new roles were saved in a draft but not published: a publishing rule of the site stopped it. Publish the item from its editor.";
        }

        return Json(new
        {
            contentItemId,
            displayText = item.DisplayText,
            partName = partDefinition.Name,
            roles = selected,
            published = !saveAsDraft,
            savedAsDraft = saveAsDraft,
            message = selected.Length == 0
                ? (saveAsDraft
                    ? "The draft names no role; when it is published, everyone who may view content can view the item."
                    : "The item names no role in this part, so this part no longer restricts who may view it.")
                : (saveAsDraft
                    ? "The roles were saved in a draft. The published item, if any, keeps its current roles until the draft is published."
                    : "The item was published with the new roles. Only people in one of them, and administrators, may view it."),
        });
    }
}
