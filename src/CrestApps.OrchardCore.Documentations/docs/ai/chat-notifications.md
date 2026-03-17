---
sidebar_label: Chat Notifications
sidebar_position: 14
title: Chat UI Notifications
description: Send transient notification system messages (typing indicators, transfer status, session endings, and custom notifications) to the chat UI from C# — no JavaScript required.
---

| | |
| --- | --- |
| **Feature Name** | AI Chat |
| **Feature ID** | `CrestApps.OrchardCore.AI.Chat` |

## Overview

The **Chat Notification** system lets server-side C# code send transient UI notifications (system messages) to the chat interface in real time via SignalR. Notifications are separate from chat history — they provide visual feedback about system state changes such as:

- **Typing indicators** ("Mike is typing…")
- **Transfer status** with estimated wait times and a cancel button
- **Conversation / session ended** indicators
- **Custom notifications** with arbitrary content, icons, and action buttons

Developers interact entirely through C# interfaces and extension methods — no JavaScript changes are required.

### What Is a System Message?

A **system message** is a transient, non-persistent notification displayed in the chat UI to communicate system-level state changes to the user. Unlike chat messages (which are part of the conversation history), system messages:

- **Are not stored** in the chat history or prompt store — they exist only while the notification is active.
- **Provide visual feedback** about background operations: agent typing, transfer in progress, connection status, session lifecycle events.
- **Can include action buttons** (e.g., "Cancel Transfer") that trigger server-side callbacks.
- **Are styled per type** — built-in CSS classes distinguish typing, transfer, info, warning, error, and ended notifications.
- **Can be dismissed** by the user or removed programmatically from server-side code.

System messages are the recommended way to communicate any non-conversational state to the user within the chat interface.

## Architecture

```
C# Code (webhook, handler, background task, etc.)
  → IChatNotificationSender.SendAsync(sessionId, chatType, notification)
    → IChatNotificationTransport (resolved by chatType key)
      → SignalR group broadcast → ReceiveNotification
        → Chat UI renders the notification as a system message

User clicks an action button on a notification
  → JS: connection.invoke("HandleNotificationAction", sessionId, notificationId, actionName)
    → Hub dispatches to matching IChatNotificationActionHandler (keyed service)
```

Notifications are pushed to the SignalR group for the session, so **all connected clients** see the same notifications — including clients that reconnect.

## Quick Start

Inject `IChatNotificationSender` and call the extension methods:

```csharp
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;

public sealed class MyWebhookHandler
{
    private readonly IChatNotificationSender _notifications;
    private readonly IStringLocalizer _localizer;

    public MyWebhookHandler(
        IChatNotificationSender notifications,
        IStringLocalizer<MyWebhookHandler> localizer)
    {
        _notifications = notifications;
        _localizer = localizer;
    }

    public async Task OnAgentTyping(string sessionId)
    {
        // Show a "Mike is typing..." system message.
        await _notifications.ShowTypingAsync(sessionId, ChatContextType.AIChatSession, _localizer, "Mike");
    }

    public async Task OnAgentStoppedTyping(string sessionId)
    {
        // Remove the typing indicator.
        await _notifications.HideTypingAsync(sessionId, ChatContextType.AIChatSession);
    }
}
```

## Core Interfaces

### `IChatNotificationSender`

The primary service for sending, updating, and removing notifications:

| Method | Description |
| --- | --- |
| `SendAsync(sessionId, chatType, notification)` | Sends a notification to all clients. Replaces any existing notification with the same `Id`. |
| `UpdateAsync(sessionId, chatType, notification)` | Updates an existing notification by `Id`. |
| `RemoveAsync(sessionId, chatType, notificationId)` | Removes a notification by its `Id`. |

### `IChatNotificationActionHandler`

Handles user-initiated actions on notification system messages (e.g., clicking "Cancel Transfer"). Handlers are registered as **keyed services** where the key is the action name:

```csharp
public interface IChatNotificationActionHandler
{
    Task HandleAsync(ChatNotificationActionContext context, CancellationToken cancellationToken = default);
}
```

The hub resolves the handler by looking up the keyed service matching the action name clicked by the user.

## Models

### `ChatNotification`

| Property | Type | Description |
| --- | --- | --- |
| `Id` | `string` | Unique identifier. Used for update/remove targeting. |
| `Type` | `string` | Visual type: `"typing"`, `"transfer"`, `"ended"`, `"info"`, `"warning"`, or any custom value. Maps to CSS class `ai-chat-notification-{type}`. |
| `Content` | `string` | Display text. |
| `Icon` | `string` | FontAwesome class (e.g., `"fa-solid fa-headset"`). |
| `CssClass` | `string` | Additional CSS class on the container. |
| `Dismissible` | `bool` | Shows a close (×) button when `true`. |
| `Actions` | `IList<ChatNotificationAction>` | Action buttons rendered inside the notification. |
| `Metadata` | `IDictionary<string, string>` | Extensible key-value data passed to the client. |

### `ChatNotificationAction`

| Property | Type | Description |
| --- | --- | --- |
| `Name` | `string` | Action identifier sent to the hub on click. |
| `Label` | `string` | Button label text. |
| `CssClass` | `string` | CSS class for the button (e.g., `"btn-outline-danger"`). |
| `Icon` | `string` | Optional FontAwesome icon class. |

### `ChatNotificationActionContext`

Passed to `IChatNotificationActionHandler.HandleAsync`:

| Property | Type | Description |
| --- | --- | --- |
| `SessionId` | `string` | Session or interaction ID. |
| `NotificationId` | `string` | The notification that contains the action. |
| `ActionName` | `string` | The action the user clicked. |
| `ChatType` | `ChatContextType` | `AIChatSession` or `ChatInteraction`. |
| `ConnectionId` | `string` | SignalR connection ID of the client. |
| `Services` | `IServiceProvider` | Scoped service provider. |

## Extension Methods

`ChatNotificationSenderExtensions` provides convenient methods so you don't need to build `ChatNotification` objects manually:

All extension methods that produce user-facing text accept an `IStringLocalizer` parameter to ensure messages are localized. Methods that only remove notifications (e.g., `HideTypingAsync`) do not require a localizer.

| Method | Description |
| --- | --- |
| `ShowTypingAsync(sessionId, chatType, localizer, agentName?)` | Shows a typing indicator system message. |
| `HideTypingAsync(sessionId, chatType)` | Removes the typing indicator. |
| `ShowTransferAsync(sessionId, chatType, localizer, message?, estimatedWaitTime?, cancellable?)` | Shows a transfer indicator with optional wait time and cancel button. |
| `UpdateTransferAsync(sessionId, chatType, localizer, message?, estimatedWaitTime?, cancellable?)` | Updates the transfer indicator (e.g., with a new wait time). |
| `HideTransferAsync(sessionId, chatType)` | Removes the transfer indicator. |
| `ShowAgentConnectedAsync(sessionId, chatType, localizer, agentName?, message?)` | Shows an "agent connected" system message. |
| `HideAgentConnectedAsync(sessionId, chatType)` | Removes the agent-connected notification. |
| `ShowConversationEndedAsync(sessionId, chatType, localizer, message?)` | Shows a "conversation ended" dismissible system message. |
| `ShowSessionEndedAsync(sessionId, chatType, localizer, message?)` | Shows a "session ended" dismissible system message. |

### Well-Known Constants

The extension class also exposes constants for built-in notification IDs and action names:

```csharp
ChatNotificationSenderExtensions.NotificationIds.Typing          // "typing"
ChatNotificationSenderExtensions.NotificationIds.Transfer        // "transfer"
ChatNotificationSenderExtensions.NotificationIds.AgentConnected  // "agent-connected"
ChatNotificationSenderExtensions.NotificationIds.ConversationEnded // "conversation-ended"
ChatNotificationSenderExtensions.NotificationIds.SessionEnded    // "session-ended"

ChatNotificationSenderExtensions.ActionNames.CancelTransfer      // "cancel-transfer"
ChatNotificationSenderExtensions.ActionNames.EndSession          // "end-session"
```

## Built-In Action Handlers

Two action handlers are registered automatically when the AI Chat feature is enabled:

| Action Name | Behavior |
| --- | --- |
| `cancel-transfer` | Resets the session's `ResponseHandlerName` to `null` (returns to AI), removes the transfer notification. |
| `end-session` | Closes the session (sets `Status = Closed`, `ClosedAtUtc = now`), shows a "session ended" notification. |

## Extensible Transport Architecture

The notification system uses a **transport provider** pattern so that each hub independently registers how notifications are delivered to its clients. The `IChatNotificationSender` resolves the correct `IChatNotificationTransport` by `ChatContextType` key — no hardcoded hub references.

### How It Works

1. **`IChatNotificationTransport`** — defines low-level delivery methods (`SendNotificationAsync`, `UpdateNotificationAsync`, `RemoveNotificationAsync`).
2. Each hub module registers its transport as a **keyed service** using `ChatContextType` as the key.
3. **`IChatNotificationSender`** (the high-level API you call) resolves the transport by key and delegates.

### Built-In Transports

| Chat Type | Transport | Registered By |
| --- | --- | --- |
| `ChatContextType.AIChatSession` | `AIChatNotificationTransport` | `CrestApps.OrchardCore.AI.Chat` |
| `ChatContextType.ChatInteraction` | `ChatInteractionNotificationTransport` | `CrestApps.OrchardCore.AI.Chat.Interactions` |

### Registering a Custom Transport

If you create a custom chat hub that supports notification system messages, implement `IChatNotificationTransport` and register it as a keyed service:

```csharp
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.SignalR;

internal sealed class MyCustomChatNotificationTransport : IChatNotificationTransport
{
    private readonly IHubContext<MyCustomHub, IMyCustomHubClient> _hubContext;

    public MyCustomChatNotificationTransport(
        IHubContext<MyCustomHub, IMyCustomHubClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task SendNotificationAsync(string sessionId, ChatNotification notification)
    {
        var groupName = MyCustomHub.GetGroupName(sessionId);
        return _hubContext.Clients.Group(groupName).ReceiveNotification(notification);
    }

    public Task UpdateNotificationAsync(string sessionId, ChatNotification notification)
    {
        var groupName = MyCustomHub.GetGroupName(sessionId);
        return _hubContext.Clients.Group(groupName).UpdateNotification(notification);
    }

    public Task RemoveNotificationAsync(string sessionId, string notificationId)
    {
        var groupName = MyCustomHub.GetGroupName(sessionId);
        return _hubContext.Clients.Group(groupName).RemoveNotification(notificationId);
    }
}
```

Register in your module's `Startup`:

```csharp
services.AddKeyedScoped<IChatNotificationTransport, MyCustomChatNotificationTransport>(
    MyChatContextType);
```

Now any call to `IChatNotificationSender.SendAsync(sessionId, MyChatContextType, notification)` automatically routes to your transport.

## Scenarios & Examples

### Scenario 1: Live Agent Typing Indicator

When the external agent platform notifies your webhook that an agent is typing, show and hide the indicator:

```csharp
internal static class AgentTypingWebhook
{
    public static IEndpointRouteBuilder MapAgentTypingEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("api/agent/typing", HandleAsync).AllowAnonymous().DisableAntiforgery();
        return builder;
    }

    private static async Task<IResult> HandleAsync(
        AgentTypingPayload payload,
        IChatNotificationSender notifications,
        IStringLocalizer<AgentTypingWebhook> localizer)
    {
        if (payload.IsTyping)
        {
            await notifications.ShowTypingAsync(
                payload.SessionId,
                ChatContextType.AIChatSession,
                localizer,
                payload.AgentName);
        }
        else
        {
            await notifications.HideTypingAsync(
                payload.SessionId,
                ChatContextType.AIChatSession);
        }

        return TypedResults.Ok();
    }
}
```

### Scenario 2: Transfer to Live Agent with Wait Time

Show a transfer indicator when a session is handed off, update it with wait time changes, and let the user cancel:

```csharp
internal static class TransferWebhook
{
    public static IEndpointRouteBuilder MapTransferEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("api/agent/transfer-started", OnTransferStarted).AllowAnonymous().DisableAntiforgery();
        builder.MapPost("api/agent/transfer-update", OnTransferUpdate).AllowAnonymous().DisableAntiforgery();
        builder.MapPost("api/agent/transfer-completed", OnTransferCompleted).AllowAnonymous().DisableAntiforgery();
        return builder;
    }

    private static async Task<IResult> OnTransferStarted(
        TransferPayload payload,
        IChatNotificationSender notifications,
        IStringLocalizer<TransferWebhook> localizer)
    {
        // Show a transfer system message with estimated wait time and a cancel button.
        await notifications.ShowTransferAsync(
            payload.SessionId,
            ChatContextType.AIChatSession,
            localizer,
            message: localizer["Transferring you to a live agent..."].Value,
            estimatedWaitTime: localizer["About 2 minutes"].Value,
            cancellable: true);

        return TypedResults.Ok();
    }

    private static async Task<IResult> OnTransferUpdate(
        TransferPayload payload,
        IChatNotificationSender notifications,
        IStringLocalizer<TransferWebhook> localizer)
    {
        // Update the wait time as the queue changes.
        await notifications.UpdateTransferAsync(
            payload.SessionId,
            ChatContextType.AIChatSession,
            localizer,
            message: localizer["Still waiting for an available agent..."].Value,
            estimatedWaitTime: payload.EstimatedWaitTime,
            cancellable: true);

        return TypedResults.Ok();
    }

    private static async Task<IResult> OnTransferCompleted(
        TransferPayload payload,
        IChatNotificationSender notifications,
        IStringLocalizer<TransferWebhook> localizer)
    {
        // Agent connected — remove the transfer indicator and show connected notification.
        await notifications.HideTransferAsync(
            payload.SessionId,
            ChatContextType.AIChatSession);

        await notifications.ShowAgentConnectedAsync(
            payload.SessionId,
            ChatContextType.AIChatSession,
            localizer,
            payload.AgentName);

        return TypedResults.Ok();
    }
}
```

When the user clicks **Cancel Transfer**, the built-in `cancel-transfer` action handler automatically:
1. Resets `ResponseHandlerName` to `null` (routes future prompts back to AI).
2. Removes the transfer notification from the UI.

### Scenario 3: End Session Programmatically

End a chat session from server-side code and notify the user:

```csharp
public sealed class SessionTimeoutService
{
    private readonly IChatNotificationSender _notifications;
    private readonly IStringLocalizer _localizer;

    public SessionTimeoutService(
        IChatNotificationSender notifications,
        IStringLocalizer<SessionTimeoutService> localizer)
    {
        _notifications = notifications;
        _localizer = localizer;
    }

    public async Task EndSessionDueToInactivity(string sessionId, ChatContextType chatType)
    {
        await _notifications.ShowSessionEndedAsync(
            sessionId,
            chatType,
            _localizer,
            _localizer["This session was ended due to inactivity."].Value);
    }
}
```

### Scenario 4: Custom Notification with Actions

Create a completely custom notification with your own action buttons and handler:

```csharp
// Send a custom notification with two action buttons.
await notifications.SendAsync(sessionId, ChatContextType.AIChatSession, new ChatNotification
{
    Id = "feedback-request",
    Type = "info",
    Content = "Was this conversation helpful?",
    Icon = "fa-solid fa-star",
    Dismissible = true,
    Actions =
    [
        new ChatNotificationAction
        {
            Name = "feedback-positive",
            Label = "Yes, it helped!",
            CssClass = "btn-outline-success",
            Icon = "fa-solid fa-thumbs-up",
        },
        new ChatNotificationAction
        {
            Name = "feedback-negative",
            Label = "Not really",
            CssClass = "btn-outline-secondary",
            Icon = "fa-solid fa-thumbs-down",
        },
    ],
});
```

Handle the action:

```csharp
public sealed class FeedbackActionHandler : IChatNotificationActionHandler
{
    public async Task HandleAsync(
        ChatNotificationActionContext context,
        CancellationToken cancellationToken = default)
    {
        // Record positive feedback.
        var feedbackService = context.Services.GetRequiredService<IFeedbackService>();
        await feedbackService.RecordAsync(context.SessionId, positive: true);

        // Remove the feedback notification.
        var notifications = context.Services.GetRequiredService<IChatNotificationSender>();
        await notifications.RemoveAsync(context.SessionId, context.ChatType, context.NotificationId);
    }
}
```

Register the handler as a keyed service where the key is the action name:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddKeyedScoped<IChatNotificationActionHandler, FeedbackActionHandler>("feedback-positive");
    }
}
```

### Scenario 5: Custom Notification Type with Custom Styling

Define a custom notification type and add CSS in your theme or module:

```csharp
await notifications.SendAsync(sessionId, ChatContextType.AIChatSession, new ChatNotification
{
    Id = "queue-position",
    Type = "queue",  // custom type
    Content = "You are #3 in the queue.",
    Icon = "fa-solid fa-users",
    CssClass = "my-custom-queue-notification",
});
```

The UI applies the CSS class `ai-chat-notification-queue` (derived from the `Type` value) plus your custom `CssClass`. Add matching styles in your theme:

```css
.ai-chat-notification-queue {
    border-color: #6f42c1;
    background-color: rgba(111, 66, 193, 0.05);
}
```

## Integration with Response Handlers

The notification system is designed to complement [Chat Response Handlers](./response-handlers.md). A typical integration pattern:

1. **Transfer function** sets `ResponseHandlerName` on the session → calls `ShowTransferAsync()`.
2. **External system** (via webhook, WebSocket, or any protocol) receives agent connected event → removes transfer notification + sends agent-connected notification.
3. **External system** receives typing events → sends/removes typing notifications.
4. **External system** receives agent response → removes typing notification + writes message via `IHubContext`.
5. **User clicks Cancel Transfer** → built-in handler resets `ResponseHandlerName` to `null`.

### Agent Connected Notification Example

When the external platform signals that an agent has joined, you can send the notification directly using `IChatNotificationSender`:

```csharp
// In your webhook endpoint or any external protocol callback:
var notifications = services.GetRequiredService<IChatNotificationSender>();
var localizer = services.GetRequiredService<IStringLocalizer<MyHandler>>();

// First, hide the transfer indicator.
await notifications.HideTransferAsync(sessionId, ChatContextType.AIChatSession);

// Then, show the agent-connected notification with the agent's name.
await notifications.ShowAgentConnectedAsync(
    sessionId,
    ChatContextType.AIChatSession,
    localizer,
    agentName: "Sarah");
// Result: "You are now connected to Sarah." with a dismissible info system message.
```

When using the **external chat relay** infrastructure, events are routed automatically through the keyed builder/handler pattern. The `DefaultExternalChatRelayEventHandler` resolves an `IExternalChatRelayNotificationBuilder` keyed by event type, creates a `ChatNotification` with the builder's `NotificationType`, and delegates to `IExternalChatRelayNotificationHandler` for sending/removing notifications:

```csharp
// The relay receives a JSON event like:
// { "type": "agent_connected", "agent_name": "Sarah" }
//
// The DefaultExternalChatRelayEventHandler automatically:
// 1. Resolves the AgentConnectedNotificationBuilder (keyed by "agent-connected").
// 2. Creates a ChatNotification with Type = "info" (from builder.NotificationType).
// 3. The builder populates Content, Icon, Dismissible, and removes the transfer notification.
// 4. The IExternalChatRelayNotificationHandler processes the result (remove + send).
//
// Built-in event types with registered builders:
// - ExternalChatRelayEventTypes.AgentTyping        → typing indicator
// - ExternalChatRelayEventTypes.AgentStoppedTyping  → removes typing indicator
// - ExternalChatRelayEventTypes.AgentConnected      → agent-connected info notification
// - ExternalChatRelayEventTypes.AgentDisconnected   → removes agent-connected notification
// - ExternalChatRelayEventTypes.AgentReconnecting   → reconnecting warning notification
// - ExternalChatRelayEventTypes.ConnectionLost      → connection-lost error notification
// - ExternalChatRelayEventTypes.ConnectionRestored  → removes connection-lost notification
// - ExternalChatRelayEventTypes.WaitTimeUpdated     → updates transfer notification
// - ExternalChatRelayEventTypes.SessionEnded        → session-ended notification
//
// Custom event types: register a keyed IExternalChatRelayNotificationBuilder.
```

### Adding Custom Relay Events

To handle custom event types from your external platform, register a keyed builder:

```csharp
// In your module's Startup.cs:
services.AddKeyedScoped<IExternalChatRelayNotificationBuilder, SupervisorJoinedBuilder>("supervisor-joined");
```

Implement the builder. The `NotificationType` property declares the notification type, and `Build` populates other properties:

```csharp
public sealed class SupervisorJoinedBuilder : IExternalChatRelayNotificationBuilder
{
    public string NotificationType => "info";

    public void Build(
        ExternalChatRelayEvent relayEvent,
        ChatNotification notification,
        ExternalChatRelayNotificationResult result,
        IStringLocalizer T)
    {
        notification.Id = "supervisor-joined";
        notification.Content = T["A supervisor has joined the conversation."].Value;
        notification.Icon = "fa-solid fa-user-shield";
        notification.Dismissible = true;
    }
}
```

See the [Response Handlers documentation](./response-handlers.md) for the full handler implementation pattern, including both webhook and persistent relay integration examples.

## Built-In Notification Types (CSS)

The chat UI ships with styles for these notification types:

| Type | Visual Style |
| --- | --- |
| `typing` | Green-tinted, pulsing icon animation |
| `transfer` | Yellow/warning-tinted, scaling pulse animation |
| `ended` | Gray/secondary-tinted, static |
| `info` | Cyan/info-tinted |
| `warning` | Yellow/amber-tinted (used for agent-reconnecting) |
| `error` | Red/danger-tinted (used for connection-lost) |

Custom types receive the base `.ai-chat-notification` styling. Add your own CSS for custom types using `.ai-chat-notification-{type}`.
