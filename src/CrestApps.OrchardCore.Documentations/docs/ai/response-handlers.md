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
| **Deferred** | Returns `IsDeferred = true`. The hub saves the user prompt and completes without an assistant message. The response arrives later via webhook or external callback. | Live agent platforms |

### Key Interfaces

- **`IChatResponseHandler`** — Processes prompts and returns a result (streaming or deferred).
- **`IChatResponseHandlerResolver`** — Resolves handler instances by name.
- **`ChatResponseHandlerContext`** — Context passed to handlers (prompt, connection ID, session, conversation history, etc.).
- **`ChatResponseHandlerResult`** — Result with `IsDeferred` flag and optional `ResponseStream`.

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

When the external system sends a response, create a webhook endpoint that writes the response to the chat session and notifies the user via SignalR:

```csharp
using CrestApps.OrchardCore.AI.Chat.Hubs;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;

[Route("api/genesys/webhook")]
public sealed class GenesysWebhookController : Controller
{
    private readonly IAIChatSessionManager _sessionManager;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly IHubContext<AIChatHub> _hubContext;
    private readonly IClock _clock;

    public GenesysWebhookController(
        IAIChatSessionManager sessionManager,
        IAIChatSessionPromptStore promptStore,
        IHubContext<AIChatHub> hubContext,
        IClock clock)
    {
        _sessionManager = sessionManager;
        _promptStore = promptStore;
        _hubContext = hubContext;
        _clock = clock;
    }

    [HttpPost]
    public async Task<IActionResult> ReceiveMessage([FromBody] GenesysWebhookPayload payload)
    {
        // 1. Find the chat session.
        var session = await _sessionManager.FindByIdAsync(payload.SessionId);
        if (session == null)
        {
            return NotFound();
        }

        // 2. Save the agent's response as an assistant prompt.
        var prompt = new AIChatSessionPrompt
        {
            ItemId = IdGenerator.GenerateId(),
            SessionId = session.SessionId,
            Role = ChatRole.Assistant,
            Content = payload.AgentMessage,
        };
        await _promptStore.CreateAsync(prompt);

        // 3. Notify the connected client(s) via SignalR group.
        // The group name follows the pattern used by AIChatHub.
        var groupName = $"aichat-session-{session.SessionId}";

        await _hubContext.Clients.Group(groupName).SendAsync("ReceiveMessage", new
        {
            sessionId = session.SessionId,
            messageId = prompt.ItemId,
            content = payload.AgentMessage,
            role = "assistant",
        });

        return Ok();
    }
}
```

:::note
For **Chat Interactions** (not AI Chat Sessions), use `IHubContext<ChatInteractionHub>` instead and the group name pattern `chat-interaction-{itemId}`.
:::

## Mid-Conversation Handler Transfer

An AI function can transfer the session to a different handler mid-conversation by updating the session's `ResponseHandlerName` property:

```csharp
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;

public sealed class TransferToAgentFunction
{
    [Description("Transfers the user to a live support agent")]
    public async Task<string> TransferToLiveAgent(
        [Description("The queue to transfer to")] string queueName,
        IAIChatSessionManager sessionManager,
        AIChatSession chatSession)
    {
        // Update the session to use the Genesys handler for subsequent prompts.
        chatSession.ResponseHandlerName = "Genesys";
        await sessionManager.SaveAsync(chatSession);

        // Create a session in the external system.
        // (implementation details depend on your integration)

        return $"Transferring you to a live agent in the {queueName} queue. Please wait...";
    }
}
```

After this function executes, all subsequent prompts from the user are routed to the `GenesysResponseHandler` instead of AI.

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
