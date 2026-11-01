# CrestApps.OrchardCore.AI.WebMcp

Connects Orchard Core to [WebMCP](https://github.com/webmachinelearning/webmcp), the proposed web standard that lets a page give tools to an AI agent that runs in the browser.

## Features

| Feature | ID | What it adds |
| --- | --- | --- |
| WebMCP Tools | `CrestApps.OrchardCore.AI.WebMcp` | Gives the AI tools and tool instances selected on the **WebMCP** settings card to browser agents, on the pages a signed-in user with the **Use WebMCP tools in the browser** permission opens. |
| WebMCP Page Tools | `CrestApps.OrchardCore.AI.WebMcp.PageTools` | Adds browser-only tools that read the page, list its links, open another page of the site, and read and fill its forms without submitting them. |
| WebMCP Chat Bridge | `CrestApps.OrchardCore.AI.WebMcp.Chat` | Lets the AI chat call the page tools of the tab the user has open, for AI profiles with **Allow page tools** on. |

## Installation

Reference the `CrestApps.OrchardCore.AI.WebMcp` package (it is included in `CrestApps.OrchardCore.Cms.Core.Targets`), then enable the features in **Tools > Features**. WebMCP Tools depends on the AI Services feature; the Chat Bridge also depends on AI Chat.

## Configuration

- **Settings > Artificial Intelligence**, **WebMCP** card: the page kinds that offer tools (admin pages on, front-end pages off by default) and the tools browser agents may call (none by default).
- **AI profile editor**, **Capabilities** tab: **Allow page tools** (off by default).
- The `CrestApps:AI:WebMcp` shell configuration section overrides the stored settings (`ExposeAllTools`, `Tools`, `EnableOnAdminPages`, `EnableOnFrontEndPages`), and `CrestApps:AI:WebMcp:Chat:ToolCallTimeout` (default one minute) sets how long the chat waits for a page tool.

## Usage

Register a page tool from a theme or module script that loads after the `CrestAppsWebMcp` resource:

```javascript
window.crestAppsWebMcp.registerTool({
    name: 'contoso_find_store',
    description: 'Finds the Contoso store nearest to a postal code.',
    inputSchema: { type: 'object', properties: { postalCode: { type: 'string' } } },
    execute: async ({ postalCode }) => ({ content: [{ type: 'text', text: await findStore(postalCode) }] }),
});
```

Add scripts and settings to every page that offers tools with `IWebMcpClientConfigurationHandler`.

## Dependencies

- `CrestApps.OrchardCore.AI` (the tool catalog, tool instances and the **Access AI tool** permission)
- `CrestApps.OrchardCore.AI.Chat` (for the Chat Bridge)
- Orchard Core SignalR (for the Chat Bridge)

## Documentation

- Technical Manual: `src/CrestApps.Docs/docs/ai/mcp/webmcp.md`
- User Manual: `src/CrestApps.Docs/docs/user-manual/ai/webmcp.md`
- Plan: `docs/engineering/ai/webmcp-plan.md`
