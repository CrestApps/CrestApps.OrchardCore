---
sidebar_label: A2A Client (Agent Connections)
sidebar_position: 2
title: A2A Client Integration
description: Connect to remote A2A hosts to discover and use external AI agents.
user_manual:
  - user-manual/ai/tools-and-agents
---

# A2A Client Integration

| | |
| --- | --- |
| **Feature Name** | Agent-to-Agent (A2A) Client |
| **Feature ID** | `CrestApps.OrchardCore.AI.A2A` |

The A2A Client feature allows your Orchard Core application to connect to external A2A hosts, enabling AI models to discover and communicate with remote AI agents.

---

## Managing Agent Connections

### Add a Connection

Connections are managed under **Artificial Intelligence** → **Agent to Agent Hosts** (permission `ManageA2AConnections`). Each connection has a **Title**, an **Endpoint** (the base URL of the A2A host, e.g. `https://agents.example.com`; the agent card is automatically resolved at `/.well-known/agent-card.json`), and an **Authentication** method. The admin steps are in the User Manual under [A2A connections](../../user-manual/ai/tools-and-agents.md#a2a-connections).

Each connection represents a single A2A host that may expose multiple agents through its agent card. A2A 1.0-style cards advertise protocol endpoints through `supportedInterfaces`; older cards that still expose a top-level URL are handled by the shared A2A client support.

### Authentication Types

The A2A client supports the same authentication types available for MCP SSE connections:

| Type | Description |
|------|-------------|
| **Anonymous** | No authentication (default). |
| **API Key** | Sends an API key via a configurable HTTP header with an optional prefix. |
| **Basic Authentication** | Standard HTTP Basic authentication with username and password. |
| **OAuth 2.0 Client Credentials** | Acquires a Bearer token using the client credentials grant. |
| **OAuth 2.0 + Private Key JWT** | Uses a PEM-encoded private key to sign a JWT client assertion. |
| **OAuth 2.0 + Mutual TLS (mTLS)** | Authenticates using a client certificate (PFX/PKCS#12). |
| **Custom Headers** | Sends arbitrary HTTP headers defined as a JSON object. |

Sensitive fields (API keys, passwords, secrets, private keys, certificates) are encrypted at rest using ASP.NET Core Data Protection.

For the OAuth-based options, **Token Endpoint**, **Client ID**, and **Scopes** are shared across the OAuth 2.0 auth types in the editor and are preserved when you save the connection.

---

## Assigning Agent Connections to AI Profiles

Once connections are created, you can assign them to specific AI profiles, profile templates (Profile source), chat interactions, and the **AI Completion using Direct Config** workflow task: each of these editors has an **A2A Connections** section on its **Capabilities** tab. The section only renders when at least one connection exists.

---

## How Agent Connections Work

When an AI profile has agent connections configured:

1. **Discovery**: The system fetches the agent card from each connected A2A host (cached for 15 minutes) and reads each agent endpoint from the card's supported interfaces.
2. **Tool Registration**: Each agent skill from the agent card is registered as an AI tool available to the model.
3. **Invocation**: When the AI model decides to use a remote agent, the `A2AAgentProxyTool` sends the message to the remote agent via the A2A protocol.
4. **Response**: The remote agent's response is returned to the AI model as tool output.

This means remote agents appear as callable tools to the AI model, just like local tools or MCP tools.

---

## Permissions

| Permission | Description |
|-----------|-------------|
| Manage A2A Connections | Required to create, edit, and delete A2A connections |
