---
sidebar_label: Agent-to-Agent (A2A)
title: Agents and Agent-to-Agent (A2A)
description: Create specialist Agent profiles, offer them to other systems, and let your assistants call the agents of other systems.
technical_manual:
  - ai/a2a/index
  - ai/a2a/host
  - ai/a2a/client
---

Some jobs are best done by a specialist. An **Agent** is an AI profile with one job, its own instructions and its own tools. Another assistant hands a job to it and uses its answer. The **Agent-to-Agent (A2A)** protocol lets agents on different systems find each other and work together: your site can offer its agents to other systems, and your assistants can call the agents of other systems.

| | |
| --- | --- |
| **Menu** | Artificial Intelligence > Profiles (create agents, and choose them on the **Capabilities** tab); Artificial Intelligence > Agent to Agent Hosts |
| **Permission** | Manage AI profiles; Manage Agent-to-Agent Connections; Access the A2A Host (for callers of your agents) |
| **Feature** | Agent-to-Agent (A2A) Client; Agent-to-Agent (A2A) Host |

<AskYourAdmin />

<YouTube video="ai-a2a" title="Video overview of agents and the Agent-to-Agent protocol" />

The video covers why specialist agents help, how Agent profiles, the A2A Host and the A2A Client fit together, and each task below.

Each part is its own feature, so you only see the menus of the features that are turned on: **Agent to Agent Hosts** comes with the A2A Client.

## Create an agent

<YouTube video="ai-a2a" title="Video of creating an agent" start={91} end={141} />

1. Open **Artificial Intelligence > Profiles** and click **Add Profile**.
2. In the **New AI Profile** window, click **Start** on **Blank profile**. The other cards are ready-made profiles, including agents you can start from.
3. Enter a **Title**, and set the **Profile type** to **Agent**. The **Description** and **Availability** appear.
4. Write the **Description** for the other assistants. It says exactly what the agent does and when to call it, and it is how they decide.
5. Choose the **Availability**:
   - **On demand**: the agent is only included when it fits the request.
   - **Always available**: the agent is included in every AI request. This uses more tokens and costs more; use it only for agents every assistant must be able to reach.
6. Under **System instructions**, tell the agent how to behave, and click **Save**.

To try an agent on its own, use **Invoke Profile** from its **Actions** menu (see [Chat with an AI assistant](chat.md#try-out-a-utility-or-agent-profile)).

## Offer your agents to other systems

<YouTube video="ai-a2a" title="Video of how the A2A Host offers your agents" start={141} end={185} />

With the **Agent-to-Agent (A2A) Host** feature on, your site offers every **Agent** profile to other systems, which discover them in the site's agent card, at `/.well-known/agent-card.json`, and send them messages to the single A2A address of the site, `/a2a`. Your technical team chooses how callers sign in (no sign-in for testing, an API key, or OpenID Connect). When the host asks for it, a caller also needs the **Access the A2A Host** permission. See the [Technical Manual](../../ai/a2a/host.md).

## Connect to another system

<YouTube video="ai-a2a" title="Video of adding an A2A connection" start={185} end={234} />

1. Open **Artificial Intelligence > Agent to Agent Hosts** and click **Add Connection**.
2. Enter a **Title** and the **Endpoint**, the address of the other system's A2A host. The host of a CrestApps site answers at the site's address followed by `/a2a`.
3. Choose the **Authentication**. The options are the same as for an [MCP connection](mcp.md#choose-how-to-sign-in): **Anonymous (No Authentication)**, **API Key**, **Basic Authentication**, three **OAuth 2.0** options and **Custom Headers (Advanced)**. **Anonymous** suits an open host or a test.
4. Click **Save**.

The site reads the agent card of the host when an assistant first uses the connection, so a wrong address shows up the first time an assistant calls the agent. The list shows each connection with its endpoint.

## Give an assistant the agents

<YouTube video="ai-a2a" title="Video of giving an assistant the agents" start={234} end={262} />

1. Open **Artificial Intelligence > Profiles**, edit the profile of the assistant, and open the **Capabilities** tab.
2. Under **A2A Connections**, tick the connection. Each agent that the host offers becomes something the assistant can call.
3. Under **Agents**, tick the agents of this site that the assistant may call. Always-available agents are already included, and the tab says how many there are.
4. Click **Save**.

Profile templates and chat interactions have the same lists.

<YouTube video="ai-a2a" title="Video of asking a specialist" start={262} end={277} />

Then ask the assistant something for the specialist. It hands the question to the agent and answers with what the agent returned.

## Use cases

- **Specialists.** A legal, finance or warranty agent that answers in its own area, with its own instructions.
- **Partners.** A supplier or a sister company offers an agent, and your assistants call it.
- **Reuse.** One agent serves many assistants, so you keep its instructions in one place.
- **Care.** Describe each agent precisely, and give an assistant only the agents it needs.

## Related

- [Tools, agents and connections](tools-and-agents.md): built-in tools, tool instances and MCP connections.
- [Model Context Protocol (MCP)](mcp.md)
- [Profiles](profiles.md): the **Capabilities** tab.
