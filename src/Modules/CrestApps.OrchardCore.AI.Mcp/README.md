## Table of Contents

- [Model Context Protocol (MCP)](#model-context-protocol-mcp)
- [Features](#features)
  - [Model Context Protocol (MCP) Client Feature](#model-context-protocol-mcp-client-feature)
  - [Model Context Protocol (Local MCP) Client Feature](#model-context-protocol-local-mcp-client-feature)
  - [Admin Chat UI with Time MCP Server Integration](#admin-chat-ui-with-time-mcp-server-integration-mcp-demonstration)
- [Explore More MCP Servers](#explore-more-mcp-servers)

# Model Context Protocol (MCP)

The [Model Context Protocol (MCP)](https://modelcontextprotocol.io/introduction) is an open standard that allows seamless integration between large language model (LLM) applications and external tools or data sources. Whether you're building an AI-enhanced IDE, a chat interface, or custom AI workflows, MCP makes it easy to supply LLMs with the context they need.

---

## Features

### Model Context Protocol (MCP) Client Feature

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

👉 [Learn how to create an AI Profile](../CrestApps.OrchardCore.AI/README.md#creating-ai-profiles)

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

👉 [Learn how to create an AI Profile](../CrestApps.OrchardCore.AI/README.md#creating-ai-profiles)

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

The **Model Context Protocol (MCP) Server Feature** allows your Orchard Core application to expose its AI tools and capabilities to external MCP clients. This feature supports the SSE transport type, enabling real-time communication. For more information, refer to the [MCP Server Documentation](./README-server.md).

#### Prompt Support

The MCP server also exposes MCP prompts registered in Orchard Core, so clients can list and invoke prompts via `ListPrompts` and `GetPrompt`.

#### Resource Support

The MCP server exposes MCP resources registered in Orchard Core, allowing clients to access various data sources through the MCP protocol. See [MCP Resources](#mcp-resources) for details.

---

## MCP Resources

MCP Resources allow you to expose various data sources through the MCP protocol. Resources are type-based, with each type having its own handler that knows how to read and process the resource content.

### Built-in Resource Types

| Type | URI Pattern | Description |
|------|-------------|-------------|
| **File** | `file://{itemId}/{path}` | Local file system access |
| **Content** | `content://{itemId}/...` | Orchard Core content items |
| **Recipe Schema** | `recipe-schema://{itemId}/...` | JSON schema definitions |
| **FTP/FTPS** | `ftp://{itemId}/{path}` | Remote files via FTP (separate module) |
| **SFTP** | `sftp://{itemId}/{path}` | Remote files via SSH (separate module) |

### Creating Resources via Admin UI

1. Navigate to **Artificial Intelligence** → **MCP Resources**
2. Click **Add Resource**
3. Select a resource type (e.g., File, Content, FTP)
4. Fill in the required fields:
   - **Display Text**: A friendly name for the resource
   - **URI**: The resource URI following the type's pattern
   - **Name**: The MCP resource name (used by clients)
   - **Title**: Optional human-readable title
   - **Description**: Optional description
   - **MIME Type**: Content type of the resource
5. Configure type-specific settings (e.g., FTP connection details)
6. Save the resource

### Content Resource Strategies

The Content resource type supports multiple URI patterns through the strategy provider pattern:

| Pattern | Description |
|---------|-------------|
| `content://{itemId}/id/{contentItemId}` | Get a specific content item by ID |
| `content://{itemId}/{contentType}/list` | List all content items of a type |
| `content://{itemId}/{contentType}/{contentItemId}` | Get content item by type and ID |

#### Extending Content Resources

You can add custom content resource strategies by implementing `IContentResourceStrategyProvider`:

```csharp
public class SearchContentResourceStrategy : IContentResourceStrategyProvider
{
    public string[] UriPatterns => ["content://{itemId}/{contentType}/search"];
    
    public bool CanHandle(Uri uri)
    {
        // Check if URI matches your pattern
        return uri.Segments.Length >= 4 && 
               uri.Segments[^1].TrimEnd('/') == "search";
    }
    
    public async Task<ReadResourceResult> ReadAsync(
        McpResource resource, 
        Uri uri, 
        CancellationToken cancellationToken)
    {
        // Implement your search logic
    }
}
```

Register your strategy in `Startup.cs`:

```csharp
services.AddContentResourceStrategy<SearchContentResourceStrategy>();
```

The strategy's URI patterns are automatically added to the Content resource type's displayed patterns in the UI.

### Registering Custom Resource Types

You can register custom resource types with their handlers:

```csharp
services.AddMcpResourceType<DatabaseResourceTypeHandler>("database", entry =>
{
    entry.DisplayName = S["Database"];
    entry.Description = S["Query data from databases."];
    entry.UriPatterns = ["db://{itemId}/{table}/{id}"];
});
```

Implement the handler:

```csharp
public class DatabaseResourceTypeHandler : IMcpResourceTypeHandler
{
    public string Type => "database";
    
    public async Task<ReadResourceResult> ReadAsync(
        McpResource resource, 
        CancellationToken cancellationToken)
    {
        var uri = new Uri(resource.Resource.Uri);
        // Parse URI and query database
        // Return ReadResourceResult with content
    }
}
```

### Resource Type Modules

Additional resource type handlers are available as separate modules:

- **[FTP/FTPS](../CrestApps.OrchardCore.AI.Mcp.Resources.Ftp/README.md)** - Access files on FTP servers
- **[SFTP](../CrestApps.OrchardCore.AI.Mcp.Resources.Sftp/README.md)** - Access files via SSH/SFTP

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
            "Uri": "file://abc123/etc/config.json",
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

![Screen cast of the admin chat](../../../docs/images/mcp-integration.gif)

---

## 🔍 Explore More MCP Servers

Looking for more MCP-compatible tools? Explore these resources:

- [Docker Hub: MCP Images](https://hub.docker.com/search?q=mcp)
- [MCP.so](https://mcp.so/)
- [Glama.ai MCP Servers](https://glama.ai/mcp/servers)
- [MCPServers.org](https://mcpservers.org/)
