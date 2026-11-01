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
  - ai/mcp/webmcp
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

<YouTube video="ai-walkthroughs" title="Video of enabling AI Agents and creating an agent profile with Orchard-aware tools" start={388} end={422} />

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

Who can use a tool depends on where it is ticked.

- **On an AI profile** (and on the agents it calls), the profile decides. Everyone who can chat with the profile gets every tool ticked on it, including visitors who are not signed in. The tools do not check the person's own permissions, so a tool that changes the site, such as deleting content, can be used by anyone who can reach the assistant. Only tick tools that are safe for everyone the profile serves.
- **In a chat interaction**, the person chooses the tools, so each one needs a permission. A person can only use a tool when their role has **Access any AI tool**, or the permission for that one tool, **Access AI tool - *tool name***. A tool the person may not use is quietly left out, and the assistant answers without it. That often looks like a vague answer or "I can't do that".

When a chat interaction works for an administrator but not for someone else, check that person's role under **Access Control > Roles**. **Access any AI tool** is a powerful permission and is given only to administrators by default; prefer granting the individual tools.

## Tool instances

A tool instance is a tool that you set up yourself, from a ready-made kind, without code. For example, an *Order lookup* that calls your order system's web service, or a *Product docs search* that searches your public documentation site. Each instance becomes its own tool, with its own permission.

<YouTube video="ai-walkthroughs" title="Video of enabling AI Tool Instances and creating an HTTP API Request instance" start={354} end={385} />

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
   - **Always available**: the agent is included in every AI request, for every assistant on the site. This uses more tokens and costs more, and it gives every assistant the agent's tools; use it only for agents every assistant must be able to reach.
3. If the agent should act and not only answer, tick **Let this agent use its tools**, then tick its tools on its **Capabilities** tab. Without this box the agent runs without any tools when another assistant calls it, even if tools are ticked.
4. In the assistant that should call the agent, open the **Capabilities** tab, tick the agent under **Agents**, and save.

To try an agent on its own, use **Invoke Profile** from its **Actions** menu (see [Chat with an AI assistant](chat.md#try-out-a-utility-or-agent-profile)).

### Ready-made agents for running the site

The **New AI Profile** picker has agents that each look after one part of the site. Each already has its tools ticked, **Let this agent use its tools** on, and **Availability** set to **On demand**.

| Agent | What it does | Needs |
| --- | --- | --- |
| **Content Agent** | Finds, creates, edits, clones, publishes, unpublishes and deletes content items, and adds, changes and removes content types and parts. Saves changes as drafts unless asked to publish. | Orchard Core AI Agent, Contents, Content Types |
| **Workflow Agent** | Lists and explains workflows, and creates or changes them. New workflows start turned off unless asked otherwise. | Orchard Core AI Agent, Workflows |
| **AI Operations Agent** | Answers questions about AI profiles, conversations, what the AI remembers about a contact, quality scores, tests, alerts and chat usage. It can run AI tests when asked, but cannot change profiles. | Orchard Core AI Agent |
| **Calendar Agent** | Finds events and free time, adds, moves and cancels events, and finds, books and changes appointments. | Calendars (appointments also need Scheduling) |

There is no ready-made agent for tenants, features, recipes or settings, because those tools can change the whole site and do not check who is asking. Tick them only on profiles that just administrators can reach.

### Choose between tools and agents

For each part of the site, give an assistant either the tools or the agent, not both.

- **Tick the tools** when the assistant needs only a few of them, or does that kind of work in most conversations. It is faster and cheaper, because nothing is handed off.
- **Tick the agent** when the assistant does many kinds of work and the area needs many tools, such as content. The agent brings its own instructions for that area, and its tools only come into play when the job needs them.

If an assistant has both the Content Agent and the content tools, it has two ways to do the same thing. It picks between them unpredictably, and you pay for both on every request.

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

<YouTube video="ai-walkthroughs" title="Video of the admin chat working with content and tools through the MCP server integration" start={425} end={500} />

1. Open **Settings > Artificial Intelligence** and find the **MCP Server** section.
2. Choose the **Authentication type** your technical team asks for: **OpenID Connect**, **API key** or **None (Anonymous access)**. Use **None** only for testing.
3. Tick the tools and tool instances to share under **Tools** and **Tool Instances**. Nothing is shared until you pick it. **Expose all tools** shares every tool; use it only when you trust every app that connects.
4. Click **Save**. The site reloads its settings.

You need the **Manage the MCP Server settings** permission. How outside apps connect is described in the [Technical Manual](../../ai/mcp/server.md).

The server can also share reusable prompts and data with those apps. Manage them under **Artificial Intelligence > Model Context Protocol > Prompts** (**Add Prompt**: a **Title**, a **Name**, a **Description**, optional **Arguments**, and the **Messages** with their **Role** and **Content**) and **Resources** (**Add Resource**, then a type such as **File**, **Content Item** or **Media**, and a **Display Text**, **Name**, **Path**, **Description** and **MIME Type**). These menus appear only with the MCP Server feature on.
