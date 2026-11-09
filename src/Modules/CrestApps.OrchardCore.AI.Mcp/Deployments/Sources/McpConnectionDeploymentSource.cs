using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Mcp.Deployments.Steps;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.AI.Mcp.Deployments.Sources;

/// <summary>
/// Writes the selected MCP client connections to a deployment plan, with the credentials of SSE connections blanked.
/// </summary>
internal sealed class McpConnectionDeploymentSource : CatalogDeploymentSourceBase<McpConnectionDeploymentStep, McpConnection>
{
    private readonly ISourceCatalog<McpConnection> _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpConnectionDeploymentSource"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    public McpConnectionDeploymentSource(ISourceCatalog<McpConnection> store)
        : base("Connections")
    {
        _store = store;
    }

    protected override async Task<IEnumerable<McpConnection>> GetEntriesAsync()
        => await _store.GetAllAsync();

    protected override bool IsSelected(McpConnectionDeploymentStep step, McpConnection entry)
        => IsSelectedByKey(step.IncludeAll, step.ConnectionIds, entry.ItemId);

    protected override JsonObject Export(McpConnection entry)
    {
        var deploymentInfo = new JsonObject()
        {
            { "ItemId", entry.ItemId },
            { "DisplayText", entry.DisplayText },
            { "Source", entry.Source },
            { "Author", entry.Author },
            { "CreatedUtc", entry.CreatedUtc },
            { "OwnerId", entry.OwnerId },
        };

        var properties = JsonSerializer.SerializeToNode(entry.Properties)?.AsObject() ?? new JsonObject();

        SanitizeSensitiveData(entry, properties);

        deploymentInfo["Properties"] = properties;

        return deploymentInfo;
    }

    private static void SanitizeSensitiveData(McpConnection connection, JsonObject properties)
    {
        if (!string.Equals(connection.Source, McpConstants.TransportTypes.Sse, StringComparison.Ordinal))
        {
            return;
        }

        var metadataNode = properties[nameof(SseMcpConnectionMetadata)]?.AsObject();

        if (metadataNode == null)
        {
            return;
        }

        // Clear all sensitive fields to prevent accidental credential exposure.
        metadataNode[nameof(SseMcpConnectionMetadata.ApiKey)] = string.Empty;
        metadataNode[nameof(SseMcpConnectionMetadata.BasicPassword)] = string.Empty;
        metadataNode[nameof(SseMcpConnectionMetadata.OAuth2ClientSecret)] = string.Empty;
        metadataNode[nameof(SseMcpConnectionMetadata.OAuth2PrivateKey)] = string.Empty;
        metadataNode[nameof(SseMcpConnectionMetadata.OAuth2ClientCertificate)] = string.Empty;
        metadataNode[nameof(SseMcpConnectionMetadata.OAuth2ClientCertificatePassword)] = string.Empty;
    }
}
