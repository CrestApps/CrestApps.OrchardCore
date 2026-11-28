using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Agent.Roles;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Modules;
using OrchardCore.Roles;
using OrchardCore.Security;
using OrchardCore.Security.Services;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.AI.Agent.Users;

/// <summary>
/// Explains why a user can or cannot do something: it builds the user's own claims principal with the claims principal
/// factory, as sign-in does, and asks the authorization service for the permission (on the content item, when one is
/// given), so the answer is the site's own. It then lists which of the user's roles hold the permission, which roles of
/// the site would grant it, and the account state that stops a sign-in. The user's claims are never returned.
/// </summary>
public sealed class ExplainUserAccessTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "explainUserAccess";

    // The most role names one list of the result holds.
    private const int MaxRoles = 50;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "userId": {
          "type": "string",
          "description": "The ID of the user, from listUsers or getUser."
        },
        "permission": {
          "type": "string",
          "description": "The technical name of the permission, from listPermissions, for example EditContent, PublishContent or ManageUsers."
        },
        "contentItemId": {
          "type": "string",
          "description": "The ID of a content item, to check the permission on that item (its type, its owner and its access rules count). Leave it out to check the permission alone."
        }
      },
      "required": [
        "userId",
        "permission"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Explains why a user can or cannot do something, for questions like \"why can't Jane edit this page?\": it asks the site's own authorization for the user and a permission, optionally on a content item, and returns the answer, the user's roles that hold the permission, the roles of the site that would grant it, and whether the account is disabled or locked out. Use listPermissions for permission names.";

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

        // The answer shows a user's roles and what each role may do: the caller needs to see roles as well as the user.
        if (caller is null || !await AgentToolAuthorization.IsAuthorizedAsync(services, RolesPermissions.ViewRoles))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("userId", out var userId);

        var user = string.IsNullOrWhiteSpace(userId)
            ? null
            : await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync(userId.Trim()) as User;

        if (user is null)
        {
            return await UserToolAccess.GetNotFoundMessageAsync(
                services,
                UsersPermissions.ViewUsers,
                string.IsNullOrWhiteSpace(userId)
                    ? "Give the userId of the user. Use listUsers to find it."
                    : $"There is no user with the ID '{userId.Trim()}'. Use listUsers to find the ID.");
        }

        // The View page of the Users admin checks the permission on the user. A person may always read their own account.
        if (!UserToolAccess.IsCaller(caller, user) &&
            !await AgentToolAuthorization.IsAuthorizedAsync(services, UsersPermissions.ViewUsers, user))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("permission", out var permissionName);
        permissionName = permissionName?.Trim();

        var installed = await RoleToolSupport.GetInstalledPermissionsAsync(services);
        var permission = string.IsNullOrEmpty(permissionName)
            ? null
            : installed.FirstOrDefault(item => string.Equals(item.Permission.Name, permissionName, StringComparison.OrdinalIgnoreCase))?.Permission;

        if (permission is null)
        {
            var close = RoleToolSupport.FindCloseNames(permissionName, installed.Select(item => item.Permission.Name));

            return string.IsNullOrEmpty(permissionName)
                ? "Give permission, the technical name of a permission. Use listPermissions to find it."
                : $"There is no permission named '{permissionName}' on this site.{(close.Count > 0 ? $" Did you mean {string.Join(", ", close)}?" : string.Empty)} Use listPermissions to find the name.";
        }

        ContentItem contentItem = null;

        if (arguments.TryGetFirstString("contentItemId", out var contentItemId) && !string.IsNullOrWhiteSpace(contentItemId))
        {
            var contentManager = services.GetService<IContentManager>();

            if (contentManager is null)
            {
                return "Content items cannot be checked on this site: the Contents feature is off. Leave contentItemId out to check the permission alone.";
            }

            contentItem = await contentManager.GetAsync(contentItemId.Trim(), VersionOptions.Latest);

            // The caller must be able to see the item, so the tool cannot be used to learn about items the caller
            // cannot open.
            if (contentItem is null || !await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.ViewContent, contentItem))
            {
                return contentItem is null
                    ? $"There is no content item with the ID '{contentItemId.Trim()}'. Use listContentItems to find it."
                    : AIToolCallerAuthorization.NotAllowedMessage;
            }
        }

        // The user's own principal, as sign-in builds it: their roles, the permissions of those roles and their claims.
        var principal = await services.GetRequiredService<IUserClaimsPrincipalFactory<IUser>>().CreateAsync(user);
        var allowed = await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal, permission, contentItem);

        var roleService = services.GetRequiredService<IRoleService>();
        var grantingService = services.GetRequiredService<IPermissionGrantingService>();
        var requirement = new PermissionRequirement(permission);
        var userRoles = (user.RoleNames ?? []).Where(role => !string.IsNullOrWhiteSpace(role)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // Every signed-in user also gets what the Anonymous and Authenticated roles may do.
        var appliedRoles = userRoles
            .Concat([OrchardCoreConstants.Roles.Authenticated, OrchardCoreConstants.Roles.Anonymous])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var grantingUserRoles = new List<string>();

        foreach (var roleName in appliedRoles)
        {
            if (await GrantsAsync(roleService, grantingService, requirement, roleName))
            {
                grantingUserRoles.Add(roleName);
            }
        }

        var grantingSiteRoles = new List<string>();

        foreach (var roleName in (await roleService.GetRolesAsync()).Select(role => role.RoleName))
        {
            if (!string.IsNullOrWhiteSpace(roleName) && await GrantsAsync(roleService, grantingService, requirement, roleName))
            {
                grantingSiteRoles.Add(roleName);
            }
        }

        var isSiteOwner = await UserToolAccess.IsSiteOwnerAsync(services, user.UserId);
        var localClock = services.GetRequiredService<ILocalClock>();
        var utcNow = services.GetRequiredService<IClock>().UtcNow;
        var lockedOut = user.IsLockoutEnabled && user.LockoutEndUtc is { } lockoutEnd && lockoutEnd > utcNow;

        return AgentToolResults.Json(new
        {
            userId = user.UserId,
            userName = user.UserName,
            permission = new
            {
                name = permission.Name,
                description = permission.Description?.Value,
                securityCritical = permission.IsSecurityCritical,
            },
            contentItem = contentItem is null
                ? null
                : new
                {
                    contentItemId = contentItem.ContentItemId,
                    contentType = contentItem.ContentType,
                    displayText = contentItem.DisplayText,
                    ownedByUser = string.Equals(contentItem.Owner, user.UserId, StringComparison.Ordinal),
                },
            allowed,
            accountEnabled = user.IsEnabled,
            lockedOutUntil = lockedOut ? await AgentToolResults.ToLocalAsync(localClock, user.LockoutEndUtc) : null,
            isSiteOwner,
            userRoles,
            rolesOfUserThatGrantIt = grantingUserRoles.Take(MaxRoles).ToList(),
            rolesThatGrantIt = grantingSiteRoles.Take(MaxRoles).ToList(),
            explanation = Explain(allowed, contentItem is not null, isSiteOwner, grantingUserRoles, grantingSiteRoles, user.IsEnabled, lockedOut),
            timeZone = lockedOut ? await AgentToolResults.GetTimeZoneIdAsync(localClock) : null,
        });
    }

    // A role grants the permission when it holds it, holds a permission that implies it, or is the administrator role,
    // which holds every permission.
    private static async Task<bool> GrantsAsync(IRoleService roleService, IPermissionGrantingService grantingService, PermissionRequirement requirement, string roleName)
    {
        if (await roleService.IsAdminRoleAsync(roleName))
        {
            return true;
        }

        var claims = await roleService.GetRoleClaimsAsync(roleName);

        return grantingService.IsGranted(requirement, claims);
    }

    private static string Explain(bool allowed, bool onItem, bool isSiteOwner, List<string> grantingUserRoles, List<string> grantingSiteRoles, bool enabled, bool lockedOut)
    {
        var sentences = new List<string>();
        var target = onItem ? " on this content item" : string.Empty;

        if (allowed)
        {
            if (isSiteOwner)
            {
                sentences.Add($"Allowed{target}: the user is the site owner (the super user), who holds every permission.");
            }
            else if (grantingUserRoles.Count > 0)
            {
                sentences.Add($"Allowed{target} through the role{(grantingUserRoles.Count == 1 ? string.Empty : "s")} {string.Join(", ", grantingUserRoles)}.");
            }
            else
            {
                sentences.Add($"Allowed{target}, but by a rule other than a role that holds this permission itself: for a content item, the content type's own permission, the user owning the item, or the item's access rules decided it.");
            }
        }
        else if (grantingUserRoles.Count > 0)
        {
            sentences.Add(onItem
                ? $"Not allowed on this content item, although the role{(grantingUserRoles.Count == 1 ? string.Empty : "s")} {string.Join(", ", grantingUserRoles)} hold{(grantingUserRoles.Count == 1 ? "s" : string.Empty)} the permission: a rule of the item decided it (for example a permission that covers only the user's own items, the content type's own permission, or the item's access rules)."
                : $"Not allowed, although the role{(grantingUserRoles.Count == 1 ? string.Empty : "s")} {string.Join(", ", grantingUserRoles)} hold{(grantingUserRoles.Count == 1 ? "s" : string.Empty)} the permission: another rule of the site refused it.");
        }
        else
        {
            sentences.Add(grantingSiteRoles.Count > 0
                ? $"Not allowed{target}: none of the user's roles holds this permission. Roles that grant it: {string.Join(", ", grantingSiteRoles.Take(MaxRoles))}. Giving the user one of them (setUserRoles), or adding the permission to one of the user's roles (saveRole), would allow it."
                : $"Not allowed{target}: none of the user's roles holds this permission, and no role of the site grants it. Add it to a role with saveRole.");
        }

        if (!enabled)
        {
            sentences.Add("The account is disabled, so the user cannot sign in at all; enable it with setUserAccountState.");
        }

        if (lockedOut)
        {
            sentences.Add("The account is locked out after failed sign-ins until lockedOutUntil; unlock it with setUserAccountState.");
        }

        return string.Join(" ", sentences);
    }
}
