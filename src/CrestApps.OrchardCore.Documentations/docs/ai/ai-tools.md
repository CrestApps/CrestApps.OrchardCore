---
sidebar_label: AI Tools
sidebar_position: 10
title: AI Tool Management
description: How to create, register, and manage custom AI tools in Orchard Core.
---

# AI Tool Management

This section is part of the **AI Services** (`CrestApps.OrchardCore.AI`) feature.

## Extending AI Chat with Custom Functions

You can enhance the AI chat functionality by adding custom functions. To create a custom function, inherit from `AIFunction` and register it as a service. AI tools are registered as singletons, so dependencies must be resolved at execution time using `arguments.Services`.

Below is an example of a custom function that retrieves weather information based on the user's location:

```csharp
public sealed class GetWeatherFunction : AIFunction
{
    public const string TheName = "get_weather";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
     {
       "type": "object",
       "properties": {
         "Location": {
           "type": "string",
           "description": "The geographic location for which the weather information is requested."
         }
       },
       "additionalProperties": false,
       "required": ["Location"]
     }
    """);

    public override string Name => TheName;

    public override string Description => "Retrieves weather information for a specified location.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        // For dependencies, resolve them at runtime via arguments.Services:
        // var someService = arguments.Services.GetRequiredService<ISomeService>();

        if (!arguments.TryGetValue("Location", out var prompt) || prompt is null) 
        {
            return ValueTask.FromResult<object>("Location is required.");
        }

        string location = null;

        if (prompt is JsonElement jsonElement)
        {
            location = jsonElement.GetString();
        }
        else
        {
            location = prompt?.ToString();
        }

        var weather = Random.Shared.NextDouble() > 0.5 ? $"It's sunny in {location}." : $"It's raining in {location}.";

        return ValueTask.FromResult<object>(weather);
    }
}
```

## Registering the Custom Function

To register the custom function, add it as a service in the `Startup` class. AI tools use a fluent builder pattern for registration:

```csharp
services.AddAITool<GetWeatherFunction>(GetWeatherFunction.TheName)
    .WithTitle("Weather Getter")
    .WithDescription("Retrieves weather information for a specified location.")
    .WithCategory("Service")
    .Selectable();
```

### Builder Methods

| Method | Description |
| --- | --- |
| `.WithTitle(string)` | Sets the display title for the tool. |
| `.WithDescription(string)` | Sets the description shown in the UI and used by the orchestrator for planning. |
| `.WithCategory(string)` | Sets the category for grouping in the UI. |
| `.WithPurpose(string)` | Tags the tool with a purpose identifier (e.g., `AIToolPurposes.DocumentProcessing`). The orchestrator uses purpose tags to dynamically discover tools by function. |
| `.Selectable()` | Makes the tool visible in the UI for user selection. **By default, tools are system tools** (hidden from the UI and managed by the orchestrator). Call `.Selectable()` to allow users to select the tool in Chat Interactions or AI Profiles. |

### System Tools vs. Selectable Tools

- **System tools** (default): Automatically included by the orchestrator based on context. Not shown in the UI. Ideal for document processing, content generation, or other infrastructure tools.
- **Selectable tools**: Visible in the UI for users to choose per Chat Interaction or AI Profile. Use `.Selectable()` when the tool represents a user-facing capability.

#### Context-Dependent System Tools

Some system tool purposes are **context-gated** — the tool is only included in the tool registry when the relevant data is available:

| Purpose | Condition | Examples |
| --- | --- | --- |
| `AIToolPurposes.DataSourceSearch` | `AICompletionContext.DataSourceId` is set (a data source is attached to the profile or interaction) | `search_data_sources` |
| `AIToolPurposes.DocumentProcessing` | `AICompletionContextKeys.HasDocuments` is set in `AICompletionContext.AdditionalProperties` (documents are attached) | `search_documents`, `list_documents`, `read_document`, `read_tabular_data` |
| `AIToolPurposes.ContentGeneration` | Always included (no gating) | `generate_image`, `generate_chart` |
| No purpose set | Always included (no gating) | Any custom system tool without a purpose tag |

This ensures the AI model never sees tools it cannot use, preventing hallucinated tool calls and reducing token overhead.

```csharp
// System tool (hidden from UI, orchestrator-managed)
services.AddAITool<ListDocumentsTool>(ListDocumentsTool.TheName)
    .WithTitle("List Documents")
    .WithDescription("Lists all documents attached to the current chat session.")
    .WithPurpose(AIToolPurposes.DocumentProcessing);

// Selectable tool (visible in UI for user selection)
services.AddAITool<SearchForContentsTool>(SearchForContentsTool.TheName)
    .WithTitle("Search Content Items")
    .WithDescription("Provides a way to search for content items.")
    .WithCategory("Content Management")
    .Selectable();
```

### Well-Known Purpose Constants

The `AIToolPurposes` class provides well-known purpose identifiers:

| Constant | Value | Description |
| --- | --- | --- |
| `AIToolPurposes.DocumentProcessing` | `"document_processing"` | Tools that process, read, search, or manage documents attached to a chat session. Only included when documents are available. |
| `AIToolPurposes.DataSourceSearch` | `"data_source_search"` | Tools that search data source embeddings for RAG. Only included when a data source is attached. |
| `AIToolPurposes.ContentGeneration` | `"content_generation"` | Tools that generate content such as images or charts. Always included. |

You can also define custom purpose strings for domain-specific tool grouping.

Once registered, the function can be accessed via `IAIToolsService` in your module, which resolves tools by their name using keyed service resolution.

## Invocation Context (`AIInvocationScope`)

AI tools are registered as **singletons**, yet they often need access to per-request data such as the current AI provider, connection, or the resource (profile/interaction) that initiated the request. In addition, when SignalR is used for real-time chat, the same `HttpContext` is shared across all hub method invocations on the same WebSocket connection. Writing per-request data to `HttpContext.Items` would leak state between concurrent or sequential chat messages from the same user.

To solve this, the system uses **`AIInvocationScope`** — an `AsyncLocal<T>`-backed ambient context that provides true invocation-scoped isolation.

### How It Works

1. **Hub starts a scope**: When a SignalR hub method (e.g., `AIChatHub.HandlePromptAsync`) begins, it creates a scope:

   ```csharp
   using var invocationScope = AIInvocationScope.Begin();
   ```

   This sets `AIInvocationScope.Current` to a fresh `AIInvocationContext` for the duration of the method, and automatically clears it on `Dispose()`.

2. **Orchestration handlers populate the context**: During orchestration context building, handlers like `AIToolExecutionContextOrchestrationHandler` write to `AIInvocationScope.Current`:

   ```csharp
   var invocationContext = AIInvocationScope.Current;
   invocationContext.ToolExecutionContext = new AIToolExecutionContext(resource) { ... };
   ```

3. **Tools read from the scope**: When the AI model calls a tool (e.g., `DataSourceSearchTool`), the tool retrieves the current context:

   ```csharp
   var invocationContext = AIInvocationScope.Current;
   var executionContext = invocationContext?.ToolExecutionContext;
   var dataSourceId = invocationContext?.DataSourceId;
   ```

   Because `AsyncLocal<T>` flows through `async`/`await` continuations, each concurrent invocation sees its own isolated context — even though the tool is a singleton shared across all requests.

### `AIInvocationContext` Properties

| Property | Type | Description |
| --- | --- | --- |
| `ToolExecutionContext` | `AIToolExecutionContext` | Provider name, connection name, and the initiating resource. |
| `DataSourceId` | `string` | The data source ID for data source search tools. |
| `ToolReferences` | `Dictionary<string, AICompletionReference>` | Citation references collected by tools during execution. |
| `Items` | `Dictionary<string, object>` | General-purpose extensibility bag for per-invocation data (e.g., the hub stores `AIChatSession` under the key `"AIChatSession"`). |

### Shared Reference Counter

`AIInvocationContext.NextReferenceIndex()` returns a monotonically increasing integer (thread-safe via `Interlocked.Increment`). All citation-producing components — preemptive RAG handlers and search tools — use this shared counter so that `[doc:N]` indices never collide, even when data source and document references are generated in the same request.

### Example: Custom Tool Using Invocation Context

```csharp
public sealed class MyCustomTool : AIFunction
{
    public override string Name => "my_custom_tool";
    public override string Description => "A custom tool that uses invocation context.";

    protected override ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        // Access the current invocation context (no DI, no HttpContext needed).
        var invocationContext = AIInvocationScope.Current;

        if (invocationContext is null)
        {
            return ValueTask.FromResult<object>("No invocation context available.");
        }

        // Access provider/resource info.
        var executionContext = invocationContext.ToolExecutionContext;
        var providerName = executionContext?.ProviderName;

        // Use the shared reference counter for citations.
        var refIndex = invocationContext.NextReferenceIndex();

        // Store references for downstream collection.
        invocationContext.ToolReferences.TryAdd(
            $"[doc:{refIndex}]",
            new AICompletionReference
            {
                Text = "My Source",
                Title = "My Source",
                Index = refIndex,
                ReferenceId = "my-source-id",
                ReferenceType = "MyCustomType",
            });

        return ValueTask.FromResult<object>($"[doc:{refIndex}] Result from my custom tool.");
    }
}
```

### How the Citation Pipeline Works

The citation system ensures that every `[doc:N]` marker in an AI response maps to a unique, resolvable reference — even when references come from multiple sources (data source preemptive RAG, document preemptive RAG, and AI-invoked tool calls) within the same request.

#### Reference Lifecycle

1. **Scope creation**: The SignalR hub wraps each invocation in `using var scope = AIInvocationScope.Begin()`. This creates a fresh `AIInvocationContext` that is visible to all code in the same async flow.

2. **Preemptive RAG handlers** (run during orchestration context building):
   - `DataSourcePreemptiveRagOrchestrationHandler` searches the configured data source, generates `[doc:N]` markers, and stores references in `orchestrationContext.Properties["DataSourceReferences"]`. It calls `AIInvocationScope.Current.NextReferenceIndex()` for each reference.
   - `DocumentPreemptiveRagHandler` does the same for uploaded documents, storing in `orchestrationContext.Properties["DocumentReferences"]`. Because it uses the same shared counter, its indices continue where data sources left off.
   - After all handlers have run, `PreemptiveRagOrchestrationHandler` evaluates the `IsInScope` constraint. If no references were produced and `IsInScope` is enabled, a scoping directive is injected. When tools are available, the directive encourages the model to try tool-based search before concluding no answer exists.

3. **Pre-stream collection**: Before the streaming loop begins, `CitationReferenceCollector.CollectPreemptiveReferences()` merges the preemptive RAG references into the `references` dictionary and resolves their links. The first streaming chunk sent to the client already includes these references.

4. **Tool-invoked searches** (run during streaming when the AI model calls tools):
   - `DataSourceSearchTool` and `SearchDocumentsTool` use the same `NextReferenceIndex()` counter and write their references to `AIInvocationScope.Current.ToolReferences`.

5. **Incremental collection**: Inside the streaming loop, `CitationReferenceCollector.CollectToolReferences()` checks for newly added tool references on each chunk and merges them into the `references` dictionary. Since the dictionary is passed by reference to each `CompletionPartialMessage`, the client receives progressively richer reference data.

6. **Final collection**: After the streaming loop completes, a final `CollectToolReferences()` call picks up any references from the last tool execution.

7. **Link resolution**: `CompositeAIReferenceLinkResolver` dispatches each reference to a keyed `IAIReferenceLinkResolver` based on its `ReferenceType`, generating the appropriate URL.

8. **Client rendering**: The JavaScript client (`ai-chat.js` / `chat-interaction.js`) accumulates references during streaming, then performs a final rendering pass:
   - **Filter**: Only references whose `[doc:N]` key appears in the response text are included.
   - **Sort & remap**: Cited references are sorted by their original index and assigned sequential **display indices** starting at 1. For example, if only `[doc:2]` and `[doc:5]` were cited, the user sees superscripts **1** and **2** (not 2 and 5). This avoids confusing gaps in the visible numbering.
   - **Two-phase replacement**: To prevent collisions during remapping, all markers are first replaced with unique placeholders, then placeholders are replaced with their final display indices.
   - **Reference list**: A numbered list is appended at the end of the message. References with a link are rendered as clickable titles; references without a link (e.g., uploaded documents) are shown as plain text.

#### Text Normalization

Before content is chunked and embedded during data source or document indexing, the `RagTextNormalizer` utility applies a multi-stage normalization pipeline:

1. **HTML stripping** — `<br>` variants become newlines; block-level close tags become newlines; all remaining tags are removed; entities decoded via `WebUtility.HtmlDecode`
2. **Markdown parsing** — The HTML-stripped text is fed to `MarkdownReader` from [`Microsoft.Extensions.DataIngestion.Markdig`](https://www.nuget.org/packages/Microsoft.Extensions.DataIngestion.Markdig), which parses the content into structured `IngestionDocument` elements and extracts plain text
3. **Whitespace normalization** — Horizontal runs collapsed to single spaces; 3+ newlines collapsed to double newlines

For **chunking**, `RagTextNormalizer.NormalizeAndChunkAsync()` combines normalization with `DocumentTokenChunker` from [`Microsoft.Extensions.DataIngestion`](https://www.nuget.org/packages/Microsoft.Extensions.DataIngestion), producing token-aware chunks (500 tokens max, 50 token overlap) using the GPT-4o tokenizer.

Titles are also normalized when citation references are created, as a defense-in-depth measure for data indexed before this feature was added. After upgrading, re-index your data sources to fully benefit from normalization.

#### Why AsyncLocal Is Safe for Singletons

AI tools are registered as singletons — one instance shared across all requests. The AI model calls tools without passing any invocation identifier. Tools retrieve context via `AIInvocationScope.Current`, which reads from an `AsyncLocal<AIInvocationContext>`.

`AsyncLocal<T>` works by storing its value on the .NET `ExecutionContext`, which is captured at every `await` and restored on the continuation — even if it runs on a different thread-pool thread. Critically, when `Task.Run` or similar APIs fork a new async flow, the child gets a **copy** of the parent's `ExecutionContext`. Writes to the copy do not affect the parent or sibling flows.

This means:
- Two concurrent SignalR hub invocations on the same WebSocket connection each have their own `AIInvocationContext`.
- A singleton tool called concurrently from two invocations sees the correct context for each call.
- No locks or request-ID passing are needed.
