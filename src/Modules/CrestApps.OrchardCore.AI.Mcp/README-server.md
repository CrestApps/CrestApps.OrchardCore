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

The MCP server supports multiple authentication modes that can be configured via settings:

| Mode | Description | Use Case |
|------|-------------|----------|
| `OpenId` | OpenID Connect authentication via the "Api" scheme (default) | Production environments |
| `ApiKey` | Predefined API key authentication | Simple integrations, testing |
| `None` | No authentication required | Local development only |

## Configuration

Configure the MCP server authentication in your `appsettings.json`:

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "McpServer": {
        "AuthenticationType": "OpenId",
        "RequireAccessPermission": true
      }
    }
  }
}
```

### Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `AuthenticationType` | `string` | `OpenId` | Authentication type: `OpenId`, `ApiKey`, or `None` |
| `ApiKey` | `string` | `null` | API key for `ApiKey` authentication |
| `RequireAccessPermission` | `bool` | `true` | Whether to require the `AccessMcpServer` permission (OpenId only) |

### Authentication Types

#### OpenId (Default - Recommended for Production)

Uses OpenID Connect authentication via the "Api" authentication scheme. This is the most secure option and integrates with Orchard Core's existing OpenID Server feature.

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "McpServer": {
        "AuthenticationType": "OpenId",
        "RequireAccessPermission": true
      }
    }
  }
}
```

When `RequireAccessPermission` is `true`, users must also have the `AccessMcpServer` permission.

#### ApiKey (For Simple Integrations)

Uses a predefined API key for authentication. The client must provide the API key in the `Authorization` header.

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "McpServer": {
        "AuthenticationType": "ApiKey",
        "ApiKey": "your-secure-api-key-here"
      }
    }
  }
}
```

The API key can be provided in the `Authorization` header in any of these formats:
- `Bearer your-api-key`
- `ApiKey your-api-key`
- `your-api-key` (raw key)

#### None (For Local Development Only)

> ⚠️ **WARNING**: This option disables all authentication and should **NEVER** be used in production environments. Only use this for local development and testing.

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "McpServer": {
        "AuthenticationType": "None"
      }
    }
  }
}
```

## Getting Started

### 1. Enable the Feature

In the Orchard Core Admin, navigate to **Tools → Features** and enable **Model Context Protocol (MCP) Server**.

### 2. Configure Authentication

Choose an authentication method based on your environment:

**For Production (OpenId):**
- Enable the OpenID Server feature for token-based authentication
- Configure your OAuth client applications
- Grant the `AccessMcpServer` permission to appropriate users/roles

**For Simple Integrations (ApiKey):**
- Set a secure API key in your configuration
- Share the API key securely with authorized clients

**For Local Development (None):**
- Set `AuthenticationType` to `None`
- Remember to change this before deploying to production

### 3. Grant Permissions (OpenId mode only)

Assign the `AccessMcpServer` permission to users or roles that should be able to connect to the MCP server.

### 4. Connect from an MCP Client

Use the SSE (Server-Sent Events) endpoint to connect:

```
POST /mcp/sse
Authorization: Bearer <your-token-or-api-key>
```

## MCP Endpoint

The MCP server exposes a single SSE endpoint:

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/mcp/sse` | POST | SSE transport for MCP communication |

## Example: Connecting from an MCP Client

### Using OpenId Authentication

```json
{
  "mcpServers": {
    "orchard-core": {
      "transport": {
        "type": "sse",
        "url": "https://your-orchard-site.com/mcp/sse",
        "headers": {
          "Authorization": "Bearer <your-oauth-token>"
        }
      }
    }
  }
}
```

### Using API Key Authentication

```json
{
  "mcpServers": {
    "orchard-core": {
      "transport": {
        "type": "sse",
        "url": "https://your-orchard-site.com/mcp/sse",
        "headers": {
          "Authorization": "ApiKey <your-api-key>"
        }
      }
    }
  }
}
```

### Using No Authentication (Local Development)

```json
{
  "mcpServers": {
    "orchard-core": {
      "transport": {
        "type": "sse",
        "url": "http://localhost:5000/mcp/sse"
      }
    }
  }
}
```

## Security Considerations

- **Production**: Always use `OpenId` authentication in production environments
- **API Keys**: If using API key authentication, ensure the key is:
  - Long and randomly generated
  - Stored securely (not committed to source control)
  - Rotated periodically
- **Never use `None`**: The `None` authentication type should only be used for local development
- **Permission-Based Access**: When using OpenId, configure `RequireAccessPermission: true` for additional security
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
- Check that the appropriate authentication is configured
- For OpenId: Ensure API authentication is configured
- For ApiKey: Verify the API key matches the configured value
- For OpenId: Ensure the user has the `AccessMcpServer` permission (if `RequireAccessPermission` is enabled)

### Tools Not Appearing

- Verify the required features are enabled (e.g., OrchardCore.Contents for content tools)
- Check that AI tools are registered in the system

### Authentication Errors

**For OpenId:**
- Verify your OAuth token is valid and not expired
- Ensure you're using the correct authentication scheme
- Check the authorization header format: `Bearer <token>`

**For ApiKey:**
- Verify the API key matches exactly (case-sensitive)
- Ensure the `ApiKey` is configured in the server settings
- Check the authorization header format: `Bearer <key>`, `ApiKey <key>`, or just `<key>`

**For None:**
- Ensure you're connecting to the correct endpoint
- Check that no authentication headers are causing issues

### Configuration Not Taking Effect

- Ensure the configuration path is correct: `CrestApps_AI:McpServer`
- Restart the application after changing configuration
- Verify the JSON syntax is valid

## Related Features

- [AI Services](../CrestApps.OrchardCore.AI/README.md) - Core AI infrastructure
- [AI Agent](../CrestApps.OrchardCore.AI.Agent/README.md) - AI tools for Orchard Core management
- [MCP Client](../CrestApps.OrchardCore.AI.Mcp/README.md) - Connect to external MCP servers
