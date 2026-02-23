---
sidebar_label: MCP Client Sample
sidebar_position: 1
title: MCP Client Sample
description: Sample ASP.NET Core Razor Pages app that connects to the MCP server hosted by CrestApps Orchard Core.
---

This sample project is a small ASP.NET Core Razor Pages app that connects to the MCP server hosted by `CrestApps.OrchardCore.Cms.Web` when running with Aspire. It provides a tabbed UI to explore MCP capabilities:

- **Home** – Prerequisites and setup instructions.
- **Tools** – List all MCP tools and invoke them with custom JSON arguments.
- **Prompts** – List all MCP prompts and retrieve prompt details.
- **Resources** – List all MCP resources and read their content by URI.

:::tip

**Templated Resources** are also supported. These are dynamic MCP resources defined with URI templates (e.g., `content://items/{contentItemId}`). The MCP server exposes them as resource templates that clients can discover and resolve by supplying the appropriate parameters.

:::

## Run with Aspire

The easiest way to run this sample is using the **Aspire AppHost** project:

1. Make sure [Docker Desktop](https://www.docker.com/products/docker-desktop) is installed and running locally.
2. Set `CrestApps.Aspire.AppHost` as your startup project.
3. Run the project. The Aspire dashboard will open in your browser.
4. From the Aspire dashboard, click on the endpoint for the resource you want to access:
   - **orchardcore** — Opens the Orchard Core CMS application (on port 5001).
   - **mcpclientsample** — Opens the MCP Client sample UI (on port 5002).

:::note

The Aspire AppHost orchestrates several services including **Ollama** (AI model host), **Elasticsearch**, **Redis**, and both the Orchard Core app and MCP Client sample. All service dependencies and environment variables are configured automatically.

:::

## MCP Server Feature Requirement

If the MCP server feature is not enabled, MCP requests will return `404`. Enable the MCP server feature in the default tenant:

- Feature ID: `CrestApps.OrchardCore.AI.Mcp.Server`
- Admin UI: **Tools → Features**

After enabling the feature, refresh the sample UI to see tools, prompts, and resources.

## Configuration

The MCP endpoint is configured via `Mcp:Endpoint`. Aspire sets it automatically, but you can override it in `appsettings.json`:

```json
{
  "Mcp": {
    "Endpoint": "https://localhost:5001/mcp/sse"
  }
}
```
