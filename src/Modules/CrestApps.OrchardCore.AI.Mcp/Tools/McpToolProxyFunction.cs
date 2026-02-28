using System.Text.Json;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Mcp.Tools;

/// <summary>
/// A lightweight <see cref="AIFunction"/> proxy that wraps a single MCP server tool.
/// When the AI model invokes this function, the call is transparently routed to the
/// appropriate MCP server via <see cref="McpService"/>.
/// </summary>
internal sealed class McpToolProxyFunction : AIFunction
{
    private readonly string _name;
    private readonly string _description;
    private readonly JsonElement _jsonSchema;
    private readonly string _connectionId;

    public McpToolProxyFunction(
        string name,
        string description,
        JsonElement jsonSchema,
        string connectionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(connectionId);

        _name = name;
        _description = description ?? name;
        _jsonSchema = jsonSchema;
        _connectionId = connectionId;
    }

    public override string Name => _name;

    public override string Description => _description;

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var store = arguments.Services.GetRequiredService<ISourceCatalog<McpConnection>>();
        var connection = await store.FindByIdAsync(_connectionId);

        if (connection is null)
        {
            return JsonSerializer.Serialize(new { error = $"MCP connection '{_connectionId}' not found." });
        }

        var mcpService = arguments.Services.GetRequiredService<McpService>();
        var client = await mcpService.GetOrCreateClientAsync(connection);

        if (client is null)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to connect to MCP server '{_connectionId}'." });
        }

        try
        {
            var args = new Dictionary<string, object>();

            foreach (var kvp in arguments)
            {
                if (kvp.Value is JsonElement je)
                {
                    args[kvp.Key] = ConvertJsonElement(je);
                }
                else if (kvp.Value is not null)
                {
                    args[kvp.Key] = kvp.Value;
                }
            }

            var result = await client.CallToolAsync(_name, args, cancellationToken: cancellationToken);

            return JsonSerializer.Serialize(result);
        }
        catch (Exception ex)
        {
            var logger = arguments.Services.GetRequiredService<ILogger<McpToolProxyFunction>>();

            logger.LogError(
                ex,
                "Error invoking MCP tool '{ToolName}' on server '{ConnectionId}'.",
                _name, _connectionId);

            return JsonSerializer.Serialize(new
            {
                error = $"Error invoking MCP tool '{_name}'.",
            });
        }
    }

    private static object ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
            _ => element.GetRawText(),
        };
    }
}
