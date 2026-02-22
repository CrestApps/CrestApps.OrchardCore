---
sidebar_label: Overview
sidebar_position: 1
title: Model Context Protocol (MCP)
description: MCP client and server support for integrating LLM applications with external tools and data sources.
---

| | |
| --- | --- |
| **Feature Name** | Model Context Protocol (MCP) Client |
| **Feature ID** | `CrestApps.OrchardCore.AI.Mcp` |

Offers core services and a user interface for connecting to Model Context Protocol (MCP) servers, enabling AI models to leverage additional capabilities and resources.

## Model Context Protocol (MCP)

The [Model Context Protocol (MCP)](https://modelcontextprotocol.io/introduction) is an open standard that allows seamless integration between large language model (LLM) applications and external tools or data sources. Whether you're building an AI-enhanced IDE, a chat interface, or custom AI workflows, MCP makes it easy to supply LLMs with the context they need.

---

## Features

### Model Context Protocol (MCP) Client Feature

| | |
| --- | --- |
| **Feature Name** | Model Context Protocol (MCP) Client |
| **Feature ID** | `CrestApps.OrchardCore.AI.Mcp` |

Offers core services and a user interface for connecting to Model Context Protocol (MCP) servers, enabling AI models to leverage additional capabilities and resources.

The **Model Context Protocol (MCP) Client Feature** enables your application to connect to remote MCP servers using standard HTTP requests. One of the supported transport types is **Server-Sent Events (SSE)**, which allows real-time data flow between LLMs and external services.

---

#### 🛠 Connect to a Remote MCP Server (SSE Transport)

To connect your application to a remote MCP server using SSE:

1. Open your Orchard Core project.
2. Navigate to **Artificial Intelligence** → **MCP Connections**.
3. Click the **Add Connection** button.
4. Under the **Server Sent Events (SSE)** source, click **Add**.
5. Enter the following connection details:
   - **Display Text**: `Remote AI Time Server`
   - **Endpoint**: `https://localhost:1234/`
   - **Additional Headers**: Leave empty or supply any required headers.
6. Save the connection.

#### ➕ Create an AI Profile

Now that the connection is added, you can create an AI profile that uses this connection:

👉 [Learn how to create an AI Profile](../ai-services#creating-ai-profiles)

##### 📄 Alternative: Recipe-Based Setup (SSE)

You can also configure the SSE connection programmatically using a recipe:

```json
{
  "steps": [
    {
      "name": "McpConnection",
      "connections": [
        {
          "DisplayText": "Example server",
          "Properties": {
            "SseMcpConnectionMetadata": {
              "Endpoint": "https://localhost:1234/",
              "AdditionalHeaders": {}
            }
          }
        }
      ]
    }
  ]
}
```

---

### Model Context Protocol (Local MCP) Client Feature

| | |
| --- | --- |
| **Feature Name** | Model Context Protocol (MCP) Local Client |
| **Feature ID** | `CrestApps.OrchardCore.AI.Mcp.Stdio` |

Extends the Model Context Protocol Client with standard input/output (STDIO) transport for connecting to local MCP servers.

The **Local MCP Client Feature** allows your application to connect to MCP servers running locally, typically in containers. It uses **Standard Input/Output (Stdio)** for communication — ideal for offline tools or running local services.

#### 🌐 Example Use Case: Global Time Capabilities with `mcp/time`

Let's equip your AI model with time zone intelligence using the [`mcp/time`](https://hub.docker.com/r/mcp/time) Docker image.

### 🧭 Step-by-Step: Connect to a Local MCP Server (Stdio Transport)

#### Step 1: Install Docker Desktop

Download and install [Docker Desktop](https://www.docker.com/products/docker-desktop), then launch the app.

#### Step 2: Pull the MCP Docker Image

1. Open Docker Desktop.
2. Search for `mcp/time` in the **Docker Hub** tab.
3. Click on the image and hit **Pull**.

#### Step 3: Add the Connection via Orchard Core

1. Open your Orchard Core project.
2. Navigate to **Artificial Intelligence** → **MCP Connections**.
3. Click the **Add Connection** button.
4. Under the **Standard Input/Output (Stdio)** source, click **Add**.
5. Enter the following connection details:
   - **Display Text**: `Global Time Capabilities`
   - **Command**: `docker`
   - **Command Arguments**:
     ```json
     ["run", "-i", "--rm", "mcp/time"]
     ```

💡 These arguments are based on the official usage from the [`mcp/time` Docker Hub page](https://hub.docker.com/r/mcp/time).

#### ➕ Create an AI Profile

Now that the connection is added, you can create an AI profile that uses it:

👉 [Learn how to create an AI Profile](../ai-services#creating-ai-profiles)

##### 📄 Alternative: Recipe-Based Setup (Stdio)

Prefer configuration through code? Here's how to define the same connection using a recipe:

```json
{
  "steps": [
    {
      "name": "McpConnection",
      "connections": [
        {
          "DisplayText": "Global Time Capabilities",
          "Properties": {
            "StdioMcpConnectionMetadata": {
              "Command": "docker",
              "Arguments": [
                "run",
                "-i",
                "--rm",
                "mcp/time"
              ]
            }
          }
        }
      ]
    }
  ]
}
```

---

### Model Context Protocol (MCP) Server

| | |
| --- | --- |
| **Feature Name** | Model Context Protocol (MCP) Server |
| **Feature ID** | `CrestApps.OrchardCore.AI.Mcp.Server` |

Exposes Orchard Core AI tools through the MCP protocol, enabling external MCP-compatible clients to connect and invoke AI capabilities.

The **Model Context Protocol (MCP) Server Feature** allows your Orchard Core application to expose its AI tools and capabilities to external MCP clients. This feature supports the SSE transport type, enabling real-time communication.

#### Prompt Support

The MCP server also exposes MCP prompts registered in Orchard Core, so clients can list and invoke prompts via `ListPrompts` and `GetPrompt`.

#### Resource Support

The MCP server exposes MCP resources registered in Orchard Core, allowing clients to access various data sources through the MCP protocol. See [MCP Resources](#mcp-resources) for details.

---

## MCP Resources

MCP Resources allow you to expose various data sources through the MCP protocol. Resources are type-based, with each type having its own handler that knows how to read and process the resource content.

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

When creating a resource in the admin UI, you only provide the **path** portion. The system automatically prepends the scheme and resource ID. For example:

- Path: `{providerName}/{fileName}` → Full URI: `file://abc123/{providerName}/{fileName}`
- Path: `steps/{stepName}` → Full URI: `recipe-step-schema://def456/steps/{stepName}`
- Path: *(empty)* → Full URI: `recipe-schema://ghi789`

The MCP server uses **URI template routing** to match incoming requests. It parses the scheme and resource ID from the URI for direct lookup, then extracts variable values from the path. The last variable in a path can match multi-segment values (e.g., `{fileName}` matches `documents/report.pdf`).

### Creating Resources via Admin UI

1. Navigate to **Artificial Intelligence** → **MCP Resources**
2. Click **Add Resource**
3. Select a resource type (e.g., File, Content Item, Recipe Step Schema)
4. Fill in the required fields:
   - **Display Text**: A friendly name for the resource
   - **Path**: The path portion of the URI, using any supported variables shown in the UI (e.g., `{providerName}/{fileName}`). Leave empty for resource types with no variables.
   - **Name**: The MCP resource name (used by clients)
   - **Title**: Optional human-readable title
   - **Description**: Optional description
   - **MIME Type**: Content type of the resource
5. Configure type-specific settings if needed
6. Save the resource

The admin UI displays a **URI preview** showing the full constructed URI, and lists the **supported variables** for the selected resource type so you know which placeholders to include in your path.

### Registering Custom Resource Types

You can register custom resource types with their handlers. Each handler should handle **one purpose only** and declare its supported variables:

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

        // Query database using table and id
        // Return ReadResourceResult with content
    }
}
```

The `variables` dictionary contains values extracted from the URI pattern match. For example, if the user defined the path `{table}/{id}` and a client requests `database://abc123/users/42`, the handler receives `{ "table": "users", "id": "42" }`.

### Resource Type Modules

Additional resource type handlers are available as separate modules:

- **[FTP/FTPS](ftp)** - Access files on FTP servers
- **[SFTP](sftp)** - Access files via SSH/SFTP

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

---

### Admin Chat UI with Time MCP Server Integration (MCP Demonstration)

![Screen cast of the admin chat](/img/docs/mcp-integration.gif)

---

## 🔍 Explore More MCP Servers

Looking for more MCP-compatible tools? Explore these resources:

- [Docker Hub: MCP Images](https://hub.docker.com/search?q=mcp)
- [MCP.so](https://mcp.so/)
- [Glama.ai MCP Servers](https://glama.ai/mcp/servers)
- [MCPServers.org](https://mcpservers.org/)
