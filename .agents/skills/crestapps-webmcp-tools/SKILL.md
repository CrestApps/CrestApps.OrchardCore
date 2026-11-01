---
name: crestapps-webmcp-tools
description: >
  Extends the CrestApps.OrchardCore.AI.WebMcp module, a thin Orchard Core layer over CrestApps.Core's WebMCP
  (CrestApps.Core.AI.WebMcp and the @crestapps/webmcp scripts): adds a WebMCP tool to a page with the standard
  document.modelContext.registerTool API, adds a script or page setting to every page that gets the WebMCP scripts
  through IWebMcpClientConfigurationHandler, or exposes server AI tools to browser agents. Covers the WebMCP tool
  contract (names, annotations, results), resource dependencies, the server-side checks (UseWebMcpTools, the WebMCP
  allow-list, AccessAITool, antiforgery), the chat bridge's limits and options, and the tests to add. Use whenever a
  change touches WebMCP.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "2.0"
---

# Extending WebMCP

The WebMCP module (`src/Modules/CrestApps.OrchardCore.AI.WebMcp`) is an Orchard Core layer over CrestApps.Core's WebMCP. Core owns the tool service, the endpoints, the chat's page tools and the browser scripts; this module owns the settings, the permission, the script filter, the resources and the profile options. Read the Technical Manual page `src/CrestApps.Docs/docs/ai/mcp/webmcp.md` first; it is the source of truth for routes, settings and limits.

Use only the standard WebMCP API (`document.modelContext`). Never add a CrestApps-specific registry or a second hub: the chat bridge already finds every tool the page registers.

## Pick the right extension point

| You want to... | Use |
| --- | --- |
| Let browser agents call an existing server AI tool | Nothing new: register the tool with `AddCoreAITool<T>().Selectable()` and let the operator select it on the **WebMCP** card. Never bypass the allow-list. |
| Add a tool that works on the page (DOM, client state) | `document.modelContext.registerTool(...)` from a script resource that depends on `CrestAppsWebMcpPolyfill` |
| Add that script, or a setting it needs, to every page that gets the WebMCP scripts | `IWebMcpClientConfigurationHandler` |
| Change the server behavior (tool service, endpoints, page tools in the chat, scripts) | Change CrestApps.Core, release it, then bump `CrestAppsCoreVersion` and `@crestapps/webmcp` here |

## WebMCP tool contract

- `name` (required): matches `^[A-Za-z0-9_.-]{1,128}$` and is unique on the page (a duplicate throws). Prefix it with your module (`contoso.find_store`). The chat offers it to the model as a function name with other characters replaced by `_`.
- `description` (required): what the tool does and when to use it, at most 2,000 characters for the chat bridge.
- `inputSchema` (optional): a JSON Schema object, at most 16 KB as JSON for the chat bridge.
- `title` (optional): the label agents show.
- `annotations` (optional): `readOnlyHint` for tools that only read; `consequentialHint` for tools with effects that cannot be undone (the chat runs them only with **Allow consequential page tools**); `untrustedContentHint` when the output holds content the site did not write (the chat tells the model to treat it as data).
- `execute(input, client)` (required): returns a JSON-serializable value and throws to report a failure.
- Register with `{ signal }` from an `AbortController` when the tool belongs to part of the page that can go away. Use `exposedTo` only for tools a cross-origin parent should see.

Rules: page tools never submit forms, never navigate off the site, and never read password, hidden, file or antiforgery fields. The user reviews and saves.

## Script wiring

1. Define the resource in your module's `ResourceManagementOptionsConfiguration` with `SetDependencies(WebMcpConstants.ResourceNames.Polyfill)` (or `WebMcpConstants.ResourceNames.Script` when it reads `window.crestAppsWebMcp.config`).
2. Add it to the page from an `IWebMcpClientConfigurationHandler` with `context.AddScript(...)`, registered in the feature's startup. Put settings in `context.Settings`; the script reads them from `window.crestAppsWebMcp.config`.
3. The module's own scripts are copied from `@crestapps/webmcp` by `Assets.json`; when you bump that package, update the jsDelivr URLs and SRI hashes in `Services/ResourceManagementOptionsConfiguration.cs`.

## Server-side rules that must hold

- Every list and call checks: signed-in user, `WebMcpPermissionsProvider.UseWebMcpTools` (through `PermissionWebMcpAccessEvaluator`), the WebMCP allow-list (or `ExposeAllTools`), and `IAIToolAccessEvaluator` for the tool. Calls also validate the antiforgery token.
- A tool the user cannot call answers `NotFound`, never a different error.
- A tool failure answers a generic message; details go to the log.
- Page tools reach the AI chat only for messages sent from the same chat connection, and only when the profile's `WebMcpProfileSettings.AllowPageTools` is on (`AllowConsequentialPageTools` too for consequential tools).
- Saving the WebMCP settings releases the tenant, because Core reads `IOptionsMonitor<WebMcpOptions>`.

## Tests to add

- .NET (`tests/CrestApps.OrchardCore.Tests/AI/WebMcp/`): handlers (scripts and settings added), options configuration, the script filter, and any new server rule.
- Browser behavior of the scripts is tested in CrestApps.Core (`tests/realtime-client/webmcp*.js`), against the real polyfill.

Update `src/CrestApps.Docs/docs/ai/mcp/webmcp.md`, the User Manual page `src/CrestApps.Docs/docs/user-manual/ai/webmcp.md`, and this skill in the same change.
