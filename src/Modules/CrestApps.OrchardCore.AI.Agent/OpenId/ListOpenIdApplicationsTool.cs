using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.OpenId;
using OrchardCore.OpenId.Abstractions.Managers;

namespace CrestApps.OrchardCore.AI.Agent.OpenId;

/// <summary>
/// Lists the OpenID Connect client applications through the application manager, as the Applications admin page
/// does: every application, filtered by display name, ordered by display name and paged.
/// </summary>
public sealed class ListOpenIdApplicationsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listOpenIdApplications";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Text the display name of an application must contain. Leave it out to list every application."
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
          "description": "The number of applications on a page. The default is 20 and the most is 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the OpenID Connect client applications registered on this site with id, client id, display name and type; optional search by display name and paging. Use it to find the id of an application before any other OpenID application tool.";

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

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, OpenIdPermissions.ManageApplications))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var manager = services.GetRequiredService<IOpenIdApplicationManager>();
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);
        var applications = new List<(string Id, string ClientId, string DisplayName, string Type)>();

        arguments.TryGetFirstString("search", out var search);

        // The manager pages but does not filter, so the search runs over every application, as on the admin page. A
        // site registers few applications.
        await foreach (var application in manager.ListAsync(null, null, cancellationToken))
        {
            var displayName = await manager.GetDisplayNameAsync(application, cancellationToken);

            if (!string.IsNullOrWhiteSpace(search) &&
                (displayName is null || !displayName.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            applications.Add((
                await manager.GetPhysicalIdAsync(application, cancellationToken),
                await manager.GetClientIdAsync(application, cancellationToken),
                displayName,
                await manager.GetClientTypeAsync(application, cancellationToken)));
        }

        var items = applications
            .OrderBy(application => application.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(application => application.Id, StringComparer.Ordinal)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(application => new
            {
                id = application.Id,
                clientId = application.ClientId,
                displayName = application.DisplayName,
                type = application.Type,
            })
            .ToList();

        return AgentToolResults.Json(new
        {
            items,
            total = applications.Count,
            page,
            pageSize,
            truncated = applications.Count > page * pageSize
                ? true
                : (bool?)null,
            note = "Use getOpenIdApplication for the flows, redirect URIs, roles and scopes of one application. No tool returns a client secret.",
        });
    }
}
