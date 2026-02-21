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

## Run with Aspire

1. Start the Aspire AppHost (`CrestApps.Aspire.AppHost`).
2. Ensure the Orchard Core site is running on the `HttpsOrchardCore` endpoint.
3. Open the `McpClientSample` endpoint to view the UI.

## MCP Server Feature Requirement

If the MCP server feature is not enabled, MCP requests will return `404`. Enable the MCP server feature in the default tenant:

- Feature ID: `CrestApps.OrchardCore.AI.Mcp.Server`
- Admin UI: **Configuration → Features**

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
