using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using CrestApps.OrchardCore.TenantHierarchy.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.TenantHierarchy.AI;

/// <summary>
/// Lists the child tenants of this parent tenant with the search, state filter and sort of the child tenants page.
/// </summary>
internal sealed class ListChildTenantsTool : TenantHierarchyAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listChildTenants";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Text to find in the name, address or description of a child tenant."
        },
        "state": {
          "type": "string",
          "enum": ["All", "Running", "Suspended", "SettingUp", "Failed", "PendingRemoval", "ChangedByPlatform"],
          "description": "Only child tenants in this state. Defaults to All."
        },
        "orderBy": {
          "type": "string",
          "enum": ["Name", "Newest", "State"],
          "description": "The order of the list. Defaults to Name."
        },
        "includePolicy": {
          "type": "boolean",
          "description": "Also return the limits the platform set for this parent: the child limit, the address pattern, the removal grace period and the setup recipes that can be used."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, from 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of child tenants per page, at most 50. Defaults to 20."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists this parent tenant's child tenants with entry id, name, address, state, description and creation date; filter by text and state, and optionally include the limits and setup recipes the platform allows. Call it first: every other child tenant tool needs the entry id.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override TenantHierarchyToolScope Scope => TenantHierarchyToolScope.Parent;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, user, TenantHierarchyPermissions.ViewChildTenants))
        {
            return NotAllowedMessage;
        }

        if (!TryReadEnum<ChildTenantStateFilter>(arguments, "state", out var state))
        {
            return "The state must be All, Running, Suspended, SettingUp, Failed, PendingRemoval or ChangedByPlatform.";
        }

        if (!TryReadEnum<ChildTenantSort>(arguments, "orderBy", out var orderBy))
        {
            return "The order must be Name, Newest or State.";
        }

        var (page, pageSize) = ReadPaging(arguments);
        var broker = services.GetRequiredService<ITenantHierarchyBroker>();
        var all = await broker.ListChildrenAsync();
        var filtered = ChildTenantListFilter.Apply(all, new ChildTenantListOptions
        {
            Search = ReadString(arguments, "search"),
            State = state ?? ChildTenantStateFilter.All,
            OrderBy = orderBy ?? ChildTenantSort.Name,
        }).ToList();

        // The open link is offered only for the child tenants the caller may enter now, as on the list page.
        HashSet<string> enterable = await IsAuthorizedAsync(services, user, TenantHierarchyPermissions.EnterChildTenants)
            ? (await services.GetRequiredService<DelegatedAccessIssuer>().GetEnterableChildrenAsync(user)).Select(info => info.Entry.EntryId).ToHashSet(StringComparer.Ordinal)
            : [];
        var pathBase = services.GetService<IHttpContextAccessor>()?.HttpContext?.Request.PathBase.Value ?? string.Empty;

        var items = new List<object>();

        foreach (var info in filtered.Skip((page - 1) * pageSize).Take(pageSize))
        {
            var canEnter = info.CanEnter && enterable.Contains(info.Entry.EntryId);

            items.Add(new
            {
                child = await DescribeChildAsync(services, info),
                canEnter,
                openPath = canEnter
                    ? $"{pathBase}/{TenantHierarchyConstants.Routes.Open.Replace("{entryId}", Uri.EscapeDataString(info.Entry.EntryId), StringComparison.Ordinal)}"
                    : null,
            });
        }

        var manager = services.GetRequiredService<ChildTenantManager>();
        object policy = null;

        if (ReadBool(arguments, "includePolicy") == true)
        {
            var parentPolicy = manager.GetPolicy();
            var recipes = await manager.GetAllowedRecipesAsync();

            policy = new
            {
                maxChildren = parentPolicy.MaxChildren,
                childCount = all.Count,
                addressPattern = manager.GetHostPattern(),
                removalGraceDays = parentPolicy.RemovalGraceDays,
                setupRecipes = recipes.Select(recipe => new
                {
                    name = recipe.Name,
                    displayName = recipe.DisplayName ?? recipe.Name,
                    description = recipe.Description,
                }).ToList(),
                canCreate = manager.IsHostGuardInstalled,
            };
        }

        return Json(new
        {
            items,
            total = filtered.Count,
            page,
            pageSize,
            truncated = filtered.Count > page * pageSize,
            totalChildren = all.Count,
            policy,
            listPath = GetAdminPath(services, "TenantHierarchyChildren"),
        });
    }
}
