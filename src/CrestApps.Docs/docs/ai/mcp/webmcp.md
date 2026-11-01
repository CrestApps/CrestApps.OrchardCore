---
sidebar_label: WebMCP
sidebar_position: 5
title: WebMCP
description: Register AI tools and page tools with the standard WebMCP API, and let the AI chat discover and run every WebMCP tool the user's page exposes.
user_manual:
  - user-manual/ai/webmcp
---

| | |
| --- | --- |
| **Module** | `CrestApps.OrchardCore.AI.WebMcp` |
| **Category** | Artificial Intelligence - MCP |

[WebMCP](https://webmachinelearning.github.io/webmcp/) is a proposed web standard of the W3C Web Machine Learning Community Group. It lets a web page give tools to an AI agent in the browser, such as the browser's own assistant, an agent extension, or a chat on the page. The page registers each tool with `document.modelContext.registerTool()`: a name, a description, a JSON schema, annotations and a JavaScript `execute` function. An agent lists the tools with `getTools()` and runs one with `executeTool()`. The tool runs in the page with the user's session, so no API key or OAuth setup is needed.

The module is a thin Orchard Core layer over [CrestApps.Core's WebMCP](https://crestapps.com/docs/core/mcp/webmcp) (`CrestApps.Core.AI.WebMcp` and the `@crestapps/webmcp` scripts), and uses only the standard API. It has three features:

| Feature name | Feature ID | What it adds |
| --- | --- | --- |
| WebMCP Tools | `CrestApps.OrchardCore.AI.WebMcp` | Gives the AI tools and [tool instances](../tool-instances.md) you select to browser agents, on the pages a signed-in user opens. |
| WebMCP Page Tools | `CrestApps.OrchardCore.AI.WebMcp.PageTools` | Adds tools that work on the page itself: page info, links, opening a page, and reading and filling forms. |
| WebMCP Chat Bridge | `CrestApps.OrchardCore.AI.WebMcp.Chat` | Lets the Orchard Core [AI chat](../chat.md) discover and run **every** WebMCP tool the page the user has open exposes, whoever registered it, for the AI profiles that allow it. |

Using the settings screen and the profile switch is described in the User Manual under [WebMCP](../../user-manual/ai/webmcp.md).

:::note[Browser support]
WebMCP is experimental. Chrome and Edge are trialling it natively. In the other browsers, the W3C reference polyfill (`webmcp-polyfill.js`, resource `CrestAppsWebMcpPolyfill`) installs `document.modelContext`, so the tools and the chat bridge work everywhere. WebMCP needs a secure context: HTTPS, or `localhost` while developing.
:::

## How it compares with the MCP server

| | [MCP Server](server.md) | WebMCP Tools |
| --- | --- | --- |
| Who calls the tools | A remote MCP client | An agent in the user's browser |
| Transport | Streamable HTTP at `/mcp` | `document.modelContext` in the page, and two same-origin endpoints |
| Authentication | API key, OpenID Connect or none | The signed-in user's cookie, plus the antiforgery token on calls |
| Tool selection | **MCP Server** settings card | **WebMCP** settings card |
| Per-user tool permission | No | Yes: every tool also needs **Access AI tool** for that user |

Both features read the same tool catalog: tools registered with `AddCoreAITool<T>()` that are selectable (neither system tools nor hidden) and the configured tool instances. WebMCP adds no tools of its own to that catalog.

## WebMCP Tools

### Who gets the scripts

A full page (an MVC view or Razor page) gets the WebMCP scripts when all of these are true:

1. The user is signed in. Anonymous visitors never get the scripts.
2. The page kind is turned on: admin pages by default, front-end pages only when **Offer tools on front-end pages** is on.
3. The user has the **Use WebMCP tools in the browser** (`UseWebMcpTools`) permission. Administrators have it by default.

The page then gets, in order: the polyfill, CrestApps.Core's `webmcp.js` (resource `CrestAppsWebMcp`), and the scripts the enabled features add. `webmcp.js` lists the tools the user may call and registers each with `document.modelContext.registerTool()`. A tool is listed when it is selectable, the settings allow it (by **Expose all tools** or the selection), and the user has **Access AI tool** for it. The server checks the same rules again on every call.

### Endpoints

The module maps CrestApps.Core's endpoints with `MapWebMcpTools()`, and replaces Core's access check with one that needs `UseWebMcpTools` (`PermissionWebMcpAccessEvaluator`).

| Method and route | What it does | Answers |
| --- | --- | --- |
| `GET /api/ai/webmcp/tools` | Lists the tools the user may call: `{ "tools": [{ "name", "title", "description", "inputSchema" }] }` | `401` when not signed in, `403` without `UseWebMcpTools` |
| `POST /api/ai/webmcp/tools/{name}/invoke` | Calls a tool with the JSON object in the body as its arguments, inside an AI invocation scope, and answers an MCP-style result: `{ "content": [{ "type": "text", "text" }], "isError" }` | `401`, `403`, `400` without a valid antiforgery token in the `RequestVerificationToken` header or when the body is not a JSON object or is larger than 256 KB, `404` when the tool is not listed for the user |

A tool that throws answers `isError: true` with a generic message. The exception goes to the site log only. A tool that is not listed for the user answers `404` whatever the reason, so the endpoint does not tell which tools exist.

### Registering your own tools

A theme or module registers its own tools with the standard API. Nothing CrestApps-specific is needed, and the chat bridge finds them like any other tool:

```javascript
const registration = new AbortController();

document.modelContext.registerTool({
    name: 'contoso.find_store',
    title: 'Find a store',
    description: 'Finds the Contoso store nearest to a postal code.',
    inputSchema: {
        type: 'object',
        properties: { postalCode: { type: 'string' } },
        required: ['postalCode'],
    },
    annotations: { readOnlyHint: true },
    execute: async ({ postalCode }) => findStore(postalCode),
}, { signal: registration.signal });
```

Make the resource that registers the tool depend on `CrestAppsWebMcpPolyfill`, so `document.modelContext` exists when it runs. A tool answers a JSON value and reports a failure by throwing. Use the `readOnlyHint`, `consequentialHint` and `untrustedContentHint` annotations to tell agents what the tool does.

### Adding scripts and settings to the page

Implement `IWebMcpClientConfigurationHandler` to add a script resource or a setting to every page that gets the WebMCP scripts. The handler runs after the user and the page have qualified:

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

Scripts load after `webmcp.js`, in the order they were added. Settings are written to the `<script type="application/json" id="crestapps-webmcp-config">` block in the page head, and scripts read them from `window.crestAppsWebMcp.config`. The serializer escapes HTML characters, so a setting cannot close the block.

### Configuration

The `CrestApps:AI:WebMcp` shell configuration section overrides the stored settings. Keys:

| Key | What it does |
| --- | --- |
| `ExposeAllTools`, `Tools` | The tools given to browser agents (tool and tool instance names). |
| `EnableOnAdminPages`, `EnableOnFrontEndPages` | The page kinds that get the scripts. |
| `PageToolOrigins` | The origins of cross-origin frames whose shared tools the chat bridge may use. Empty by default. See [Frames on other origins](#frames-on-other-origins). |

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

Saving the **WebMCP** settings card releases the tenant, so the new selection takes effect on the next request.

## WebMCP Page Tools

The feature adds CrestApps.Core's `webmcp-page-tools.js` (resource `CrestAppsWebMcpPageTools`) to the pages that get the WebMCP scripts. It registers these tools with `document.modelContext`, so any WebMCP agent can use them:

| Tool | Read-only | What it does |
| --- | --- | --- |
| `page_get_info` | Yes | The page's title, address, description, first 20 `h1` and `h2` headings, and whether it is an admin page. |
| `page_list_links` | Yes | Up to 200 same-origin links in `nav`, `header`, `aside` and `[role="navigation"]`, with their text. An optional `filter` keeps the links whose text contains it. |
| `page_navigate` | No | Opens a same-origin address. Other origins and script addresses are refused. |
| `page_list_form_fields` | Yes | Up to 200 fields of the page's forms: name, id, label, type, value (cut to 2,000 characters), required and, for lists, the choices. Hidden, password, file, button and disabled fields and the antiforgery token are left out. |
| `page_set_form_field` | No | Puts a value into one listed field, found by name, id or label, and raises `input` and `change`. For a list, the value is a choice's value or text. It never submits the form. |

## WebMCP Chat Bridge

The chat bridge makes the AI chat a WebMCP agent for the tab the user has open. It uses every tool `document.modelContext.getTools()` lists, not only the CrestApps ones. It runs through the AI chat's own SignalR hub, so it opens no extra connection:

1. Before it sends a message, the chat (`ai-chat.js`) reads the page's tools and sends them to the hub with `SetPageTools`, when they changed since the last message. It leaves out the site's AI tools, because the chat reaches those on the server. A tool registered after the page loaded is picked up with the next message.
2. The hub checks each tool: at most 64 tools, WebMCP names (`^[A-Za-z0-9_.-]{1,128}$`), a description of at most 2,000 characters and a JSON object schema of at most 16 KB, and keeps them for the connection.
3. When the chat session's profile has **Allow page tools** on, the tools are offered to the model.
4. When the model calls one, the hub asks the tab to run it with SignalR client results (`ExecutePageTool`). The tab runs it with `executeTool()` and answers the result, up to `ToolCallTimeout`.

The connection and the completion of a message it sends are always on the same server, so this needs no sticky sessions or shared store in a web farm.

### Profiles

The AI profile editor has two options, stored as CrestApps.Core's `WebMcpProfileSettings`:

| Option | What it does |
| --- | --- |
| **Allow page tools** (`AllowPageTools`) | Offers the page's WebMCP tools to the model. Off by default. |
| **Allow consequential page tools** (`AllowConsequentialPageTools`) | Also offers the tools the page marks with `consequentialHint`, such as placing an order. Needs **Allow page tools**. Off by default. |

### Tool names and annotations

WebMCP names may contain `.`, which AI providers do not accept in function names, so the model sees each tool under a function name: `store.add_to_cart` is offered as `store_add_to_cart`, and the tab still runs `store.add_to_cart`. Results of tools marked `untrustedContentHint` reach the model after a notice to treat them as data, not as instructions.

### Frames on other origins

A frame on another origin can share its tools with the page by registering them with `exposedTo`, and the page must allow it with the `tools` permissions policy: `<iframe src="https://partner.example" allow="tools">`. List the frame's origin under `PageToolOrigins` for the chat bridge to use those tools.

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
- A profile with **Allow page tools** lets the model run any tool the page exposes, including a third-party script's. Allow it only on profiles used on pages whose scripts you trust. Tools marked `consequentialHint` need the second option, and results marked `untrustedContentHint` reach the model as data.
- Page tools only reach the completions of messages sent from the same chat connection.
