using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AgenticCommerce.Acp;

/// <summary>
/// Advertises ACP's MCP binding in the discovery document: <c>"mcp"</c> in <c>transports</c>.
/// </summary>
public sealed class AcpMcpDiscoveryContributor : IAcpDiscoveryContributor
{
    /// <inheritdoc />
    public Task ContributeAsync(JsonObject document, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document["transports"] is not JsonArray transports)
        {
            document["transports"] = transports = [];
        }

        if (!transports.Any(transport => transport?.GetValue<string>() == "mcp"))
        {
            transports.Add("mcp");
        }

        return Task.CompletedTask;
    }
}
