---
sidebar_label: MCP Server
sidebar_position: 4
title: MCP Server
description: Expose Orchard Core AI tools, prompts, and resources through the Model Context Protocol.
---

| | |
| --- | --- |
| **Feature Name** | Model Context Protocol (MCP) Server |
| **Feature ID** | `CrestApps.OrchardCore.AI.Mcp.Server` |

Exposes Orchard Core AI tools through the MCP protocol, enabling external MCP-compatible clients to connect and invoke AI capabilities.

## Overview

The **MCP Server Feature** allows your Orchard Core application to expose its AI tools and capabilities to external MCP clients. This feature supports the SSE transport type, enabling real-time communication.

## Supported Capabilities

The MCP server exposes the following capabilities:

| Capability | Description |
|-----------|-------------|
| **Tools** | All registered AI tools in Orchard Core are exposed as MCP tools that clients can discover and invoke |
| **Prompts** | MCP prompts registered in Orchard Core are exposed so clients can list and invoke prompts via `ListPrompts` and `GetPrompt`. Prompts can be added and managed via the admin UI. |
| **Resources** | MCP resources registered in Orchard Core are exposed, allowing clients to access various data sources. Resources can be added and managed via the admin UI. |
| **Templated Resources** | Resources with URI variable placeholders (e.g., `{fileName}`, `{contentType}`) that resolve dynamically based on client requests |

## Prompt Support

The MCP server exposes MCP prompts registered in Orchard Core. Prompts can be:

- Created and managed via the admin UI under **Artificial Intelligence → MCP Prompts**
- Registered programmatically in code
- Discovered by external MCP clients via `ListPrompts` and invoked via `GetPrompt`

## Resource Support

The MCP server exposes MCP resources registered in Orchard Core, allowing clients to access various data sources through the MCP protocol.

Resources can be:
- Created and managed via the admin UI under **Artificial Intelligence → MCP Resources**
- Registered programmatically in code
- Discovered and accessed by external MCP clients

### Built-in Resource Types

| Type | Supported Variables | Description |
|------|---------------------|-------------|
| **File** (`file`) | `{providerName}`, `{fileName}` | File access via named file providers |
| **Media** (`media`) | `{path}` | Orchard Core media library files |
| **Content Item** (`content-item`) | `{contentItemId}`, `{contentItemVersionId}` | Fetch a specific content item by ID or version |
| **Content Type** (`content-type`) | `{contentType}` | List all published content items of a type |
| **Recipe Schema** (`recipe-schema`) | *(none)* | Full JSON schema for all recipe steps |
| **Recipe Step Schema** (`recipe-step-schema`) | `{stepName}` | JSON schema for a specific recipe step |
| **Recipe** (`recipe`) | `{recipeName}` | Recipe content by name |
| **FTP/FTPS** (`ftp`) | `{path}` | Remote files via FTP (separate module) |
| **SFTP** (`sftp`) | `{path}` | Remote files via SSH (separate module) |

### How URI Patterns Work

Each resource instance has a URI that is auto-constructed by the system as:

```
{source}://{itemId}/{path}
```

- **`{source}`**: the resource type name (e.g., `file`, `content-item`, `recipe`)
- **`{itemId}`**: the auto-generated resource instance identifier
- **`{path}`**: the user-defined path portion with optional variable placeholders

When creating a resource in the admin UI, you only provide the **path** portion. The system automatically prepends the scheme and resource ID.

### Creating Resources via Admin UI

1. Navigate to **Artificial Intelligence** → **MCP Resources**
2. Click **Add Resource**
3. Select a resource type (e.g., File, Content Item, Recipe Step Schema)
4. Fill in the required fields:
   - **Display Text**: A friendly name for the resource
   - **Path**: The path portion of the URI, using any supported variables shown in the UI
   - **Name**: The MCP resource name (used by clients)
   - **Title**: Optional human-readable title
   - **Description**: Optional description
   - **MIME Type**: Content type of the resource
5. Save the resource

### Registering Custom Resource Types

You can register custom resource types with their handlers:

```csharp
services.AddMcpResourceType<DatabaseResourceTypeHandler>("database", entry =>
{
    entry.DisplayName = S["Database"];
    entry.Description = S["Query data from databases."];
    entry.SupportedVariables =
    [
        new McpResourceVariable("table") { Description = S["The database table name."] },
        new McpResourceVariable("id") { Description = S["The row ID to fetch."] },
    ];
});
```

Implement the handler by extending `McpResourceTypeHandlerBase`:

```csharp
public class DatabaseResourceTypeHandler : McpResourceTypeHandlerBase
{
    public DatabaseResourceTypeHandler() : base("database") { }

    protected override Task<ReadResourceResult> GetResultAsync(
        McpResource resource,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        variables.TryGetValue("table", out var table);
        variables.TryGetValue("id", out var id);
        // Query database and return results
    }
}
```

### Recipe Support

Resources can be exported and imported via recipes:

```json
{
  "steps": [
    {
      "name": "McpResource",
      "Resources": [
        {
          "Source": "file",
          "DisplayText": "Configuration File",
          "Resource": {
            "Uri": "file://configs/{providerName}/{fileName}",
            "Name": "config-file",
            "Description": "Application configuration",
            "MimeType": "application/json"
          }
        }
      ]
    }
  ]
}
```

## Admin Chat UI with MCP Server Integration

![Screen cast of the admin chat](/img/docs/mcp-integration.gif)
