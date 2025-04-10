# Model Context Protocol (MCP) Server

The [Model Context Protocol (MCP)](https://modelcontextprotocol.io/introduction) is an open protocol that enables seamless integration between LLM applications and external data sources and tools. Whether you're building an AI-powered IDE, enhancing a chat interface, or creating custom AI workflows, MCP provides a standardized way to connect LLMs with the context they need.

## Defining MCP Capabilities
The **Model Context Protocol (MCP) Server** feature allows you to expose an MCP server that can be accessed by other MCP clients. These clients can interact with your server and take advantage of all the capabilities you define—making it easy to share, reuse, and integrate powerful AI-driven tools across distributed systems.

We use the official [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) to enable and configure the MCP server. For detailed guidance on defining server capabilities, MCP prompts, and MCP resources, please refer to the [MCP C# SDK documentation](https://github.com/modelcontextprotocol/csharp-sdk).

Below is a simple example of how to create an MCP capability that echoes a message back to the client.

### Step 1: Install the Required Package

Install the `CrestApps.OrchardCore.AI.Mcp.Core` NuGet package in the module where you want to define the server capabilities.

### Step 2: Define the Capability

Create a tool class and decorate it with the appropriate attributes to define the MCP capability:

```csharp
[McpServerToolType]
public class EchoTool
{
    [McpServerTool(Name = "echo"), Description("Echoes the message back to the client.")]
    public static string Echo(string message) => $"Echo: {message}";
}
```

### Step 3: Register the Capability in `Startup.cs`

Finally, register the tool in your module's `Startup` class:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddMcpServer()
            .WithTools<EchoTool>();
    }
}
```

This setup enables your MCP server to expose the `echo` capability, allowing any compatible MCP client to call it and receive a response.
