---
sidebar_label: SignalR
sidebar_position: 11
title: SignalR Hub Management
description: Centralized SignalR hub route registration and URL generation with multi-tenant path prefix support.
---

# SignalR Hub Management

> Centralized hub route registration and URL generation with support for multi-tenant path prefixes.

## Quick Start

```csharp
builder.Services.AddCrestAppsSignalR();
```

## Why This Abstraction?

In a standard ASP.NET Core application, SignalR hub paths are hardcoded at startup:

```csharp
app.MapHub<ChatHub>("/chatHub");
```

This works fine for a single-tenant app. But in Orchard Core's **multi-tenant architecture**, each tenant has its own URL prefix (e.g., `/tenant-a`, `/tenant-b`). Without centralized route management, every module that registers a hub would need to:

1. Discover the current tenant's URL prefix
2. Build the correct hub path with that prefix
3. Expose the full URL to client-side JavaScript for connection

The `HubRouteManager` solves all three problems by providing a single service that:

- **Centralizes path construction** — one place handles the prefix logic
- **Generates correct URLs** — both relative paths (for `MapHub`) and absolute URIs (for JavaScript clients)
- **Prevents path conflicts** — all hubs follow the same `/Communication/Hub/{HubName}` pattern

Without this, you would see bugs like tenant A's JavaScript connecting to tenant B's hub, or paths breaking after deployment behind a reverse proxy.

## Problem & Solution

SignalR hubs need consistent route registration and URL generation across features. In multi-tenant environments, hub paths must include a tenant prefix. The `HubRouteManager` centralizes this so individual features don't manage paths independently.

## Real-time Chat Example

The primary consumer of `HubRouteManager` is the AI Chat system. Here is how the pieces fit together:

### Server-side: Hub Registration

```csharp
// In the Chat module's Startup.Configure():
public override void Configure(
    IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
{
    // Maps the AIChatHub at /Communication/Hub/AIChatHub (with tenant prefix)
    HubRouteManager.MapHub<AIChatHub>(routes);
}
```

### Server-side: URL Generation for Client

```csharp
// In a Razor view or controller, generate the hub URL for the client:
public class ChatController(HubRouteManager hubRouteManager)
{
    public IActionResult Index()
    {
        var hubUrl = hubRouteManager.GetUriByHub<AIChatHub>(HttpContext);
        // Returns: "https://example.com/tenant-a/Communication/Hub/AIChatHub"

        ViewBag.ChatHubUrl = hubUrl;
        return View();
    }
}
```

### Client-side: JavaScript Connection

```javascript
// Connect to the hub using the URL generated server-side
const connection = new signalR.HubConnectionBuilder()
    .withUrl(chatHubUrl) // URL from the server
    .withAutomaticReconnect()
    .build();

// Listen for streamed AI responses
connection.on("ReceiveMessage", (update) => {
    appendMessage(update.text);
});

// Send a message
await connection.invoke("SendMessage", {
    profileId: "my-profile",
    sessionId: sessionId,
    message: userInput,
});

await connection.start();
```

### How Streaming Works

When a user sends a message, the `AIChatHub` processes it through the response handler pipeline. For the default AI handler, the response is streamed token-by-token:

```text
Client                          AIChatHub                       Orchestrator
  │                                │                                │
  │── SendMessage(msg) ──────────▶│                                │
  │                                │── ExecuteStreamingAsync() ───▶│
  │                                │                                │
  │                                │◀── ChatResponseUpdate ────────│
  │◀── ReceiveMessage(chunk1) ────│                                │
  │                                │◀── ChatResponseUpdate ────────│
  │◀── ReceiveMessage(chunk2) ────│                                │
  │                                │◀── (stream complete) ─────────│
  │◀── MessageCompleted ──────────│                                │
```

## Hub Registration

### Registering a Custom Hub

To register your own SignalR hub that works correctly in multi-tenant environments:

**1. Define your hub:**

```csharp
public sealed class NotificationHub : Hub
{
    public async Task Subscribe(string topic)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, topic);
    }

    public async Task Broadcast(string topic, string message)
    {
        await Clients.Group(topic).SendAsync("Notify", message);
    }
}
```

**2. Map it using `HubRouteManager`:**

```csharp
public sealed class Startup : StartupBase
{
    public override void Configure(
        IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        // The static MapHub<T> method uses the default path pattern
        HubRouteManager.MapHub<NotificationHub>(routes);
        // Maps to: /{tenant-prefix}/Communication/Hub/NotificationHub
    }
}
```

**3. Generate the URL for clients:**

```csharp
public class MyService(HubRouteManager hubRouteManager)
{
    public string GetNotificationHubUrl(HttpContext httpContext)
    {
        return hubRouteManager.GetUriByHub<NotificationHub>(httpContext);
    }
}
```

## Scale-out with Redis Backplane

By default, SignalR keeps all connection state in-memory on a single server. In a multi-server deployment, messages sent on one server won't reach clients connected to another server.

The solution is a **Redis backplane**, which broadcasts SignalR messages across all servers:

```text
Server 1 ──┐                ┌── Server 2
            │                │
            ▼                ▼
         ┌─────────────────────┐
         │    Redis Backplane   │
         └─────────────────────┘
```

### Enabling Redis in Orchard Core

Orchard Core has a built-in Redis module. When enabled, it automatically configures SignalR to use Redis as a backplane.

Configure the Redis connection in your environment:

```json title="appsettings.json"
{
  "OrchardCore": {
    "OrchardCore_Redis": {
      "Configuration": "localhost:6379,allowAdmin=true"
    }
  }
}
```

Or via environment variables:

```bash
OrchardCore__OrchardCore_Redis__Configuration=localhost:6379,allowAdmin=true
```

:::info
When using the Aspire AppHost for local development, Redis is configured automatically as part of the orchestration. See the Aspire project at `src/Startup/CrestApps.Aspire.AppHost/`.
:::

### When You Need Scale-out

- **Single server**: No backplane needed. SignalR works out of the box.
- **Multiple servers behind a load balancer**: Redis backplane required for message delivery across servers.
- **Azure App Service with multiple instances**: Enable Redis or use Azure SignalR Service.

## Services Registered by `AddCrestAppsSignalR()`

| Service | Implementation | Lifetime | Purpose |
|---------|---------------|----------|---------|
| `HubRouteManager` | — | Singleton | Hub route registration and URL generation |

## Configuration

### Path Prefix (Multi-Tenant)

```csharp
builder.Services.AddCrestAppsSignalR(pathPrefix: "/tenant-a");
```

All hub routes will be prefixed with the tenant path.

## Using the Hub Route Manager

### Mapping Hubs

```csharp
var app = builder.Build();

// Map hubs during endpoint routing
app.MapHub<AIChatHub>(
    app.Services.GetRequiredService<HubRouteManager>().GetPathByHub<AIChatHub>());
```

### Generating URLs

```csharp
public class MyService(HubRouteManager hubRouteManager)
{
    public string GetChatHubUrl(HttpContext httpContext)
    {
        return hubRouteManager.GetUriByHub<AIChatHub>(httpContext);
        // Returns: "https://example.com/tenant-a/Communication/Hub/AIChatHub"
    }
}
```

### Default Hub Path Pattern

```text
/Communication/Hub/{HubName}
```

With a prefix of `/tenant-a`:

```text
/tenant-a/Communication/Hub/{HubName}
```

## Key Methods

| Method | Description |
|--------|-------------|
| `GetPathByHub<T>()` | Get the route path for a hub type |
| `GetPathByRoute(pattern)` | Get the route path with prefix applied |
| `GetUriByHub<T>(httpContext)` | Full URI including scheme and host |
| `GetUriByRoute(httpContext, pattern)` | Full URI for a custom route pattern |

## Orchard Core Integration

The [SignalR module](../modules/signalr.md) registers `AddCrestAppsSignalR()` automatically with the current tenant's path prefix and maps chat hubs for AI Chat Interactions.
