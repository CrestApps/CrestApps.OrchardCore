# Chat Response Handlers - Complete Integration Example

## Full Genesys Live Agent Integration

This example shows a complete module that:
1. Registers a deferred response handler for Genesys
2. Creates an AI transfer function
3. Handles webhooks for agent messages
4. Sends UI notifications using direct `ChatNotification` construction (no extension methods)
5. Optionally uses the protocol-agnostic relay for persistent connections

### Module Structure

```
MyModule.Genesys/
├── Services/
│   ├── GenesysResponseHandler.cs
│   └── GenesysWebSocketRelay.cs        (optional: for relay approach)
├── Tools/
│   └── TransferToAgentFunction.cs
├── Endpoints/
│   ├── GenesysWebhookEndpoint.cs
│   └── AgentEventEndpoint.cs
├── Models/
│   ├── GenesysWebhookPayload.cs
│   └── AgentEventPayload.cs
├── Manifest.cs
└── Startup.cs
```

### Startup.cs

```csharp
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

namespace MyModule.Genesys;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Register the deferred response handler.
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IChatResponseHandler, GenesysResponseHandler>());

        // Register the AI transfer function.
        services.AddAITool<TransferToAgentFunction>(TransferToAgentFunction.TheName);

        // Register custom notification action handlers as keyed services.
        services.AddKeyedScoped<IChatNotificationActionHandler, FeedbackActionHandler>("feedback-positive");

        // Optional: register a custom relay event builder for custom events.
        // services.AddKeyedScoped<IExternalChatRelayNotificationBuilder, SupervisorJoinedBuilder>("supervisor-joined");
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapGenesysWebhookEndpoint();
        routes.MapAgentEventEndpoints();
    }
}
```

### GenesysResponseHandler.cs

```csharp
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MyModule.Genesys.Services;

internal sealed class GenesysResponseHandler : IChatResponseHandler
{
    public string Name => "Genesys";

    public async Task<ChatResponseHandlerResult> HandleAsync(
        ChatResponseHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        var genesysClient = context.Services.GetRequiredService<IGenesysClient>();

        // Forward user prompt to Genesys.
        await genesysClient.SendMessageAsync(new GenesysMessage
        {
            SessionId = context.SessionId,
            ConnectionId = context.ConnectionId,
            ChatType = context.ChatType.ToString(),
            Text = context.Prompt,
        });

        // Deferred: hub completes without an assistant response.
        return ChatResponseHandlerResult.Deferred();
    }
}
```

### GenesysWebhookEndpoint.cs

```csharp
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Chat.Hubs;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;

namespace MyModule.Genesys.Endpoints;

internal static class GenesysWebhookEndpoint
{
    public static IEndpointRouteBuilder MapGenesysWebhookEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("api/genesys/webhook", HandleAsync).AllowAnonymous().DisableAntiforgery();
        return builder;
    }

    private static async Task<IResult> HandleAsync(
        GenesysWebhookPayload payload,
        IAIChatSessionManager sessionManager,
        IAIChatSessionPromptStore promptStore,
        IHubContext<AIChatHub> hubContext,
        IChatNotificationSender notifications)
    {
        // TODO: Validate webhook signature.

        var session = await sessionManager.FindByIdAsync(payload.SessionId);

        if (session is null)
        {
            return TypedResults.NotFound();
        }

        // Remove the typing indicator since agent responded.
        await notifications.RemoveAsync(
            session.SessionId,
            ChatContextType.AIChatSession,
            ChatNotificationTypes.Typing);

        // Save the agent's response.
        var prompt = new AIChatSessionPrompt
        {
            ItemId = IdGenerator.GenerateId(),
            SessionId = session.SessionId,
            Role = ChatRole.Assistant,
            Content = payload.AgentMessage,
        };
        await promptStore.CreateAsync(prompt);

        // Notify connected clients.
        var groupName = AIChatHub.GetSessionGroupName(session.SessionId);
        await hubContext.Clients.Group(groupName).SendAsync("ReceiveMessage", new
        {
            sessionId = session.SessionId,
            messageId = prompt.ItemId,
            content = payload.AgentMessage,
            role = "assistant",
        });

        return TypedResults.Ok();
    }
}
```

### AgentEventEndpoint.cs (Typing, Transfer, Session End Events)

```csharp
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;

namespace MyModule.Genesys.Endpoints;

internal static class AgentEventEndpoint
{
    public static IEndpointRouteBuilder MapAgentEventEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("api/genesys/typing", OnTyping).AllowAnonymous().DisableAntiforgery();
        builder.MapPost("api/genesys/transfer", OnTransfer).AllowAnonymous().DisableAntiforgery();
        builder.MapPost("api/genesys/transfer-update", OnTransferUpdate).AllowAnonymous().DisableAntiforgery();
        builder.MapPost("api/genesys/transfer-completed", OnTransferCompleted).AllowAnonymous().DisableAntiforgery();
        builder.MapPost("api/genesys/session-end", OnSessionEnd).AllowAnonymous().DisableAntiforgery();
        return builder;
    }

    private static async Task<IResult> OnTyping(
        AgentTypingPayload payload,
        IChatNotificationSender notifications,
        IStringLocalizer<AgentEventEndpoint> T)
    {
        if (payload.IsTyping)
        {
            await notifications.SendAsync(
                payload.SessionId,
                ChatContextType.AIChatSession,
                new ChatNotification(ChatNotificationTypes.Typing)
                {
                    Content = string.IsNullOrEmpty(payload.AgentName)
                        ? T["Agent is typing"].Value
                        : T["{0} is typing", payload.AgentName].Value,
                    Icon = "fa-solid fa-ellipsis",
                });
        }
        else
        {
            await notifications.RemoveAsync(
                payload.SessionId,
                ChatContextType.AIChatSession,
                ChatNotificationTypes.Typing);
        }

        return TypedResults.Ok();
    }

    private static async Task<IResult> OnTransfer(
        TransferPayload payload,
        IChatNotificationSender notifications,
        IStringLocalizer<AgentEventEndpoint> T)
    {
        await notifications.SendAsync(
            payload.SessionId,
            ChatContextType.AIChatSession,
            new ChatNotification(ChatNotificationTypes.Transfer)
            {
                Content = !string.IsNullOrEmpty(payload.EstimatedWait)
                    ? T["Transferring you to a live agent... Estimated wait: {0}.", payload.EstimatedWait].Value
                    : T["Transferring you to a live agent..."].Value,
                Icon = "fa-solid fa-headset",
                Actions =
                [
                    new ChatNotificationAction
                    {
                        Name = ChatNotificationActionNames.CancelTransfer,
                        Label = T["Cancel Transfer"].Value,
                        CssClass = "btn-outline-danger",
                        Icon = "fa-solid fa-xmark",
                    },
                ],
            });

        return TypedResults.Ok();
    }

    private static async Task<IResult> OnTransferUpdate(
        TransferPayload payload,
        IChatNotificationSender notifications,
        IStringLocalizer<AgentEventEndpoint> T)
    {
        await notifications.UpdateAsync(
            payload.SessionId,
            ChatContextType.AIChatSession,
            new ChatNotification(ChatNotificationTypes.Transfer)
            {
                Content = T["Still waiting for an available agent... Estimated wait: {0}.", payload.EstimatedWait].Value,
                Icon = "fa-solid fa-headset",
                Actions =
                [
                    new ChatNotificationAction
                    {
                        Name = ChatNotificationActionNames.CancelTransfer,
                        Label = T["Cancel Transfer"].Value,
                        CssClass = "btn-outline-danger",
                        Icon = "fa-solid fa-xmark",
                    },
                ],
            });

        return TypedResults.Ok();
    }

    private static async Task<IResult> OnTransferCompleted(
        TransferPayload payload,
        IChatNotificationSender notifications,
        IStringLocalizer<AgentEventEndpoint> T)
    {
        await notifications.RemoveAsync(
            payload.SessionId,
            ChatContextType.AIChatSession,
            ChatNotificationTypes.Transfer);

        await notifications.SendAsync(
            payload.SessionId,
            ChatContextType.AIChatSession,
            new ChatNotification(ChatNotificationTypes.AgentConnected)
            {
                Content = string.IsNullOrEmpty(payload.AgentName)
                    ? T["You are now connected to a live agent."].Value
                    : T["You are now connected to {0}.", payload.AgentName].Value,
                Icon = "fa-solid fa-user-check",
                Dismissible = true,
            });

        return TypedResults.Ok();
    }

    private static async Task<IResult> OnSessionEnd(
        SessionEndPayload payload,
        IChatNotificationSender notifications,
        IStringLocalizer<AgentEventEndpoint> T)
    {
        await notifications.SendAsync(
            payload.SessionId,
            ChatContextType.AIChatSession,
            new ChatNotification(ChatNotificationTypes.SessionEnded)
            {
                Content = T["This chat session has ended."].Value,
                Icon = "fa-solid fa-circle-check",
                Dismissible = true,
            });

        return TypedResults.Ok();
    }
}
```

### TransferToAgentFunction.cs

```csharp
using System.Text.Json;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace MyModule.Genesys.Tools;

public sealed class TransferToAgentFunction : AIFunction
{
    public const string TheName = "transfer_to_live_agent";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "queue_name": { "type": "string", "description": "Agent queue name." },
            "reason": { "type": "string", "description": "Transfer reason." }
          },
          "required": ["queue_name"],
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;
    public override string Description => "Transfers the user to a live support agent.";
    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        if (!arguments.TryGetFirstString("queue_name", out var queueName))
        {
            return "Missing 'queue_name' argument.";
        }

        var invocationScope = AIInvocationScope.Current;

        if (invocationScope?.Items.TryGetValue(nameof(AIChatSession), out var obj) == true
            && obj is AIChatSession chatSession)
        {
            // Reject transfer in Conversation mode.
            var profileManager = arguments.Services.GetRequiredService<IAIProfileManager>();
            var profile = await profileManager.FindByIdAsync(chatSession.ProfileId);

            if (profile != null
                && profile.TryGetSettings<ChatModeProfileSettings>(out var settings)
                && settings.ChatMode == ChatMode.Conversation)
            {
                return "Transfer not available in Conversation mode.";
            }

            chatSession.ResponseHandlerName = "Genesys";

            // Show transfer notification with cancel button.
            var notifications = arguments.Services.GetRequiredService<IChatNotificationSender>();
            var T = arguments.Services.GetRequiredService<IStringLocalizer<TransferToAgentFunction>>();
            await notifications.SendAsync(
                chatSession.SessionId,
                ChatContextType.AIChatSession,
                new ChatNotification(ChatNotificationTypes.Transfer)
                {
                    Content = T["Transferring you to a live agent..."].Value,
                    Icon = "fa-solid fa-headset",
                    Actions =
                    [
                        new ChatNotificationAction
                        {
                            Name = ChatNotificationActionNames.CancelTransfer,
                            Label = T["Cancel Transfer"].Value,
                            CssClass = "btn-outline-danger",
                            Icon = "fa-solid fa-xmark",
                        },
                    ],
                });
        }
        else if (invocationScope?.ToolExecutionContext?.Resource is ChatInteraction interaction)
        {
            interaction.ResponseHandlerName = "Genesys";

            var notifications = arguments.Services.GetRequiredService<IChatNotificationSender>();
            var T = arguments.Services.GetRequiredService<IStringLocalizer<TransferToAgentFunction>>();
            await notifications.SendAsync(
                interaction.ItemId,
                ChatContextType.ChatInteraction,
                new ChatNotification(ChatNotificationTypes.Transfer)
                {
                    Content = T["Transferring you to a live agent..."].Value,
                    Icon = "fa-solid fa-headset",
                    Actions =
                    [
                        new ChatNotificationAction
                        {
                            Name = ChatNotificationActionNames.CancelTransfer,
                            Label = T["Cancel Transfer"].Value,
                            CssClass = "btn-outline-danger",
                            Icon = "fa-solid fa-xmark",
                        },
                    ],
                });
        }
        else
        {
            return "No active chat session found.";
        }

        return $"Transferring to '{queueName}' queue. Please wait...";
    }
}
```

### FeedbackActionHandler.cs

A custom notification action handler that records feedback when the user clicks a button:

```csharp
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MyModule.Genesys.Services;

internal sealed class FeedbackActionHandler : IChatNotificationActionHandler
{
    public async Task HandleAsync(
        ChatNotificationActionContext context,
        CancellationToken cancellationToken = default)
    {
        // Record the feedback.
        var feedbackService = context.Services.GetRequiredService<IFeedbackService>();
        await feedbackService.RecordAsync(context.SessionId, positive: true);

        // Remove the feedback notification from the UI.
        var notifications = context.Services.GetRequiredService<IChatNotificationSender>();
        await notifications.RemoveAsync(
            context.SessionId,
            context.ChatType,
            context.NotificationType);
    }
}
```

### Sending a Custom Notification with Action Buttons

Send a feedback request notification after the agent response:

```csharp
// In a webhook or response handler:
var notifications = services.GetRequiredService<IChatNotificationSender>();
await notifications.SendAsync(sessionId, ChatContextType.AIChatSession, new ChatNotification("info")
{
    Id = "feedback-request",
    Content = "Was this helpful?",
    Icon = "fa-solid fa-star",
    Dismissible = true,
    Actions =
    [
        new ChatNotificationAction
        {
            Name = "feedback-positive",
            Label = "Yes!",
            CssClass = "btn-outline-success",
            Icon = "fa-solid fa-thumbs-up",
        },
        new ChatNotificationAction
        {
            Name = "feedback-negative",
            Label = "No",
            CssClass = "btn-outline-secondary",
            Icon = "fa-solid fa-thumbs-down",
        },
    ],
});
```

### Protocol-Agnostic Relay Example (WebSocket to Genesys)

For persistent connections, implement `IExternalChatRelay` and use `IExternalChatRelayManager`:

```csharp
using System.Net.WebSockets;
using System.Text;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MyModule.Genesys.Services;

internal sealed class GenesysWebSocketRelay : IExternalChatRelay
{
    private readonly IServiceScopeFactory _scopeFactory;
    private ClientWebSocket? _webSocket;

    public GenesysWebSocketRelay(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public string SessionId { get; set; } = string.Empty;
    public ChatContextType ChatType { get; set; }

    public Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_webSocket?.State == WebSocketState.Open);

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _webSocket = new ClientWebSocket();
        await _webSocket.ConnectAsync(new Uri("wss://genesys.example.com/ws"), cancellationToken);
        _ = Task.Run(() => ListenForEventsAsync(cancellationToken), cancellationToken);
    }

    public async Task SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var bytes = Encoding.UTF8.GetBytes(prompt);
        await _webSocket!.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    public Task SendSignalAsync(string signal, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_webSocket?.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _webSocket?.Dispose();
    }

    private async Task ListenForEventsAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        while (_webSocket?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await _webSocket.ReceiveAsync(buffer, cancellationToken);
            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            var relayEvent = ParseEvent(json);

            // Create a new scope per event for proper DI lifetime management.
            await using var scope = _scopeFactory.CreateAsyncScope();
            var eventHandler = scope.ServiceProvider.GetRequiredService<IExternalChatRelayEventHandler>();
            var context = new ExternalChatRelayContext
            {
                SessionId = SessionId,
                ChatType = ChatType,
            };
            await eventHandler.HandleAsync(context, relayEvent, cancellationToken);
        }
    }

    private static ExternalChatRelayEvent ParseEvent(string json)
    {
        // Map your platform's event types to ExternalChatRelayEventTypes constants.
        return new ExternalChatRelayEvent
        {
            EventType = ExternalChatRelayEventTypes.AgentTyping,
        };
    }
}
```

#### Using the Relay in a Response Handler

```csharp
internal sealed class GenesysRelayResponseHandler : IChatResponseHandler
{
    public string Name => "GenesysRelay";

    public async Task<ChatResponseHandlerResult> HandleAsync(
        ChatResponseHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        var relay = context.Services.GetRequiredService<GenesysWebSocketRelay>();
        relay.SessionId = context.SessionId;
        relay.ChatType = context.ChatType;

        var relayManager = context.Services.GetRequiredService<IExternalChatRelayManager>();
        await relayManager.ConnectAsync(relay, cancellationToken);
        await relay.SendPromptAsync(context.Prompt, cancellationToken);

        return ChatResponseHandlerResult.Deferred();
    }
}
```
