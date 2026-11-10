# CrestApps.OrchardCore.AI.WebMcp

Connects Orchard Core to [WebMCP](https://webmachinelearning.github.io/webmcp/), the proposed W3C Community Group standard that lets a page give tools to an AI agent in the browser through `document.modelContext`. It is a thin Orchard Core layer over CrestApps.Core's `CrestApps.Core.AI.WebMcp` package and `@crestapps/webmcp` scripts, and uses only the standard API.

## Feature

One feature, **WebMCP** (`CrestApps.OrchardCore.AI.WebMcp`):

| Part | What it adds |
| --- | --- |
| Site tools | Registers the AI tools and tool instances selected on the **WebMCP** settings card with `document.modelContext`, on the pages a signed-in user with the **Use WebMCP tools in the browser** permission opens. Adds the W3C reference polyfill for browsers without native WebMCP. |
| Storefront tools | Off by default (**Offer storefront tools to visitors**). Gives everyone else on front-end pages, signed in or not, the selected tools marked safe for anyone with `MarkAIToolAnonymousSafe`, through `api/ai/webmcp/storefront/...`. Rate limited per visitor when Orchard Core's Rate Limits feature is on. |
| Content item tools | The **WebMCP tools** content part (`WebMcpPart`, when Orchard Core's Contents feature is on): each content item picks, among the tools the editor may use, the tools its own page exposes. |
| Page tools | Tools that read the page, list its links, open another page of the site, and read and fill its forms without submitting them. On by default (**Add page tools**). |
| AI chat | When AI Chat is enabled (`[RequireFeatures]`), the AI chat discovers and runs every WebMCP tool the page exposes, through the AI chat hub, for AI profiles with **Allow page tools** on. |

## Installation

Reference the `CrestApps.OrchardCore.AI.WebMcp` package (it is included in `CrestApps.OrchardCore.Cms.Core.Targets`), then enable **WebMCP** in **Tools > Features**. It depends on the AI Services feature.

## Configuration

- **Settings > Artificial Intelligence**, **WebMCP** card: the page kinds that get the scripts (admin pages on, front-end pages off by default), **Add page tools** (on), and the tools browser agents may call on every page (none by default; the lists are hidden while **Expose all tools** is on). Saving it releases the tenant.
- **Content types**: attach **WebMCP tools** and pick tools per content item.
- **AI profile editor**, **Capabilities** tab: **Allow page tools** and **Allow consequential page tools** (both off by default).
- The `CrestApps:AI:WebMcp` shell configuration section overrides the stored settings (`ExposeAllTools`, `Tools`, `EnableOnAdminPages`, `EnableOnFrontEndPages`, `EnablePageTools`, `EnableStorefrontTools`) and sets `PageToolOrigins`, and `CrestApps:AI:WebMcp:Chat:ToolCallTimeout` (default one minute) sets how long the chat waits for a page tool.

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
