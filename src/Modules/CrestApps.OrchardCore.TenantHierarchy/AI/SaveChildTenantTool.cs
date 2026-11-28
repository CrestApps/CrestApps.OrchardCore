using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.TenantHierarchy.AI;

/// <summary>
/// Creates a child tenant and queues its setup, or changes the name, address and description of one, through the
/// <see cref="ChildTenantManager"/> the child tenants page uses. The address is only the slug: the host comes from the
/// parent's address pattern, and the setup recipe must be one the parent's policy allows, as on the page.
/// </summary>
internal sealed class SaveChildTenantTool : TenantHierarchyAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveChildTenant";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "entryId": {
          "type": "string",
          "description": "The entry id of the child tenant to change, from listChildTenants. Leave it out to create a child tenant."
        },
        "displayName": {
          "type": "string",
          "maxLength": 100,
          "description": "The name of the child tenant. Required to create one."
        },
        "slug": {
          "type": "string",
          "minLength": 3,
          "maxLength": 40,
          "description": "The address of the child tenant: lowercase letters, digits and inner hyphens, placed in the parent's address pattern. Required to create one; a new one on an existing child tenant changes its address."
        },
        "description": {
          "type": "string",
          "maxLength": 500,
          "description": "A description of the child tenant. An empty text clears it."
        },
        "recipeName": {
          "type": "string",
          "description": "The setup recipe of a new child tenant, one of those listChildTenants returns with includePolicy. It is used only when creating; it is optional when the policy allows a single recipe."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a child tenant (name, slug, description, setup recipe) and starts its setup, or changes the name, slug and description of an existing one. Creating provisions a database and fails when the policy's child limit is reached or the address is taken.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override TenantHierarchyToolScope Scope => TenantHierarchyToolScope.Parent;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var entryId = ReadString(arguments, "entryId");

        return entryId is null
            ? await CreateAsync(arguments, services, user)
            : await UpdateAsync(arguments, services, user, entryId);
    }

    private static async Task<object> CreateAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user)
    {
        if (!await IsAuthorizedAsync(services, user, TenantHierarchyPermissions.CreateChildTenants))
        {
            return NotAllowedMessage;
        }

        var manager = services.GetRequiredService<ChildTenantManager>();
        var recipeName = ReadString(arguments, "recipeName");

        if (recipeName is null)
        {
            // The create page selects the recipe when the policy allows only one.
            var recipes = await manager.GetAllowedRecipesAsync();

            if (recipes.Count == 1)
            {
                recipeName = recipes[0].Name;
            }
        }

        var request = new CreateChildTenantRequest
        {
            DisplayName = ReadString(arguments, "displayName"),
            Slug = ReadString(arguments, "slug"),
            Description = ReadString(arguments, "description"),
            RecipeName = recipeName,

            // The registry records who created the child tenant; the chat's caller, not the request's.
            ActorUserId = user.FindFirstValue(ClaimTypes.NameIdentifier),
            ActorUserName = user.Identity?.Name,
        };

        var errors = await manager.ValidateCreateAsync(request);

        if (errors.Count > 0)
        {
            return Json(new
            {
                created = false,
                errors = errors.Select(error => new { field = ToArgumentName(error.Key), message = error.Value }).ToList(),
                hint = "Call listChildTenants with includePolicy to see the setup recipes and the address pattern.",
            });
        }

        // A child tenant created with no setup to follow would stay in the Setting up state, so nothing is created
        // when the setup cannot be queued.
        if (!ChildTenantSetupQueue.CanQueue)
        {
            return $"Nothing was created: the setup could not be queued here. A person can create the child tenant on the child tenants page: {GetAdminPath(services, "TenantHierarchyChildrenCreate")}";
        }

        var (result, entry) = await manager.CreateAsync(request);

        if (!result.Succeeded)
        {
            return $"The child tenant was not created: {result.Error}";
        }

        ChildTenantSetupQueue.Queue(entry.EntryId);

        var info = await services.GetRequiredService<ITenantHierarchyBroker>().GetChildAsync(entry.EntryId);

        return Json(new
        {
            created = true,
            child = info is null ? null : await DescribeChildAsync(services, info),
            entryId = entry.EntryId,
            setupQueued = true,
            message = $"{entry.DisplayName} was created and its setup runs in the background after this reply, which can take a minute. Call listChildTenants to follow it: it is Running when the setup succeeded and Failed, with the error, when it did not.",
        });
    }

    private static async Task<object> UpdateAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, string entryId)
    {
        if (!await IsAuthorizedAsync(services, user, TenantHierarchyPermissions.ManageChildTenants))
        {
            return NotAllowedMessage;
        }

        if (ReadString(arguments, "recipeName") is not null)
        {
            return "Nothing was changed: the setup recipe of an existing child tenant cannot change. Leave recipeName out to change its name, address or description.";
        }

        var info = await FindChildAsync(services, entryId);

        if (info is null)
        {
            return ChildNotFound(entryId);
        }

        var entry = info.Entry;
        var previousHost = entry.Host;
        var displayName = ReadString(arguments, "displayName") ?? entry.DisplayName;
        var slug = ReadString(arguments, "slug") ?? entry.Slug;
        var description = HasArgument(arguments, "description")
            ? ReadString(arguments, "description")
            : entry.Description;

        var (result, errors) = await services.GetRequiredService<ChildTenantManager>().UpdateAsync(entryId, displayName, slug, description);

        if (!result.Succeeded)
        {
            var problems = errors.Select(error => new { field = ToArgumentName(error.Key), message = error.Value }).ToList();

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

        var updated = await services.GetRequiredService<ITenantHierarchyBroker>().GetChildAsync(entryId);

        return Json(new
        {
            saved = true,
            child = updated is null ? null : await DescribeChildAsync(services, updated),
            addressChanged = updated is not null && !string.Equals(previousHost, updated.Entry.Host, StringComparison.OrdinalIgnoreCase),
            editPath = GetAdminPath(services, "TenantHierarchyChildrenEdit", new { id = entryId }),
        });
    }

    private static string ToArgumentName(string field)
        => field switch
        {
            nameof(CreateChildTenantRequest.DisplayName) => "displayName",
            nameof(CreateChildTenantRequest.Slug) => "slug",
            nameof(CreateChildTenantRequest.Description) => "description",
            nameof(CreateChildTenantRequest.RecipeName) => "recipeName",
            _ => field,
        };
}
