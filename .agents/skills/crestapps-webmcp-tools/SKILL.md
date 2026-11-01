---
name: crestapps-webmcp-tools
description: >
  Extends the CrestApps.OrchardCore.AI.WebMcp module: adds a WebMCP page tool (a browser-side tool registered with
  window.crestAppsWebMcp.registerTool), adds a script or page setting to every page that offers WebMCP tools
  through IWebMcpClientConfigurationHandler, or exposes server AI tools to browser agents. Covers the page tool
  contract (name, description, inputSchema, annotations, execute and its MCP-style result), resource dependencies,
  the server-side checks (UseWebMcpTools, the WebMCP allow-list, AccessAITool, antiforgery), the chat bridge's
  name and size limits, and the tests to add. Use whenever a change touches WebMCP.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Extending WebMCP

The WebMCP module (`src/Modules/CrestApps.OrchardCore.AI.WebMcp`) gives tools to AI agents in the browser. Read the Technical Manual page `src/CrestApps.Docs/docs/ai/mcp/webmcp.md` first; it is the source of truth for routes, settings and limits.

## Pick the right extension point

| You want to... | Use |
| --- | --- |
| Let browser agents call an existing server AI tool | Nothing new: register the tool with `AddCoreAITool<T>().Selectable()` and let the operator select it on the **WebMCP** card. Never bypass the allow-list. |
| Add a tool that works on the page (DOM, client state) | A page tool registered with `window.crestAppsWebMcp.registerTool(...)` from a script resource |
| Add that script, or a setting it needs, to every page that offers tools | `IWebMcpClientConfigurationHandler` |

## Page tool contract (required unless marked optional)

- `name` (required): matches `^[a-zA-Z0-9_-]{1,64}$`, or the chat bridge drops it. Prefix it with your module (`contoso_...`); the built-in ones use `page_`.
- `description` (required): what the tool does and when to use it, at most 2,000 characters.
- `inputSchema` (optional, defaults to an empty object schema): a JSON Schema object, at most 16 KB as JSON.
- `title` (optional): the label browsers show.
- `annotations` (optional): `readOnlyHint: true` for tools that only read; `consequentialHint: true` for tools with real-world effects; `untrustedContentHint: true` when the output holds content the site did not write.
- `execute(input, { signal })` (required): returns `{ content: [{ type: 'text', text }], isError }`. Use `textResult(text, isError)` from `Assets/js/webmcp-results.js` when the script lives in this module.

Rules: page tools never submit forms, never navigate off the site, and never read password, hidden, file or antiforgery fields. The user reviews and saves.

## Script wiring

1. Put the source in `Assets/js/`, import shared helpers as ES modules (never attach helpers to `window`), and add a `vite` group to `Assets.json`.
2. Define the resource in `Services/ResourceManagementOptionsConfiguration.cs` with `SetDependencies(WebMcpConstants.ResourceNames.Script)`, so it loads after `webmcp.js` and `window.crestAppsWebMcp` exists.
3. Add it to the page from an `IWebMcpClientConfigurationHandler` with `context.AddScript(...)`, registered in the feature's startup. Put settings in `context.Settings`; the script reads them from `window.crestAppsWebMcp.config`.
4. Keep the DOM work behind functions that take `doc` and `loc`, and expose them on `globalThis` for the unit tests (the JS tests run in Node without a DOM).

## Server-side rules that must hold

- Every list and call checks: signed-in user, `WebMcpPermissionsProvider.UseWebMcpTools`, the WebMCP allow-list (or `ExposeAllTools`), and `IAIToolAccessEvaluator` for the tool. Calls also validate the antiforgery token.
- A tool the user cannot call answers `NotFound`, never a different error.
- A tool failure answers a generic message; details go to the log.
- Browser tools reach the AI chat only through `WebMcpBrowserToolRegistryProvider`, only for the session's own signed-in user, and only when the profile's `WebMcpProfileSettings.AllowPageTools` is on.

## Tests to add

- JavaScript (`tests/js/webmcp/`): each tool's result against a small fake document, refusals (missing input, other origins, hidden fields), and registration.
- .NET (`tests/CrestApps.OrchardCore.Tests/AI/WebMcp/`): handlers (scripts and settings added), and any new server rule.

Update `src/CrestApps.Docs/docs/ai/mcp/webmcp.md`, the User Manual page `docs/user-manual/ai/webmcp.md`, and this skill in the same change.
