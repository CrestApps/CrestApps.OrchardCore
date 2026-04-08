---
sidebar_label: AI Memory
sidebar_position: 13
title: AI Memory
description: Persistent, user-scoped long-term memory that enables AI conversations to retain and recall durable contextual information across sessions.
---

:::info Canonical framework docs
The shared framework guidance now lives in **[CrestApps.Core](https://core.crestapps.com/docs/framework/ai-memory)**. This Orchard Core page is kept for Orchard-specific integration context and cross-links.
:::

# AI Memory

> A persistent memory system that lets AI conversations remember user preferences, active projects, recurring topics, and other durable facts across sessions.

## Quick Start

Enable the `CrestApps.OrchardCore.AI.Memory` feature. Memory requires the AI Chat Core feature and an indexing provider:

```csharp
builder.Services
    .AddCrestAppsAI()
    .AddOrchestrationServices()
    .AddAIMemoryServices()
    .AddOpenAIProvider(); // at least one provider
```

`AddAIMemoryServices()` registers the shared framework behavior — safety validation, memory tools, orchestration handlers, preemptive memory retrieval, the shared memory-indexing service, and the default semantic memory search service. Hosts still provide the durable store plus any provider-specific vector-search adapters (`IAIMemoryStore`, `ISearchIndexProfileStore`, and keyed `IMemoryVectorSearchService`) while configuring runtime options such as `AIMemoryOptions`, `GeneralAIOptions`, and `ChatInteractionMemoryOptions` through standard `IOptions<>` registration.

The shared memory search service now reuses the active request scope, so preemptive memory retrieval, `search_user_memories`, and `remove_user_memory` work in both Orchard Core and `CrestApps.Core.Mvc.Web` without opening a nested YesSql scope that can trigger SQLite locking.

The Orchard Core memory module layers its YesSql storage, indexing, and admin UI on top of that shared framework registration. Once enabled, authenticated users gain four AI-callable memory tools out of the box.

## Problem & Solution

Standard AI conversations are stateless — every session starts from scratch. Users must re-explain their preferences, projects, and context each time. This creates friction and makes AI assistants feel impersonal.

AI Memory solves this by providing:

- **Durable storage** — memories persist across sessions in a YesSql-backed catalog
- **User scoping** — each user's memories are private and isolated
- **Safety validation** — sensitive data (SSNs, credit cards, API keys) is blocked before storage
- **Semantic search** — vector embeddings enable meaning-based retrieval, not just keyword matching
- **Automatic integration** — the orchestration pipeline injects memory tools when the user is authenticated and memory is enabled

## Core Concepts

### Memory Entries

A memory entry is a discrete fact about a user. Each entry has a short name, a semantic description, and the actual content:

| Field | Type | Constraints | Purpose |
|-------|------|------------|---------|
| `ItemId` | `string` | 26 chars (inherited from `CatalogItem`) | Unique identifier |
| `UserId` | `string` | Required | Owner of the memory |
| `Name` | `string` | Max 256 chars | Short identifier (e.g., `"preferred-language"`) |
| `Description` | `string` | Max 1000 chars | Semantic summary for search relevance |
| `Content` | `string` | Max 4000 chars | The actual memory data |
| `CreatedUtc` | `DateTime` | Auto-set | When the memory was first created |
| `UpdatedUtc` | `DateTime` | Auto-set | When the memory was last modified |

```csharp
public sealed class AIMemoryEntry : CatalogItem
{
    public string UserId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Content { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
```

### User Scoping

All memory operations are scoped to the authenticated user. The user ID is resolved from:

1. `AIInvocationScope.Current.Items[MemoryConstants.CompletionContextKeys.UserId]` (set during orchestration)
2. `HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)` (fallback)
3. `HttpContext.User.FindFirstValue(ClaimTypes.Name)` (final fallback for hosts that only issue name-based cookie identities)

Anonymous users cannot create, search, or manage memories.

### Safety Validation

Every save operation passes through `IAIMemorySafetyService` before persisting. The default implementation rejects:

- Empty or whitespace-only fields
- Sensitive keywords (`password`, `api_key`, `secret`, `access_token`, `refresh_token`, `private_key`, `connection_string`, `credit_card`, `ssn`, `social_security`)
- Social Security Number patterns (`###-##-####`)
- Credit card numbers (13–19 digit sequences validated with the Luhn algorithm)

## Key Interfaces

| Interface | Namespace | Purpose |
|-----------|-----------|---------|
| `IAIMemoryStore` | `CrestApps.Core.AI` | CRUD operations for memory entries, extends `ICatalog<AIMemoryEntry>` |
| `IAIMemorySearchService` | `CrestApps.Core.AI` | Shared semantic memory search service used by memory tools and preemptive RAG |
| `IMemoryVectorSearchService` | `CrestApps.Core.AI` | Provider-specific vector-search adapter resolved by search-provider name |
| `IAIMemorySafetyService` | `CrestApps.Core.AI` | Validates memory content against sensitive data patterns |
| `ICatalogEntryHandler<AIMemoryEntry>` | `CrestApps.Core.Services` | Lifecycle hooks for memory create/update/delete events |
| `IOrchestrationContextBuilderHandler` | `CrestApps.Core.AI.Orchestration` | Injects memory tools and context into the orchestration pipeline |
| `IPreemptiveRagHandler` | `CrestApps.Core.AI.Orchestration` | Proactively retrieves relevant memories before AI responds |

### `IAIMemoryStore`

Extends `ICatalog<AIMemoryEntry>` with user-scoped queries:

```csharp
public interface IAIMemoryStore : ICatalog<AIMemoryEntry>
{
    Task<int> CountByUserAsync(string userId);
    Task<AIMemoryEntry> FindByUserAndNameAsync(string userId, string name);
    Task<IReadOnlyCollection<AIMemoryEntry>> GetByUserAsync(string userId, int limit = 100);
}
```

### `IAIMemorySafetyService`

Validates memory content before persistence:

```csharp
public interface IAIMemorySafetyService
{
    bool TryValidate(string name, string description, string content, out string errorMessage);
}
```

### `IAIMemorySearchService`

This shared abstraction is implemented by the framework runtime so Orchard Core, MVC, or any other host can reuse the same memory-search behavior:

```csharp
public interface IAIMemorySearchService
{
    Task<IEnumerable<AIMemorySearchResult>> SearchAsync(
        string userId,
        IEnumerable<string> queries,
        int? requestedTopN,
        CancellationToken cancellationToken = default);
}
```

### Runtime Options

Shared memory orchestration now reads runtime settings through `IOptions<>` instead of host-specific provider interfaces:

- `IOptions<AIMemoryOptions>` controls which AI Memory index profile is used at runtime and the default `TopN` result count.
- `IOptions<GeneralAIOptions>` controls cross-cutting AI runtime behavior such as preemptive memory retrieval.
- `IOptions<ChatInteractionMemoryOptions>` controls whether ad-hoc chat interactions expose user memory by default.

MVC binds those options from its `App_Data/appsettings.json`-backed sections, while Orchard Core composes tenant site settings into scoped options so the shared framework code stays host-agnostic. In the MVC admin UI, the related toggles now live together under the **Memory** and **Orchestration** settings cards instead of being split across separate cards.

## Memory Tools

When memory is enabled and the user is authenticated, the orchestration handler adds four tools to the AI completion context. All tools use the purpose tag `AIToolPurposes.Memory`.

### `save_user_memory`

Creates or updates a durable memory for the authenticated user.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `name` | `string` | Yes | Short identifier (max 256 chars) |
| `description` | `string` | Yes | Semantic summary of the memory category/name, not a duplicate of the stored value (max 1000 chars) |
| `content` | `string` | Yes | Memory data (max 4000 chars) |

**Behavior:**
1. Validates all fields are non-empty
2. Runs `IAIMemorySafetyService.TryValidate()` to block sensitive data
3. Checks if a memory with the same name exists for this user
4. Creates a new entry or updates the existing one
5. Returns the saved memory with `ItemId`, timestamps, and a `created` flag

The tool stores the provided `description` as-is. Prompt guidance should keep `description` category-oriented and `content` value-oriented.

The default memory-availability prompt also tells the model to save durable facts proactively. Names, preferences, likes/dislikes, roles, active projects, and recurring interests should trigger `save_user_memory` automatically unless the content is sensitive and must be rejected. When a user shares multiple durable facts, the model should save them as separate memories instead of bundling unrelated facts into one entry.

### `search_user_memories`

Performs semantic vector search over the user's memory embeddings.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `query` | `string` | Yes | Natural language search query |
| `top_n` | `int` | No | Maximum results to return |

**Behavior:** Generates an embedding for the query, searches the configured vector index, and returns matching memories ranked by relevance score.

### `list_user_memories`

Lists all saved memories for the authenticated user.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `limit` | `int` | No | Maximum results (default: 25, range: 1–100) |

**Behavior:** Retrieves memories from the `IAIMemoryStore` ordered by storage. Returns all fields including timestamps.

### `remove_user_memory`

Deletes a previously saved memory by name.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `name` | `string` | Yes | Name of the memory to delete (max 256 chars) |

**Behavior:** Looks up the memory by user ID and name. Returns an error if not found. On success, deletes through `ICatalogManager<AIMemoryEntry>` which triggers handler lifecycle events (including deferred vector index removal).

### Tool Registration

All four tools are registered as system tools (not selectable in UI) with the `Memory` purpose:

```csharp
services.AddAITool<SearchUserMemoriesTool>(SearchUserMemoriesTool.TheName)
    .WithTitle("Search User Memories")
    .WithDescription("Search the current authenticated user's long-term memory...")
    .WithPurpose(AIToolPurposes.Memory);

services.AddAITool<ListUserMemoriesTool>(ListUserMemoriesTool.TheName)
    .WithTitle("List User Memories")
    .WithDescription("List the current authenticated user's saved long-term memories...")
    .WithPurpose(AIToolPurposes.Memory);

services.AddAITool<SaveUserMemoryTool>(SaveUserMemoryTool.TheName)
    .WithTitle("Save User Memory")
    .WithDescription("Create or update a long-term memory for the current authenticated user...")
    .WithPurpose(AIToolPurposes.Memory);

services.AddAITool<RemoveUserMemoryTool>(RemoveUserMemoryTool.TheName)
    .WithTitle("Remove User Memory")
    .WithDescription("Remove a previously saved long-term memory...")
    .WithPurpose(AIToolPurposes.Memory);
```

## Implementing `IAIMemoryStore`

The module ships with `DefaultAIMemoryStore`, a YesSql-backed implementation. Here is the pattern it follows.

### YesSql Index

Define an index for efficient querying:

```csharp
public sealed class AIMemoryEntryIndex : CatalogItemIndex
{
    public string UserId { get; set; }
    public string Name { get; set; }
    public string NormalizedName { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
```

### Index Provider

Map `AIMemoryEntry` to the index:

```csharp
internal sealed class AIMemoryEntryIndexProvider : IndexProvider<AIMemoryEntry>
{
    private readonly ILookupNormalizer _lookupNormalizer;

    public AIMemoryEntryIndexProvider(ILookupNormalizer lookupNormalizer)
    {
        _lookupNormalizer = lookupNormalizer;
        Collection = MemoryConstants.CollectionName;
    }

    public override void Describe(DescribeContext<AIMemoryEntry> context)
    {
        context.For<AIMemoryEntryIndex>()
            .Map(entry => new AIMemoryEntryIndex
            {
                ItemId = entry.ItemId,
                UserId = entry.UserId,
                Name = entry.Name,
                NormalizedName = _lookupNormalizer.NormalizeName(entry.Name),
                CreatedUtc = entry.CreatedUtc,
                UpdatedUtc = entry.UpdatedUtc,
            });
    }
}
```

### Store Implementation

The built-in store extends `DocumentCatalog<AIMemoryEntry, AIMemoryEntryIndex>`:

```csharp
public sealed class DefaultAIMemoryStore
    : DocumentCatalog<AIMemoryEntry, AIMemoryEntryIndex>, IAIMemoryStore
{
    private readonly ILookupNormalizer _lookupNormalizer;

    public DefaultAIMemoryStore(ISession session, ILookupNormalizer lookupNormalizer)
        : base(session)
    {
        _lookupNormalizer = lookupNormalizer;
    }

    public Task<int> CountByUserAsync(string userId)
        => Query(index => index.UserId == userId).CountAsync();

    public async Task<AIMemoryEntry> FindByUserAndNameAsync(string userId, string name)
    {
        var normalizedName = _lookupNormalizer.NormalizeName(name);
        return await Query(index =>
            index.UserId == userId &&
            index.NormalizedName == normalizedName
        ).FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyCollection<AIMemoryEntry>> GetByUserAsync(
        string userId, int limit = 100)
        => await Query(index => index.UserId == userId)
            .Take(limit)
            .ListAsync() as IReadOnlyCollection<AIMemoryEntry>;
}
```

### Catalog Manager

Route all write operations through `ICatalogManager<AIMemoryEntry>` so that lifecycle handlers run:

```csharp
public sealed class DefaultAIMemoryManager : CatalogManager<AIMemoryEntry>
{
    public DefaultAIMemoryManager(
        IAIMemoryStore catalog,
        IEnumerable<ICatalogEntryHandler<AIMemoryEntry>> handlers,
        ILogger<DefaultAIMemoryManager> logger)
        : base(catalog, handlers, logger)
    {
    }
}
```

## Safety Validation

### Default Implementation

`DefaultAIMemorySafetyService` validates every save request using four checks:

1. **Required fields** — name, description, and content must be non-empty
2. **Sensitive keywords** — regex scan for `password`, `api_key`, `secret`, `access_token`, `refresh_token`, `private_key`, `connection_string`, `credit_card`, `ssn`, `social_security`
3. **SSN patterns** — matches `\b\d{3}-\d{2}-\d{4}\b`
4. **Credit card numbers** — finds 13–19 digit sequences and validates each with the Luhn algorithm

```csharp
public sealed class DefaultAIMemorySafetyService : IAIMemorySafetyService
{
    public bool TryValidate(
        string name, string description, string content,
        out string errorMessage)
    {
        // 1. Check for null/whitespace
        // 2. Check sensitive keywords via regex
        // 3. Check SSN pattern
        // 4. Check credit card numbers with Luhn validation
        // Returns false with errorMessage on failure
    }
}
```

### Custom Safety Service

Replace the default validation by registering your own implementation:

```csharp
services.AddScoped<IAIMemorySafetyService, StrictMemorySafetyService>();
```

```csharp
public sealed class StrictMemorySafetyService : IAIMemorySafetyService
{
    public bool TryValidate(
        string name, string description, string content,
        out string errorMessage)
    {
        // Add your custom validation rules
        if (content.Length > 2000)
        {
            errorMessage = "Content exceeds custom limit.";
            return false;
        }

        errorMessage = null;
        return true;
    }
}
```

## Orchestration Integration

Memory integrates into the orchestration pipeline through two handlers.

### `AIMemoryOrchestrationHandler`

Implements `IOrchestrationContextBuilderHandler`. Runs during the `BuiltAsync` phase of context building:

1. **Checks prerequisites** — verifies the user is authenticated and memory is enabled for the current resource (AI profile or chat interaction)
2. **Injects memory availability** — renders the `memory-availability` template into the system message so the model knows memory tools are available
3. **Registers memory tools** — adds all four tool definitions to `MustIncludeTools` in the completion context
4. **Sets context flags** — sets `AICompletionContextKeys.HasMemory = true` and stores the user ID in `AIInvocationScope.Current.Items`

### `AIMemoryPreemptiveRagHandler`

Implements `IPreemptiveRagHandler`. Proactively searches user memories before the AI model generates a response:

1. **Checks prerequisites** — verifies the user is authenticated, memory is enabled, and preemptive retrieval is configured
2. **Extracts queries** — derives search queries from the conversation content
3. **Searches vector index** — calls `AIMemorySearchService` with the extracted queries
4. **Injects results** — renders matching memories into the system message via the `memory-context-header` template

This ensures the AI model has relevant user context even before it decides to call any memory tools explicitly.

### Lifecycle Handlers

`AIMemoryEntryHandler` implements `ICatalogEntryHandler<AIMemoryEntry>` and defers vector indexing work to `ShellScope.AddDeferredTask(...)`:

- **Created** — schedules embedding generation and vector index insertion
- **Updated** — schedules re-embedding and vector index update
- **Deleted** — schedules vector index removal

This pattern ensures that external indexing operations run outside the catalog transaction, following the deferred task convention used throughout the framework.

## Vector Search

Memory entries are embedded and stored in external vector indexes for semantic search.

### Architecture

```
┌──────────────────┐     ┌─────────────────────┐     ┌─────────────────────┐
│ AIMemoryEntry     │────▶│ AIMemoryIndexing     │────▶│ Vector Index         │
│ (YesSql catalog) │     │ Service               │     │ (Elasticsearch or   │
│                  │     │ - Generate embedding  │     │  Azure AI Search)   │
│                  │     │ - Create/update doc   │     │                     │
└──────────────────┘     └─────────────────────┘     └─────────────────────┘
                                                              │
┌──────────────────┐     ┌─────────────────────┐              │
│ Search query      │────▶│ AIMemorySearch       │◀────────────┘
│                  │     │ Service               │
│                  │     │ - Embed query         │
│                  │     │ - Search index        │
│                  │     │ - Deduplicate + rank  │
└──────────────────┘     └─────────────────────┘
```

### Index Document Structure

Each memory entry is projected into an `AIMemoryEntryIndexDocument` for the vector store:

| Field | Type | Purpose |
|-------|------|---------|
| `MemoryId` | `string` | Links back to `AIMemoryEntry.ItemId` |
| `UserId` | `string` | Scopes search to the owning user |
| `Name` | `string` | Memory name |
| `Description` | `string` | Semantic description |
| `Content` | `string` | Memory content |
| `UpdatedUtc` | `DateTime` | Freshness indicator |
| `Embedding` | `float[]` | Vector embedding generated from the entry |

### Search Flow

1. `SearchUserMemoriesTool` receives a query string
2. `AIMemorySearchService` generates an embedding for the query
3. The service calls `IMemoryVectorSearchService.SearchAsync()` with the embedding, user ID, and top N
4. The vector store returns `AIMemorySearchResult` items ranked by cosine similarity
5. Results are deduplicated by memory ID (keeping the highest score) and returned

### Supported Backends

| Backend | Module | Service |
|---------|--------|---------|
| Elasticsearch | `CrestApps.OrchardCore.AI.Memory.Elasticsearch` | `ElasticsearchMemoryVectorSearchService` |
| Azure AI Search | `CrestApps.OrchardCore.AI.Memory.AzureAI` | `AzureAISearchMemoryVectorSearchService` |

### `IMemoryVectorSearchService`

Implement this interface to add a custom vector search backend:

```csharp
public interface IMemoryVectorSearchService
{
    Task<IEnumerable<AIMemorySearchResult>> SearchAsync(
        IndexProfile indexProfile,
        float[] embedding,
        string userId,
        int topN,
        CancellationToken cancellationToken = default);
}
```

## Configuration

### Site-Level Settings (`AIMemorySettings`)

Configured via the Orchard Core admin panel under **Settings → Artificial Intelligence**:

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `IndexProfileName` | `string` | `null` | Name of the index profile to use for vector search |
| `TopN` | `int` | `5` | Default number of results for vector search |

### Shared Memory Metadata (`MemoryMetadata`)

The same metadata shape now backs both AI Profiles and chat-interaction memory defaults:

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `EnableUserMemory` | `bool?` | `null` | Enables memory when explicitly set; hosts interpret an unset value with context-specific defaults |

### Effective Defaults

`MemoryMetadata` is interpreted differently depending on where it is attached:

| Attachment | Default when unset | Description |
|------------|--------------------|-------------|
| `AIProfile` / profile-source `AIProfileTemplate` | `false` | Memory stays opt-in for profiles |
| Chat Interaction site settings | `true` | Chat interactions keep user memory enabled by default |

### Index Profile Metadata (`AIMemoryIndexProfileMetadata`)

Attached to the index profile used for memory embeddings:

| Setting | Type | Description |
|---------|------|-------------|
| `EmbeddingProviderName` | `string` | AI provider to use for embedding generation |
| `EmbeddingConnectionName` | `string` | Connection name for the embedding provider |
| `EmbeddingDeploymentName` | `string` | Deployment/model name for embeddings |

### Constants (`MemoryConstants`)

Key constants used throughout the memory system:

| Constant | Value | Purpose |
|----------|-------|---------|
| `CollectionName` | `"AIMemory"` | YesSql collection for memory documents |
| `Feature.Memory` | `"CrestApps.OrchardCore.AI.Memory"` | Feature ID |
| `TemplateIds.MemoryAvailability` | `"memory-availability"` | Template rendered when memory is enabled |
| `TemplateIds.MemoryContextHeader` | `"memory-context-header"` | Template for injecting preemptive RAG results |

## Data Flow

The complete lifecycle of a memory operation:

```
1. User sends message
   ↓
2. AIMemoryOrchestrationHandler.BuiltAsync()
   - Is user authenticated? Is memory enabled?
   - Yes → Add memory tools + set HasMemory flag
   ↓
3. AIMemoryPreemptiveRagHandler.HandleAsync()
   - Search existing memories for relevant context
   - Inject matches into system message
   ↓
4. AI model generates response
   - May call save_user_memory, search_user_memories, etc.
   ↓
5. Tool execution (e.g., SaveUserMemoryTool)
   - Validate via IAIMemorySafetyService
   - Write to IAIMemoryStore
   ↓
6. ICatalogEntryHandler<AIMemoryEntry> fires
   - AIMemoryEntryHandler defers indexing via ShellScope
   ↓
7. Deferred task runs
   - AIMemoryIndexingService generates embedding
   - Writes AIMemoryEntryIndexDocument to vector index
```

## Orchard Core Integration

The [AI Memory module](../ai/memory.md) adds admin UI for:

- Configuring memory settings at the site level
- Enabling/disabling memory per AI profile
- Managing memory index profiles with embedding configuration
- Viewing and managing user memories through the admin dashboard
- Chat interaction memory settings when the Chat Interactions feature is active
