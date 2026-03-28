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

MCP **Prompts** are reusable prompt templates that MCP clients can discover and invoke. They allow you to define pre-configured system or user messages that external AI agents can request on demand — for example, a "summarize" prompt that instructs the model to summarize a given document, or a "translate" prompt that translates text into a target language.

Prompts are listed by clients via `ListPrompts` and invoked via `GetPrompt`, which returns the prompt messages for the client to include in its conversation.

### Managing Prompts via Admin UI

1. Navigate to **Artificial Intelligence** → **MCP Prompts**
2. Click **Add Prompt** to create a new prompt
3. Fill in the required fields:
   - **Name**: A unique identifier for the prompt (used by MCP clients to reference it)
   - **Display Text**: A human-readable name shown in the admin list
   - **Description**: Optional description that helps clients understand what the prompt does
4. Add one or more **Messages** to the prompt:
   - Each message has a **Role** (e.g., `system`, `user`) and **Content** (the message text)
   - Messages are returned in order when a client calls `GetPrompt`
5. Save the prompt

Prompts can also be registered programmatically in code or imported via recipes.

## Resource Support

MCP **Resources** represent data that MCP clients can read. A resource has a URI that the client uses to request its content. Resources come in two flavors:

- **Static Resources**: Have a fixed URI with no variable placeholders (e.g., `recipe-schema://abc123/recipe`). They return the same data every time and appear in `ListResources`.
- **Templated Resources**: Have a URI containing `{variable}` placeholders (e.g., `file://abc123/{fileName}`). The client fills in the variables when reading the resource. These appear in `ListResourceTemplates` and allow dynamic content resolution.

Resources can be:
- Created and managed via the admin UI under **Artificial Intelligence** → **MCP Resources**
- Registered programmatically in code
- Discovered and accessed by external MCP clients

### Managing Resources via Admin UI

1. Navigate to **Artificial Intelligence** → **MCP Resources**
2. Click **Add Resource** to create a new resource
3. Select a **Resource Type** (e.g., File, Content Item, Recipe Step Schema). Each type defines what kind of data the resource serves and which URI variables are available.
4. Fill in the required fields:
   - **Display Text**: A friendly name for the resource shown in the admin list
   - **Path**: The path portion of the URI. For templated resources, include variable placeholders from the supported variables list shown in the UI (e.g., `{fileName}`, `{contentType}`)
   - **Name**: The MCP resource name (used by clients to identify the resource)
   - **Title**: Optional human-readable title
   - **Description**: Optional description that helps clients understand the resource
   - **MIME Type**: The content type of the resource (e.g., `application/json`, `text/plain`)
5. Save the resource

The system automatically constructs the full URI by prepending the scheme and a unique resource ID to your path. For example, if you select the **File** resource type and enter `{fileName}` as the path, the full URI might be `file://abc123/{fileName}`.

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
