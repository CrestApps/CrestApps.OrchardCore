# Model Context Protocol (MCP)

The [Model Context Protocol (MCP)](https://modelcontextprotocol.io/introduction) is an open protocol that enables seamless integration between LLM applications and external data sources and tools. Whether you're building an AI-powered IDE, enhancing a chat interface, or creating custom AI workflows, MCP provides a standardized way to connect LLMs with the context they need.

## Features 

### Model Context Protocol (Local MCP) Client Feature

The **Model Context Protocol (Local MCP) Client** allows your application to connect to locally running MCP servers, enabling seamless integration with AI profiles.

#### Example Use Case: Time Zone AI Model

Let's say you want to equip your AI model with the ability to provide time zone information for any location in the world. The `mcp/time` Docker image is built for this purpose.

Follow the steps below to set up the MCP server locally and connect to it using a Windows PC.

---

#### Step 1: Install Docker Desktop

Download and install [Docker Desktop](https://www.docker.com/products/docker-desktop/). Once installed, launch Docker Desktop.

---

#### Step 2: Pull the MCP Docker Image

1. In Docker Desktop, navigate to the **Docker Hub** tab.
2. Search for `mcp/time`.
3. Click on the image and then click the **Pull** button to install the Docker container locally.

---

#### Step 3: Connect the Docker Image to Orchard Core

1. Open your Orchard Core project.
2. Go to **Artificial Intelligence** → **MCP Connections**.
3. Click the **Add Connection** button.
4. Under the **Standard Input/Output** source, click **Add**.

Now fill in the connection details:

- **Display text**: A user-friendly name, e.g., `Global Time Capabilities`.
- **Command**: `docker`
- **Command arguments**:  
  ```json
  ["run", "-i", "--rm", "mcp/time"]
  ```

> These arguments come from the usage instructions on the [Docker Hub page for `mcp/time`](https://hub.docker.com/r/mcp/time).

---

#### Step 4: Create an AI Profile

Create a new AI Profile and select the **Global Time Capabilities** connection you just configured.  
For detailed instructions, [click here to learn how to create an AI Profile](../CrestApps.OrchardCore.AI/README.md#creating-ai-profiles).

---

#### 📦 Explore More MCP Servers (For Reference)

- Discover additional MCP server Docker containers on the [MCP section of Docker Hub](https://hub.docker.com/search?q=mcp).
- Explore MCP servers on [MCP.so](https://mcp.so/).
- Check out [Glama.ai's MCP Servers page](https://glama.ai/mcp/servers) for further options.
- Visit [MCPServers.org](https://mcpservers.org/) for more resources.

---

### Recipes

You can add or update an MCP connection using **recipes**. Recipes define the necessary configuration steps to register and connect to an MCP-compatible service.

#### Example 1: Connect to a locally running MCP server (SSE Transport)

This recipe creates or updates an MCP connection that uses the **Server-Sent Events (SSE)** transport to connect to a server running at `https://localhost:1234`:

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

#### Example 2: Connect to a local Docker container (Standard I/O Transport)

This recipe uses the **Standard Input/Output (Stdio)** transport to run a local Docker container with the `mcp/time` image:

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
