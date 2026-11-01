---
sidebar_label: WebMCP
sidebar_position: 5
title: WebMCP
description: Give AI tools and page tools to AI agents in the browser through WebMCP, and let the AI chat call the tools on the user's page.
user_manual:
  - user-manual/ai/webmcp
---

| | |
| --- | --- |
| **Module** | `CrestApps.OrchardCore.AI.WebMcp` |
| **Category** | Artificial Intelligence - MCP |

[WebMCP](https://github.com/webmachinelearning/webmcp) is a proposed web standard that lets a web page give tools to an AI agent that runs in the browser, such as the browser's own assistant or an agent extension. The page registers each tool with a name, a description, a JSON schema and a JavaScript function, and the agent calls it. The tool runs in the page with the user's session, so no API key or OAuth setup is needed.

The WebMCP module connects Orchard Core to that API in three ways:

| Feature name | Feature ID | What it adds |
| --- | --- | --- |
| WebMCP Tools | `CrestApps.OrchardCore.AI.WebMcp` | Gives the AI tools and [tool instances](../tool-instances.md) you select to browser agents, on the pages a signed-in user opens. |
| WebMCP Page Tools | `CrestApps.OrchardCore.AI.WebMcp.PageTools` | Adds tools that work on the page itself: page info, links, opening a page, and reading and filling forms. |
| WebMCP Chat Bridge | `CrestApps.OrchardCore.AI.WebMcp.Chat` | Lets the Orchard Core [AI chat](../chat.md) call the WebMCP tools on the page the user has open, for the AI profiles that allow it. |

Using the settings screen and the profile switch is described in the User Manual under [WebMCP](../../user-manual/ai/webmcp.md).

:::note[Browser support]
WebMCP is experimental. It is behind an origin trial in Chrome 149 and Edge 150, and behind the `enable-webmcp-testing` flag in `about:flags` for local work. The module's scripts look for `document.modelContext` (the current draft) and then `navigator.modelContext` (earlier Chrome previews), and give nothing to the browser when neither exists. The page's own list of tools still works without browser support, so the chat bridge works in every browser.
:::

## How it compares with the MCP server

| | [MCP Server](server.md) | WebMCP Tools |
| --- | --- | --- |
| Who calls the tools | A remote MCP client | An agent in the user's browser |
| Transport | Streamable HTTP at `/mcp` | A page script and two same-origin endpoints |
| Authentication | API key, OpenID Connect or none | The signed-in user's cookie, plus the antiforgery token on calls |
| Tool selection | **MCP Server** settings card | **WebMCP** settings card |
| Per-user tool permission | No | Yes: every tool also needs **Access AI tool** for that user |

Both features read the same tool catalog: tools registered with `AddCoreAITool<T>()` that are selectable (neither system tools nor hidden) and the configured tool instances. WebMCP adds no tools of its own to that catalog.

## WebMCP Tools

### Who gets tools

A full page (an MVC view or Razor page) gets the WebMCP script when all of these are true:

1. The user is signed in. Anonymous visitors never get the script.
2. The page kind is turned on: admin pages by default, front-end pages only when **Offer tools on front-end pages** is on.
3. The user has the **Use WebMCP tools in the browser** (`UseWebMcpTools`) permission. Administrators have it by default.

The script then lists the tools the user may call. A tool is listed when it is selectable, the settings allow it (by **Expose all tools** or the selection), and the user has **Access AI tool** for it. The server checks the same rules again on every call.

### Endpoints

| Method and route | What it does | Answers |
| --- | --- | --- |
| `GET /api/ai/webmcp/tools` | Lists the tools the user may call: `{ "tools": [{ "name", "title", "description", "inputSchema" }] }` | `401` when not signed in, `403` without `UseWebMcpTools` |
| `POST /api/ai/webmcp/tools/{name}/invoke` | Calls a tool with the JSON object in the body as its arguments, inside an AI invocation scope, and answers an MCP-style result: `{ "content": [{ "type": "text", "text" }], "isError" }` | `401`, `403`, `400` without a valid antiforgery token in the `RequestVerificationToken` header or when the body is not a JSON object or is larger than 256 KB, `404` when the tool is not listed for the user |

A tool that throws answers `isError: true` with a generic message. The exception goes to the site log only. A tool that is not listed for the user answers `404` whatever the reason, so the endpoint does not tell which tools exist.

### The page script

The script (`webmcp.js`, resource `CrestAppsWebMcp`) reads its configuration from a `<script type="application/json" id="crestapps-webmcp-config">` block in the page head. It exposes the page's list of tools as `window.crestAppsWebMcp`:

| Member | What it does |
| --- | --- |
| `registerTool(tool)` | Adds a tool `{ name, title?, description, inputSchema?, annotations?, execute }` and gives it to the browser's `ModelContext`. A tool registered again under the same name replaces the first. |
| `unregisterTool(name)` | Removes a tool from the page and from the browser. |
| `getTools()` | Lists the tools without their functions. Tools that call the site have `source: 'server'`; the others have `source: 'page'`. |
| `executeTool(name, input)` | Runs a tool on the page. |
| `onToolsChanged(listener)` | Calls the listener when the list changes, and answers a function that stops it. |
| `isSupported()` | Says whether the browser supports WebMCP. |
| `config` | The page configuration. |

Each server tool is registered as a proxy that posts to the invoke endpoint with the antiforgery token and has the `untrustedContentHint` annotation. Tools are unregistered from the browser with the `AbortSignal` of `registerTool`, and with `unregisterTool` on browsers that offer it. The module never sets `exposedTo`, so tools are not given to cross-origin frames.

A theme or module adds its own tools after the script has loaded:

```javascript
window.crestAppsWebMcp.registerTool({
    name: 'contoso_find_store',
    title: 'Find a store',
    description: 'Finds the Contoso store nearest to a postal code.',
    inputSchema: {
        type: 'object',
        properties: { postalCode: { type: 'string' } },
        required: ['postalCode'],
    },
    annotations: { readOnlyHint: true },
    execute: async ({ postalCode }) => ({
        content: [{ type: 'text', text: await findStore(postalCode) }],
    }),
});
```

Make the resource that registers the tool depend on `CrestAppsWebMcp`, so it loads after the core script. Forms can also use the declarative WebMCP attributes (`toolname`, `tooldescription`, `toolparamdescription`) directly; the browser handles those itself.

### Adding scripts and settings to the page

Implement `IWebMcpClientConfigurationHandler` to add a script resource or a setting to every page that gets the WebMCP script. The handler runs after the user and the page have qualified:

```csharp
internal sealed class ContosoWebMcpHandler : IWebMcpClientConfigurationHandler
{
    public Task ConfigureAsync(WebMcpClientConfigurationContext context)
    {
        context.AddScript("ContosoWebMcpTools");
        context.Settings["storesUrl"] = $"{context.HttpContext.Request.PathBase}/stores";

        return Task.CompletedTask;
    }
}

services.AddScoped<IWebMcpClientConfigurationHandler, ContosoWebMcpHandler>();
```

Scripts load after `webmcp.js`, in the order they were added. Settings are written to the configuration block and the scripts read them from `window.crestAppsWebMcp.config`. The serializer escapes HTML characters, so a setting cannot close the block.

### Configuration

The `CrestApps:AI:WebMcp` shell configuration section overrides the stored settings. Keys: `ExposeAllTools`, `Tools` (tool and tool instance names), `EnableOnAdminPages` and `EnableOnFrontEndPages`.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "WebMcp": {
          "EnableOnFrontEndPages": false,
          "Tools": [ "listTimeZones" ]
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__AI__WebMcp__EnableOnFrontEndPages=false
OrchardCore__CrestApps__AI__WebMcp__Tools__0=listTimeZones
```

## WebMCP Page Tools

The feature adds `webmcp-page-tools.js` (resource `CrestAppsWebMcpPageTools`) to the pages that get the WebMCP script. Its tools run in the browser only:

| Tool | Read-only | What it does |
| --- | --- | --- |
| `page_get_info` | Yes | The page's title, address, description, first 20 `h1` and `h2` headings, and whether it is an admin page. |
| `page_list_links` | Yes | Up to 200 same-origin links in `nav`, the admin menu, the admin top bar and `header`, with their text. An optional `filter` keeps the links whose text contains it. |
| `page_navigate` | No | Opens a same-origin address. Other origins and script addresses are refused. |
| `page_list_form_fields` | Yes | Up to 200 fields of the page's forms: name, id, label, type, value (cut to 2,000 characters), required and, for lists, the choices. Hidden, password, file, button and disabled fields and the antiforgery token are left out. |
| `page_set_form_field` | No | Puts a value into one listed field, found by name, id or label, and raises `input` and `change`. For a list, the value is a choice's value or text. It never submits the form. |

## WebMCP Chat Bridge

The chat bridge lets the AI chat call the tools on the user's page, through the same list a browser agent sees. Only page tools are sent: the chat reaches the site's AI tools directly.

1. On a page with an AI chat, `webmcp-chat-bridge.js` (resource `CrestAppsWebMcpChatBridge`, which depends on `signalr`) connects to `WebMcpHub` at `/Communication/Hub/WebMcpHub`. It waits until the page shows a chat input (`[data-profile-id]`), the user moves into one, or a chat session begins, so pages without a chat open no connection.
2. It reports the page tools with `RegisterPageTools` when it connects, when the list changes, when the tab gets focus and when the connection comes back. The hub checks each tool: at most 64 tools, names AI providers accept (`^[a-zA-Z0-9_-]{1,64}$`), a description of at most 2,000 characters and a JSON object schema of at most 16 KB. Tools that fail are dropped.
3. `IWebMcpBrowserToolRegistry` keeps one entry per user: the tab that reported last.
4. `WebMcpBrowserToolRegistryProvider`, an `IToolRegistryProvider`, adds one `WebMcpBrowserToolFunction` per page tool to a chat completion when the completion's chat session belongs to a signed-in user who has page tools, and the session's profile has **Allow page tools** on (`WebMcpProfileSettings.AllowPageTools`, off by default).
5. When the model calls a page tool, `IWebMcpBrowserToolInvoker` sends `ExecuteTool(callId, name, argumentsJson)` to the tab and waits for `SubmitToolResult` from the same connection. It gives up after `ToolCallTimeout` or when the tab closes.

The hub needs a signed-in user with **Use WebMCP tools in the browser**. Anonymous chat sessions never get page tools.

:::warning[Web farms]
The invoker keeps waiting calls in memory, so the tab's SignalR connection and the chat completion must be on the same server. Use sticky sessions when the site runs on more than one server.
:::

### Configuration

| Key | Default | What it does |
| --- | --- | --- |
| `CrestApps:AI:WebMcp:Chat:ToolCallTimeout` | `00:01:00` | How long the chat waits for the browser to answer a page tool call. |

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "WebMcp": {
          "Chat": {
            "ToolCallTimeout": "00:00:30"
          }
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__AI__WebMcp__Chat__ToolCallTimeout=00:00:30
```

## Security

- Nothing is given by default. You select the tools, and front-end pages are off.
- The server checks the user, `UseWebMcpTools`, the selection and **Access AI tool** on every list and every call. The script's list only decides what is registered in the browser.
- Calls need the antiforgery token, so another site cannot make the user's browser call a tool.
- A browser agent acts as the signed-in user. Give agents only the tools you would let the user run, and prefer read-only tools.
- Page tools never submit forms and never leave the site, so the user reviews and saves what an agent changes.
- Page tools only reach chat sessions of the same signed-in user, and only for profiles that allow them.
