---
sidebar_label: A2A Host
sidebar_position: 3
title: A2A Host
description: Expose Orchard Core Agent AI Profiles to external clients via the Agent-to-Agent protocol.
---

# A2A Host

| | |
| --- | --- |
| **Feature Name** | Agent to Agent Protocol (A2A) Host |
| **Feature ID** | `CrestApps.OrchardCore.AI.A2A.Host` |

The A2A Host feature exposes all AI Profiles of type **Agent** as discoverable agents via the Agent-to-Agent protocol. External A2A clients can discover available agents and send messages to them.

---

## Agent Exposure Modes

The A2A Host supports two modes for how agents are exposed:

### Multi-Agent Mode (Default)

Each Agent AI Profile is exposed as its own independent agent card. The `/.well-known/agent-card.json` endpoint returns a **JSON array** of agent cards. Each card has its own `url` property pointing to `/a2a?agent={agentName}`.

### Skill Mode

When `ExposeAgentsAsSkill` is enabled, a single combined agent card is returned. All Agent AI Profiles are listed as **skills** within that card. Messages are routed using the `agentName` metadata field.

---

## Endpoints

When the A2A Host feature is enabled, the following endpoints become available:

| Endpoint | Description |
|----------|-------------|
| `/.well-known/agent-card.json` | Returns agent card(s) for discovery (array in multi-agent mode, single object in skill mode) |
| `/a2a` | The A2A JSON-RPC endpoint for sending messages to agents |
| `/a2a?agent={name}` | Routes to a specific agent in multi-agent mode |

### Sending Messages

Messages sent to `/a2a` are routed to the appropriate agent based on:

1. **Query parameter routing** (multi-agent mode): The `?agent={name}` query parameter specifies the target agent.
2. **Metadata routing**: If the message includes `agentName` in its metadata, it routes to that specific agent.
3. **Default routing**: If no agent is specified, the message is routed to the first available agent.

---

## Authentication

The A2A Host supports the same authentication patterns as the MCP Server:

| Authentication Type | Description |
|---|---|
| **None** | No authentication required. Suitable for development environments. |
| **API Key** | Clients must provide an API key for access. |
| **OpenId** | Clients authenticate using OpenID Connect tokens. Enable the OrchardCore OpenIddict server feature for automatic integration. |

### Configuration

Authentication is configured via shell configuration (e.g., `appsettings.json`):

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "A2AHost": {
          "AuthenticationType": "None",
          "ApiKey": "your-api-key-here",
          "RequireAccessPermission": false,
          "ExposeAgentsAsSkill": false
        }
      }
    }
  }
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `AuthenticationType` | `None` \| `ApiKey` \| `OpenId` | `OpenId` | The authentication method for the A2A endpoint |
| `ApiKey` | `string` | | The API key value (required when using `ApiKey` authentication) |
| `RequireAccessPermission` | `bool` | `true` | When `true`, authenticated users must also have the `AccessA2AHost` permission |
| `ExposeAgentsAsSkill` | `bool` | `false` | When `true`, all agents are combined into a single card with skills instead of individual cards |

---

## Permissions

| Permission | Description |
|-----------|-------------|
| Access A2A Host | Required when `RequireAccessPermission` is enabled in host options |
