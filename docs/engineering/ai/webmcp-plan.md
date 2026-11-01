# WebMCP integration plan

## Status and purpose

This plan covers the `CrestApps.OrchardCore.AI.WebMcp` module. The module connects Orchard Core sites to [WebMCP](https://webmachinelearning.github.io/webmcp/), the W3C Web Machine Learning Community Group proposal that lets a web page give tools to an AI agent in the browser.

**Status 2026-11-01:** the framework-neutral parts moved to CrestApps.Core (`CrestApps.Core.AI.WebMcp` in `2.0.0-preview.354`, and the `@crestapps/webmcp` npm package). This module is now a thin Orchard Core layer on top of them.

## What WebMCP is

A page registers tools with `document.modelContext.registerTool({ name, title, description, inputSchema, annotations, execute }, { signal, exposedTo })`. An agent lists them with `getTools({ fromOrigins })` and runs one with `executeTool(tool, input)`; the `toolchange` event tells it the list changed. Names match `^[A-Za-z0-9_.-]{1,128}$`. The annotations are `readOnlyHint`, `consequentialHint` and `untrustedContentHint`. Cross-origin frames share tools with `exposedTo`, and the page allows them with the `tools` permissions policy. WebMCP needs a secure context.

The API is experimental and trialled natively in Chrome and Edge. Every other browser gets the W3C reference polyfill (`@mcp-b/webmcp-polyfill`, MIT), which CrestApps.Core ships as `webmcp-polyfill.js`.

## What CrestApps.Core provides

- `AddCoreAIWebMcp()`: `IWebMcpToolService` (lists and calls the selectable, allowed and authorized tools), `IWebMcpAccessEvaluator` and `WebMcpOptions` (`ExposeAllTools`, `Tools`).
- `MapWebMcpTools()`: `GET api/ai/webmcp/tools` and `POST api/ai/webmcp/tools/{name}/invoke`, with the antiforgery token.
- `AddCoreAIWebMcpPageTools<THub>()`: the chat's page tools. `AIChatHubCore.SetPageTools` keeps the tab's tools per connection, `WebMcpPageToolRegistryProvider` offers them to the model for profiles with `WebMcpProfileSettings.AllowPageTools` (and `AllowConsequentialPageTools` for consequential tools), and the hub runs a tool in the tab with SignalR client results.
- `@crestapps/webmcp`: `webmcp-polyfill.js`, `webmcp.js` (registers the site's tools with `document.modelContext` and connects the chat to every tool the page exposes) and `webmcp-page-tools.js`. `@crestapps/ai-chat-ui` (`2.0.0-preview.243`) calls `crestAppsWebMcp.connectChat` before each message when the page has it.

## What this module adds

| Piece | Orchard Core layer |
| --- | --- |
| Settings | `WebMcpSettings` on the **WebMCP** card of the AI settings group (`ManageWebMcpSettings`). `WebMcpOptionsConfiguration` loads them into Core's `WebMcpOptions` and into `WebMcpPageOptions` (page kinds, `PageToolOrigins`); `CrestApps:AI:WebMcp` overrides them. Saving releases the tenant, because Core reads `IOptionsMonitor<WebMcpOptions>`. |
| Access | `PermissionWebMcpAccessEvaluator` replaces Core's default: the endpoints need `UseWebMcpTools`. |
| Scripts | `WebMcpScriptFilter` adds the configuration block and the `CrestAppsWebMcp` resource (which depends on `CrestAppsWebMcpPolyfill`) to full pages for qualifying users. `IWebMcpClientConfigurationHandler` lets features add scripts and settings; WebMCP Page Tools adds `CrestAppsWebMcpPageTools`. The asset build copies the scripts from `@crestapps/webmcp`, and the resources also point to jsDelivr with SRI hashes. |
| Chat bridge | `AddCoreAIWebMcpPageTools<AIChatHub>()` on the existing AI chat hub, `CrestApps:AI:WebMcp:Chat` for `WebMcpPageToolOptions`, and the **Allow page tools** / **Allow consequential page tools** options in the profile editor. |

## Testing

- Unit tests: options configuration, the access evaluator, the script filter, the configuration handlers, settings and permissions. Core's own tests cover the tool service, endpoints, registry, name mapping, annotations and the scripts (including a browser suite against the real polyfill).
- End to end: run the CMS host with a mock OpenAI-compatible model, enable the features, select a tool, and check from a browser that `document.modelContext.getTools()` lists the site tools and page tools, that `executeTool` runs them, and that the admin chat widget calls a page tool (and a consequential one only with the second option).
