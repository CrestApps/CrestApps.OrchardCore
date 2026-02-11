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
            if (server.Tools.Count > 0 || server.Prompts.Count > 0 || server.Resources.Count > 0 || server.ResourceTemplates.Count > 0)
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
        sb.AppendLine();
        sb.AppendLine("IMPORTANT invocation rules:");
        sb.AppendLine("- Always specify the correct 'clientId', 'type', and 'id' parameters.");
        sb.AppendLine("- For tools: set type='tool' and id=<tool name>.");
        sb.AppendLine("- For prompts: set type='prompt' and id=<prompt name>.");
        sb.AppendLine("- For resources: set type='resource' and id=<the full resource URI>. Do NOT use the resource name as id.");
        sb.AppendLine("- For resource templates: set type='resource' and id=<the URI template with all {parameter} placeholders replaced with actual values from the user's request>.");
        sb.AppendLine();
        sb.AppendLine("Available MCP Capabilities:");

        foreach (var server in capabilities)
        {
            if (server.Tools.Count == 0 && server.Prompts.Count == 0 && server.Resources.Count == 0 && server.ResourceTemplates.Count == 0)
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
                sb.AppendLine("  Tools (pass required arguments via 'inputs'):");

                foreach (var tool in server.Tools.OrderBy(t => t.Name))
                {
                    sb.Append("    - ");
                    sb.Append(tool.Name);

                    if (!string.IsNullOrEmpty(tool.Description))
                    {
                        sb.Append(": ");
                        sb.Append(tool.Description);
                    }

                    if (tool.InputSchema.HasValue)
                    {
                        sb.AppendLine();
                        sb.Append("      Parameters: ");
                        sb.Append(tool.InputSchema.Value.ToString());
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
                sb.AppendLine("  Resources (use the URI as 'id' when invoking):");

                foreach (var resource in server.Resources.OrderBy(r => r.Name))
                {
                    sb.Append("    - ");
                    sb.Append(resource.Uri ?? resource.Name);

                    if (!string.IsNullOrEmpty(resource.Description))
                    {
                        sb.Append(": ");
                        sb.Append(resource.Description);
                    }

                    sb.AppendLine();
                }
            }

            if (server.ResourceTemplates.Count > 0)
            {
                sb.AppendLine("  Resource Templates (replace {parameter} placeholders with actual values and use the resolved URI as 'id'):");

                foreach (var template in server.ResourceTemplates.OrderBy(r => r.Name))
                {
                    sb.Append("    - ");
                    sb.Append(template.UriTemplate ?? template.Name);

                    if (!string.IsNullOrEmpty(template.Description))
                    {
                        sb.Append(": ");
                        sb.Append(template.Description);
                    }

                    sb.AppendLine();
                }
            }
        }

        return sb.ToString();
    }
}
