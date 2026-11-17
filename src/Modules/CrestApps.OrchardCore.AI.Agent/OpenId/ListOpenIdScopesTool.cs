using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.OpenId;
using OrchardCore.OpenId.Abstractions.Managers;

namespace CrestApps.OrchardCore.AI.Agent.OpenId;

/// <summary>
/// Lists the OpenID Connect scopes through the scope manager, as the Scopes admin page does: every scope, filtered by
/// name or display name, ordered by display name and paged.
/// </summary>
public sealed class ListOpenIdScopesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listOpenIdScopes";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Text the name or the display name of a scope must contain. Leave it out to list every scope."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1. The default is 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of scopes on a page. The default is 20 and the most is 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the OpenID scopes with id, name, display name, description and resources. Use it to get the id of a scope before saveOpenIdScope or deleteOpenIdScope, and the scope names for saveOpenIdApplication.";

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

        var manager = services.GetRequiredService<IOpenIdScopeManager>();
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);
        var scopes = new List<(object Scope, string Name, string DisplayName)>();

        arguments.TryGetFirstString("search", out var search);

        search = search?.Trim();

        // The manager pages but does not filter, so the search runs over every scope, as on the admin page. A site
        // defines few scopes.
        await foreach (var scope in manager.ListAsync(null, null, cancellationToken))
        {
            var name = await manager.GetNameAsync(scope, cancellationToken);
            var displayName = await manager.GetDisplayNameAsync(scope, cancellationToken);

            if (!string.IsNullOrEmpty(search) &&
                displayName?.Contains(search, StringComparison.OrdinalIgnoreCase) != true &&
                name?.Contains(search, StringComparison.OrdinalIgnoreCase) != true)
            {
                continue;
            }

            scopes.Add((scope, name, displayName));
        }

        var items = new List<object>();

        foreach (var (scope, name, displayName) in scopes
            .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Skip((page - 1) * pageSize)
            .Take(pageSize))
        {
            items.Add(new
            {
                id = await manager.GetPhysicalIdAsync(scope, cancellationToken),
                name,
                displayName,
                description = await manager.GetDescriptionAsync(scope, cancellationToken),
                resources = (await manager.GetResourcesAsync(scope, cancellationToken))
                    .Where(resource => !string.IsNullOrEmpty(resource))
                    .ToArray(),
            });
        }

        return AgentToolResults.Json(new
        {
            items,
            total = scopes.Count,
            page,
            pageSize,
            truncated = scopes.Count > page * pageSize
                ? true
                : (bool?)null,
        });
    }
}
