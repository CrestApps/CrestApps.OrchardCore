using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TenantHierarchy.Core;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using CrestApps.OrchardCore.TenantHierarchy.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Setup.Services;

namespace CrestApps.OrchardCore.TenantHierarchy.AI;

/// <summary>
/// Makes an ordinary tenant a parent, or changes the display name and policy of a parent, through the
/// <see cref="TenantHierarchyPlatformService"/> the policy editor uses. The arguments are applied on top of the stored
/// policy (or the default one for a new parent) through the editor's own model, so a field not given keeps its value:
/// "raise the child limit" never clears the blocked features or the allowed recipes.
/// </summary>
internal sealed class SaveParentTenantTool : TenantHierarchyAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveParentTenant";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "tenantName": {
          "type": "string",
          "description": "The tenant name: an ordinary tenant to make a parent, or an existing parent to change, from getTenantHierarchyOverview."
        },
        "slug": {
          "type": "string",
          "description": "The parent's address, 3 to 40 lowercase letters, digits and inner hyphens; its host becomes slug.platform-domain. Required to make a parent; it cannot change afterwards."
        },
        "displayName": {
          "type": "string",
          "description": "The parent's display name. Required to make a parent."
        },
        "maxChildren": {
          "type": "integer",
          "minimum": 0,
          "maximum": 10000,
          "description": "The highest number of child tenants."
        },
        "childHostPattern": {
          "type": "string",
          "description": "The host pattern of child tenants, starting with '{business}.' followed by a host name. An empty text uses {business}.parent-host."
        },
        "databaseStrategy": {
          "type": "string",
          "enum": ["SqlitePerChild", "DatabasePerChild", "SchemaPerChild", "TablePrefixPerChild"],
          "description": "Where each child tenant's data is stored."
        },
        "databasePool": {
          "type": "string",
          "description": "The name of a database pool configured on the host, from getTenantHierarchyOverview, for the strategies that need one. An empty text clears it."
        },
        "recipes": {
          "type": "array",
          "items": { "type": "string" },
          "description": "The names of the setup recipes child tenants may use; an empty list allows every setup recipe. Replaces the current list."
        },
        "blockedFeatures": {
          "type": "array",
          "items": { "type": "string" },
          "description": "The ids of the features child tenants may never enable. Replaces the current list."
        },
        "deniedLocalPermissions": {
          "type": "array",
          "items": { "type": "string" },
          "description": "The permissions denied to child tenants' own users. Replaces the current list."
        },
        "requireMfa": {
          "type": "boolean",
          "description": "Whether entering a child tenant requires multi-factor authentication."
        },
        "sessionValidationMinutes": {
          "type": "integer",
          "minimum": 1,
          "maximum": 60,
          "description": "How often an open child tenant session is checked with the parent, in minutes."
        },
        "sessionIdleMinutes": {
          "type": "integer",
          "minimum": 1,
          "description": "After how many idle minutes a child tenant session ends."
        },
        "sessionLifetimeHours": {
          "type": "integer",
          "minimum": 1,
          "description": "After how many hours a child tenant session ends."
        },
        "switcherMode": {
          "type": "string",
          "enum": ["Hosted", "Embedded"],
          "description": "How the tenant switcher is shown."
        },
        "parentLabel": {
          "type": "string",
          "description": "The word for the parent tenant, such as Firm. An empty text uses the default."
        },
        "childLabel": {
          "type": "string",
          "description": "The word for one child tenant, such as Client. An empty text uses the default."
        },
        "childrenLabel": {
          "type": "string",
          "description": "The word for several child tenants, such as Clients. An empty text uses the default."
        },
        "removalGraceDays": {
          "type": "integer",
          "minimum": 0,
          "maximum": 365,
          "description": "How many days a removed child tenant can still be restored; 0 removes at once."
        }
      },
      "required": ["tenantName"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Makes an ordinary tenant a parent tenant (slug, display name, policy) or changes the display name and policy of an existing parent. Making a parent changes the tenant's host and forces the Parent feature on; it fails until AddTenantHierarchy() is in the host.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override TenantHierarchyToolScope Scope => TenantHierarchyToolScope.Platform;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, user, TenantHierarchyPermissions.ManageTenantHierarchy))
        {
            return NotAllowedMessage;
        }

        var tenantName = ReadString(arguments, "tenantName");

        if (tenantName is null || !services.GetRequiredService<IShellHost>().TryGetSettings(tenantName, out var settings))
        {
            return $"No tenant is named '{tenantName}'. Call getTenantHierarchyOverview to find it.";
        }

        var isNew = !settings.IsParentTenant();

        if (!isNew && ReadString(arguments, "slug") is { } newSlug && !string.Equals(newSlug, settings.GetHierarchySlug(), StringComparison.OrdinalIgnoreCase))
        {
            return "Nothing was changed: the slug of a parent tenant is set once, when it is made a parent.";
        }

        // The editor's model applies the stored policy, then the given fields, and builds the policy the editor would post.
        var model = new ParentPolicyEditViewModel
        {
            TenantName = settings.Name,
            DisplayName = isNew ? null : settings.GetHierarchyDisplayName(),
            Slug = isNew ? null : settings.GetHierarchySlug(),
        };

        model.FromPolicy(isNew ? new ParentTenantPolicy() : settings.GetParentPolicy());

        var error = await ApplyAsync(arguments, services, model);

        if (error is not null)
        {
            return $"Nothing was changed: {error}";
        }

        var platformService = services.GetRequiredService<TenantHierarchyPlatformService>();

        if (isNew)
        {
            if (model.Slug is null || model.DisplayName is null)
            {
                return "Nothing was changed: making a parent needs a slug and a displayName.";
            }

            // Without a configured platform domain the host is built from the request; with no request it would be
            // localhost, which is never the right address of a parent.
            var options = services.GetRequiredService<IOptions<TenantHierarchyOptions>>().Value;

            if (string.IsNullOrWhiteSpace(options.PlatformDomain) && services.GetService<IHttpContextAccessor>()?.HttpContext?.Request.Host.HasValue != true)
            {
                return $"Nothing was changed: the platform domain is not configured (TenantHierarchy:PlatformDomain), so the parent's address cannot be built here. Make the parent on the Tenant Hierarchy page: {GetAdminPath(services, "TenantHierarchyPlatformMakeParent")}";
            }
        }

        var policy = model.ToPolicy();
        var (result, errors) = isNew
            ? await platformService.MakeParentAsync(settings.Name, model.Slug, model.DisplayName, policy)
            : await platformService.UpdateParentAsync(settings.Name, model.DisplayName, policy);

        if (!result.Succeeded)
        {
            var problems = errors.Select(problem => new { field = ToArgumentName(problem.Key), message = problem.Value }).ToList();

            if (problems.Count == 0)
            {
                problems.Add(new { field = (string)null, message = result.Error });
            }

            return Json(new
            {
                saved = false,
                errors = problems,
            });
        }

        services.GetRequiredService<IShellHost>().TryGetSettings(settings.Name, out var saved);

        return Json(new
        {
            saved = true,
            madeParent = isNew,
            tenantName = settings.Name,
            displayName = model.DisplayName,
            host = saved?.GetPrimaryHost(),
            policy = TenantHierarchyPlatformToolResults.DescribePolicy(policy),
            message = isNew
                ? $"{model.DisplayName} is now a parent tenant at its new host, with the Parent feature on."
                : "The policy was saved and copied into the parent's child tenants.",
            parentPath = GetAdminPath(services, "TenantHierarchyPlatformParent", new { id = settings.Name }),
        });
    }

    private static async Task<string> ApplyAsync(AIFunctionArguments arguments, IServiceProvider services, ParentPolicyEditViewModel model)
    {
        model.Slug = ReadString(arguments, "slug") ?? model.Slug;
        model.DisplayName = ReadString(arguments, "displayName") ?? model.DisplayName;

        if (HasArgument(arguments, "maxChildren"))
        {
            if (ReadInt(arguments, "maxChildren") is not { } maxChildren)
            {
                return "maxChildren must be a whole number.";
            }

            model.MaxChildren = maxChildren;
        }

        if (HasArgument(arguments, "childHostPattern"))
        {
            model.ChildHostPattern = ReadString(arguments, "childHostPattern");
        }

        if (!TryReadEnum<ChildDatabaseStrategy>(arguments, "databaseStrategy", out var strategy))
        {
            return "databaseStrategy must be SqlitePerChild, DatabasePerChild, SchemaPerChild or TablePrefixPerChild.";
        }

        model.DatabaseStrategy = strategy ?? model.DatabaseStrategy;

        if (HasArgument(arguments, "databasePool"))
        {
            model.DatabasePool = ReadString(arguments, "databasePool");
        }

        if (ReadStringList(arguments, "recipes") is { } recipes)
        {
            // The editor offers only the setup recipes the host has.
            var available = (await services.GetRequiredService<ISetupService>().GetSetupRecipesAsync()).Select(recipe => recipe.Name).ToList();
            var resolved = new List<string>();

            foreach (var recipe in recipes)
            {
                var match = available.FirstOrDefault(name => string.Equals(name, recipe, StringComparison.OrdinalIgnoreCase));

                if (match is null)
                {
                    return $"'{recipe}' is not a setup recipe of this site. Use one of: {string.Join(", ", available)}.";
                }

                resolved.Add(match);
            }

            model.Recipes = resolved.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        if (ReadStringList(arguments, "blockedFeatures") is { } blockedFeatures)
        {
            model.BlockedFeatures = string.Join(Environment.NewLine, blockedFeatures);
        }

        if (ReadStringList(arguments, "deniedLocalPermissions") is { } deniedLocalPermissions)
        {
            model.DeniedLocalPermissions = string.Join(Environment.NewLine, deniedLocalPermissions);
        }

        model.RequireMfa = ReadBool(arguments, "requireMfa") ?? model.RequireMfa;

        foreach (var (name, apply) in new (string, Action<int>)[]
        {
            ("sessionValidationMinutes", value => model.SessionValidationMinutes = value),
            ("sessionIdleMinutes", value => model.SessionIdleMinutes = value),
            ("sessionLifetimeHours", value => model.SessionLifetimeHours = value),
            ("removalGraceDays", value => model.RemovalGraceDays = value),
        })
        {
            if (!HasArgument(arguments, name))
            {
                continue;
            }

            if (ReadInt(arguments, name) is not { } value)
            {
                return $"{name} must be a whole number.";
            }

            apply(value);
        }

        if (!TryReadEnum<SwitcherMode>(arguments, "switcherMode", out var switcherMode))
        {
            return "switcherMode must be Hosted or Embedded.";
        }

        model.SwitcherMode = switcherMode ?? model.SwitcherMode;

        if (HasArgument(arguments, "parentLabel"))
        {
            model.ParentLabel = ReadString(arguments, "parentLabel");
        }

        if (HasArgument(arguments, "childLabel"))
        {
            model.ChildLabel = ReadString(arguments, "childLabel");
        }

        if (HasArgument(arguments, "childrenLabel"))
        {
            model.ChildrenLabel = ReadString(arguments, "childrenLabel");
        }

        return null;
    }

    private static string ToArgumentName(string field)
        => field switch
        {
            nameof(ParentTenantPolicy.SessionValidationInterval) => "sessionValidationMinutes",
            nameof(ParentTenantPolicy.SessionIdleTimeout) => "sessionIdleMinutes",
            nameof(ParentTenantPolicy.SessionLifetime) => "sessionLifetimeHours",
            null or "" => null,
            _ => char.ToLowerInvariant(field[0]) + field[1..],
        };
}
