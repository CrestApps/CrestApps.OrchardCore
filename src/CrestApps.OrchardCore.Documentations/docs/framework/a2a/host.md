---
sidebar_label: A2A Host
sidebar_position: 3
title: A2A Host
description: Expose your AI agents to remote clients using the A2A protocol host — configuration, authentication modes, agent card generation, and skill exposure.
---

:::info Canonical framework docs
The shared framework guidance now lives in **[CrestApps.Core](https://core.crestapps.com/docs/framework/a2a/host)**. This Orchard Core page is kept for Orchard-specific integration context and cross-links.
:::

# A2A Host

> Expose your AI agents to remote A2A clients so they can discover and invoke your agents over HTTP.

## Quick Start

```csharp
builder.Services.Configure<A2AHostOptions>(options =>
{
    options.AuthenticationType = A2AHostAuthenticationType.ApiKey;
    options.ApiKey = "your-secret-api-key";
});
```

## Problem & Solution

You have AI agents (profiles) running in your application and you want other applications to be able to discover and invoke them. The A2A host configuration:

- **Publishes** your agents via Agent Cards at a well-known endpoint
- **Authenticates** incoming requests using OpenID Connect, API keys, or no auth
- **Authorizes** access with optional permission checks
- **Controls** whether agents appear as individual agent cards or as skills of a single combined card

The framework provides the configuration models and option types. The actual HTTP endpoints and agent card generation are implemented by your application layer (or the Orchard Core A2A Host module).

## Host Configuration

### `A2AHostOptions`

All host behavior is controlled through `A2AHostOptions`:

```csharp
public sealed class A2AHostOptions
{
    /// The authentication type for incoming A2A requests.
    /// Default: OpenId
    public A2AHostAuthenticationType AuthenticationType { get; set; }
        = A2AHostAuthenticationType.OpenId;

    /// The API key required when AuthenticationType is ApiKey.
    public string ApiKey { get; set; }

    /// Whether to require the AccessA2AHost permission.
    /// Only applies to OpenId authentication. Default: true
    public bool RequireAccessPermission { get; set; } = true;

    /// Whether to expose all agents as skills of a single combined agent card.
    /// When false (default), each agent gets its own agent card.
    public bool ExposeAgentsAsSkill { get; set; } = false;
}
```

### Configuration via `IServiceCollection`

```csharp
// In Program.cs or Startup.cs
builder.Services.Configure<A2AHostOptions>(options =>
{
    options.AuthenticationType = A2AHostAuthenticationType.OpenId;
    options.RequireAccessPermission = true;
});
```

### Configuration via `appsettings.json`

```json
{
  "A2AHost": {
    "AuthenticationType": "ApiKey",
    "ApiKey": "your-secret-api-key",
    "RequireAccessPermission": true,
    "ExposeAgentsAsSkill": false
  }
}
```

```csharp
builder.Services.Configure<A2AHostOptions>(
    builder.Configuration.GetSection("A2AHost"));
```

## Authentication Modes

The host supports three authentication types via `A2AHostAuthenticationType`:

### OpenID Connect (`OpenId`) — Default

The most secure option for production. Incoming requests are authenticated using the `"Api"` OpenID Connect scheme. Tokens are validated against your OpenID provider.

```csharp
builder.Services.Configure<A2AHostOptions>(options =>
{
    options.AuthenticationType = A2AHostAuthenticationType.OpenId;
    options.RequireAccessPermission = true; // Require AccessA2AHost permission
});
```

When `RequireAccessPermission` is `true`, the authenticated user must also have the `AccessA2AHost` permission. When `false`, any valid authenticated user can access the host.

:::tip
In Orchard Core, configure the OpenID server module and assign the `AccessA2AHost` permission to the appropriate roles.
:::

### API Key (`ApiKey`)

A simple shared-secret authentication. The client must send the API key in the `Authorization` header:

```text
Authorization: Bearer your-secret-api-key
```

```csharp
builder.Services.Configure<A2AHostOptions>(options =>
{
    options.AuthenticationType = A2AHostAuthenticationType.ApiKey;
    options.ApiKey = "your-secret-api-key";
});
```

:::warning
Store the API key in a secure location (environment variable, Azure Key Vault, etc.). Never hardcode it in source code. The `RequireAccessPermission` option does **not** apply to API key authentication.
:::

### None — Development Only

Disables all authentication. Any request is accepted.

```csharp
builder.Services.Configure<A2AHostOptions>(options =>
{
    options.AuthenticationType = A2AHostAuthenticationType.None;
});
```

:::danger
**Never use `None` in production.** This option exists solely for local development and testing.
:::

### Authentication Comparison

| Feature | OpenId | ApiKey | None |
|---------|--------|--------|------|
| **Security level** | High | Medium | ❌ None |
| **Token validation** | ✅ JWT/OIDC | ❌ Shared secret | ❌ |
| **User identity** | ✅ Full claims | ❌ Anonymous | ❌ Anonymous |
| **Permission checks** | ✅ Optional | ❌ | ❌ |
| **Best for** | Production | Internal/partner APIs | Local dev |

## Agent Card Generation

### How Profiles Become Agent Cards

When a remote client fetches the Agent Card from your host, the host implementation reads your AI profiles and converts them into the A2A Agent Card format:

```text
┌──────────────┐         ┌──────────────┐         ┌──────────────────────┐
│  AI Profile  │────────►│  Agent Card  │────────►│ /.well-known/        │
│  (your app)  │         │  Generator   │         │ agent.json           │
│              │         │              │         │                      │
│ Name         │         │ Name         │         │ Published to remote  │
│ Description  │         │ Description  │         │ A2A clients          │
│ Type: Agent  │         │ Skills []    │         │                      │
└──────────────┘         └──────────────┘         └──────────────────────┘
```

Each AI profile of type `Agent` becomes either:
- An **independent Agent Card** (default behavior), or
- A **skill on a combined Agent Card** (when `ExposeAgentsAsSkill` is `true`)

### Agent Card Structure (A2A Protocol)

A published Agent Card follows the A2A specification:

```json
{
  "name": "My AI Assistant",
  "description": "An AI assistant that can help with various tasks.",
  "url": "https://myapp.example.com/a2a",
  "skills": [
    {
      "id": "translate-text",
      "name": "Text Translator",
      "description": "Translates text between languages.",
      "tags": ["translation", "language"]
    },
    {
      "id": "summarize-document",
      "name": "Document Summarizer",
      "description": "Summarizes long documents into key points.",
      "tags": ["summarization", "documents"]
    }
  ]
}
```

## Skill Exposure

### Individual Agent Cards (Default)

When `ExposeAgentsAsSkill` is `false` (the default), each agent profile is exposed as its own independent Agent Card. Remote clients see separate agents and can invoke them individually.

```text
Profile: "Code Reviewer"   →  Agent Card: { name: "Code Reviewer", skills: [...] }
Profile: "Translator"      →  Agent Card: { name: "Translator", skills: [...] }
```

This is the recommended approach when your agents are independent and serve different purposes.

### Combined Agent Card

When `ExposeAgentsAsSkill` is `true`, a single Agent Card is published with each agent profile listed as a skill:

```text
Combined Agent Card: {
  name: "My Application",
  skills: [
    { id: "code-reviewer", name: "Code Reviewer", ... },
    { id: "translator", name: "Translator", ... }
  ]
}
```

This approach is useful when:
- You want remote clients to see a **single entry point** to your application
- The client's AI model should choose which skill to invoke based on the task
- You want to simplify discovery for clients that don't need to manage multiple connections

```csharp
builder.Services.Configure<A2AHostOptions>(options =>
{
    options.ExposeAgentsAsSkill = true;
});
```

## Endpoint Setup

The A2A protocol defines two key endpoints that your host must serve:

### Agent Card Endpoint

Remote clients discover your agents by fetching the Agent Card:

```text
GET /.well-known/agent.json
```

This returns the Agent Card JSON (or multiple cards, depending on your `ExposeAgentsAsSkill` setting).

### Message Endpoint

Remote clients send tasks to your agents via:

```text
POST /a2a
Content-Type: application/json

{
  "message": {
    "role": "user",
    "messageId": "msg-123",
    "contextId": "ctx-456",
    "parts": [{ "type": "text", "text": "Translate this to French: Hello world" }]
  }
}
```

The host routes the message to the appropriate AI profile, processes it, and returns a response.

### Implementation Pattern

The actual endpoint implementation depends on your application framework. Here is a conceptual pattern:

```csharp
// Agent Card endpoint
app.MapGet("/.well-known/agent.json", async (
    IOptions<A2AHostOptions> options,
    IAIProfileManager profileManager) =>
{
    // 1. Load agent profiles
    // 2. Convert to Agent Card format
    // 3. Return JSON
});

// Message endpoint
app.MapPost("/a2a", async (
    HttpContext context,
    IOptions<A2AHostOptions> options,
    IAICompletionService completionService) =>
{
    // 1. Authenticate the request based on A2AHostOptions
    // 2. Parse the incoming AgentMessage
    // 3. Route to the appropriate AI profile
    // 4. Process and return the response
});
```

:::info
The Orchard Core A2A Host module implements these endpoints with full authentication, routing, and response handling. See below for the integration link.
:::

## Security Best Practices

1. **Always use OpenID or API Key authentication in production** — never deploy with `AuthenticationType = None`
2. **Rotate API keys regularly** — treat them as secrets with a defined rotation policy
3. **Use `RequireAccessPermission = true`** with OpenID — this ensures only authorized users/applications can invoke your agents
4. **Restrict agent exposure** — only expose agent profiles that are intended for remote consumption
5. **Monitor agent invocations** — log and audit incoming A2A requests
6. **Use HTTPS** — the A2A protocol sends messages over HTTP; always use TLS in production

## Orchard Core Integration

The [A2A Host module](../../ai/a2a/host.md) provides:

- Full endpoint implementation (Agent Card + message endpoints)
- Admin UI for configuring `A2AHostOptions`
- Automatic conversion of AI profiles to Agent Cards
- Built-in authentication middleware for all three modes
- Permission management for the `AccessA2AHost` permission
- Support for both individual and combined Agent Card modes
