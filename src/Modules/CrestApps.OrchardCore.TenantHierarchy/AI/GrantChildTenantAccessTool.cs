using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.AI;

/// <summary>
/// Lets a parent user or a parent role enter one child tenant or every child tenant with chosen child roles, through the
/// <see cref="AccessGrantManager"/> the Access screens use. The child roles must be among those the screen offers.
/// </summary>
internal sealed class GrantChildTenantAccessTool : TenantHierarchyAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "grantChildTenantAccess";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "principalType": {
          "type": "string",
          "enum": ["User", "Role"],
          "description": "Whether the grant is for one parent user or for everyone in a parent role."
        },
        "principal": {
          "type": "string",
          "description": "The parent user's user name or email, or the parent role's name, from listChildTenantAccessGrants."
        },
        "entryId": {
          "type": "string",
          "description": "The entry id of one child tenant, from listChildTenants. Leave it out to grant access to every child tenant."
        },
        "childRoles": {
          "type": "array",
          "items": { "type": "string" },
          "minItems": 1,
          "description": "The roles the person gets in the child tenant, from the childRoles listChildTenantAccessGrants returns. They replace the roles of an existing grant for the same user or role."
        }
      },
      "required": ["principalType", "principal", "childRoles"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lets a parent user (by user name or email) or a parent role enter one child tenant or all of them, with the given child roles.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override TenantHierarchyToolScope Scope => TenantHierarchyToolScope.Parent;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, user, TenantHierarchyPermissions.ManageChildAccess))
        {
            return NotAllowedMessage;
        }

        if (!TryReadEnum<AccessGrantPrincipalType>(arguments, "principalType", out var principalType) || principalType is null)
        {
            return "Nothing was granted: principalType must be User or Role.";
        }

        var principal = ReadString(arguments, "principal");

        if (principal is null)
        {
            return principalType == AccessGrantPrincipalType.User
                ? "Nothing was granted: give the parent user's user name or email in principal."
                : "Nothing was granted: give the parent role's name in principal.";
        }

        var entryId = ReadString(arguments, "entryId");
        ChildTenantInfo child = null;

        if (entryId is not null)
        {
            child = await FindChildAsync(services, entryId);

            if (child is null)
            {
                return ChildNotFound(entryId);
            }
        }

        var requestedRoles = (ReadStringList(arguments, "childRoles") ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (requestedRoles.Count == 0)
        {
            return "Nothing was granted: give at least one child role in childRoles.";
        }

        // The Access screen offers only these child roles, so a grant never names a role the children do not have.
        var offeredRoles = await services.GetRequiredService<ITenantHierarchyBroker>().GetChildRolesAsync(entryId);
        var roles = new List<string>();

        foreach (var role in requestedRoles)
        {
            var match = offeredRoles.FirstOrDefault(offered => string.Equals(offered, role, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                return $"Nothing was granted: '{role}' is not a role of {(child is null ? "the child tenants" : child.Entry.DisplayName)}. Use one of: {string.Join(", ", offeredRoles)}.";
            }

            roles.Add(match);
        }

        var grantManager = services.GetRequiredService<AccessGrantManager>();
        var principalId = await ResolvePrincipalIdAsync(services, grantManager, principalType.Value, principal);
        var existing = principalId is null
            ? null
            : (await grantManager.ListAsync(entryId)).FirstOrDefault(grant => grant.PrincipalType == principalType &&
                string.Equals(grant.PrincipalId, principalId, StringComparison.OrdinalIgnoreCase));
        var previousRoles = existing?.ChildRoles.ToArray();

        var result = await grantManager.AddAsync(principalType.Value, principal, entryId, roles);

        if (!result.Succeeded)
        {
            return $"Nothing was granted: {result.Error}";
        }

        var saved = principalId is null
            ? null
            : (await grantManager.ListAsync(entryId)).FirstOrDefault(grant => grant.PrincipalType == principalType &&
                string.Equals(grant.PrincipalId, principalId, StringComparison.OrdinalIgnoreCase));

        return Json(new
        {
            granted = true,
            grant = saved is null ? null : await ListChildTenantAccessGrantsTool.DescribeGrantAsync(services, saved),
            appliesTo = child is null ? "Every child tenant" : child.Entry.DisplayName,
            replacedExistingGrant = existing is not null,
            previousChildRoles = previousRoles,
            message = existing is null
                ? "Access was granted."
                : "The existing grant for this user or role was updated: its child roles were replaced.",
        });
    }

    private static async Task<string> ResolvePrincipalIdAsync(IServiceProvider services, AccessGrantManager grantManager, AccessGrantPrincipalType principalType, string principal)
    {
        if (principalType == AccessGrantPrincipalType.Role)
        {
            return (await grantManager.GetParentRolesAsync()).FirstOrDefault(role => string.Equals(role, principal, StringComparison.OrdinalIgnoreCase));
        }

        var userManager = services.GetRequiredService<UserManager<IUser>>();
        var found = await userManager.FindByNameAsync(principal) ?? await userManager.FindByEmailAsync(principal);

        return (found as User)?.UserId;
    }
}
