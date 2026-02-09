using System.Text;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

public sealed class DefaultMcpMetadataPromptGenerator : IMcpMetadataPromptGenerator
{
    public string Generate(IReadOnlyList<McpServerCapabilities> capabilities)
    {
        if (capabilities is null || capabilities.Count == 0)
        {
            return null;
        }

        var hasAnyCapability = false;

        foreach (var server in capabilities)
        {
            if (server.Tools.Count > 0 || server.Prompts.Count > 0 || server.Resources.Count > 0)
            {
                hasAnyCapability = true;

                break;
            }
        }

        if (!hasAnyCapability)
        {
            return null;
        }

        var sb = new StringBuilder();

        sb.AppendLine("You have access to external MCP (Model Context Protocol) servers via the 'mcp_invoke' tool.");
        sb.AppendLine("Use the 'mcp_invoke' tool to call any of the capabilities listed below.");
        sb.AppendLine("Always specify the correct 'clientId', 'type', and 'id' parameters.");
        sb.AppendLine();
        sb.AppendLine("Available MCP Capabilities:");

        foreach (var server in capabilities)
        {
            if (server.Tools.Count == 0 && server.Prompts.Count == 0 && server.Resources.Count == 0)
            {
                continue;
            }

            sb.AppendLine();
            sb.Append("## Server: ");
            sb.AppendLine(server.ConnectionDisplayText ?? server.ConnectionId);
            sb.Append("  clientId: ");
            sb.AppendLine(server.ConnectionId);

            if (server.Tools.Count > 0)
            {
                sb.AppendLine("  Tools:");

                foreach (var tool in server.Tools.OrderBy(t => t.Name))
                {
                    sb.Append("    - ");
                    sb.Append(tool.Name);

                    if (!string.IsNullOrEmpty(tool.Description))
                    {
                        sb.Append(": ");
                        sb.Append(tool.Description);
                    }

                    sb.AppendLine();
                }
            }

            if (server.Prompts.Count > 0)
            {
                sb.AppendLine("  Prompts:");

                foreach (var prompt in server.Prompts.OrderBy(p => p.Name))
                {
                    sb.Append("    - ");
                    sb.Append(prompt.Name);

                    if (!string.IsNullOrEmpty(prompt.Description))
                    {
                        sb.Append(": ");
                        sb.Append(prompt.Description);
                    }

                    sb.AppendLine();
                }
            }

            if (server.Resources.Count > 0)
            {
                sb.AppendLine("  Resources:");

                foreach (var resource in server.Resources.OrderBy(r => r.Name))
                {
                    sb.Append("    - ");
                    sb.Append(resource.Name);

                    if (!string.IsNullOrEmpty(resource.Uri))
                    {
                        sb.Append(" (uri: ");
                        sb.Append(resource.Uri);
                        sb.Append(')');
                    }

                    if (!string.IsNullOrEmpty(resource.Description))
                    {
                        sb.Append(": ");
                        sb.Append(resource.Description);
                    }

                    sb.AppendLine();
                }
            }
        }

        return sb.ToString();
    }
}
