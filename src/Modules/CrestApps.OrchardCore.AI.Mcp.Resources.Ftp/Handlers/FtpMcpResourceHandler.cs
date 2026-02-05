using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Resources.Ftp.Models;

namespace CrestApps.OrchardCore.AI.Mcp.Resources.Ftp.Handlers;

/// <summary>
/// Handles MCP resource events for FTP resources, particularly to ensure
/// sensitive credentials are not exported.
/// </summary>
public sealed class FtpMcpResourceHandler : IMcpResourceHandler
{
    public void Exporting(ExportingMcpResourceContext context)
    {
        if (!string.Equals(context.Resource.Source, FtpResourceConstants.Type, StringComparison.Ordinal))
        {
            return;
        }

        var metadataNode = context.ExportData["Properties"]?[nameof(FtpConnectionMetadata)]?.AsObject();

        if (metadataNode == null || metadataNode.Count == 0)
        {
            return;
        }

        // Always set the password to an empty string during export to prevent accidental exposure.
        metadataNode[nameof(FtpConnectionMetadata.Password)] = string.Empty;

        context.ExportData["Properties"][nameof(FtpConnectionMetadata)] = metadataNode;
    }
}
