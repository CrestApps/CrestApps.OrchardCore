---
sidebar_label: Overview
sidebar_position: 1
title: Model Context Protocol (MCP)
description: Overview of MCP client and server support for integrating LLM applications with external tools and data sources.
---

# Model Context Protocol (MCP)

The [Model Context Protocol (MCP)](https://modelcontextprotocol.io/introduction) is an open standard that allows seamless integration between large language model (LLM) applications and external tools or data sources. Whether you're building an AI-enhanced IDE, a chat interface, or custom AI workflows, MCP makes it easy to supply LLMs with the context they need.

## Features Overview

CrestApps provides both **client** and **server** MCP support:

| Feature | Feature ID | Description |
|---------|-----------|-------------|
| [MCP Client Integration](client) | `CrestApps.OrchardCore.AI.Mcp` / `.Mcp.Stdio` | Connect to remote (SSE) or local (Stdio) MCP servers |
| [MCP Server](server) | `CrestApps.OrchardCore.AI.Mcp.Server` | Expose Orchard Core AI capabilities to external MCP clients |

## Supported Capabilities

The MCP implementation supports the following capabilities:

| Capability | Client | Server | Description |
|-----------|--------|--------|-------------|
| **Tools** | ✅ | ✅ | Discover and invoke AI tools |
| **Prompts** | ✅ | ✅ | List and invoke prompts. Can be managed via the admin UI. |
| **Resources** | ✅ | ✅ | Access various data sources. Can be managed via the admin UI. |
| **Templated Resources** | ✅ | ✅ | Resources with URI variable placeholders that resolve dynamically |

## Resource Type Modules

Additional resource type handlers are available as separate modules:

- **[FTP/FTPS](ftp)** — Access files on FTP servers
- **[SFTP](sftp)** — Access files via SSH/SFTP

## Explore More MCP Servers

Looking for more MCP-compatible tools? Explore these resources:

- [Docker Hub: MCP Images](https://hub.docker.com/search?q=mcp)
- [MCP.so](https://mcp.so/)
- [Glama.ai MCP Servers](https://glama.ai/mcp/servers)
- [MCPServers.org](https://mcpservers.org/)
