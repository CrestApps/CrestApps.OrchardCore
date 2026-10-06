---
sidebar_label: Tools and Agents
title: Tools, Agents and Connections
description: Let an AI assistant take action with built-in tools and your own tool instances, call other AI agents, and connect to MCP and A2A services.
technical_manual:
  - ai/tool-instances
  - ai/tools
  - ai/agent
  - ai/mcp/client
  - ai/mcp/server
  - ai/a2a/client
---

Out of the box an assistant can only talk. **Tools** let it act: look up an order, search your documentation, list content items, send a notification. **Agents** are specialist assistants it can hand a job to. **Connections** bring in tools and agents from outside the site. You give an assistant any of these on the **Capabilities** tab of its [profile](profiles.md#capabilities-tab), and the AI decides by itself when to use them.

| | |
| --- | --- |
| **Menu** | Artificial Intelligence > Tool Instances; Artificial Intelligence > Model Context Protocol > MCP Hosts; Artificial Intelligence > Agent to Agent Hosts |
| **Permission** | Manage AI tool instances; Manage MCP Connections; Manage Agent-to-Agent Connections; Access any AI tool, or Access AI tool - *tool name*, to use a tool |
| **Feature** | Orchard Core AI Agent (built-in tools), AI Tool Instances, Model Context Protocol (MCP) Client, Agent-to-Agent (A2A) Client |

<AskYourAdmin />

## Built-in tools

<video controls preload="metadata" width="100%" aria-label="Screencast of enabling AI Agents and creating an agent profile with Orchard-aware tools">
  <source src="/img/docs/ai-agent.mp4" type="video/mp4" />
</video>

With the **Orchard Core AI Agent** feature on, the **Tools** section of the **Capabilities** tab lists actions the AI can take on this site, grouped by category. Some only appear when the matching site feature is on.

| Category | Examples |
| --- | --- |
| **Content Management** | Search, create, update, publish, unpublish, clone and delete content items |
| **Content Definitions** | List and change content types and parts |
| **Features Management** | List, search, enable and disable site features |
| **Users Management** / **Roles Management** | Search users, get user and role information |
| **Communications** | Send a user notification, send emails, send an SMS message |
| **Workflow Management** | List and create workflows |
| **Recipes** | Apply site configuration and run recipes |
| **Tenants Management** | Create, set up, enable, disable and remove tenants |
| **AI Profiles** / **AI Analytics** | List and view AI profiles; query chat analytics |
| **System** | List time zones |

To give tools to a profile, open the profile's **Capabilities** tab, find tools with **Search tools...**, and tick them, or use **Select All Tools** or **Select All in** *category*. Then **Save**.

:::caution[Choose tools with care]
Tools really act: a profile with **Delete Content Item** can delete content when someone asks it to. Give a public website assistant no tools that change anything, and give an admin assistant only the tools its users need.
:::

The full list of tools and what each does is in the [Technical Manual](../../ai/tools.md).

## Who can use tools

Using a profile and using its tools are two separate permissions. A person can only use a tool when their role has **Access any AI tool**, or the permission for that one tool, **Access AI tool - *tool name***. A tool the person may not use is quietly left out, and the assistant answers without it. That often looks like a vague answer or "I can't do that".

When an assistant works for an administrator but not for someone else, check that person's role under **Access Control > Roles**. **Access any AI tool** is a powerful permission and is given only to administrators by default; prefer granting the individual tools.

## Tool instances

A tool instance is a tool that you set up yourself, from a ready-made kind, without code. For example, an *Order lookup* that calls your order system's web service, or a *Product docs search* that searches your public documentation site. Each instance becomes its own tool, with its own permission.

<video controls preload="metadata" width="100%" aria-label="Screencast of enabling AI Tool Instances and creating an HTTP API Request instance">
  <source src="/img/docs/ai-tool-instances.mp4" type="video/mp4" />
</video>

### Create a tool instance

1. Open **Artificial Intelligence > Tool Instances** and click **Add Tool Instance**.
2. In **Available Sources**, click **Add** on the kind of tool you need (see below).
3. Enter a **Name** and a **Description**, fill in the fields of that kind, and click **Save**.

| Field | What it does |
| --- | --- |
| **Name** | A unique name. The tool's function name, which the AI uses, is built from it, so it cannot be changed later. |
| **Description** | Tells the AI what this tool is for and when to use it. Be specific: "Looks up the status of a customer's order by order number". When several instances are of the same kind, the description is the only way the AI tells them apart. |

### Kinds of tool instances

| Kind | Use it to | Fields to fill in |
| --- | --- | --- |
| **HTTP API Request** | Call a web service, such as your order or booking system. | **Base URL**, **HTTP method**, **Timeout**, **Headers**, which values the AI may fill in (**Model provided values**), and the **Authentication type** with its credentials. Your technical team provides these. |
| **Documentation search (sitemap)** | Search a public documentation site that has a sitemap, such as most help centers. | **Base URL** of the site, and optionally **Sitemap URL**, **Maximum results** and **Maximum pages**. |
| **Documentation search (search index)** | Search a documentation site that publishes a search index file. | **Base URL**, and optionally **Index URL** and **Maximum results**. |
| **Documentation search (Algolia DocSearch)** | Search a documentation site that uses Algolia search. | **Application id**, **Search-only API key** and **Index name** from the site, and optionally **Maximum results**. |
| **Website search (live API)** | Search a site through its own search, such as a WordPress site. | **Base URL**. The other fields are already set for WordPress. |
| **Data source search (vector)** | Let the AI search one of your [data sources](knowledge.md#data-sources) only when it needs to. | **Data source**, **Retrieval mode**, **Strictness**, **Retrieved documents** and **Filter**. |

Secrets such as API keys and passwords are stored securely and never shown again. To keep a stored secret, leave its field blank when you edit the instance.

:::note
A data source search tool cannot force the AI to answer only from your data. When answers must come from your content, attach the data source to the profile on its **Knowledge** tab and turn on **Restrict answers to retrieved data only** instead.
:::

### Use a tool instance

Tick the instance under **Tool Instances** on the **Capabilities** tab of a profile, a profile template or a chat interaction, and save. People also need permission to use it (see [Who can use tools](#who-can-use-tools)).

## Agents

An agent is a profile of type **Agent**: a specialist with its own instructions and tools, such as a *Research Agent* or a *Writer Agent*. Another assistant can hand a job to it and use its answer. The **New AI Profile** picker has ready-made agents to start from.

1. Create a profile, choose **Profile type** **Agent**, and fill in its **Description**. Other assistants read the description to decide when to call the agent, so say exactly what it does.
2. Choose its **Availability**:
   - **On demand**: the agent is only included when it fits the user's request, and only for assistants that pick it.
   - **Always available**: the agent is included in every AI request. This uses more tokens and costs more; use it only for agents every assistant must be able to reach.
3. In the assistant that should call the agent, open the **Capabilities** tab, tick the agent under **Agents**, and save.

To try an agent on its own, use **Invoke Profile** from its **Actions** menu (see [Chat with an AI assistant](chat.md#try-out-a-utility-or-agent-profile)).

## MCP connections

The Model Context Protocol (MCP) is a common way for AI apps to share tools. An MCP connection brings the tools of an outside MCP server, such as a time-zone service or your company's internal tools, into this site.

1. Open **Artificial Intelligence > Model Context Protocol > MCP Hosts** and click **Add Connection**.
2. In **Available Sources**, click **Add** on the type of server:
   - **Server-Sent Events**: a server reached over the web. Enter its **Endpoint** and choose the **Authentication** your technical team gives you, such as **Anonymous (No Authentication)**, **API Key** or one of the **OAuth 2.0** options.
   - **Standard Input/Output**: a program that runs on the site's own server. Your technical team provides the **Command** and the other fields. This kind is only available when the **Model Context Protocol (MCP) Local Client** feature is on.
3. Enter a **Title** and click **Save**.
4. In a profile or chat interaction, tick the connection under **MCP Connections** (in a chat interaction: **Connections**) on the **Capabilities** tab.

## A2A connections

The Agent-to-Agent (A2A) protocol lets AI agents on different systems work together. An A2A connection lets your assistants call the agents of another system.

1. Open **Artificial Intelligence > Agent to Agent Hosts** and click **Add Connection**.
2. Enter a **Title**, the **Endpoint** (the other system's base address), and the **Authentication** your technical team gives you.
3. Click **Save**.
4. In a profile or chat interaction, tick the connection under **A2A Connections** on the **Capabilities** tab.

Each agent the other system offers becomes something your assistant can call.

Your site can also offer its own **Agent** profiles to other systems over A2A. That is set up by your technical team; see the [Technical Manual](../../ai/a2a/host.md).

## Share your tools with other AI apps

With the **Model Context Protocol (MCP) Server** feature on, AI apps outside the site, such as a desktop AI assistant, can use selected tools of this site.

<video controls preload="metadata" width="100%" aria-label="Screencast of the admin chat working with content and tools through the MCP server integration">
  <source src="/img/docs/mcp-integration.mp4" type="video/mp4" />
</video>

1. Open **Settings > Artificial Intelligence** and find the **MCP Server** section.
2. Choose the **Authentication type** your technical team asks for: **OpenID Connect**, **API key** or **None (Anonymous access)**. Use **None** only for testing.
3. Tick the tools and tool instances to share under **Tools** and **Tool Instances**. Nothing is shared until you pick it. **Expose all tools** shares every tool; use it only when you trust every app that connects.
4. Click **Save**. The site reloads its settings.

You need the **Manage the MCP Server settings** permission. How outside apps connect is described in the [Technical Manual](../../ai/mcp/server.md).

The server can also share reusable prompts and data with those apps. Manage them under **Artificial Intelligence > Model Context Protocol > Prompts** (**Add Prompt**: a **Title**, a **Name**, a **Description**, optional **Arguments**, and the **Messages** with their **Role** and **Content**) and **Resources** (**Add Resource**, then a type such as **File**, **Content Item** or **Media**, and a **Display Text**, **Name**, **Path**, **Description** and **MIME Type**). These menus appear only with the MCP Server feature on.
