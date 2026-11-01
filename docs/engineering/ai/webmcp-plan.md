# WebMCP integration plan

## Status and purpose

This plan covers the `CrestApps.OrchardCore.AI.WebMcp` module. The module connects Orchard Core sites to [WebMCP](https://github.com/webmachinelearning/webmcp), the W3C Web Machine Learning Community Group proposal that lets a web page give tools to an AI agent that runs in the browser.

**Status 2026-11-01:** all three phases below are built in one change, with unit tests for every service and every browser script.

## What WebMCP is

WebMCP adds a `ModelContext` object to the page (`document.modelContext` in the current draft, `navigator.modelContext` in earlier Chrome previews). A page calls `registerTool({ name, title, description, inputSchema, annotations, execute })`. A browser agent (Gemini in Chrome, Edge Copilot, ChatGPT Desktop, browser extensions) lists the tools and calls `execute`. The tool runs in the page, with the user's session, so the page needs no API keys or OAuth to let the agent act.

The draft also gives pages `getTools()` and `executeTool(tool, input)`, so a page can call tools that it or a same-origin frame registered.

The API is experimental. It is behind an origin trial in Chrome 149 and Edge 150, and behind the `enable-webmcp-testing` flag for local work. Every script in the module checks for the API first and does nothing without it.

## How it relates to what exists

| Piece | Direction | Transport | Authentication |
| --- | --- | --- | --- |
| MCP Client (`CrestApps.OrchardCore.AI.Mcp`) | Orchard Core calls remote MCP servers | HTTP/SSE, STDIO | Per connection |
| MCP Server (`CrestApps.OrchardCore.AI.Mcp.Server`) | Remote MCP clients call Orchard Core | Streamable HTTP at `/mcp` | API key, OpenID or none |
| **WebMCP Tools** (new) | A browser agent calls Orchard Core tools | Page script + same-origin HTTP | The signed-in user's cookie and antiforgery token |
| **WebMCP Page Tools** (new) | A browser agent works with the current page | Page script only | Runs in the page |
| **WebMCP Chat Bridge** (new) | The Orchard Core AI Chat calls tools on the user's page | SignalR | The signed-in user's cookie |

The new module reuses the AI tool catalog (`AIToolDefinitionOptions`, `AddCoreAITool<T>()`), tool instances (`INamedCatalog<AIToolInstance>`, `IAIToolInstanceSource`), the per-tool permission (`IAIToolAccessEvaluator` / `AccessAITool`) and the tool picker shapes (`EditProfileTools_Edit`, `EditToolInstances_Edit`) that the MCP Server settings use. It writes no new tools.

## Phase 1: WebMCP Tools (`CrestApps.OrchardCore.AI.WebMcp`)

Gives selected AI tools to browser agents on pages the signed-in user opens.

- **Settings** (`WebMcpSettings`, AI settings group, `ManageWebMcpSettings` permission): `ExposeAllTools` (default off), `Tools` allow-list of tool and tool instance names, `EnableOnAdminPages` (default on) and `EnableOnFrontEndPages` (default off). `WebMcpOptionsConfiguration` loads them into `WebMcpOptions`, and the `CrestApps:AI:WebMcp` shell configuration section can override them.
- **Permission** `UseWebMcpTools`: a user without it gets no script and no tools. Every tool also needs the user's `AccessAITool` permission for that tool, checked on the server for every list and every call.
- **Tool service** (`IWebMcpToolService`): lists the tools a user may call (selectable, on the allow-list, authorized) as name, title, description and JSON schema; and calls one of them inside an `AIInvocationScope`. A tool that is not on the list answers "not found", whether it does not exist or the user may not use it, so the endpoint does not tell which tools exist. A tool that throws answers a generic error; the details go to the log only.
- **Endpoints**: `GET /api/ai/webmcp/tools` and `POST /api/ai/webmcp/tools/{name}/invoke`. Both need a signed-in user with `UseWebMcpTools`. The POST also needs a valid antiforgery token in the `RequestVerificationToken` header, so another site cannot make the user's browser call a tool.
- **Page script** (`webmcp.js`, resource `CrestAppsWebMcp`): `WebMcpScriptFilter` adds it, with a JSON configuration block, to full page views when the user may use it and the page kind (admin or front end) is turned on. The script exposes `window.crestAppsWebMcp` (`registerTool`, `unregisterTool`, `getTools`, `executeTool`, `onToolsChanged`, `isSupported`). It keeps its own list of tools and mirrors each one to the browser's `ModelContext` when there is one, unregistering it with an `AbortSignal`. It fetches the server tools and registers one proxy per tool; a proxy calls the invoke endpoint and returns an MCP-style result `{ content: [{ type: 'text', text }], isError }`.
- **Extension point** `IWebMcpClientConfigurationHandler`: other features add scripts and settings to the page configuration. Phases 2 and 3 use it.

## Phase 2: WebMCP Page Tools (`CrestApps.OrchardCore.AI.WebMcp.PageTools`)

Built-in tools that work on the page the user sees. All of them run in the browser only.

| Tool | Read-only | What it does |
| --- | --- | --- |
| `page_get_info` | Yes | The page's title, address, description, main headings and whether it is an admin page. |
| `page_list_links` | Yes | Same-origin links in the admin menu and main navigation, with their text. |
| `page_navigate` | No | Opens a same-origin address. Other origins are refused. |
| `page_list_form_fields` | Yes | The editable fields of the page's forms: name, label, type, value, required and choices. Hidden, password, file and antiforgery fields are left out. |
| `page_set_form_field` | No | Puts a value into one field and raises `input` and `change`. It never submits the form: the user reviews and saves. |

Front-end themes and other modules add their own tools with `window.crestAppsWebMcp.registerTool(...)`. Forms can also use the declarative WebMCP attributes (`toolname`, `tooldescription`, `toolparamdescription`) directly; the browser handles those itself.

## Phase 3: WebMCP Chat Bridge (`CrestApps.OrchardCore.AI.WebMcp.Chat`)

Lets the Orchard Core AI Chat call the tools on the page the user has open, the same way a browser agent does.

- **Profile setting** `WebMcpProfileSettings.AllowPageTools` (AI profile editor, default off): only profiles that opt in see browser tools.
- **Browser** (`webmcp-chat-bridge.js`, resource `CrestAppsWebMcpChatBridge`): connects to `WebMcpHub`, sends the page's tool list (from `window.crestAppsWebMcp.getTools()`) when it connects, when the list changes and when the tab gets focus, and runs the tools the server asks for.
- **Hub** `WebMcpHub` (signed-in users with `UseWebMcpTools`): `RegisterPageTools`, `ClearPageTools`, `SubmitToolResult`. It checks every tool: at most 64 tools, names that AI providers accept (`^[a-zA-Z0-9_-]{1,64}$`), descriptions of at most 2,000 characters and schemas of at most 16 KB.
- **Registry** `IWebMcpBrowserToolRegistry` (singleton, in memory): one entry per user, from the tab that registered last. It forgets a connection when it closes.
- **Invoker** `IWebMcpBrowserToolInvoker` (singleton): sends `ExecuteTool(callId, name, argumentsJson)` to the connection, waits for `SubmitToolResult` from that same connection, and gives up after `WebMcpChatOptions.ToolCallTimeout` (60 seconds by default) or when the connection closes.
- **Tool registry provider** `WebMcpBrowserToolRegistryProvider` (`IToolRegistryProvider`): for a chat session whose user has page tools and whose profile allows them, adds one `WebMcpBrowserToolFunction` per tool to the completion.

Limit: the invoker keeps pending calls in memory, so the browser's SignalR connection and the chat completion must be on the same server. Use sticky sessions in a web farm.

## Security

- Nothing is exposed by default: the operator picks tools, and front-end pages are off.
- The server checks the user, `UseWebMcpTools`, the allow-list and `AccessAITool` on every call. The page script's list only decides what is registered, not what is allowed.
- The invoke endpoint validates the antiforgery token.
- The module never sets `exposedTo`, so tools are not given to cross-origin frames.
- Page tools never submit forms or leave the site.
- Browser tools only reach chat sessions of the same signed-in user, and only for profiles that opt in.

## Tests

- .NET (`tests/CrestApps.OrchardCore.Tests/AI/WebMcp`): settings defaults, options configuration and overrides, permissions, the tool service (allow-list, system and hidden tools, authorization, tool instances, invocation, failures), result formatting, endpoints (401, 403, 400, 404, 200), script filter scope rules, configuration handlers, the browser tool registry, the invoker (completion, wrong connection, timeout, closed connection), the proxy function, the tool registry provider and the hub's validation.
- JavaScript (`tests/js/webmcp`): the core script (registry, `ModelContext` mirroring, server proxies, result shaping), the page tools against small fake documents, and the chat bridge's message handling.
