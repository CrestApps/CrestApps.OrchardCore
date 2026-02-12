using System.Text;
using System.Text.Json;
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
        sb.AppendLine("- For tools: set type='tool', id=<tool name>, and inputs=<object matching the tool's Parameters schema>.");
        sb.AppendLine("  The 'inputs' object must include all required properties as defined in the tool's Parameters schema. It must be a valid JSON object, with no wrappers (such as code fences) or additional formatting—only pure JSON.");
        sb.AppendLine("  Example: if a tool has Parameters with required property 'featureIds' (array of strings), call mcp_invoke with inputs={\"featureIds\":[\"value1\",\"value2\"]}.");
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

                    sb.AppendLine();

                    if (tool.InputSchema.HasValue)
                    {
                        AppendParameterSummary(sb, tool.InputSchema.Value);
                    }
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

    private static void AppendParameterSummary(StringBuilder sb, JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!schema.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var requiredSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (schema.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in required.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    requiredSet.Add(item.GetString());
                }
            }
        }

        foreach (var property in properties.EnumerateObject())
        {
            var name = property.Name;
            var isRequired = requiredSet.Contains(name);
            var typeName = GetTypeName(property.Value);
            var description = property.Value.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.String
                ? desc.GetString()
                : null;

            sb.Append("      ");
            sb.Append(name);
            sb.Append(" (");
            sb.Append(typeName);

            if (isRequired)
            {
                sb.Append(", required");
            }

            sb.Append(')');

            if (!string.IsNullOrEmpty(description))
            {
                sb.Append(": ");
                sb.Append(description);
            }

            sb.AppendLine();
        }
    }

    private static string GetTypeName(JsonElement propertySchema)
    {
        if (!propertySchema.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
        {
            return "object";
        }

        var type = typeElement.GetString();

        if (type == "array" && propertySchema.TryGetProperty("items", out var items))
        {
            var itemType = items.TryGetProperty("type", out var it) && it.ValueKind == JsonValueKind.String
                ? it.GetString()
                : "object";

            return $"{itemType}[]";
        }

        return type;
    }
}
