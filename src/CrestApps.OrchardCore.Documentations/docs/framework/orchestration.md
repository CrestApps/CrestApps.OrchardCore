---
sidebar_label: Orchestration
sidebar_position: 4
title: Orchestration
description: The orchestration pipeline manages tool calling, progressive scoping, RAG, and the agentic execution loop.
---

# Orchestration

> High-level agentic pipeline that manages tool calling, progressive scoping, retrieval-augmented generation, and streaming responses.

## Quick Start

```csharp
builder.Services
    .AddCrestAppsAI()
    .AddOrchestrationServices()
    .AddOpenAIProvider(); // at least one provider
```

Then inject and use:

```csharp
public class MyController(IOrchestrator orchestrator)
{
    public async IAsyncEnumerable<string> StreamAsync(OrchestrationContext context)
    {
        await foreach (var update in orchestrator.ExecuteStreamingAsync(context))
        {
            yield return update.Text;
        }
    }
}
```

## Problem & Solution

A raw completion call sends messages and returns text. Real AI applications need:

- **Tool calling** — the model invokes functions and uses results
- **Progressive scoping** — large tool sets are narrowed based on context
- **RAG** — relevant documents are retrieved and injected before completion
- **Streaming** — responses are streamed token-by-token for responsiveness
- **Response routing** — output may go to a chat UI, webhook, or external system

The orchestrator handles all of this in a single pipeline.

## Services Registered by `AddOrchestrationServices()`

| Service | Implementation | Lifetime | Purpose |
|---------|---------------|----------|---------|
| `IOrchestrator` | `DefaultOrchestrator` | Scoped | Agentic execution loop |
| `IOrchestratorResolver` | `DefaultOrchestratorResolver` | Scoped | Resolves orchestrators by name |
| `IToolRegistry` | `DefaultToolRegistry` | Scoped | Merges tools from all providers |
| `IAIToolsService` | `DefaultAIToolsService` | Scoped | Tool metadata and access control |
| `IChatResponseHandlerResolver` | `DefaultChatResponseHandlerResolver` | Scoped | Resolves response handlers by name |
| `IOrchestrationContextBuilder` | `DefaultOrchestrationContextBuilder` | Scoped | Builds orchestration context |
| `IExternalChatRelayManager` | `ExternalChatRelayConnectionManager` | Singleton | Manages external relay connections |

### Tool Registry Providers (all Scoped, enumerable)

| Provider | Purpose |
|----------|---------|
| `SystemToolRegistryProvider` | Built-in system tools (image gen, chart gen, date/time) |
| `ProfileToolRegistryProvider` | Tools assigned to the current AI profile |
| `AgentToolRegistryProvider` | Tools from agent configurations |

### Orchestration Context Handlers (all Scoped, enumerable)

| Handler | Purpose |
|---------|---------|
| `CompletionContextOrchestrationHandler` | Builds the AI completion context |
| `PreemptiveRagOrchestrationHandler` | Pre-fetches RAG data before completion |
| `AIToolExecutionContextOrchestrationHandler` | Sets up tool execution context |

### Built-in System Tools

| Tool | Purpose | Category |
|------|---------|----------|
| `GenerateImageTool` | DALL-E compatible image generation | Content Generation |
| `GenerateChartTool` | Chart.js configuration generation | Content Generation |
| `CurrentDateTimeTool` | Returns current date and time | Utilities (Selectable) |

## Key Interfaces

### `IOrchestrator`

The main entry point for agentic AI execution.

```csharp
public interface IOrchestrator
{
    string Name { get; }

    IAsyncEnumerable<StreamingChatCompletionUpdate> ExecuteStreamingAsync(
        OrchestrationContext context,
        CancellationToken cancellationToken = default);
}
```

### `IOrchestratorResolver`

Resolves an orchestrator by name. Falls back to the default if the name is not found.

```csharp
public interface IOrchestratorResolver
{
    IOrchestrator Resolve(string name = null);
}
```

### `IToolRegistryProvider`

Implement this to supply tools from a custom source.

```csharp
public interface IToolRegistryProvider
{
    ValueTask<IEnumerable<AIToolMetadataEntry>> GetToolsAsync(
        AICompletionContext context,
        CancellationToken cancellationToken = default);
}
```

### `IOrchestrationContextBuilder`

Builds the full `OrchestrationContext` by running all `IOrchestrationContextBuilderHandler` instances.

```csharp
public interface IOrchestrationContextBuilder
{
    ValueTask<OrchestrationContext> BuildAsync(
        OrchestrationContextBuildingContext context,
        CancellationToken cancellationToken = default);
}
```

## Progressive Tool Scoping

When an AI profile has many tools available (from agents, MCP connections, built-in tools, etc.), the orchestrator uses **progressive tool scoping** to avoid overwhelming the model's context window. Here is how the algorithm works:

### Scoping Thresholds

```text
Total Tools Available
        │
        ├── ≤ ScopingThreshold (30)
        │       └── All tools are injected directly. No scoping overhead.
        │
        ├── > ScopingThreshold (30) AND ≤ PlanningThreshold (100)
        │       └── Lightweight token-based relevance scoping (no LLM call).
        │           Tools are scored by how relevant their names/descriptions
        │           are to the current conversation context.
        │
        └── > PlanningThreshold (100) OR MCP tools present
                └── Full LLM planning phase:
                    1. Send recent conversation history to a utility model
                    2. Ask it to identify which tool categories are needed
                    3. Scope tools based on the plan
                    4. Then proceed with the main completion
```

### How Token-Based Scoring Works

When the tool count exceeds `ScopingThreshold` but stays below `PlanningThreshold`, the orchestrator:

1. Builds a **scoring context** from the user's latest message and the last assistant message
2. Tokenizes each tool's name, description, and parameter names
3. Scores each tool based on token overlap with the scoring context
4. Selects the top `InitialToolCount` (default: 20) tools
5. "Must include" tools (explicitly requested by the profile) are always included

### How LLM Planning Works

When tools exceed `PlanningThreshold` or MCP tools are present:

1. The orchestrator renders a planning prompt via `ITemplateService`
2. Sends the last `PlanningHistoryMessageCount` (default: 10) messages to a utility model
3. The utility model returns a plan identifying needed tool categories
4. Tools are scoped based on the plan
5. If planning fails (timeout, model error), the orchestrator **gracefully falls back** to token-based scoping

:::info
Planning uses a lightweight utility model (Temperature=0.1, MaxOutputTokens=300) to keep costs low. If the utility model is unavailable, the system falls back to the main completion model.
:::

### Configuration Reference

```csharp
services.Configure<DefaultOrchestratorOptions>(options =>
{
    options.InitialToolCount = 20;              // Tools loaded initially during scoping
    options.ScopingThreshold = 30;              // Below this: no scoping at all
    options.PlanningThreshold = 100;            // Above this: LLM planning phase
    options.MaxExpansionRounds = 3;             // Max rounds of progressive expansion
    options.MaxToolCount = 30;                  // Hard cap on active tools
    options.PlanningHistoryMessageCount = 10;   // Messages included in planning context
});
```

## Streaming vs Non-Streaming

The orchestrator supports both streaming and non-streaming completion. The choice depends on your use case.

### Streaming (Recommended for Chat UIs)

Streaming sends response tokens to the client as they are generated, providing a responsive "typing" experience:

```csharp
public sealed class StreamingChatService(IOrchestrator orchestrator)
{
    public async IAsyncEnumerable<string> StreamResponseAsync(
        OrchestrationContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in orchestrator.ExecuteStreamingAsync(context, cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }
    }
}
```

**When to use streaming:**
- Chat interfaces where responsiveness matters
- Long-form content generation (users see progress immediately)
- Any scenario where perceived latency is important

### Non-Streaming (Collecting the Full Response)

When you need the complete response before processing (e.g., for structured parsing, database storage, or API responses):

```csharp
public sealed class BatchCompletionService(IOrchestrator orchestrator)
{
    public async Task<string> GetCompleteResponseAsync(
        OrchestrationContext context,
        CancellationToken cancellationToken = default)
    {
        var fullResponse = new StringBuilder();

        await foreach (var update in orchestrator.ExecuteStreamingAsync(context, cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                fullResponse.Append(update.Text);
            }
        }

        return fullResponse.ToString();
    }
}
```

:::tip
The orchestrator always returns `IAsyncEnumerable<ChatResponseUpdate>`. For non-streaming scenarios, simply collect all updates into a `StringBuilder`. There is no separate non-streaming API — this keeps the interface simple and consistent.
:::

**When to use non-streaming:**
- Background processing (no user waiting)
- API endpoints that return a complete JSON response
- Post-processing that requires the full text (e.g., JSON parsing)

## Configuration

### `DefaultOrchestratorOptions`

Controls the progressive tool scoping behavior:

```csharp
services.Configure<DefaultOrchestratorOptions>(options =>
{
    options.InitialToolCount = 20;      // Tools loaded initially
    options.ScopingThreshold = 30;      // Trigger scoping when tools exceed this
    options.PlanningThreshold = 100;    // Trigger planning mode above this count
    options.MaxExpansionRounds = 3;     // Max rounds of tool expansion
    options.MaxToolCount = 30;          // Hard cap on active tools
    options.PlanningHistoryMessageCount = 10; // Messages used for planning
});
```

### `OrchestratorOptions`

```csharp
services.Configure<OrchestratorOptions>(options =>
{
    options.DefaultOrchestratorName = "Default Orchestrator";
});
```

## Registering a Custom Orchestrator

```csharp
services.AddOrchestrator<MyCustomOrchestrator>("My Orchestrator");
```

Your orchestrator must implement `IOrchestrator`. Here is a complete example that adds logging, custom pre-processing, and error recovery:

```csharp
public sealed class MyCustomOrchestrator : IOrchestrator
{
    private readonly IAICompletionService _completionService;
    private readonly ILogger<MyCustomOrchestrator> _logger;

    public MyCustomOrchestrator(
        IAICompletionService completionService,
        ILogger<MyCustomOrchestrator> logger)
    {
        _completionService = completionService;
        _logger = logger;
    }

    public string Name => "My Orchestrator";

    public async IAsyncEnumerable<ChatResponseUpdate> ExecuteStreamingAsync(
        OrchestrationContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Pre-processing: add custom system instructions
        context.SystemMessageBuilder.AppendLine("Always respond in formal English.");

        // Build messages from conversation history + current message
        var messages = new List<ChatMessage>();

        if (context.CompletionContext.SystemMessage is not null)
        {
            messages.Add(new ChatMessage(ChatRole.System, context.CompletionContext.SystemMessage));
        }

        messages.AddRange(context.ConversationHistory);
        messages.Add(new ChatMessage(ChatRole.User, context.UserMessage));

        // Resolve deployment
        var deployment = context.CompletionContext.Deployment
            ?? throw new InvalidOperationException("No deployment configured.");

        // Execute completion with error recovery
        IAsyncEnumerable<ChatResponseUpdate> stream;

        try
        {
            stream = _completionService.CompleteStreamingAsync(
                deployment, messages, context.CompletionContext.Options, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Completion failed for orchestrator '{Name}'.", Name);
            yield return new ChatResponseUpdate { Text = "I'm sorry, I encountered an error. Please try again." };
            yield break;
        }

        await foreach (var update in stream.WithCancellation(cancellationToken))
        {
            yield return update;
        }
    }
}
```

## Error Recovery

The orchestrator has a layered error handling strategy:

### Planning Failures (Graceful Degradation)

When the LLM planning phase fails (e.g., the utility model times out or returns an error), the orchestrator **does not fail the request**. Instead:

1. The exception is caught and logged at Warning level
2. Planning returns `null`
3. The orchestrator falls back to token-based tool scoping
4. The user's request is still processed

```text
Planning fails → Log warning → Fall back to token scoping → Continue
```

### Deployment Resolution Failures (Fatal)

If no deployment can be resolved (no profile-level, connection-level, or global default), an `InvalidOperationException` is thrown. This is a configuration error that must be fixed.

### Cancellation

`OperationCanceledException` is always re-thrown immediately — it is never caught or suppressed. This ensures the cancellation token works correctly through the entire pipeline.

### Provider Errors

Errors from AI providers (rate limits, authentication failures, server errors) propagate up to the caller. The orchestrator does not retry automatically — retry policies should be configured at the HTTP client level or in the provider.

| Error Type | Behavior |
|-----------|----------|
| Planning failure | Graceful fallback to token scoping |
| Missing deployment | `InvalidOperationException` (fatal) |
| `OperationCanceledException` | Re-thrown immediately |
| Provider error (rate limit, auth, etc.) | Propagated to caller |
| Tool execution error | Logged, tool result indicates failure, model continues |

## Orchard Core Integration

The [AI Services module](../ai/overview.md) wraps orchestration with admin UI for selecting orchestrators per profile, configuring tool assignments, and managing deployment fallback chains.
