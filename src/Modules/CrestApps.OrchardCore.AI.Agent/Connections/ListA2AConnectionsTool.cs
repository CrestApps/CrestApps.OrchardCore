using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.Connections;

/// <summary>
/// Lists the remote Agent-to-Agent hosts the site connects to, as the Agent to Agent Hosts page does, with the
/// profiles that use each. Credentials are only reported as stored or not, and custom headers by name only.
/// </summary>
public sealed class ListA2AConnectionsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listA2AConnections";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Only connections whose title contains this text."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "How many connections per page, at most 50. Defaults to 20."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists A2A connections with title, endpoint, authentication type, whether credentials are stored and which profiles use them. Credentials and header values are never shown.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIModulePermissions.ManageA2AConnections))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("search", out var search);
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        IEnumerable<A2AConnection> connections = await services.GetRequiredService<ICatalogManager<A2AConnection>>().GetAllAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            connections = connections.Where(connection => connection.DisplayText?.Contains(text, StringComparison.OrdinalIgnoreCase) == true);
        }

        var filtered = connections
            .OrderBy(connection => connection.DisplayText, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var users = await ConnectionToolSupport.GetA2AUsersAsync(services.GetRequiredService<IAIProfileManager>(), cancellationToken);
        var localClock = services.GetRequiredService<ILocalClock>();
        var items = new List<object>();

        foreach (var connection in filtered.Skip((page - 1) * pageSize).Take(pageSize))
        {
            var metadata = connection.TryGet<A2AConnectionMetadata>(out var stored) ? stored : null;

            items.Add(new
            {
                id = connection.ItemId,
                title = connection.DisplayText,
                endpoint = connection.Endpoint,
                authentication = RemoteConnectionAuth.Describe(metadata),
                usedBy = users.GetValueOrDefault(connection.ItemId) ?? [],
                created = await AgentToolResults.ToLocalAsync(localClock, connection.CreatedUtc == default ? null : connection.CreatedUtc),
                modified = await AgentToolResults.ToLocalAsync(localClock, connection.ModifiedUtc),
                editPath = ConnectionToolSupport.GetA2AConnectionEditPath(services, connection.ItemId),
            });
        }

        return AgentToolResults.Json(new
        {
            items,
            total = filtered.Count,
            page,
            pageSize,
            truncated = filtered.Count > page * pageSize,
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
        });
    }
}
