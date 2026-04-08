---
sidebar_label: Overview
sidebar_position: 1
title: Model Context Protocol (MCP)
description: MCP client and server support for connecting to remote tool servers and exposing your application's tools.
---

:::info Canonical framework docs
The shared framework guidance now lives in **[CrestApps.Core](https://core.crestapps.com/docs/framework/mcp/index)**. This Orchard Core page is kept for Orchard-specific integration context and cross-links.
:::

# Model Context Protocol (MCP)

> Connect to remote MCP servers as a client and expose your application's tools, prompts, and resources as an MCP server.

The [Model Context Protocol](https://modelcontextprotocol.io/) standardizes how AI applications discover and invoke tools, prompts, and resources across process boundaries. The framework provides both sides of the protocol.

## Client — Consume Remote MCP Servers

Connect to external MCP servers, discover their tools, and make them available in the AI orchestrator.

```csharp
builder.Services
    .AddCrestAppsAI()
    .AddOrchestrationServices()
    .AddCrestAppsMcpClient();
```

**Key capabilities:**

- **SSE transport** — Connect to remote MCP servers over HTTP with full authentication support (API key, Basic, OAuth2, custom headers)
- **StdIO transport** — Communicate with locally installed MCP server processes via stdin/stdout
- **Automatic tool discovery** — Discovered tools appear in the orchestrator's tool registry and are invoked transparently
- **Capability resolution** — Semantic similarity filtering to select relevant tools from large MCP server catalogs

📖 **[MCP Client →](./client.md)** — Full documentation with transport configuration, authentication, and integration details.

## Server — Expose Your AI Capabilities

Expose your registered AI tools, prompts, and resources to external MCP clients.

```csharp
builder.Services
    .AddCrestAppsAI()
    .AddOrchestrationServices()
    .AddCrestAppsMcpServer();
```

**Key capabilities:**

- **Tool exposure** — Registered AI tools become callable by external MCP clients
- **Prompt serving** — Prompts from the catalog and code-registered instances are listed and retrievable
- **Resource serving** — Files and data served through pluggable resource type handlers (FTP, SFTP, custom)
- **Authentication** — OpenID Connect, API key, or no-auth for development

📖 **[MCP Server →](./server.md)** — Full documentation with endpoint setup, authentication, and resource configuration.

## Resource Type Handlers

Create custom resource type handlers to serve files, data, or content from any protocol as MCP resources.

```csharp
builder.Services
    .AddCrestAppsMcpServer()
    .AddMcpResourceType<MyDatabaseResourceHandler>("database");
```

📖 **[Resource Types →](./resource-types.md)** — Implementation guide with built-in handlers and custom handler examples.

## Orchard Core Integration

- [MCP Client module](../../ai/mcp/client.md) — Admin UI for managing MCP server connections
- [MCP Server module](../../ai/mcp/server.md) — Admin UI for MCP server configuration, prompts, and resources
