---
sidebar_label: Response Handlers
sidebar_position: 13
title: Chat Response Handlers
description: Extensible response handler system for routing chat prompts to AI or external backends like live agent platforms.
---

| | |
| --- | --- |
| **Feature Name** | AI Services |
| **Feature ID** | `CrestApps.OrchardCore.AI` |

## Overview

By default, all chat prompts in both **AI Chat** and **Chat Interactions** are processed by the built-in AI handler, which routes prompts through the orchestration pipeline (AI completion services). The **Chat Response Handler** system makes this extensible — you can register custom handlers that route prompts to external systems such as live agent platforms (e.g., Genesys, Twilio Flex, LivePerson) instead of AI.

This enables scenarios like:

- **Live agent handoff**: An AI function detects the user needs human support and transfers the session to a live agent queue.
- **Hybrid AI + human**: AI handles initial triage, then a human agent takes over mid-conversation.
- **Non-AI chat profiles**: Configure a profile to skip AI entirely and route all prompts to an external system from the start.

## Conversation Mode Limitation

:::warning
**Custom response handlers are not supported in Conversation mode.** When Conversation mode (`ChatMode.Conversation`) is active, the resolver always returns the built-in AI handler regardless of the session's `ResponseHandlerName`. This is by design — Conversation mode requires the AI orchestration pipeline for speech-to-text transcription, text-to-speech synthesis, and real-time audio streaming.

If a user is in Conversation mode and an AI function attempts to transfer the session to a custom handler, the transfer will still update the `ResponseHandlerName` on the session, but prompts will continue to be processed by the AI handler until the user switches out of Conversation mode.

When implementing transfer functions, check the current chat mode and reject the transfer if Conversation mode is active. See the [example below](#mid-conversation-handler-transfer) for the recommended pattern.
:::

## Architecture

### Request Flow

```
User sends prompt
  → Hub validates & saves user prompt
  → Resolves handler by session.ResponseHandlerName
  → Calls handler.HandleAsync(context)
  → Streaming: enumerate response, save assistant message, stream to client
  → Deferred: save user prompt only, return without assistant message
```

### Handler Types

| Type | Description | Example |
| --- | --- | --- |
| **Streaming** | Returns an `IAsyncEnumerable<StreamingChatCompletionUpdate>` immediately. The hub enumerates the stream and sends chunks to the client in real time. | AI handler (default) |
| **Deferred (Webhook)** | Returns `IsDeferred = true`. The hub saves the user prompt and completes without an assistant message. The response arrives later via webhook or external callback. | Live agent platforms (one-way HTTP) |
| **Deferred (Persistent Relay)** | Returns `IsDeferred = true`. The handler opens a persistent connection (WebSocket, SSE, gRPC, WebRTC, or other transport) to the external system. Events flow back in real time and are routed to notifications and messages automatically. | Live agent platforms (bidirectional) |

### Key Interfaces

- **`IChatResponseHandler`** — Processes prompts and returns a result (streaming or deferred).
- **`IChatResponseHandlerResolver`** — Resolves handler instances by name.
- **`ChatResponseHandlerContext`** — Context passed to handlers (prompt, connection ID, session, conversation history, etc.).
- **`ChatResponseHandlerResult`** — Result with `IsDeferred` flag and optional `ResponseStream`.
- **`IExternalChatRelay`** — Persistent relay connection for real-time communication with a third-party system.
- **`IExternalChatRelayManager`** — Singleton that manages relay connection lifecycles by session ID.
- **`IExternalChatRelayEventHandler`** — Routes events received from an external relay to notifications and messages.

## Creating a Custom Handler

### Step 1: Implement `IChatResponseHandler`

Create a class that implements `IChatResponseHandler`:

```csharp
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;

public sealed class GenesysResponseHandler : IChatResponseHandler
{
    // This name is used to match the handler to the session's ResponseHandlerName.
    public string Name => "Genesys";

    public async Task<ChatResponseHandlerResult> HandleAsync(
        ChatResponseHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        // Forward the prompt to the external system.
        // The external system will respond later via webhook.
        var genesysClient = context.Services.GetRequiredService<IGenesysClient>();

        await genesysClient.SendMessageAsync(new GenesysMessage
        {
            SessionId = context.SessionId,
            ConnectionId = context.ConnectionId,
            ChatType = context.ChatType.ToString(),
            Text = context.Prompt,
        });

        // Return a deferred result — the hub will NOT wait for a response.
        return ChatResponseHandlerResult.Deferred();
    }
}
```

### Step 2: Register the Handler

Register your handler in your module's `Startup.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IChatResponseHandler, GenesysResponseHandler>());
    }
}
```

### Step 3: Handle Deferred Responses (Webhook)

When the external system sends a response, create a webhook endpoint that writes the response to the chat history and notifies the user via SignalR. The payload should identify whether the chat is an **AI Chat Session** or a **Chat Interaction**, so you can resolve the correct hub context and follow the appropriate pipeline.

:::tip
The hub classes (`AIChatHub`, `ChatInteractionHub`) and their client interfaces live in the **`CrestApps.OrchardCore.AI.Chat.Core`** library. Reference this Core project (not the module projects) when you need to resolve `IHubContext<AIChatHub>` or `IHubContext<ChatInteractionHub>` from your webhook or external module.
:::

```csharp
using CrestApps.OrchardCore.AI.Chat.Hubs;
using CrestApps.OrchardCore.AI.Chat.Interactions.Hubs;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

internal static class GenesysWebhookEndpoint
{
    public static IEndpointRouteBuilder AddGenesysWebhookEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("api/genesys/webhook", HandleAsync)
            .AllowAnonymous()
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        GenesysWebhookPayload payload,
        IAIChatSessionManager sessionManager,
        IAIChatSessionPromptStore promptStore,
        IHubContext<AIChatHub> chatHubContext,
        ISourceCatalogManager<ChatInteraction> interactionManager,
        IChatInteractionPromptStore interactionPromptStore,
        IHubContext<ChatInteractionHub> interactionHubContext)
    {
        // TODO: Validate the webhook payload signature to ensure it's authentic.

        if (payload.ChatType == ChatContextType.AIChatSession)
        {
            // --- AI Chat Session pipeline ---
            var session = await sessionManager.FindByIdAsync(payload.SessionId);

            if (session is null)
            {
                return TypedResults.NotFound();
            }

            // Save the agent's response as an assistant prompt.
            var prompt = new AIChatSessionPrompt
            {
                ItemId = IdGenerator.GenerateId(),
                SessionId = session.SessionId,
                Role = ChatRole.Assistant,
                Content = payload.AgentMessage,
            };
            await promptStore.CreateAsync(prompt);

            // Notify connected client(s) via the SignalR group.
            var groupName = AIChatHub.GetSessionGroupName(session.SessionId);

            await chatHubContext.Clients.Group(groupName).SendAsync("ReceiveMessage", new
            {
                sessionId = session.SessionId,
                messageId = prompt.ItemId,
                content = payload.AgentMessage,
                role = "assistant",
            });
        }
        else if (payload.ChatType == ChatContextType.ChatInteraction)
        {
            // --- Chat Interaction pipeline ---
            var interaction = await interactionManager.FindByIdAsync(payload.SessionId);

            if (interaction is null)
            {
                return TypedResults.NotFound();
            }

            // Save the agent's response as an assistant prompt.
            var prompt = new ChatInteractionPrompt
            {
                ItemId = IdGenerator.GenerateId(),
                InteractionId = interaction.ItemId,
                Role = ChatRole.Assistant,
                Content = payload.AgentMessage,
            };
            await interactionPromptStore.CreateAsync(prompt);

            // Notify connected client(s) via the SignalR group.
            var groupName = ChatInteractionHub.GetInteractionGroupName(interaction.ItemId);

            await interactionHubContext.Clients.Group(groupName).SendAsync("ReceiveMessage", new
            {
                sessionId = interaction.ItemId,
                messageId = prompt.ItemId,
                content = payload.AgentMessage,
                role = "assistant",
            });
        }
        else
        {
            return TypedResults.BadRequest("Unknown chat type.");
        }

        return TypedResults.Ok();
    }
}
```

Register the endpoint in your module's `Startup.cs`:

```csharp
public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
{
    routes.AddGenesysWebhookEndpoint();
}
```

### Step 4: Real-Time Communication via Persistent Relay (Alternative to Webhook)

While webhooks work well for many integration scenarios, some third-party platforms support persistent connections for real-time bidirectional communication. The external chat relay infrastructure keeps a connection open, enabling instant delivery of events like typing indicators, agent-connected notifications, wait-time updates, and messages — without polling or callback endpoints.

:::tip
The relay infrastructure is available in the **`CrestApps.OrchardCore.AI.Core`** library and is protocol-agnostic — you can implement it with WebSocket, SSE, gRPC streaming, WebRTC data channels, message queues, event buses, or any other transport. The key services are:

- **`IExternalChatRelayManager`** — Singleton that manages relay connections per session.
- **`IExternalChatRelay`** — Protocol-agnostic interface for a persistent relay connection to an external system.
- **`IExternalChatRelayEventHandler`** — Routes incoming relay events to notifications and messages.
:::

#### Message Flow: Which Interface Does What

Understanding which interface handles each direction of communication is key:

| Direction | Interface | Responsibility |
| --- | --- | --- |
| **User → External App** | `IExternalChatRelay.SendPromptAsync()` | Sends the user's chat message text to the external system via the relay connection. Called by the response handler when it receives a prompt. |
| **User → External App** (signals) | `IExternalChatRelay.SendSignalAsync()` | Sends user signals (e.g., thumbs up/down, user typing, feedback) to the external system. Called by `IChatNotificationActionHandler` implementations. |
| **External App → User** (notifications) | `IExternalChatRelayEventHandler.HandleEventAsync()` | Receives events from the relay's background listener and routes them to `IChatNotificationSender` for UI notifications (typing indicators, agent connected, wait times, connection status, session ended). |
| **External App → User** (messages) | Your `IExternalChatRelay` implementation | Receives message events from the external system and writes them to the prompt store via `IAIChatSessionPromptStore`, then notifies the SignalR group via `IHubContext<AIChatHub>`. This is handled directly in your relay implementation's `DispatchEventAsync` method. |

:::note
`ExternalChatRelayEvent.EventType` is a **string**, not an enum. Well-known event types are defined as constants in `ExternalChatRelayEventTypes` (e.g., `ExternalChatRelayEventTypes.AgentTyping`). You can use **any custom string** for platform-specific events — the default handler will log and skip unrecognized types, and custom `IExternalChatRelayEventHandler` implementations can handle them.
:::

#### Extending with Custom Event Types

The relay event system is fully extensible. The `DefaultExternalChatRelayEventHandler` handles built-in event types. To handle custom events (or override built-in behavior), register your own `IExternalChatRelayEventHandler` implementation **before** the framework's default:

```csharp
// In your module's Startup.cs, register your handler before the default is registered.
// The default uses TryAddScoped, so your registration wins.
services.AddScoped<IExternalChatRelayEventHandler, MyCustomRelayEventHandler>();
```

Your custom handler can handle platform-specific events and delegate built-in events:

```csharp
public sealed class MyCustomRelayEventHandler : IExternalChatRelayEventHandler
{
    private readonly IChatNotificationSender _notifications;
    private readonly IStringLocalizer _localizer;

    public MyCustomRelayEventHandler(
        IChatNotificationSender notifications,
        IStringLocalizer<MyCustomRelayEventHandler> localizer)
    {
        _notifications = notifications;
        _localizer = localizer;
    }

    public async Task HandleEventAsync(
        string sessionId,
        ChatContextType chatType,
        ExternalChatRelayEvent relayEvent,
        CancellationToken cancellationToken = default)
    {
        switch (relayEvent.EventType)
        {
            // Handle a custom platform-specific event.
            case "supervisor-joined":
                await _notifications.SendAsync(sessionId, chatType, new ChatNotification
                {
                    Id = "supervisor-joined",
                    Type = "info",
                    Content = _localizer["A supervisor has joined the conversation."].Value,
                    Icon = "fa-solid fa-user-shield",
                    Dismissible = true,
                });
                break;

            // Handle built-in event types as needed.
            case ExternalChatRelayEventTypes.AgentTyping:
                await _notifications.ShowTypingAsync(sessionId, chatType, _localizer, relayEvent.AgentName);
                break;

            case ExternalChatRelayEventTypes.AgentStoppedTyping:
                await _notifications.HideTypingAsync(sessionId, chatType);
                break;

            case ExternalChatRelayEventTypes.AgentConnected:
                await _notifications.HideTransferAsync(sessionId, chatType);
                await _notifications.ShowAgentConnectedAsync(
                    sessionId, chatType, _localizer, relayEvent.AgentName, relayEvent.Content);
                break;

            // ... handle other built-in events as needed.
        }
    }
}
```

Since event types are strings, you can define any custom events your platform requires without modifying the framework.

#### When to Use a Persistent Relay vs. Webhook

| Aspect | Webhook | Persistent Relay |
| --- | --- | --- |
| **Connection** | External system calls your endpoint | You maintain a persistent connection |
| **Latency** | Higher (HTTP round-trip per event) | Lower (persistent connection) |
| **Typing indicators** | Require frequent HTTP calls | Native real-time delivery |
| **Complexity** | Simpler (stateless endpoints) | More complex (connection lifecycle management) |
| **Firewall** | Requires inbound endpoint access | Outbound connection only |
| **Protocols** | HTTP | WebSocket, SSE, gRPC, WebRTC, message queues, event buses, etc. |

#### Example: Implementing a WebSocket Chat Relay

The following example demonstrates `IExternalChatRelay` using WebSocket as the transport. The same interface can be implemented with any protocol (SSE, gRPC, WebRTC data channels, message queues, event buses, etc.) — only the transport layer changes.

Create a class that implements `IExternalChatRelay`. This class manages the `ClientWebSocket` connection to the third-party platform and routes incoming events back through the notification and message pipelines.

```csharp
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Chat.Hubs;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.Support;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public sealed class GenesysWebSocketRelay : IExternalChatRelay
{
    private readonly Uri _genesysUri;
    private readonly ILogger _logger;

    private ClientWebSocket _webSocket;
    private CancellationTokenSource _listenerCts;
    private string _sessionId;
    private ChatContextType _chatType;
    private IServiceProvider _services;

    public GenesysWebSocketRelay(Uri genesysUri, ILogger<GenesysWebSocketRelay> logger)
    {
        _genesysUri = genesysUri;
        _logger = logger;
    }

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public async Task ConnectAsync(
        ExternalChatRelayContext context,
        CancellationToken cancellationToken = default)
    {
        _sessionId = context.SessionId;
        _chatType = context.ChatType;
        _services = context.Services;

        _webSocket = new ClientWebSocket();
        // Add authentication headers as required by the external platform.
        _webSocket.Options.SetRequestHeader("Authorization", "Bearer <your-api-key>");

        await _webSocket.ConnectAsync(_genesysUri, cancellationToken);

        // Start a background listener for incoming messages from the external system.
        _listenerCts = new CancellationTokenSource();
        _ = Task.Run(() => ListenForEventsAsync(_listenerCts.Token), _listenerCts.Token);
    }

    public async Task SendPromptAsync(string text, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("WebSocket is not connected.");
        }

        var message = JsonSerializer.Serialize(new { type = "message", content = text });
        var bytes = Encoding.UTF8.GetBytes(message);
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    public async Task SendSignalAsync(
        string signalName,
        IDictionary<string, string> data = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            return;
        }

        var message = JsonSerializer.Serialize(new { type = "signal", signal = signalName, data });
        var bytes = Encoding.UTF8.GetBytes(message);
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_listenerCts is not null)
        {
            await _listenerCts.CancelAsync();
        }

        if (_webSocket?.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure, "Session ended", cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        _listenerCts?.Dispose();
        _webSocket?.Dispose();

        return ValueTask.CompletedTask;
    }

    private async Task ListenForEventsAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        try
        {
            while (_webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await _webSocket.ReceiveAsync(buffer, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var relayEvent = ParseEvent(json);

                if (relayEvent is null)
                {
                    continue;
                }

                await DispatchEventAsync(relayEvent, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when disconnecting.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in WebSocket listener for session '{SessionId}'.", _sessionId);
        }
    }

    private async Task DispatchEventAsync(
        ExternalChatRelayEvent relayEvent,
        CancellationToken cancellationToken)
    {
        // Route notification-type events (typing, connected, etc.) through the event handler.
        var eventHandler = _services.GetRequiredService<IExternalChatRelayEventHandler>();
        await eventHandler.HandleEventAsync(_sessionId, _chatType, relayEvent, cancellationToken);

        // Handle message events separately — save to prompt store and notify via SignalR.
        if (relayEvent.EventType == ExternalChatRelayEventTypes.Message)
        {
            await HandleMessageAsync(relayEvent);
        }
    }

    private async Task HandleMessageAsync(ExternalChatRelayEvent relayEvent)
    {
        if (_chatType == ChatContextType.AIChatSession)
        {
            var sessionManager = _services.GetRequiredService<IAIChatSessionManager>();
            var promptStore = _services.GetRequiredService<IAIChatSessionPromptStore>();
            var hubContext = _services.GetRequiredService<IHubContext<AIChatHub>>();

            var session = await sessionManager.FindByIdAsync(_sessionId);
            if (session is null)
            {
                return;
            }

            var prompt = new AIChatSessionPrompt
            {
                ItemId = IdGenerator.GenerateId(),
                SessionId = session.SessionId,
                Role = ChatRole.Assistant,
                Content = relayEvent.Content,
            };
            await promptStore.CreateAsync(prompt);

            var groupName = AIChatHub.GetSessionGroupName(session.SessionId);
            await hubContext.Clients.Group(groupName).SendAsync("ReceiveMessage", new
            {
                sessionId = session.SessionId,
                messageId = prompt.ItemId,
                content = relayEvent.Content,
                role = "assistant",
            });
        }
        // For ChatInteraction, follow the same pattern with the interaction pipeline.
    }

    private static ExternalChatRelayEvent ParseEvent(string json)
    {
        // Parse the JSON payload from the external platform into a relay event.
        // The actual structure depends on the third-party platform's protocol.
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeProp))
        {
            return null;
        }

        return typeProp.GetString() switch
        {
            ExternalChatRelayEventTypes.AgentTyping => new ExternalChatRelayEvent
            {
                EventType = ExternalChatRelayEventTypes.AgentTyping,
                AgentName = root.TryGetProperty("agent_name", out var name)
                    ? name.GetString() : null,
            },
            ExternalChatRelayEventTypes.AgentStoppedTyping => new ExternalChatRelayEvent
            {
                EventType = ExternalChatRelayEventTypes.AgentStoppedTyping,
            },
            ExternalChatRelayEventTypes.AgentConnected => new ExternalChatRelayEvent
            {
                EventType = ExternalChatRelayEventTypes.AgentConnected,
                AgentName = root.TryGetProperty("agent_name", out var agentName)
                    ? agentName.GetString() : null,
            },
            ExternalChatRelayEventTypes.AgentReconnecting => new ExternalChatRelayEvent
            {
                EventType = ExternalChatRelayEventTypes.AgentReconnecting,
                AgentName = root.TryGetProperty("agent_name", out var reconnAgent)
                    ? reconnAgent.GetString() : null,
            },
            ExternalChatRelayEventTypes.ConnectionLost => new ExternalChatRelayEvent
            {
                EventType = ExternalChatRelayEventTypes.ConnectionLost,
            },
            ExternalChatRelayEventTypes.ConnectionRestored => new ExternalChatRelayEvent
            {
                EventType = ExternalChatRelayEventTypes.ConnectionRestored,
            },
            ExternalChatRelayEventTypes.Message => new ExternalChatRelayEvent
            {
                EventType = ExternalChatRelayEventTypes.Message,
                Content = root.TryGetProperty("content", out var content)
                    ? content.GetString() : string.Empty,
                AgentName = root.TryGetProperty("agent_name", out var msgAgent)
                    ? msgAgent.GetString() : null,
            },
            ExternalChatRelayEventTypes.WaitTimeUpdated => new ExternalChatRelayEvent
            {
                EventType = ExternalChatRelayEventTypes.WaitTimeUpdated,
                Content = root.TryGetProperty("estimated_wait", out var wait)
                    ? wait.GetString() : null,
            },
            ExternalChatRelayEventTypes.SessionEnded => new ExternalChatRelayEvent
            {
                EventType = ExternalChatRelayEventTypes.SessionEnded,
                Content = root.TryGetProperty("reason", out var reason)
                    ? reason.GetString() : null,
            },
            // Any unrecognized event type is passed through as-is.
            // The default IExternalChatRelayEventHandler logs it and skips.
            // Custom IExternalChatRelayEventHandler implementations can handle it.
            _ => new ExternalChatRelayEvent
            {
                EventType = typeProp.GetString(),
                Content = json,
            },
        };
    }
}
```

#### Using the Relay in a Response Handler

Modify your response handler to use the relay manager instead of (or alongside) a one-way API call:

```csharp
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public sealed class GenesysWebSocketResponseHandler : IChatResponseHandler
{
    public string Name => "GenesysWebSocket";

    public async Task<ChatResponseHandlerResult> HandleAsync(
        ChatResponseHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        var relayManager = context.Services.GetRequiredService<IExternalChatRelayManager>();
        var loggerFactory = context.Services.GetRequiredService<ILoggerFactory>();

        // Get or create a persistent WebSocket relay for this session.
        var relay = await relayManager.GetOrCreateAsync(
            context.SessionId,
            new ExternalChatRelayContext
            {
                SessionId = context.SessionId,
                ChatType = context.ChatType,
                Services = context.Services,
            },
            () => new GenesysWebSocketRelay(
                new Uri("wss://api.genesys.example.com/chat"),
                loggerFactory.CreateLogger<GenesysWebSocketRelay>()),
            cancellationToken);

        // Forward the user's prompt to the external system via the WebSocket.
        await relay.SendPromptAsync(context.Prompt, cancellationToken);

        // Return deferred — the response will come back via the WebSocket listener.
        return ChatResponseHandlerResult.Deferred();
    }
}
```

#### Sending Signals Back to the External System

Users can also send signals (e.g., thumbs up/down, feedback) to the external system via the relay. Create an `IChatNotificationActionHandler` that forwards these signals:

```csharp
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.DependencyInjection;

public sealed class ThumbsUpActionHandler : IChatNotificationActionHandler
{
    public async Task HandleAsync(
        ChatNotificationActionContext context,
        CancellationToken cancellationToken = default)
    {
        var relayManager = context.Services.GetRequiredService<IExternalChatRelayManager>();
        var relay = relayManager.Get(context.SessionId);

        if (relay is not null && relay.IsConnected)
        {
            await relay.SendSignalAsync("thumbs-up", cancellationToken: cancellationToken);
        }

        var notifications = context.Services.GetRequiredService<IChatNotificationSender>();
        await notifications.RemoveAsync(context.SessionId, context.ChatType, context.NotificationId);
    }
}
```

#### Registering WebSocket Handler Services

Register everything in your module's `Startup.cs`:

```csharp
using CrestApps.OrchardCore.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Register the WebSocket-based response handler.
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IChatResponseHandler, GenesysWebSocketResponseHandler>());

        // Register signal action handlers.
        services.AddKeyedScoped<IChatNotificationActionHandler, ThumbsUpActionHandler>("thumbs-up");
    }
}
```

#### Cleaning Up WebSocket Connections

Close the relay when the session ends. The built-in `end-session` action handler closes the session, but you should also close the relay. Override or extend the action handler:

```csharp
public sealed class GenesysEndSessionActionHandler : IChatNotificationActionHandler
{
    public async Task HandleAsync(
        ChatNotificationActionContext context,
        CancellationToken cancellationToken = default)
    {
        // Close the WebSocket relay for this session.
        var relayManager = context.Services.GetRequiredService<IExternalChatRelayManager>();
        await relayManager.CloseAsync(context.SessionId, cancellationToken);

        // Show the session-ended notification.
        var notifications = context.Services.GetRequiredService<IChatNotificationSender>();
        var localizer = context.Services.GetRequiredService<IStringLocalizer<GenesysEndSessionActionHandler>>();
        await notifications.ShowSessionEndedAsync(context.SessionId, context.ChatType, localizer);
    }
}
```

## Mid-Conversation Handler Transfer

An AI function can transfer the session to a different handler mid-conversation. Implement the transfer as a proper `AIFunction` so it integrates with the AI tool registration system.

### Step 1: Create the Transfer Function

The function accesses the current `AIChatSession` or `ChatInteraction` via `AIInvocationScope.Current` and sets the `ResponseHandlerName`. The hub automatically saves the session after the AI response completes, so you do **not** need to call `SaveAsync` manually.

```csharp
using System.Text.Json;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MyModule.Tools;

/// <summary>
/// AI tool that transfers the chat session to a live support agent
/// via an external platform (e.g., Genesys).
/// </summary>
public sealed class TransferToAgentFunction : AIFunction
{
    public const string TheName = "transfer_to_live_agent";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "queue_name": {
              "type": "string",
              "description": "The name of the agent queue to transfer the user to."
            },
            "reason": {
              "type": "string",
              "description": "A brief summary of why the user is being transferred."
            }
          },
          "required": ["queue_name"],
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description => "Transfers the user to a live support agent in a specified queue.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var logger = arguments.Services.GetRequiredService<ILogger<TransferToAgentFunction>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", Name);
        }

        if (!arguments.TryGetFirstString("queue_name", out var queueName))
        {
            return "Unable to find a 'queue_name' argument.";
        }

        arguments.TryGetFirstString("reason", out var reason);

        // Get the current session or interaction from the invocation context.
        var invocationScope = AIInvocationScope.Current;
        string sessionId = null;
        ChatContextType chatType;

        // Check for an AI Chat Session.
        if (invocationScope?.Items.TryGetValue(nameof(AIChatSession), out var sessionObj) == true
            && sessionObj is AIChatSession chatSession)
        {
            chatType = ChatContextType.AIChatSession;
            sessionId = chatSession.SessionId;

            // Check if the session is in Conversation mode.
            // Custom handlers cannot process voice audio streams.
            var profileManager = arguments.Services.GetRequiredService<IAIProfileManager>();
            var profile = await profileManager.FindByIdAsync(chatSession.ProfileId);

            if (profile != null
                && profile.TryGetSettings<ChatModeProfileSettings>(out var chatModeSettings)
                && chatModeSettings.ChatMode == ChatMode.Conversation)
            {
                return "Live agent transfer is not available in Conversation mode. "
                     + "Please switch to text mode to connect with a live agent.";
            }

            // Set the handler name. The hub will persist this automatically
            // when it saves the session after the AI response completes.
            chatSession.ResponseHandlerName = "Genesys";
        }
        // Check for a Chat Interaction.
        else if (invocationScope?.ToolExecutionContext?.Resource is ChatInteraction interaction)
        {
            chatType = ChatContextType.ChatInteraction;
            sessionId = interaction.ItemId;

            // Set the handler name. The hub will persist this automatically.
            interaction.ResponseHandlerName = "Genesys";
        }
        else
        {
            logger.LogWarning("AI tool '{ToolName}' failed: no active chat session or interaction found.", Name);
            return "Unable to transfer — no active chat session found.";
        }

        // Create a session in the external system (e.g., Genesys).
        var genesysClient = arguments.Services.GetRequiredService<IGenesysClient>();

        await genesysClient.CreateSessionAsync(new GenesysSessionRequest
        {
            SessionId = sessionId,
            ChatType = chatType,
            QueueName = queueName,
            Reason = reason,
            // Pass the SignalR connection ID so the external system can send messages back.
            ConnectionId = invocationScope?.Items.TryGetValue("ConnectionId", out var connId) == true
                ? connId as string
                : null,
        });

        return $"You are being transferred to a live agent in the '{queueName}' queue. Please wait...";
    }
}
```

### Step 2: Register the Transfer Function

Register the function as a selectable AI tool in your module's `Startup.cs`:

```csharp
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<TransferToAgentFunction>(TransferToAgentFunction.TheName);
    }
}
```

Once registered, the tool is automatically available to all AI profiles. Since it is not selectable, administrators do not need to manually enable it — it will be included whenever the AI model requires it.

### How It Works

1. The AI model decides to call `transfer_to_live_agent` based on the conversation context.
2. The function accesses the current `AIChatSession` or `ChatInteraction` via `AIInvocationScope.Current`.
3. It sets `ResponseHandlerName = "Genesys"` on the session/interaction object.
4. The hub automatically saves the updated session after the AI response completes — no manual `SaveAsync` call needed.
5. From this point on, all subsequent user prompts are routed to the `GenesysResponseHandler` instead of AI.

## Configuring the Initial Response Handler

### Via AI Profile Settings

In the admin panel, navigate to **AI → Profiles → Edit** and look for the **Response Handling** section. The **Initial Response Handler** dropdown appears when at least one non-AI handler is registered.

Setting this to a specific handler means new sessions created from this profile bypass AI entirely and route all prompts to the selected handler from the start.

### Via AI Profile Template

When editing an AI Profile Template (Profile source), the **Initial Response Handler** dropdown appears in the **Connection** section alongside the orchestrator and connection name settings.

### Via Code

Set the initial handler in profile settings programmatically:

```csharp
profile.AlterSettings<ResponseHandlerProfileSettings>(settings =>
{
    settings.InitialResponseHandlerName = "Genesys";
});
```

### Via Chat UI

When multiple response handlers are registered, the chat UI displays a **Response Handler** dropdown in the placeholder area before a session starts. Users can select which handler to use for their session, overriding the profile's default.

## SignalR Groups for Deferred Responses

When a deferred handler is used, clients are automatically added to a SignalR group for the session. This allows external systems to send messages to all clients connected to a specific session, even after reconnection.

| Chat Type | Group Name Pattern |
| --- | --- |
| AI Chat Session | `aichat-session-{sessionId}` |
| Chat Interaction | `chat-interaction-{itemId}` |

Clients also join the group when loading an existing session, so reconnecting users still receive deferred messages.

## Handler Context Properties

The `ChatResponseHandlerContext` provides rich context to handlers:

| Property | Type | Description |
| --- | --- | --- |
| `Prompt` | `string` | The user's message text. |
| `ConnectionId` | `string` | The SignalR connection ID of the client. |
| `SessionId` | `string` | The session or interaction ID. |
| `ChatType` | `ChatContextType` | Either `AIChatSession` or `ChatInteraction`. |
| `ConversationHistory` | `IList<ChatMessage>` | Previous messages in the conversation. |
| `Services` | `IServiceProvider` | Scoped service provider for resolving dependencies. |
| `Profile` | `AIProfile` | The AI profile (available for AI Chat Sessions). |
| `ChatSession` | `AIChatSession` | The chat session (available for AI Chat Sessions). |
| `Interaction` | `ChatInteraction` | The interaction (available for Chat Interactions). |
| `Properties` | `Dictionary<string, object>` | Extensible property bag for passing data between handler and hub. |

## Default AI Handler

The built-in AI handler (`AIChatResponseHandler`) wraps the existing orchestration pipeline. It:

1. Builds the orchestration context using `IOrchestrationContextBuilder`.
2. Resolves the orchestrator via `IOrchestratorResolver`.
3. Executes streaming completion.
4. Stores the `OrchestrationContext` in `context.Properties["OrchestrationContext"]` for citation collection by the hub.

The AI handler's name is `"AI"`. When a session's `ResponseHandlerName` is `null` or empty, the resolver defaults to the AI handler.

## Handler Resolution Logic

The `IChatResponseHandlerResolver.Resolve()` method accepts an optional `ChatMode` parameter:

1. If `chatMode` is `ChatMode.Conversation`, the AI handler is **always** returned.
2. If `handlerName` is `null` or empty, the AI handler is returned.
3. If a handler matching `handlerName` is found, it is returned.
4. If no match is found, a warning is logged and the AI handler is returned as a fallback.

```csharp
// Resolve with conversation mode awareness
var handler = handlerResolver.Resolve(session.ResponseHandlerName, ChatMode.TextInput);
```

## UI Notifications for Response Handlers

When building deferred response handlers, you typically need to provide real-time feedback to users — typing indicators, transfer status, agent-connected notifications, and session endings. The **[Chat UI Notifications](./chat-notifications.md)** system provides a C#-only API for this:

```csharp
// In your webhook or WebSocket handler that receives external events:
var notifications = services.GetRequiredService<IChatNotificationSender>();
var localizer = services.GetRequiredService<IStringLocalizer<MyHandler>>();

// Show a typing indicator when the agent starts typing.
await notifications.ShowTypingAsync(sessionId, ChatContextType.AIChatSession, localizer, "Mike");

// Show a transfer indicator with wait time and cancel button.
await notifications.ShowTransferAsync(sessionId, ChatContextType.AIChatSession, localizer,
    estimatedWaitTime: "About 2 minutes",
    cancellable: true);

// When the agent connects, hide the transfer indicator and show agent-connected.
await notifications.HideTransferAsync(sessionId, ChatContextType.AIChatSession);
await notifications.ShowAgentConnectedAsync(sessionId, ChatContextType.AIChatSession, localizer, "Mike");

// End the session and notify the user.
await notifications.ShowSessionEndedAsync(sessionId, ChatContextType.AIChatSession, localizer);
```

See [Chat UI Notifications](./chat-notifications.md) for full documentation and examples.
