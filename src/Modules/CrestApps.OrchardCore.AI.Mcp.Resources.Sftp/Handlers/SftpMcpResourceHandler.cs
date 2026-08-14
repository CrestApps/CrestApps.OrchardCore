using CrestApps.Core.AI.Mcp;
using CrestApps.OrchardCore.AI.Mcp.Resources.Sftp.Models;

namespace CrestApps.OrchardCore.AI.Mcp.Resources.Sftp.Handlers;

/// <summary>
/// Handles MCP resource events for SFTP resources, particularly to ensure
/// sensitive credentials are not exported.
/// </summary>
public sealed class SftpMcpResourceHandler : IMcpResourceHandler
{
    /// <summary>
    /// Performs the exporting operation.
    /// </summary>
    /// <param name="context">The context.</param>
    public void Exporting(ExportingMcpResourceContext context)
    {
        if (!string.Equals(context.Resource.Source, SftpResourceConstants.Type, StringComparison.Ordinal))
        {
            return;
        }

        var metadataNode = context.ExportData["Properties"]?[nameof(SftpConnectionMetadata)]?.AsObject();

        if (metadataNode == null || metadataNode.Count == 0)
        {
            return;
        }

        // Always clear sensitive credentials during export to prevent accidental exposure.
        metadataNode[nameof(SftpConnectionMetadata.Password)] = string.Empty;
        metadataNode[nameof(SftpConnectionMetadata.PrivateKey)] = string.Empty;
        metadataNode[nameof(SftpConnectionMetadata.Passphrase)] = string.Empty;
        metadataNode[nameof(SftpConnectionMetadata.ProxyPassword)] = string.Empty;

        context.ExportData["Properties"][nameof(SftpConnectionMetadata)] = metadataNode;
    }
}
