using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Mcp.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.Connections;

/// <summary>
/// Lists the MCP servers the site connects to, as the MCP Hosts page does, with the profiles that use each. No
/// credential is returned: keys, passwords, secrets, private keys and certificates are only reported as stored or not,
/// custom headers and the environment variables of a local server by name only, and a local server's arguments, which
/// often carry tokens, by count only.
/// </summary>
public sealed class ListMcpConnectionsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listMcpConnections";

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
    public override string Description => "Lists MCP connections with transport, title, endpoint or command, authentication type, which credentials are stored (present or absent only) and which profiles use them. Credentials, header values and environment variable values are never shown.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, McpPermissions.ManageMcpConnections))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("search", out var search);
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        IEnumerable<McpConnection> connections = await services.GetRequiredService<ISourceCatalogManager<McpConnection>>().GetAllAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            connections = connections.Where(connection => connection.DisplayText?.Contains(text, StringComparison.OrdinalIgnoreCase) == true);
        }

        var filtered = connections
            .OrderBy(connection => connection.DisplayText, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var users = await ConnectionToolSupport.GetMcpUsersAsync(services.GetRequiredService<IAIProfileManager>(), cancellationToken);
        var localClock = services.GetRequiredService<ILocalClock>();
        var items = new List<object>();

        foreach (var connection in filtered.Skip((page - 1) * pageSize).Take(pageSize))
        {
            var isSse = string.Equals(connection.Source, McpConstants.TransportTypes.Sse, StringComparison.OrdinalIgnoreCase);
            var sse = isSse && connection.TryGet<SseMcpConnectionMetadata>(out var sseMetadata) ? sseMetadata : null;
            var stdio = !isSse && connection.TryGet<StdioMcpConnectionMetadata>(out var stdioMetadata) ? stdioMetadata : null;

            items.Add(new
            {
                id = connection.ItemId,
                title = connection.DisplayText,
                transport = connection.Source,
                endpoint = sse?.Endpoint?.ToString(),
                authentication = isSse ? RemoteConnectionAuth.Describe(sse) : null,
                command = stdio?.Command,
                argumentCount = stdio?.Arguments?.Length,
                workingDirectory = stdio?.WorkingDirectory,
                environmentVariableNames = stdio?.EnvironmentVariables is { Count: > 0 } variables
                    ? variables.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList()
                    : null,
                usedBy = users.GetValueOrDefault(connection.ItemId) ?? [],
                created = await AgentToolResults.ToLocalAsync(localClock, connection.CreatedUtc == default ? null : connection.CreatedUtc),
                modified = await AgentToolResults.ToLocalAsync(localClock, connection.ModifiedUtc),
                editPath = ConnectionToolSupport.GetMcpConnectionEditPath(services, connection.ItemId),
            });
        }

        return AgentToolResults.Json(new
        {
            items,
            total = filtered.Count,
            page,
            pageSize,
            truncated = filtered.Count > page * pageSize,
            transports = services.GetRequiredService<IOptions<McpClientAIOptions>>().Value.TransportTypes.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList(),
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
        });
    }
}
