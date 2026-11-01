# CrestApps.OrchardCore.AI.WebMcp

Connects Orchard Core to [WebMCP](https://webmachinelearning.github.io/webmcp/), the proposed W3C Community Group standard that lets a page give tools to an AI agent in the browser through `document.modelContext`. It is a thin Orchard Core layer over CrestApps.Core's `CrestApps.Core.AI.WebMcp` package and `@crestapps/webmcp` scripts, and uses only the standard API.

## Features

| Feature | ID | What it adds |
| --- | --- | --- |
| WebMCP Tools | `CrestApps.OrchardCore.AI.WebMcp` | Registers the AI tools and tool instances selected on the **WebMCP** settings card with `document.modelContext`, on the pages a signed-in user with the **Use WebMCP tools in the browser** permission opens. Adds the W3C reference polyfill for browsers without native WebMCP. |
| WebMCP Page Tools | `CrestApps.OrchardCore.AI.WebMcp.PageTools` | Adds tools that read the page, list its links, open another page of the site, and read and fill its forms without submitting them. |
| WebMCP Chat Bridge | `CrestApps.OrchardCore.AI.WebMcp.Chat` | Lets the AI chat discover and run every WebMCP tool the page the user has open exposes, through the AI chat hub, for AI profiles with **Allow page tools** on. |

## Installation

Reference the `CrestApps.OrchardCore.AI.WebMcp` package (it is included in `CrestApps.OrchardCore.Cms.Core.Targets`), then enable the features in **Tools > Features**. WebMCP Tools depends on the AI Services feature; the Chat Bridge also depends on AI Chat.

## Configuration

- **Settings > Artificial Intelligence**, **WebMCP** card: the page kinds that get the scripts (admin pages on, front-end pages off by default) and the tools browser agents may call (none by default). Saving it releases the tenant.
- **AI profile editor**, **Capabilities** tab: **Allow page tools** and **Allow consequential page tools** (both off by default).
- The `CrestApps:AI:WebMcp` shell configuration section overrides the stored settings (`ExposeAllTools`, `Tools`, `EnableOnAdminPages`, `EnableOnFrontEndPages`) and sets `PageToolOrigins`, and `CrestApps:AI:WebMcp:Chat:ToolCallTimeout` (default one minute) sets how long the chat waits for a page tool.

## Usage

Register a tool from a theme or module script whose resource depends on `CrestAppsWebMcpPolyfill`, with the standard API:

```javascript
document.modelContext.registerTool({
    name: 'contoso.find_store',
    description: 'Finds the Contoso store nearest to a postal code.',
    inputSchema: { type: 'object', properties: { postalCode: { type: 'string' } } },
    annotations: { readOnlyHint: true },
    execute: async ({ postalCode }) => findStore(postalCode),
});
```

Add scripts and settings to every page that gets the WebMCP scripts with `IWebMcpClientConfigurationHandler`.

## Dependencies

- `CrestApps.Core.AI.WebMcp` (the tool service, the endpoints and the page tools in the chat)
- `@crestapps/webmcp` (the browser scripts and the W3C reference polyfill, copied into `wwwroot/scripts` by the asset build)
- `CrestApps.OrchardCore.AI` (the tool catalog, tool instances and the **Access AI tool** permission)
- `CrestApps.OrchardCore.AI.Chat` (the AI chat hub, for the Chat Bridge)

## Documentation

- Technical Manual: `src/CrestApps.Docs/docs/ai/mcp/webmcp.md`
- User Manual: `src/CrestApps.Docs/docs/user-manual/ai/webmcp.md`
