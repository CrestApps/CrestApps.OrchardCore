---
sidebar_label: MCP Client Integration
sidebar_position: 2
title: MCP Client Integration
description: Connect to remote and local MCP servers using SSE or Stdio transports.
---

# MCP Client Integration

The MCP Client features allow your Orchard Core application to connect to external MCP servers, enabling AI models to leverage additional tools and resources provided by those servers.

Two transport types are supported:

| Transport | Feature ID | Description |
|---|---|---|
| **Server-Sent Events (SSE)** | `CrestApps.OrchardCore.AI.Mcp` | Connect to remote MCP servers over HTTP. |
| **Standard Input/Output (Stdio)** | `CrestApps.OrchardCore.AI.Mcp.Stdio` | Connect to local MCP servers (e.g., Docker containers). |

---

## SSE Transport (Remote Servers)

| | |
| --- | --- |
| **Feature Name** | Model Context Protocol (MCP) Client |
| **Feature ID** | `CrestApps.OrchardCore.AI.Mcp` |

The **MCP Client Feature** enables your application to connect to remote MCP servers using standard HTTP requests with **Server-Sent Events (SSE)** transport, which allows real-time data flow between LLMs and external services.

### Connect to a Remote MCP Server

1. Open your Orchard Core project.
2. Navigate to **Artificial Intelligence** → **MCP Connections**.
3. Click the **Add Connection** button.
4. Under the **Server Sent Events (SSE)** source, click **Add**.
5. Enter the following connection details:
   - **Display Text**: `Remote AI Time Server`
   - **Endpoint**: `https://localhost:1234/`
   - **Additional Headers**: Leave empty or supply any required headers.
6. Save the connection.

### SSE Recipe-Based Setup

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

## Stdio Transport (Local Servers)

| | |
| --- | --- |
| **Feature Name** | Model Context Protocol (MCP) Local Client |
| **Feature ID** | `CrestApps.OrchardCore.AI.Mcp.Stdio` |

The **Local MCP Client Feature** allows your application to connect to MCP servers running locally, typically in containers. It uses **Standard Input/Output (Stdio)** for communication — ideal for offline tools or running local services.

### Example: Global Time Capabilities with `mcp/time`

Let's equip your AI model with time zone intelligence using the [`mcp/time`](https://hub.docker.com/r/mcp/time) Docker image.

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

### Stdio Recipe-Based Setup

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

## Create an AI Profile

After adding an MCP connection (SSE or Stdio), create an AI profile that uses it:

👉 [Learn how to create an AI Profile](../overview#creating-ai-profiles)
