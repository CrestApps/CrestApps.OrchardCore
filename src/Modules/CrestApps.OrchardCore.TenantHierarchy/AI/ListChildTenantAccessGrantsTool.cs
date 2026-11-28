using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.TenantHierarchy.AI;

/// <summary>
/// Lists who may enter child tenants, as the Access screens of the child tenants page do: the grants for every child
/// tenant, or those for one child tenant with the inherited ones, plus the parent roles and child roles a grant can use.
/// </summary>
internal sealed class ListChildTenantAccessGrantsTool : TenantHierarchyAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listChildTenantAccessGrants";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "entryId": {
          "type": "string",
          "description": "The entry id of one child tenant, from listChildTenants. Leave it out for the grants that cover every child tenant."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists who may enter child tenants: the grants for all children or for one child (with the inherited ones), plus the parent roles and child roles a grant can use.";

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

        var grantManager = services.GetRequiredService<AccessGrantManager>();
        var grants = await grantManager.ListAsync(entryId);
        IReadOnlyList<AccessGrant> inherited = entryId is null ? [] : await grantManager.ListAsync(null);

        return Json(new
        {
            entryId,
            displayName = child?.Entry.DisplayName,
            grants = await DescribeAsync(services, grants),
            inheritedGrants = entryId is null ? null : await DescribeAsync(services, inherited),
            parentRoles = await grantManager.GetParentRolesAsync(),
            childRoles = await services.GetRequiredService<ITenantHierarchyBroker>().GetChildRolesAsync(entryId),
            accessPath = entryId is null
                ? GetAdminPath(services, "TenantHierarchyAccess")
                : GetAdminPath(services, "TenantHierarchyChildAccess", new { id = entryId }),
        });
    }

    internal static async Task<List<object>> DescribeAsync(IServiceProvider services, IEnumerable<AccessGrant> grants)
    {
        var items = new List<object>();

        foreach (var grant in grants)
        {
            items.Add(await DescribeGrantAsync(services, grant));
        }

        return items;
    }

    internal static async Task<object> DescribeGrantAsync(IServiceProvider services, AccessGrant grant)
        => new
        {
            grantId = grant.GrantId,
            principalType = grant.PrincipalType.ToString(),
            principal = grant.PrincipalName,
            childEntryId = grant.ChildEntryId,
            appliesTo = string.IsNullOrEmpty(grant.ChildEntryId) ? "Every child tenant" : "One child tenant",
            childRoles = grant.ChildRoles,
            createdAt = await ToLocalAsync(services, grant.CreatedUtc),
            createdBy = grant.CreatedByName,
        };
}
