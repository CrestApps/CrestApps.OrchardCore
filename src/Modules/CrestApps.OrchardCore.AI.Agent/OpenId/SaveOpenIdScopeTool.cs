using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OrchardCore.Environment.Shell;
using OrchardCore.OpenId;
using OrchardCore.OpenId.Abstractions.Descriptors;
using OrchardCore.OpenId.Abstractions.Managers;

namespace CrestApps.OrchardCore.AI.Agent.OpenId;

/// <summary>
/// Creates or changes an OpenID Connect scope through the scope manager, with the checks of the Scopes admin page:
/// a name no other scope has, and no resource that names the tenant itself.
/// </summary>
public sealed class SaveOpenIdScopeTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveOpenIdScope";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the scope to update, from listOpenIdScopes. Leave it out to create a scope."
        },
        "name": {
          "type": "string",
          "description": "The name clients request, for example orders.read, without spaces. It is required for a new scope. For an update it renames the scope; applications that were allowed the old name must be allowed the new one."
        },
        "displayName": {
          "type": "string",
          "description": "The name people see. It is required for a new scope."
        },
        "description": {
          "type": "string",
          "description": "What the scope gives access to. Leave it out to keep the description of a scope that is updated."
        },
        "resources": {
          "type": "array",
          "description": "The complete list of resources (audiences) a token with this scope is issued for; it replaces the stored list. Leave it out to keep the stored list.",
          "items": {
            "type": "string"
          }
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates an OpenID scope: name, display name, description and the resources (audiences) it grants. Use saveOpenIdApplication to allow an application to request the scope.";

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

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, OpenIdPermissions.ManageScopes))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!AgentToolArguments.TryReadStrings(arguments, "resources", out var resources))
        {
            return "Nothing was saved: resources must be a list of texts.";
        }

        arguments.TryGetFirstString("id", out var id);

        var hasName = arguments.TryGetFirstString("name", out var name) && !string.IsNullOrWhiteSpace(name);
        var hasDisplayName = arguments.TryGetFirstString("displayName", out var displayName) && !string.IsNullOrWhiteSpace(displayName);
        var hasDescription = arguments.TryGetFirstString("description", true, out var description);
        var manager = services.GetRequiredService<IOpenIdScopeManager>();
        var create = string.IsNullOrWhiteSpace(id);
        object scope = null;

        name = name?.Trim();

        if (create)
        {
            if (!hasName || !hasDisplayName)
            {
                return "Nothing was saved: give name and displayName for a new scope, or the id of the scope to update.";
            }
        }
        else
        {
            scope = await manager.FindByPhysicalIdAsync(id.Trim(), cancellationToken);

            if (scope is null)
            {
                return $"Nothing was saved: there is no OpenID scope with the id '{id}'. Use listOpenIdScopes to find it.";
            }

            if (!hasName && !hasDisplayName && !hasDescription && resources is null)
            {
                return "Nothing was saved: give at least one field to change.";
            }
        }

        // The check of the Create and Edit pages: no other scope has the name.
        if (hasName && await manager.FindByNameAsync(name, cancellationToken) is { } other)
        {
            var otherId = await manager.GetPhysicalIdAsync(other, cancellationToken);

            if (create || !string.Equals(otherId, await manager.GetPhysicalIdAsync(scope, cancellationToken), StringComparison.Ordinal))
            {
                return $"Nothing was saved: the name '{name}' is taken by the scope with the id '{otherId}'. Give that id to update it, or pick another name.";
            }
        }

        // The check of the Create and Edit pages: the tenant itself is not a resource of a scope.
        var tenantResource = OpenIdConstants.Prefixes.Tenant + services.GetRequiredService<ShellSettings>().Name;

        if (resources is not null && resources.Any(resource => resource.Contains(tenantResource, StringComparison.OrdinalIgnoreCase)))
        {
            return $"Nothing was saved: the resources cannot contain '{tenantResource}'.";
        }

        if (resources is not null && resources.Any(resource => resource.Contains(' ')))
        {
            return "Nothing was saved: a resource cannot contain a space. Give each resource as its own entry of the list.";
        }

        var descriptor = new OpenIdScopeDescriptor();

        if (!create)
        {
            await manager.PopulateAsync(descriptor, scope, cancellationToken);
        }

        if (hasName)
        {
            descriptor.Name = name;
        }

        if (hasDisplayName)
        {
            descriptor.DisplayName = displayName.Trim();
        }

        if (hasDescription)
        {
            descriptor.Description = description;
        }

        if (resources is not null)
        {
            descriptor.Resources.Clear();
            descriptor.Resources.UnionWith(resources);
        }

        try
        {
            if (create)
            {
                await manager.CreateAsync(descriptor, cancellationToken);
            }
            else
            {
                await manager.UpdateAsync(scope, descriptor, cancellationToken);
            }
        }
        catch (OpenIddictExceptions.ValidationException exception)
        {
            return $"Nothing was saved: {exception.Message}";
        }

        var saved = await manager.FindByNameAsync(descriptor.Name, cancellationToken);

        if (saved is null)
        {
            return "Nothing was saved: the scope was not found after the save. Use listOpenIdScopes to check.";
        }

        var savedId = await manager.GetPhysicalIdAsync(saved, cancellationToken);

        return AgentToolResults.Json(new
        {
            saved = true,
            created = create,
            scope = new
            {
                id = savedId,
                name = await manager.GetNameAsync(saved, cancellationToken),
                displayName = await manager.GetDisplayNameAsync(saved, cancellationToken),
                description = await manager.GetDescriptionAsync(saved, cancellationToken),
                resources = (await manager.GetResourcesAsync(saved, cancellationToken))
                    .Where(resource => !string.IsNullOrEmpty(resource))
                    .ToArray(),
                adminUrl = OpenIdToolSupport.GetScopeUrl(services, savedId),
            },
            note = "An application may request the scope after saveOpenIdApplication lists it in its scopes.",
        });
    }
}
