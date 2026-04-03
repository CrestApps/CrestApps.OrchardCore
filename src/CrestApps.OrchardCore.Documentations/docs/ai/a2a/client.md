---
sidebar_label: A2A Client (Agent Connections)
sidebar_position: 2
title: A2A Client Integration
description: Connect to remote A2A hosts to discover and use external AI agents.
---

# A2A Client Integration

| | |
| --- | --- |
| **Feature Name** | Agent to Agent Protocol (A2A) |
| **Feature ID** | `CrestApps.OrchardCore.AI.A2A` |

The A2A Client feature allows your Orchard Core application to connect to external A2A hosts, enabling AI models to discover and communicate with remote AI agents.

The shared framework-level A2A client is also used by the sample MVC host. In `CrestApps.Mvc.Web`, administrators can manage the same A2A host definitions and select them on AI profiles, profile templates, and chat interactions so MVC orchestration can use remote A2A agents without a separate MVC-only model or service stack.

---

## Managing Agent Connections

### Add a Connection

1. Navigate to **Artificial Intelligence** → **Agent to Agent Hosts**.
2. Click the **Add Connection** button.
3. Enter the following details:
   - **Display Text**: A descriptive name for the connection (e.g., "Production Agent Hub").
   - **Endpoint**: The base URL of the A2A host (e.g., `https://agents.example.com`). The agent card is automatically resolved at `/.well-known/agent-card.json`.
   - **Authentication**: Select the appropriate authentication method for the remote host.
4. Save the connection.

Each connection represents a single A2A host that may expose multiple agents through its agent card.

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

---

## Assigning Agent Connections to AI Profiles

Once connections are created, you can assign them to specific AI profiles, templates, or chat interactions:

### On AI Profiles

1. Navigate to the AI profile editor.
2. Go to the **Capabilities** tab.
3. Under **Agent Connections**, check the connections you want this profile to use.
4. Save the profile.

### On AI Profile Templates (Profile Sources)

1. Navigate to the AI profile template editor.
2. Go to the **Capabilities** tab.
3. Under **Agent Connections**, check the connections you want templates using this source to include.
4. Save the template.

### On Chat Interactions

1. Navigate to the chat interaction editor.
2. Go to the **Parameters** tab under **Capabilities**.
3. Under **Agent Connections**, check the connections to include for this interaction.
4. Save the interaction.

---

## How Agent Connections Work

When an AI profile has agent connections configured:

1. **Discovery**: The system fetches the agent card from each connected A2A host (cached for 15 minutes).
2. **Tool Registration**: Each agent skill from the agent card is registered as an AI tool available to the model.
3. **Invocation**: When the AI model decides to use a remote agent, the `A2AAgentProxyTool` sends the message to the remote agent via the A2A protocol.
4. **Response**: The remote agent's response is returned to the AI model as tool output.

This means remote agents appear as callable tools to the AI model, just like local tools or MCP tools.

---

## Permissions

| Permission | Description |
|-----------|-------------|
| Manage A2A Connections | Required to create, edit, and delete A2A connections |
