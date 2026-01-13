# Model Context Protocol (MCP) Server

The **Model Context Protocol (MCP) Server** feature enables Orchard Core to function as an MCP server, exposing its AI tools and capabilities to external MCP-compatible clients such as AI agents, IDEs, copilots, and automation tools.

## Overview

The [Model Context Protocol (MCP)](https://modelcontextprotocol.io/introduction) is an open standard that allows seamless integration between large language model (LLM) applications and external tools or data sources. This module implements the server side of the protocol, allowing external clients to:

- **Discover** available AI tools registered in Orchard Core
- **Invoke** those tools through the MCP protocol
- **Access** Orchard Core's AI capabilities remotely

## Architecture

```
External MCP Client (AI Agent, IDE, Copilot)
        ↓
   MCP Server (this module)
        ↓
   Orchard Core Tenant
        ↓
AI Models / Tools / Workflows / Content
```

The MCP server acts as a thin adapter layer that:
- Translates MCP protocol requests into Orchard Core API calls
- Preserves Orchard Core as the source of truth and permission authority
- Does not duplicate business logic or store AI-related state

## Features

### Tool Discovery

External clients can list all available AI tools through the MCP protocol. Tools are dynamically discovered from:

- **Registered AI Tools**: Tools added via `services.AddAITool<T>()` and other AI tool registrations exposed through `AIToolDefinitionOptions`

### Tool Invocation

Clients can invoke any discovered tool using the standard MCP tool invocation protocol. The server:

- Validates the request
- Executes the tool through Orchard Core's existing infrastructure
- Returns the result in MCP format

### Authentication & Authorization

The MCP server integrates with Orchard Core's authentication system:

- Requires API authentication (Bearer token)
- Respects the `AccessMcpServer` permission
- All tool invocations run in the context of the authenticated user

## Getting Started

### 1. Enable the Feature

In the Orchard Core Admin, navigate to **Configuration → Features** and enable **Model Context Protocol (MCP) Server**.

### 2. Configure Authentication

Ensure your Orchard Core instance has API authentication configured. You can use:

- **OpenID Connect**: Enable the OpenID Server feature for token-based authentication
- **API Keys**: Configure API key authentication for simpler setups

### 3. Grant Permissions

Assign the `AccessMcpServer` permission to users or roles that should be able to connect to the MCP server.

### 4. Connect from an MCP Client

Use the SSE (Server-Sent Events) endpoint to connect:

```
POST /mcp/sse
Authorization: Bearer <your-token>
```

## MCP Endpoint

The MCP server exposes a single SSE endpoint:

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/mcp/sse` | POST | SSE transport for MCP communication |

## Example: Connecting from an MCP Client

Here's an example of connecting to the Orchard Core MCP server:

```json
{
  "mcpServers": {
    "orchard-core": {
      "transport": {
        "type": "sse",
        "url": "https://your-orchard-site.com/mcp/sse",
        "headers": {
          "Authorization": "Bearer <your-token>"
        }
      }
    }
  }
}
```

## Security Considerations

- **Authentication Required**: All MCP requests must include valid API authentication
- **Permission-Based Access**: Users must have the `AccessMcpServer` permission
- **Tool Permissions**: Individual tool invocations respect Orchard Core's permission system
- **Tenant Isolation**: MCP server operates within the context of a single tenant

## Exposed Tools

The MCP server automatically exposes all AI tools available in Orchard Core, including:

### System Tools
- Time zone listing
- System configuration

### Content Management Tools (when OrchardCore.Contents is enabled)
- Search content items
- Get, create, update, delete content
- Publish/unpublish content

### Feature Management Tools (when OrchardCore.Features is enabled)
- List, enable, disable features

### User Management Tools (when OrchardCore.Users is enabled)
- Search users
- Get user information

### And many more...

The available tools depend on which Orchard Core features and CrestApps modules are enabled in your tenant.

## Integration with AI Agent Module

The **Orchard Core AI Agent** module provides a comprehensive set of AI tools that are automatically exposed through the MCP server. Enable the AI Agent feature to access tools for:

- Content management
- Tenant management
- Feature management
- User and role management
- Workflow management
- And more

## Troubleshooting

### Connection Refused

- Verify the MCP Server feature is enabled
- Check that API authentication is configured
- Ensure the user has the `AccessMcpServer` permission

### Tools Not Appearing

- Verify the required features are enabled (e.g., OrchardCore.Contents for content tools)
- Check that AI tools are registered in the system

### Authentication Errors

- Verify your token is valid and not expired
- Ensure you're using the correct authentication scheme
- Check the authorization header format: `Bearer <token>`

## Related Features

- [AI Services](../CrestApps.OrchardCore.AI/README.md) - Core AI infrastructure
- [AI Agent](../CrestApps.OrchardCore.AI.Agent/README.md) - AI tools for Orchard Core management
- [MCP Client](../CrestApps.OrchardCore.AI.Mcp/README.md) - Connect to external MCP servers
