---
sidebar_label: Response Handlers
sidebar_position: 9
title: Response Handlers
description: Route chat responses through pluggable handlers for AI, live agent handoff, webhooks, and external systems.
---

# Response Handlers

> Pluggable components that determine how a chat message is processed — through AI, a live agent, a webhook, or a custom system.

## Quick Start

Register a custom handler:

```csharp
builder.Services.AddScoped<IChatResponseHandler, MyWebhookHandler>();
```

## Problem & Solution

Not every chat message should go to the AI. Applications need to:

- **Route to AI** for standard completion (the default)
- **Hand off to live agents** when the AI cannot help
- **Send to webhooks** for external processing
- **Relay to external platforms** (Genesys, Twilio Flex, etc.)
- **Transfer mid-conversation** between different handlers

Response handlers provide a pluggable routing layer.

## Architecture

```text
User Message
    │
    ▼
IChatResponseHandlerResolver
    │ (resolves by name)
    ▼
IChatResponseHandler
    ├── AIChatResponseHandler (default — routes to IOrchestrator)
    ├── WebhookHandler (custom — sends to external URL)
    └── LiveAgentHandler (custom — routes to agent platform)
```

## Key Interfaces

### `IChatResponseHandler`

```csharp
public interface IChatResponseHandler
{
    string Name { get; }

    Task<ChatResponseHandlerResult> HandleAsync(
        ChatResponseHandlerContext context,
        CancellationToken cancellationToken = default);
}
```

The `Name` must be unique. It is persisted on the session or interaction to identify which handler processes subsequent messages.

### `ChatResponseHandlerContext`

Contains everything the handler needs:

- **Messages** — the conversation history
- **Session** — the current chat session
- **Interaction** — the current interaction
- **Profile** — the AI profile configuration
- **HttpContext** — the current request context

### `ChatResponseHandlerResult`

Return values indicate the outcome:

| Factory Method | Meaning |
|---------------|---------|
| `ChatResponseHandlerResult.Handled()` | Message was processed successfully |
| `ChatResponseHandlerResult.Transferred()` | Conversation transferred to another handler |
| `ChatResponseHandlerResult.NotHandled()` | Handler cannot process this message |

## Handler Types

### 1. Streaming Handler (Default)

The built-in `AIChatResponseHandler` streams AI responses directly:

```text
User → Handler → Orchestrator → Streaming tokens → User
```

### 2. Deferred Webhook Handler

Sends the message to an external URL and waits for a callback:

```text
User → Handler → POST to webhook → ... → Callback → User
```

### 3. Deferred Persistent Relay

Routes through an external platform (e.g., Genesys) using a persistent connection:

```text
User → Handler → IExternalChatRelayManager → External Platform → Relay → User
```

## Implementation Example

```csharp
public sealed class WebhookResponseHandler(
    IHttpClientFactory httpClientFactory) : IChatResponseHandler
{
    public string Name => "webhook";

    public async Task<ChatResponseHandlerResult> HandleAsync(
        ChatResponseHandlerContext context,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        var payload = new
        {
            sessionId = context.Session.Id,
            message = context.Messages.Last().Text,
        };

        var response = await client.PostAsJsonAsync(
            "https://my-webhook.example.com/chat",
            payload,
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return ChatResponseHandlerResult.Handled();
        }

        return ChatResponseHandlerResult.NotHandled();
    }
}
```

Register it:

```csharp
builder.Services.AddScoped<IChatResponseHandler, WebhookResponseHandler>();
```

## Mid-Conversation Transfer

An AI tool can trigger a transfer to a different handler:

```csharp
public sealed class EscalateToAgentTool : AITool
{
    protected override Task<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        // Access the current interaction via AIInvocationScope
        var scope = AIInvocationScope.Current;
        scope.TransferToHandler("live-agent");

        return Task.FromResult<object>("Transferring to a live agent...");
    }
}
```

## Complete Webhook Handler Example

A production-ready webhook handler with retries, error handling, and timeout configuration:

```csharp
public sealed class WebhookResponseHandler(
    IHttpClientFactory httpClientFactory,
    ILogger<WebhookResponseHandler> logger) : IChatResponseHandler
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public string Name => "webhook";

    public async Task<ChatResponseHandlerResult> HandleAsync(
        ChatResponseHandlerContext context,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("WebhookHandler");
        client.Timeout = Timeout;

        var payload = new
        {
            sessionId = context.Session.Id,
            profileName = context.Profile.Name,
            message = context.Messages.Last().Text,
            userId = context.HttpContext?.User?.Identity?.Name,
            timestamp = DateTimeOffset.UtcNow,
        };

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var response = await client.PostAsJsonAsync(
                    "https://my-webhook.example.com/chat",
                    payload,
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadFromJsonAsync<WebhookResponse>(
                        cancellationToken: cancellationToken);

                    // If the webhook returned a reply, write it to the interaction
                    if (!string.IsNullOrEmpty(body?.Reply))
                    {
                        await context.WriteResponseAsync(body.Reply, cancellationToken);
                    }

                    return ChatResponseHandlerResult.Handled();
                }

                logger.LogWarning(
                    "Webhook returned {StatusCode} on attempt {Attempt}/{Max}.",
                    response.StatusCode, attempt, MaxRetries);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    "Webhook timed out on attempt {Attempt}/{Max}.", attempt, MaxRetries);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex,
                    "Webhook request failed on attempt {Attempt}/{Max}.", attempt, MaxRetries);
            }

            if (attempt < MaxRetries)
            {
                // Exponential backoff
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
            }
        }

        logger.LogError("Webhook handler failed after {MaxRetries} attempts.", MaxRetries);
        return ChatResponseHandlerResult.NotHandled();
    }

    private sealed record WebhookResponse(string Reply);
}
```

Register the handler and configure the HTTP client:

```csharp
builder.Services.AddHttpClient("WebhookHandler", client =>
{
    client.DefaultRequestHeaders.Add("X-Api-Key", "your-webhook-secret");
});

builder.Services.AddScoped<IChatResponseHandler, WebhookResponseHandler>();
```

## External Relay Implementation

The `IExternalChatRelay` interface defines a persistent, bidirectional connection to an external system (e.g., a live-agent platform like Genesys or Twilio Flex):

```csharp
public interface IExternalChatRelay : IAsyncDisposable
{
    Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default);
    Task ConnectAsync(ExternalChatRelayContext context, CancellationToken cancellationToken = default);
    Task SendPromptAsync(string text, CancellationToken cancellationToken = default);
    Task SendSignalAsync(string signalName, IDictionary<string, string> data = null, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
```

A simplified implementation for a WebSocket-based external platform:

```csharp
public sealed class GenesysRelay : IExternalChatRelay
{
    private ClientWebSocket _socket;
    private ExternalChatRelayContext _context;

    public async Task ConnectAsync(
        ExternalChatRelayContext context,
        CancellationToken cancellationToken)
    {
        _context = context;
        _socket = new ClientWebSocket();
        _socket.Options.SetRequestHeader("Authorization", $"Bearer {context.ApiKey}");

        await _socket.ConnectAsync(
            new Uri("wss://api.genesys.example.com/chat"),
            cancellationToken);

        // Start receiving messages in the background
        _ = ReceiveLoopAsync(cancellationToken);
    }

    public async Task SendPromptAsync(string text, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.SerializeToUtf8Bytes(new { type = "message", text });
        await _socket.SendAsync(message, WebSocketMessageType.Text, true, cancellationToken);
    }

    public async Task SendSignalAsync(
        string signalName,
        IDictionary<string, string> data,
        CancellationToken cancellationToken)
    {
        var signal = JsonSerializer.SerializeToUtf8Bytes(new { type = "signal", name = signalName, data });
        await _socket.SendAsync(signal, WebSocketMessageType.Text, true, cancellationToken);
    }

    public Task<bool> IsConnectedAsync(CancellationToken cancellationToken)
        => Task.FromResult(_socket?.State == WebSocketState.Open);

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        if (_socket?.State == WebSocketState.Open)
        {
            await _socket.CloseAsync(
                WebSocketCloseStatus.NormalClosure, "Session ended", cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_socket is not null)
        {
            _socket.Dispose();
            _socket = null;
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        while (_socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await _socket.ReceiveAsync(buffer, cancellationToken);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                // Forward the agent's reply back to the user through the relay manager
                await _context.WriteResponseAsync(text, cancellationToken);
            }
        }
    }
}
```

## Handler Transfer Flow

When an AI tool triggers `scope.TransferToHandler("live-agent")`, the following sequence occurs:

1. **Tool returns** — The `EscalateToAgentTool.InvokeCoreAsync` calls `AIInvocationScope.Current.TransferToHandler("live-agent")` and returns a message like "Transferring to a live agent...".
2. **Orchestrator detects transfer** — After the tool invocation, the orchestrator checks `AIInvocationScope.TransferToHandlerName`. If set, it stops sending further messages to the AI model.
3. **Handler resolver switches** — The `IChatResponseHandlerResolver` looks up the handler named `"live-agent"` from all registered `IChatResponseHandler` instances.
4. **Session state is updated** — The session's active handler name is persisted so that subsequent messages from the user are routed to the new handler.
5. **New handler receives control** — The new handler's `HandleAsync` is called. It receives the full `ChatResponseHandlerContext` including:
   - Complete conversation history (all messages from both the AI and user)
   - The current session and interaction objects
   - The AI profile configuration
6. **Subsequent messages bypass AI** — All future messages in this session go directly to the new handler until the session ends or another transfer occurs.

:::info
The transfer is **per-session**. The conversation history is preserved across the transfer so the new handler (or live agent) has full context of what was discussed with the AI.
:::

### Transferring Back to AI

A live agent handler can transfer the conversation back to the AI:

```csharp
public sealed class LiveAgentHandler : IChatResponseHandler
{
    public string Name => "live-agent";

    public async Task<ChatResponseHandlerResult> HandleAsync(
        ChatResponseHandlerContext context,
        CancellationToken cancellationToken)
    {
        // Check if the agent signals a transfer back
        var lastMessage = context.Messages.Last().Text;

        if (lastMessage.Equals("/transfer-to-ai", StringComparison.OrdinalIgnoreCase))
        {
            // Return Transferred result to hand back to the default AI handler
            return ChatResponseHandlerResult.Transferred();
        }

        // Normal live agent processing
        await ForwardToAgentPlatformAsync(context, cancellationToken);
        return ChatResponseHandlerResult.Handled();
    }
}
```

## Error Recovery

When a handler fails, the framework follows these rules:

| Scenario | Behavior |
|----------|----------|
| Handler throws an exception | The exception is caught by the orchestrator, logged, and an error response is sent to the user. The session remains on the current handler. |
| Handler returns `NotHandled()` | The framework falls back to the default AI handler for that message. The session's active handler is **not** changed. |
| Handler returns `Transferred()` | The session's handler is reset to the default (AI) handler. |
| Webhook times out | Depends on the handler implementation (see retry example above). The handler should return `NotHandled()` if all retries fail. |
| External relay disconnects | The `IExternalChatRelayManager` detects the disconnection and fires `IExternalChatRelayEventHandler.DisconnectedAsync()`. Implement this to notify the user or transfer back to AI. |

:::warning
Always implement timeout and error handling in custom handlers. A handler that hangs indefinitely will block the user's chat session. Use `CancellationToken` and set reasonable timeouts.
:::

## Orchard Core Integration

The [Response Handlers module](../ai/response-handlers.md) adds Orchard Core-specific configuration for handler selection per profile, admin UI for managing relay connections, and workflow integration for handler events.
