# CrestApps.OrchardCore.Samples.McpClient

This sample project is a small ASP.NET Core Razor Pages app that connects to the MCP server hosted by `CrestApps.OrchardCore.Cms.Web` when running with Aspire. It lists available MCP prompts and tools and lets you invoke `GetPrompt` or refresh the tool list from the UI.

## Run with Aspire

1. Start the Aspire AppHost (`CrestApps.Aspire.AppHost`).
2. Ensure the Orchard Core site is running on the `HttpsOrchardCore` endpoint.
3. Open the `McpClientSample` endpoint to view the prompts/tools UI.

## MCP Server Feature Requirement

If the MCP server feature is not enabled, MCP requests will return `404`. Enable the MCP server feature in the default tenant:

- Feature ID: `CrestApps.OrchardCore.AI.Mcp.Server`
- Admin UI: **Configuration → Features**

After enabling the feature, refresh the sample UI to see prompts and tools.

## Configuration

The MCP endpoint is configured via `Mcp:Endpoint`. Aspire sets it automatically, but you can override it in `appsettings.json`:

```json
{
  "Mcp": {
    "Endpoint": "https://localhost:5001/mcp/sse"
  }
}
```
