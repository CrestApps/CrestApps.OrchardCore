---
title: Chat Interactions
sidebar_position: 5
---

# Chat Interactions (SignalR Real-Time Chat)

The Chat Interactions feature provides real-time streaming AI chat using SignalR. It includes a configurable chat hub, JavaScript clients, and a frontend chat widget.

## Architecture

```
Browser (JavaScript)
    ↕ SignalR WebSocket
Chat Hub (AIChatHubBase)
    ↕ Framework Services
┌─────────────────────────────────┐
│ IOrchestratorResolver           │ → Selects orchestrator
│ IOrchestrationContextBuilder    │ → Builds conversation context
│ DefaultOrchestrator             │ → Coordinates AI + tools
│ IAICompletionService            │ → Calls AI provider
│ IAIChatSessionManager           │ → Manages sessions
│ IAIChatSessionPromptStore       │ → Stores prompts
└─────────────────────────────────┘
```

## Setup

### Step 1: Register Services

```csharp
using CrestApps.Core.AI.ResponseHandling;
using CrestApps.Core.SignalR;

// Core AI services
builder.Services.AddCrestAppsCoreServices();
builder.Services.AddCrestAppsAI();
builder.Services.AddOrchestrationServices();

// SignalR
builder.Services.AddCrestAppsSignalR();
builder.Services.AddSignalR();

// Session management (requires a store implementation)
builder.Services.AddScoped<IAIChatSessionManager, YesSqlAIChatSessionManager>();
builder.Services.AddScoped<IAIChatSessionPromptStore, YesSqlAIChatSessionPromptStore>();
```

### Step 2: Create a SignalR Hub

Extend the `AIChatHubBase` class or create your own hub:

```csharp
using CrestApps.Core.AI.ResponseHandling;
using CrestApps.Core.AI.Chat.Hubs;
using CrestApps.Core.AI.Chat.Models;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Channels;

public class AIChatHub : AIChatHubBase
{
    public AIChatHub(IServiceProvider serviceProvider)
        : base(serviceProvider) { }
}
```

### Step 3: Map the Hub

```csharp
var app = builder.Build();
app.MapHub<AIChatHub>("/hubs/ai-chat");
```

### Step 4: Connect from JavaScript

The framework provides two JavaScript clients:

#### Option A: Floating Chat Widget (`chat-widget.js`)

A ready-to-use floating widget that provides profile selection, session management, and streaming chat:

```html
<link rel="stylesheet" href="~/css/chat-widget.css" />
<script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.7/signalr.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/marked/marked.min.js"></script>
<script src="~/js/chat-widget.js"></script>
<script>
    CrestAppsChatWidget.initialize({
        hubUrl: '/hubs/ai-chat',
        profiles: [
            {
                id: 'your-profile-id',
                displayText: 'My AI Assistant',
                welcomeMessage: 'Hello! How can I help you?'
            }
        ]
    });
</script>
```

#### Option B: Custom Integration

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/ai-chat')
    .withAutomaticReconnect()
    .build();

await connection.start();

// Create a session
const session = await connection.invoke('CreateSession', profileId);

// Stream a message
connection.stream('SendMessage', session.sessionId, 'Hello!', [])
    .subscribe({
        next: (chunk) => {
            // chunk.content contains the partial response
            appendToUI(chunk.content);
        },
        complete: () => {
            console.log('Response complete');
        },
        error: (err) => console.error(err),
    });
```

## Hub Methods

The `AIChatHubBase` provides these SignalR methods:

| Method | Parameters | Returns | Description |
|--------|-----------|---------|-------------|
| `CreateSession` | `profileId` | `SessionInfo` | Creates a new chat session |
| `SendMessage` | `sessionId`, `message`, `fileNames[]` | `ChannelReader<CompletionPartialMessage>` (streaming) | Sends a message and streams the response |
| `GetHistory` | `sessionId` | `ChatMessage[]` | Retrieves conversation history |

## Session Management

### IAIChatSessionManager

Manages the lifecycle of chat sessions:

```csharp
public interface IAIChatSessionManager
{
    Task<AIChatSession> CreateAsync(AIChatSession session);
    Task<AIChatSession> FindBySessionIdAsync(string sessionId);
    Task DeleteAsync(string sessionId);
    Task<IEnumerable<AIChatSession>> GetByProfileIdAsync(string profileId);
}
```

### IAIChatSessionPromptStore

Stores individual prompts (messages) within sessions:

```csharp
public interface IAIChatSessionPromptStore
{
    Task<IReadOnlyList<AIChatSessionPrompt>> GetPromptsAsync(string sessionId);
    Task<int> DeleteAllPromptsAsync(string sessionId);
    Task<int> CountAsync(string sessionId);
}
```

## Custom Implementations

Both interfaces can be implemented with any data store:

- **YesSql** (default): `YesSqlAIChatSessionManager`, `YesSqlAIChatSessionPromptStore`
- **Entity Framework**: Implement the interfaces with your `DbContext`
- **Redis**: For high-performance session caching
- **In-memory**: For testing

```csharp
// Use your own implementation
builder.Services.AddScoped<IAIChatSessionManager, MyCustomSessionManager>();
builder.Services.AddScoped<IAIChatSessionPromptStore, MyCustomPromptStore>();
```

## Document Upload During Chat

When `AllowSessionDocuments` is enabled on a profile, users can upload documents during chat sessions. The documents are vectorized and used as context for AI responses.

### Requirements

1. Register an `IAIDocumentStore` implementation
2. Register a file store for document storage
3. Register document readers for supported file types

```csharp
// Document persistence
builder.Services.AddScoped<IAIDocumentStore, YesSqlAIDocumentStore>();

// File storage (local filesystem)
builder.Services.AddSingleton(new FileSystemFileStore(
    Path.Combine(appDataPath, "Documents")));

// Document readers for supported formats
builder.Services.AddIngestionDocumentReader<PlainTextDocumentReader>(
    new ExtractorExtension(".txt"),
    new ExtractorExtension(".md"),
    new ExtractorExtension(".csv"));
```
