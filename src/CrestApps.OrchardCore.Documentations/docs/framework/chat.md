---
sidebar_label: Chat
sidebar_position: 5
title: Chat Interactions
description: Chat session management, interaction handlers, and response routing for conversational AI experiences.
---

# Chat Interactions

> Manages chat sessions, routes responses through pluggable handlers, and tracks interaction history.

## Quick Start

```csharp
builder.Services
    .AddCrestAppsAI()
    .AddOrchestrationServices()
    .AddChatInteractionServices()
    .AddOpenAIProvider();
```

## Problem & Solution

A chat experience involves more than sending messages to an LLM:

- **Sessions** must be created, tracked, and eventually closed
- **History** needs to be persisted so users can resume conversations
- **Routing** determines whether a message goes to an AI orchestrator, a live agent, or an external webhook
- **Interactions** are discrete conversation units within a session that can be transferred between handlers

The chat system provides all of this with a pluggable handler architecture.

## Services Registered by `AddChatInteractionServices()`

| Service | Implementation | Lifetime | Purpose |
|---------|---------------|----------|---------|
| `ChatInteractionCompletionContextBuilderHandler` | — | Scoped | Enriches completion context with chat history |
| `ChatInteractionEntryHandler` | — | Scoped | Catalog lifecycle handler for `ChatInteraction` |
| `DataExtractionService` | `DataExtractionService` | Scoped | Extracts configured fields from completed chat turns |
| `PostSessionProcessingService` | `PostSessionProcessingService` | Scoped | Runs AI-powered post-session tasks and evaluations |
| `AIChatSessionPostCloseProcessor` | `AIChatSessionPostCloseProcessor` | Scoped | Applies the shared post-close pipeline used by Orchard Core and MVC |
| `DataExtractionChatSessionHandler` | — | Scoped | Runs shared extraction and closes sessions on natural farewells |
| `PostSessionProcessingChatSessionHandler` | — | Scoped | Triggers the shared post-close processor when a session closes |

:::info
The chat system also registers embedded templates from the `CrestApps.AI.Chat` assembly for system prompts.
:::

## Core Concepts

### Chat Session (`AIChatSession`)

A session represents a conversation between a user and the AI system. It has:

- **Status** — Active, Closed, Expired
- **ResponseHandlerName** — Which handler processes messages (e.g., `"ai"`, `"genesys"`)
- **Attached documents** — Files uploaded for RAG processing
- **Metadata** — Custom key-value data

### Chat Interaction (`ChatInteraction`)

An interaction is a unit of conversation within a session. When a response handler transfers the conversation (e.g., from AI to a live agent), a new interaction is created while the session continues.

### Response Handler

A pluggable component that decides how to process a chat message. The default handler (`AIChatResponseHandler`) routes through the AI orchestrator. Custom handlers can route to external systems like Genesys, Twilio Flex, or custom webhooks.

## Key Interfaces

### `IChatResponseHandler`

Implement this to create a custom response routing strategy.

```csharp
public interface IChatResponseHandler
{
    string Name { get; }

    Task<ChatResponseHandlerResult> HandleAsync(
        ChatResponseHandlerContext context,
        CancellationToken cancellationToken = default);
}
```

The `Name` property identifies the handler. It is stored on the session or interaction so the system knows which handler to use for subsequent messages.

See [Response Handlers](./response-handlers.md) for detailed implementation guidance.

### `IChatResponseHandlerResolver`

Resolves a handler by name at runtime.

```csharp
public interface IChatResponseHandlerResolver
{
    IChatResponseHandler Resolve(string name);
}
```

### `ICatalogEntryHandler<ChatInteraction>`

React to chat interaction lifecycle events (creating, created, updating, etc.).

```csharp
public sealed class MyChatInteractionHandler : CatalogEntryHandlerBase<ChatInteraction>
{
    public override Task CreatedAsync(CreatedContext<ChatInteraction> context)
    {
        // React to new interaction
    }
}
```

### Chat document authorization

The shared document endpoints use standard ASP.NET Core resource authorization through `IAuthorizationService`.

Hosts register `IAuthorizationHandler` implementations for:

- `AIChatDocumentOperations.ManageDocuments` on `ChatInteraction`
- `AIChatDocumentOperations.ManageDocuments` on `AIChatSessionDocumentAuthorizationContext`

`AIChatSessionDocumentAuthorizationContext` carries both the `AIProfile` and `AIChatSession`, so hosts can apply different rules for admin-managed interaction documents versus end-user session uploads without introducing a separate chat-specific authorization abstraction.

### `IAIChatDocumentEventHandler`

Implement this hook when uploaded or removed chat documents need additional side effects such as indexing chunks, persisting original files, or cleaning up external stores.

```csharp
public interface IAIChatDocumentEventHandler
{
    Task UploadedAsync(AIChatDocumentUploadContext context, CancellationToken cancellationToken = default);

    Task RemovedAsync(AIChatDocumentRemoveContext context, CancellationToken cancellationToken = default);
}
```

## Session Lifecycle

A chat session moves through a well-defined lifecycle:

```text
NewAsync()          SaveAsync()         (inactivity / explicit close)
    │                   │                          │
    ▼                   ▼                          ▼
 ┌────────┐       ┌────────┐                ┌────────┐
 │ Active │──────▶│ Active │───────────────▶│ Closed │
 └────────┘       └────────┘                └────────┘
   Created       LastActivityUtc updated     ClosedAtUtc set
```

| Stage | What Happens |
|-------|-------------|
| **Creation** | `IAIChatSessionManager.NewAsync()` allocates a new `AIChatSession`, assigns a `SessionId`, sets `Status = Active`, records `CreatedUtc`, and associates it with the profile and user. |
| **Active Use** | Every user message updates `LastActivityUtc`. Prompts are appended via `IAIChatSessionPromptStore`. Documents may be attached to `session.Documents`. |
| **Interaction Transfer** | If a response handler transfers the conversation (e.g., AI → live agent), a new `ChatInteraction` is created while the session continues. The session's `ResponseHandlerName` updates to the new handler. |
| **Closure** | The session status changes to `Closed` and `ClosedAtUtc` is recorded. The shared post-close processor updates extraction state, post-session task results, resolution analysis, and conversion-goal evaluation so hosts reuse the same runtime behavior. |
| **Deletion** | `DeleteAsync()` removes the session and its associated prompts. `DeleteAllAsync()` removes all sessions for a given profile and user. |

:::info
The framework now standardizes the post-close processing pipeline, but hosts still own the storage-specific background policy that decides when inactive sessions should be closed and retried.
:::

### Key Properties of `AIChatSession`

| Property | Type | Description |
|----------|------|-------------|
| `SessionId` | `string` | Unique session identifier |
| `ProfileId` | `string` | Associated AI profile |
| `Title` | `string` | Human-readable title (often AI-generated after the first exchange) |
| `UserId` | `string` | Authenticated user who owns the session |
| `ClientId` | `string` | Anonymous client identifier (used when `UserId` is null) |
| `Status` | `ChatSessionStatus` | `Active`, `Closed`, etc. |
| `ResponseHandlerName` | `string` | Which `IChatResponseHandler` processes messages |
| `Documents` | `List<ChatDocumentInfo>` | Uploaded files for RAG processing |
| `CreatedUtc` | `DateTime` | When the session started |
| `LastActivityUtc` | `DateTime` | Last message timestamp |
| `ClosedAtUtc` | `DateTime?` | When the session was closed |
| `ExtractedData` | `Dictionary<string, ExtractedFieldState>` | Extracted conversation fields |
| `PostSessionProcessingStatus` | `PostSessionProcessingStatus` | Status of post-session tasks |

### Extracted Data Reporting Snapshots

Hosts can persist queryable extracted-data snapshots by implementing `IAIChatSessionExtractedDataRecorder`.

```csharp
public interface IAIChatSessionExtractedDataRecorder
{
    Task RecordExtractedDataAsync(
        AIProfile profile,
        AIChatSession session,
        CancellationToken cancellationToken = default);
}
```

The shared `DataExtractionChatSessionHandler` now calls these recorders whenever extraction produces new values or naturally closes the session, so hosts can upsert reporting documents such as `AIChatSessionExtractedDataRecord` without duplicating extraction logic.

## Session Management

The framework defines `IAIChatSessionManager` for session CRUD. **You must provide an implementation** since session storage is application-specific. The MVC example uses a YesSql-backed implementation:

```csharp
builder.Services.AddScoped<IAIChatSessionManager, YesSqlAIChatSessionManager>();
builder.Services.AddScoped<IAIChatSessionPromptStore, YesSqlAIChatSessionPromptStore>();
```

The MVC sample also registers an `AIChatSessionCloseBackgroundService` that runs every 5 minutes, closes inactive sessions, retries pending post-close processing, and keeps analytics / extracted-data reporting records aligned with the final session state.

## Shared document endpoints

The framework now ships reusable minimal API extensions for chat document uploads and removals:

```csharp
app.AddUploadChatInteractionDocumentEndpoint()
    .AddRemoveChatInteractionDocumentEndpoint()
    .AddUploadChatSessionDocumentEndpoint()
    .AddRemoveChatSessionDocumentEndpoint();
```

These endpoints:

- process files through `IAIDocumentProcessingService`
- persist `AIDocument` and `AIDocumentChunk` records through the configured stores
- update `ChatInteraction.Documents` or `AIChatSession.Documents`
- authorize upload and remove operations through `IAuthorizationService`
- invoke `IAIChatDocumentEventHandler` so the host can index chunks or save original files

Session uploads are also gated by `AIProfileSessionDocumentsMetadata.AllowSessionDocuments`, which keeps profile-level session upload behavior explicit.

### `IAIChatSessionManager` Interface

```csharp
public interface IAIChatSessionManager
{
    Task<AIChatSession> FindByIdAsync(string id);
    Task<AIChatSession> FindAsync(string id);
    Task<AIChatSessionResult> PageAsync(int page, int pageSize, AIChatSessionQueryContext context = null);
    Task<AIChatSession> NewAsync(AIProfile profile, NewAIChatSessionContext context);
    Task SaveAsync(AIChatSession chatSession);
    Task<bool> DeleteAsync(string sessionId);
    Task<int> DeleteAllAsync(string profileId);
}
```

| Method | Purpose |
|--------|---------|
| `FindByIdAsync` | Retrieves a session by ID (no ownership check) |
| `FindAsync` | Retrieves a session by ID with ownership verification |
| `PageAsync` | Paginated listing with optional query filters |
| `NewAsync` | Creates a new session for a profile |
| `SaveAsync` | Persists changes to a session |
| `DeleteAsync` | Deletes a single session by ID |
| `DeleteAllAsync` | Deletes all sessions for a profile and current user |

### Implementing `IAIChatSessionManager` (YesSql Example)

Below is a simplified YesSql-based implementation following the pattern used in the MVC sample:

```csharp
public sealed class YesSqlAIChatSessionManager : IAIChatSessionManager
{
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public YesSqlAIChatSessionManager(
        ISession session,
        IClock clock,
        IHttpContextAccessor httpContextAccessor)
    {
        _session = session;
        _clock = clock;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<AIChatSession> FindByIdAsync(string id)
    {
        return await _session
            .Query<AIChatSession, AIChatSessionIndex>(x => x.SessionId == id)
            .FirstOrDefaultAsync();
    }

    public async Task<AIChatSession> FindAsync(string id)
    {
        var chatSession = await FindByIdAsync(id);

        if (chatSession == null)
        {
            return null;
        }

        // Verify ownership
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (chatSession.UserId != userId)
        {
            return null;
        }

        return chatSession;
    }

    public async Task<AIChatSession> NewAsync(AIProfile profile, NewAIChatSessionContext context)
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        var chatSession = new AIChatSession
        {
            SessionId = IdGenerator.GenerateId(),
            ProfileId = profile.Id,
            UserId = userId,
            ClientId = context?.ClientId,
            Status = ChatSessionStatus.Active,
            ResponseHandlerName = profile.ResponseHandlerName ?? "ai",
            CreatedUtc = _clock.UtcNow,
            LastActivityUtc = _clock.UtcNow,
        };

        await _session.SaveAsync(chatSession);
        await _session.SaveChangesAsync();

        return chatSession;
    }

    public async Task SaveAsync(AIChatSession chatSession)
    {
        await _session.SaveAsync(chatSession);
        await _session.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(string sessionId)
    {
        var chatSession = await FindByIdAsync(sessionId);
        if (chatSession == null)
        {
            return false;
        }

        _session.Delete(chatSession);
        await _session.SaveChangesAsync();

        return true;
    }

    public async Task<int> DeleteAllAsync(string profileId)
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        var sessions = await _session
            .Query<AIChatSession, AIChatSessionIndex>(x =>
                x.ProfileId == profileId && x.UserId == userId)
            .ListAsync();

        var count = 0;
        foreach (var session in sessions)
        {
            _session.Delete(session);
            count++;
        }

        if (count > 0)
        {
            await _session.SaveChangesAsync();
        }

        return count;
    }

    public async Task<AIChatSessionResult> PageAsync(
        int page, int pageSize, AIChatSessionQueryContext context = null)
    {
        var query = _session.Query<AIChatSession, AIChatSessionIndex>();

        if (!string.IsNullOrEmpty(context?.ProfileId))
        {
            query = query.Where(x => x.ProfileId == context.ProfileId);
        }

        var count = await query.CountAsync();
        var sessions = await query
            .OrderByDescending(x => x.LastActivityUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ListAsync();

        return new AIChatSessionResult
        {
            Count = count,
            Sessions = sessions.ToArray(),
        };
    }
}
```

:::tip
Use `IdGenerator.GenerateId()` (which produces 26-character IDs) for all session and prompt identifiers. Never use `Guid.NewGuid()`.
:::

### Implementing `IAIChatSessionPromptStore`

The prompt store persists the individual messages (user prompts and assistant responses) within a session. It extends `ICatalog<AIChatSessionPrompt>`.

```csharp
public interface IAIChatSessionPromptStore : ICatalog<AIChatSessionPrompt>
{
    Task<IReadOnlyList<AIChatSessionPrompt>> GetPromptsAsync(string sessionId);
    Task<int> DeleteAllPromptsAsync(string sessionId);
    Task<int> CountAsync(string sessionId);
}
```

A YesSql implementation follows the same pattern:

```csharp
public sealed class YesSqlAIChatSessionPromptStore : IAIChatSessionPromptStore
{
    private readonly ISession _session;

    public YesSqlAIChatSessionPromptStore(ISession session)
    {
        _session = session;
    }

    public async Task<IReadOnlyList<AIChatSessionPrompt>> GetPromptsAsync(string sessionId)
    {
        var prompts = await _session
            .Query<AIChatSessionPrompt, AIChatSessionPromptIndex>(
                x => x.SessionId == sessionId)
            .OrderBy(x => x.CreatedUtc)
            .ListAsync();

        return prompts.ToArray();
    }

    public async Task<int> DeleteAllPromptsAsync(string sessionId)
    {
        var prompts = await _session
            .Query<AIChatSessionPrompt, AIChatSessionPromptIndex>(
                x => x.SessionId == sessionId)
            .ListAsync();

        var count = 0;
        foreach (var prompt in prompts)
        {
            _session.Delete(prompt);
            count++;
        }

        if (count > 0)
        {
            await _session.SaveChangesAsync();
        }

        return count;
    }

    public async Task<int> CountAsync(string sessionId)
    {
        return await _session
            .Query<AIChatSessionPrompt, AIChatSessionPromptIndex>(
                x => x.SessionId == sessionId)
            .CountAsync();
    }

    // ICatalog<T> base methods (FindByIdAsync, CreateAsync, etc.)
    // follow the same YesSql query pattern.
}
```

### Implementing `IChatInteractionPromptStore`

The interaction prompt store is similar to the session prompt store but scoped to a `ChatInteraction` rather than a session. It extends `ICatalog<ChatInteractionPrompt>`.

```csharp
public interface IChatInteractionPromptStore : ICatalog<ChatInteractionPrompt>
{
    Task<IReadOnlyCollection<ChatInteractionPrompt>> GetPromptsAsync(string chatInteractionId);
    Task<int> DeleteAllPromptsAsync(string chatInteractionId);
}
```

The implementation follows the same YesSql patterns shown above, querying against `ChatInteractionPromptIndex` with a `ChatInteractionId` predicate.

## Chat Flow Example

Here is the end-to-end flow when a user sends a message through the `AIChatHub`:

```text
1. Client sends message via SignalR
       │
       ▼
2. AIChatHub.SendMessage()
       │
       ▼
3. Session Resolution (GetOrCreateSessionAsync)
   ├── If sessionId provided → FindAsync(sessionId)
   └── If no sessionId → NewAsync(profile, context)
       │
       ▼
4. Prompt Saved (IAIChatSessionPromptStore.CreateAsync)
   └── User message stored as AIChatSessionPrompt
       │
       ▼
5. Response Handler Resolved (IChatResponseHandlerResolver)
   └── Looks up handler by session.ResponseHandlerName
       │
       ▼
6. Handler Executes (IChatResponseHandler.HandleAsync)
   ├── Default "ai" handler:
   │   ├── Builds OrchestrationContext
   │   ├── Injects conversation history
   │   ├── Runs orchestrator (tool calls, RAG, etc.)
   │   └── Returns streaming response
   └── Custom handler (e.g., "live-agent"):
       └── Routes to external system
       │
       ▼
7. Response Streamed to Client
   └── Each ChatResponseUpdate is sent via SignalR
       │
       ▼
8. Completion Finalized
   ├── Assistant response saved as AIChatSessionPrompt
   ├── Session title generated (if first exchange)
   ├── LastActivityUtc updated
   └── Citations/references collected
```

### Code Walkthrough

```csharp
// Step 1-3: The hub resolves the session
var session = !string.IsNullOrEmpty(sessionId)
    ? await sessionManager.FindAsync(sessionId)
    : await sessionManager.NewAsync(profile, new NewAIChatSessionContext { ClientId = clientId });

// Step 4: Save the user prompt
var prompt = new AIChatSessionPrompt
{
    ItemId = IdGenerator.GenerateId(),
    SessionId = session.SessionId,
    Role = ChatRole.User,
    Content = userMessage,
    CreatedUtc = clock.UtcNow,
};
await promptStore.CreateAsync(prompt);

// Step 5: Resolve the response handler
var handler = handlerResolver.Resolve(session.ResponseHandlerName);

// Step 6: Execute the handler
var handlerContext = new ChatResponseHandlerContext
{
    Session = session,
    UserMessage = userMessage,
    // ... additional context
};
var result = await handler.HandleAsync(handlerContext, cancellationToken);

// Step 7: Stream the response
if (!result.IsDeferred)
{
    await foreach (var update in result.ResponseStream)
    {
        await Clients.Caller.ReceiveMessage(update);
    }
}

// Step 8: Save assistant response, update session
session.LastActivityUtc = clock.UtcNow;
await sessionManager.SaveAsync(session);
```

### `ChatResponseHandlerResult`

The result from a handler is either **streaming** or **deferred**:

```csharp
public sealed class ChatResponseHandlerResult
{
    public bool IsDeferred { get; init; }
    public IAsyncEnumerable<ChatResponseUpdate> ResponseStream { get; init; }

    // Factory methods:
    public static ChatResponseHandlerResult Deferred();
    public static ChatResponseHandlerResult Streaming(IAsyncEnumerable<ChatResponseUpdate> stream);
}
```

- **Streaming** — The hub immediately iterates `ResponseStream` and pushes updates to the client via SignalR.
- **Deferred** — The hub saves the user prompt and completes the request. The response arrives later (e.g., via a webhook callback). This is common for live-agent handoff scenarios.

## Error Handling

### Session Not Found

When `FindAsync()` returns `null`, the hub sends a localized error to the client and stops processing. No exception is thrown — this is a normal flow when sessions expire or are deleted.

### Completion Failures

If the AI provider throws during completion (e.g., rate limit, timeout, network error), the orchestrator catches the exception and the hub sends an error notification to the client:

```csharp
try
{
    await foreach (var update in result.ResponseStream)
    {
        await Clients.Caller.ReceiveMessage(update);
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Error streaming response for session {SessionId}", session.SessionId);
    await Clients.Caller.ReceiveError("An error occurred while processing your message.");
}
```

:::warning
The user's prompt is saved **before** the completion call. If completion fails, the prompt remains in history. This is intentional — it preserves the conversation state so the user can retry.
:::

### Handler Not Found

If `IChatResponseHandlerResolver.Resolve()` cannot find a handler matching `session.ResponseHandlerName`, it falls back to the default AI handler. If no handlers are registered at all, an error is returned to the client.

### Profile Not Found

When the requested AI profile does not exist or the user lacks permission, the hub returns a "profile not found" error without creating a session.

## Example: Transferring a Conversation

A response handler can transfer a conversation to a different handler mid-session:

```csharp
public sealed class EscalationHandler : IChatResponseHandler
{
    public string Name => "escalation";

    public async Task<ChatResponseHandlerResult> HandleAsync(
        ChatResponseHandlerContext context,
        CancellationToken cancellationToken)
    {
        // Transfer to live agent system
        context.Interaction.ResponseHandlerName = "live-agent";
        return ChatResponseHandlerResult.Transferred();
    }
}
```

## Orchard Core Integration

The [AI Chat module](../ai/chat.md) provides full admin UI for managing chat profiles, viewing session history, and configuring response handlers per profile.
