---
sidebar_label: Interface Reference
---

# Interface Reference

A comprehensive reference for all public interfaces in the CrestApps AI Framework. These interfaces define the contracts you implement or consume when building on the framework.

---

## Core Abstractions

The foundation layer that all other modules build upon. These interfaces live in `CrestApps.Abstractions` and define the generic catalog/manager patterns, model markers, and cloning contracts used throughout the framework.

### IDisplayTextAwareModel

Marks a model that carries a user-facing display name. Used by drivers and admin UI to show a friendly label.

- **Namespace**: `CrestApps.Models`
- **Extends**: None
- **Project**: `CrestApps.Abstractions`

```csharp
public interface IDisplayTextAwareModel
{
    string DisplayText { get; set; }
}
```

### INameAwareModel

Marks a model that has a unique technical name. Used by catalog stores and managers for name-based look-ups.

- **Namespace**: `CrestApps.Models`
- **Extends**: None
- **Project**: `CrestApps.Abstractions`

```csharp
public interface INameAwareModel
{
    string Name { get; set; }
}
```

### ISourceAwareModel

Marks a model that tracks its originating source (e.g., a provider name or module identifier).

- **Namespace**: `CrestApps.Models`
- **Extends**: None
- **Project**: `CrestApps.Abstractions`

```csharp
public interface ISourceAwareModel
{
    string Source { get; set; }
}
```

### ICloneable&lt;T&gt;

Strongly-typed cloning contract. Produces a deep copy of the implementing model.

- **Namespace**: `CrestApps`
- **Extends**: `ICloneable`
- **Project**: `CrestApps.Abstractions`

```csharp
public interface ICloneable<T> : ICloneable
{
    new T Clone();
}
```

### IODataValidator

Validates OData filter expressions before they are passed to a search backend.

- **Namespace**: `CrestApps`
- **Extends**: None
- **Project**: `CrestApps.Abstractions`

```csharp
public interface IODataValidator
{
    bool IsValidFilter(string filter);
}
```

---

### Catalog Interfaces

The catalog pattern provides a layered data-access abstraction. **Read catalogs** expose queries; **catalogs** add write operations; **named**, **source**, and **named-source** variants add look-up helpers. Managers wrap catalogs with validation and handler pipelines.

#### IReadCatalog&lt;T&gt;

Read-only data access for catalog entries. Implement this when you only need queries.

- **Namespace**: `CrestApps.Services`
- **Extends**: None
- **Project**: `CrestApps.Abstractions`

```csharp
public interface IReadCatalog<T>
{
    Task<T> FindByIdAsync(string id);

    Task<IReadOnlyCollection<T>> GetAllAsync();

    Task<IReadOnlyCollection<T>> GetAsync(IEnumerable<string> ids);

    Task<CatalogPageResult<T>> PageAsync<TQuery>(int page, int pageSize, TQuery context);
}
```

#### ICatalog&lt;T&gt;

Full CRUD catalog for a model type. Adds create, update, delete, and save-changes to the read catalog.

- **Namespace**: `CrestApps.Services`
- **Extends**: `IReadCatalog<T>`
- **Project**: `CrestApps.Abstractions`

```csharp
public interface ICatalog<T> : IReadCatalog<T>
{
    Task DeleteAsync(T entry);

    Task CreateAsync(T entry);

    Task UpdateAsync(T entry);

    Task SaveChangesAsync();
}
```

#### INamedCatalog&lt;T&gt;

A catalog that also supports look-up by the model's unique name.

- **Namespace**: `CrestApps.Services`
- **Extends**: `ICatalog<T>`
- **Project**: `CrestApps.Abstractions`

```csharp
public interface INamedCatalog<T> : ICatalog<T>
{
    Task<T> FindByNameAsync(string name);
}
```

#### ISourceCatalog&lt;T&gt;

A catalog that supports retrieval by source identifier.

- **Namespace**: `CrestApps.Services`
- **Extends**: `ICatalog<T>`
- **Project**: `CrestApps.Abstractions`

```csharp
public interface ISourceCatalog<T> : ICatalog<T>
{
    Task<IReadOnlyCollection<T>> GetAsync(string source);
}
```

#### INamedSourceCatalog&lt;T&gt;

A catalog that supports look-up by the combination of name and source.

- **Namespace**: `CrestApps.Services`
- **Extends**: None
- **Project**: `CrestApps.Abstractions`

```csharp
public interface INamedSourceCatalog<T>
{
    Task<T> GetAsync(string name, string source);
}
```

---

### Catalog Manager Interfaces

Managers wrap catalog stores with handler lifecycle events and validation. Use managers in application code; use raw catalogs only in store implementations.

#### IReadCatalogManager&lt;T&gt;

Read-only manager. Exposes query methods with handler pipeline support.

- **Namespace**: `CrestApps.Services`
- **Extends**: None
- **Project**: `CrestApps.Abstractions`

```csharp
public interface IReadCatalogManager<T>
{
    ValueTask<T> FindByIdAsync(string id);

    ValueTask<IReadOnlyCollection<T>> GetAllAsync();

    ValueTask<CatalogPageResult<T>> PageAsync<TQuery>(int page, int pageSize, TQuery context);
}
```

#### ICatalogManager&lt;T&gt;

Full CRUD manager with handler pipeline, validation, and factory methods.

- **Namespace**: `CrestApps.Services`
- **Extends**: `IReadCatalogManager<T>`
- **Project**: `CrestApps.Abstractions`

```csharp
public interface ICatalogManager<T> : IReadCatalogManager<T>
{
    Task DeleteAsync(T model);

    Task<T> NewAsync(JsonNode data = null);

    Task CreateAsync(T model);

    Task UpdateAsync(T model, JsonNode data = null);

    Task<ValidationResult> ValidateAsync(T model);
}
```

#### INamedCatalogManager&lt;T&gt;

Adds name-based look-up to the catalog manager.

- **Namespace**: `CrestApps.Services`
- **Extends**: `ICatalogManager<T>`
- **Project**: `CrestApps.Abstractions`

```csharp
public interface INamedCatalogManager<T> : ICatalogManager<T>
{
    ValueTask<T> FindByNameAsync(string name);
}
```

#### ISourceCatalogManager&lt;T&gt;

Adds source-aware factory and retrieval methods.

- **Namespace**: `CrestApps.Services`
- **Extends**: `ICatalogManager<T>`
- **Project**: `CrestApps.Abstractions`

```csharp
public interface ISourceCatalogManager<T> : ICatalogManager<T>
{
    Task<T> NewAsync(string source, JsonNode data);

    ValueTask<IReadOnlyCollection<T>> GetAsync(string source);

    ValueTask<T> FindBySourceAsync(string source);
}
```

#### INamedSourceCatalogManager&lt;T&gt;

Adds look-up by the combination of name and source to the catalog manager.

- **Namespace**: `CrestApps.Services`
- **Extends**: None
- **Project**: `CrestApps.Abstractions`

```csharp
public interface INamedSourceCatalogManager<T>
{
    ValueTask<T> GetAsync(string name, string source);
}
```

---

### ICatalogEntryHandler&lt;T&gt;

Handles lifecycle events for catalog entries. Implement this to run custom logic when entries are created, updated, deleted, validated, or loaded.

- **Namespace**: `CrestApps.Services`
- **Extends**: None
- **Project**: `CrestApps.Abstractions`

```csharp
public interface ICatalogEntryHandler<T>
{
    Task InitializingAsync(InitializingContext<T> context);

    Task InitializedAsync(InitializedContext<T> context);

    Task LoadedAsync(LoadedContext<T> context);

    Task ValidatingAsync(ValidatingContext<T> context);

    Task ValidatedAsync(ValidatedContext<T> context);

    Task DeletingAsync(DeletingContext<T> context);

    Task DeletedAsync(DeletedContext<T> context);

    Task UpdatingAsync(UpdatingContext<T> context);

    Task UpdatedAsync(UpdatedContext<T> context);

    Task CreatingAsync(CreatingContext<T> context);

    Task CreatedAsync(CreatedContext<T> context);
}
```

---

## AI Completion

Interfaces for sending prompts to AI models and receiving responses — both one-shot and streaming.

### IAICompletionService

The primary service for sending AI completion requests. Inject this to generate AI responses from any configured provider. It resolves the correct client from the deployment and delegates to it.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAICompletionService
{
    /// <summary>
    /// Sends a series of messages to the AI chat service and returns the completion response.
    /// </summary>
    Task<ChatResponse> CompleteAsync(
        AIDeployment deployment,
        IEnumerable<ChatMessage> messages,
        AICompletionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams chat completion updates from the AI service in real time.
    /// </summary>
    IAsyncEnumerable<ChatResponseUpdate> CompleteStreamingAsync(
        AIDeployment deployment,
        IEnumerable<ChatMessage> messages,
        AICompletionContext context,
        CancellationToken cancellationToken = default);
}
```

### IAICompletionClient

Provider-specific completion client. Each AI provider (OpenAI, Azure OpenAI, Ollama) implements this interface. You rarely inject this directly — use `IAICompletionService` instead.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAICompletionClient
{
    /// <summary>
    /// Gets the unique technical name of this client implementation.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Sends messages to the AI chat service and returns the completion response.
    /// </summary>
    Task<ChatResponse> CompleteAsync(
        IEnumerable<ChatMessage> messages,
        AICompletionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams chat completion updates from the AI service in real time.
    /// </summary>
    IAsyncEnumerable<ChatResponseUpdate> CompleteStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AICompletionContext context,
        CancellationToken cancellationToken = default);
}
```

### IAICompletionHandler

Intercepts completed AI responses. Implement this to inspect or transform messages and streaming updates after the model returns them.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAICompletionHandler
{
    /// <summary>
    /// Handles a received message asynchronously.
    /// </summary>
    Task ReceivedMessageAsync(ReceivedMessageContext context);

    /// <summary>
    /// Handles a received streaming update asynchronously.
    /// </summary>
    Task ReceivedUpdateAsync(ReceivedUpdateContext context);
}
```

### IAICompletionServiceHandler

Called on every completion request to configure `ChatOptions` dynamically. Use this to inject custom temperature, stop sequences, or other model parameters per request.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAICompletionServiceHandler
{
    /// <summary>
    /// Called on every request to configure the ChatOptions.
    /// </summary>
    Task ConfigureAsync(CompletionServiceConfigureContext context);
}
```

### IAICompletionContextBuilder

Builds `AICompletionContext` instances from a resource object (e.g., an AI profile or chat interaction). The builder runs a handler pipeline around an optional caller delegate.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAICompletionContextBuilder
{
    /// <summary>
    /// Creates and configures a new AICompletionContext based on the provided resource.
    /// </summary>
    ValueTask<AICompletionContext> BuildAsync(
        object resource,
        Action<AICompletionContext> configure = null);
}
```

### IAICompletionContextBuilderHandler

Handles lifecycle events while an `AICompletionContext` is being built. Implement this to enrich the context with additional data (e.g., inject system messages, tools, or memory).

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAICompletionContextBuilderHandler
{
    /// <summary>
    /// Called while the context is being constructed, before the caller delegate.
    /// </summary>
    Task BuildingAsync(AICompletionContextBuildingContext context);

    /// <summary>
    /// Called after the context has been fully constructed and the caller delegate applied.
    /// </summary>
    Task BuiltAsync(AICompletionContextBuiltContext context);
}
```

---

## AI Client Factory &amp; Providers

Interfaces for creating and providing low-level AI clients (chat, embedding, image, speech).

### IAIClientFactory

Factory for creating AI clients. Returns `IChatClient`, `IEmbeddingGenerator`, `IImageGenerator`, `ISpeechToTextClient`, and `ITextToSpeechClient` instances from provider, connection, and deployment names.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAIClientFactory
{
    /// <summary>
    /// Creates an IChatClient for the given provider, connection, and deployment.
    /// </summary>
    ValueTask<IChatClient> CreateChatClientAsync(
        string providerName,
        string connectionName,
        string deploymentName);

    /// <summary>
    /// Creates an IEmbeddingGenerator for the given provider, connection, and deployment.
    /// </summary>
    ValueTask<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGeneratorAsync(
        string providerName,
        string connectionName,
        string deploymentName);

    /// <summary>
    /// Creates an IImageGenerator for the given provider, connection, and deployment.
    /// </summary>
    ValueTask<IImageGenerator> CreateImageGeneratorAsync(
        string providerName,
        string connectionName,
        string deploymentName = null);

    /// <summary>
    /// Creates an ISpeechToTextClient for the given provider, connection, and deployment.
    /// </summary>
    ValueTask<ISpeechToTextClient> CreateSpeechToTextClientAsync(
        string providerName,
        string connectionName,
        string deploymentName = null);

    /// <summary>
    /// Creates an ISpeechToTextClient from a deployment.
    /// </summary>
    ValueTask<ISpeechToTextClient> CreateSpeechToTextClientAsync(AIDeployment deployment);

    /// <summary>
    /// Creates an ITextToSpeechClient for the given provider, connection, and deployment.
    /// </summary>
    ValueTask<ITextToSpeechClient> CreateTextToSpeechClientAsync(
        string providerName,
        string connectionName,
        string deploymentName = null);

    /// <summary>
    /// Creates an ITextToSpeechClient from a deployment.
    /// </summary>
    ValueTask<ITextToSpeechClient> CreateTextToSpeechClientAsync(AIDeployment deployment);
}
```

### IAIClientProvider

Provider-specific client factory. Each AI provider (OpenAI, Azure OpenAI, Ollama) implements this to create chat clients, embedding generators, image generators, and speech clients from its connection configuration.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAIClientProvider
{
    /// <summary>
    /// Determines whether this provider can handle the specified provider name.
    /// </summary>
    bool CanHandle(string providerName);

    /// <summary>
    /// Gets an AI chat client for the specified connection and deployment.
    /// </summary>
    ValueTask<IChatClient> GetChatClientAsync(
        AIProviderConnectionEntry connection,
        string deploymentName = null);

    /// <summary>
    /// Gets an embedding generator for the specified connection and deployment.
    /// </summary>
    ValueTask<IEmbeddingGenerator<string, Embedding<float>>> GetEmbeddingGeneratorAsync(
        AIProviderConnectionEntry connection,
        string deploymentName = null);

    /// <summary>
    /// Gets an image generator for the specified connection and deployment.
    /// </summary>
    ValueTask<IImageGenerator> GetImageGeneratorAsync(
        AIProviderConnectionEntry connection,
        string deploymentName = null);

    /// <summary>
    /// Gets a speech-to-text client for the specified connection and deployment.
    /// </summary>
    ValueTask<ISpeechToTextClient> GetSpeechToTextClientAsync(
        AIProviderConnectionEntry connection,
        string deploymentName = null);

    /// <summary>
    /// Gets a text-to-speech client for the specified connection and deployment.
    /// </summary>
    ValueTask<ITextToSpeechClient> GetTextToSpeechClientAsync(
        AIProviderConnectionEntry connection,
        string deploymentName = null);

    /// <summary>
    /// Gets the available speech voices for the specified connection and deployment.
    /// </summary>
    Task<SpeechVoice[]> GetSpeechVoicesAsync(
        AIProviderConnectionEntry connection,
        string deploymentName = null);
}
```

### IOpenAIChatOptionsConfiguration

Configures OpenAI-specific `ChatCompletionOptions` before each completion request. Implement this to set vendor-specific options that the generic `IAICompletionServiceHandler` cannot express.

- **Namespace**: `CrestApps.AI.OpenAI`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.OpenAI.Core`

```csharp
public interface IOpenAIChatOptionsConfiguration
{
    Task InitializeConfigurationAsync(CompletionServiceConfigureContext context);

    void Configure(
        CompletionServiceConfigureContext context,
        ChatCompletionOptions chatCompletionOptions);
}
```

---

## Orchestration

Orchestrators manage the execution loop for an AI session — planning, tool scoping, iterative agent loops, and producing the final response.

### IOrchestrator

The pluggable orchestration runtime. Each chat session binds to exactly one orchestrator. Implement this to create custom planning or execution strategies.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IOrchestrator
{
    /// <summary>
    /// Gets the unique name of this orchestrator.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the orchestration pipeline and yields streaming completion updates.
    /// </summary>
    IAsyncEnumerable<ChatResponseUpdate> ExecuteStreamingAsync(
        OrchestrationContext context,
        CancellationToken cancellationToken = default);
}
```

### IOrchestratorResolver

Resolves the appropriate orchestrator by name. Falls back to the system default when the name is null or unrecognized.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IOrchestratorResolver
{
    /// <summary>
    /// Resolves an orchestrator by name. Returns the system default if unrecognized.
    /// </summary>
    IOrchestrator Resolve(string orchestratorName = null);
}
```

### IOrchestrationContextBuilder

Builds `OrchestrationContext` instances from a resource object. Runs a handler pipeline around an optional caller delegate.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IOrchestrationContextBuilder
{
    /// <summary>
    /// Creates and configures a new OrchestrationContext based on the provided resource.
    /// </summary>
    ValueTask<OrchestrationContext> BuildAsync(
        object resource,
        Action<OrchestrationContext> configure = null);
}
```

### IOrchestrationContextBuilderHandler

Handles lifecycle events while an `OrchestrationContext` is being built. Implement this to inject tools, system prompts, or RAG context into the orchestration pipeline.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IOrchestrationContextBuilderHandler
{
    /// <summary>
    /// Called while the context is being constructed, before the caller delegate.
    /// </summary>
    Task BuildingAsync(OrchestrationContextBuildingContext context);

    /// <summary>
    /// Called after the context has been fully constructed.
    /// </summary>
    Task BuiltAsync(OrchestrationContextBuiltContext context);
}
```

### IPreemptiveRagHandler

Processes preemptive RAG (Retrieval-Augmented Generation) for a specific data source type. Receives pre-extracted search queries and injects relevant context into the system message.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IPreemptiveRagHandler
{
    /// <summary>
    /// Determines whether the specified context can be handled by this instance.
    /// </summary>
    ValueTask<bool> CanHandleAsync(OrchestrationContextBuiltContext context);

    /// <summary>
    /// Handles preemptive RAG injection for the given context and search queries.
    /// </summary>
    Task HandleAsync(PreemptiveRagContext context);
}
```

---

## AI Profiles &amp; Deployments

Manage AI profile definitions, profile templates, deployments, and provider connections.

### IAIProfileManager

Manages AI profiles (chat, utility, etc.) with name-based look-up and type filtering.

- **Namespace**: `CrestApps.AI`
- **Extends**: `INamedCatalogManager<AIProfile>`
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAIProfileManager : INamedCatalogManager<AIProfile>
{
    /// <summary>
    /// Retrieves AI profiles of the specified type.
    /// </summary>
    ValueTask<IEnumerable<AIProfile>> GetAsync(AIProfileType type);
}
```

### IAIProfileStore

Persistence store for AI profiles. Adds an efficient indexed query by profile type.

- **Namespace**: `CrestApps.AI`
- **Extends**: `INamedCatalog<AIProfile>`
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAIProfileStore : INamedCatalog<AIProfile>
{
    /// <summary>
    /// Retrieves AI profiles of the specified type using an efficient index query.
    /// </summary>
    ValueTask<IReadOnlyCollection<AIProfile>> GetByTypeAsync(AIProfileType type);
}
```

### IAIDeploymentManager

Manages AI model deployments with look-up by client, connection, type, and a full fallback resolution chain.

- **Namespace**: `CrestApps.AI`
- **Extends**: `INamedSourceCatalogManager<AIDeployment>`
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAIDeploymentManager : INamedSourceCatalogManager<AIDeployment>
{
    /// <summary>
    /// Retrieves deployments for the specified client and connection.
    /// </summary>
    ValueTask<IEnumerable<AIDeployment>> GetAllAsync(
        string clientName,
        string connectionName);

    /// <summary>
    /// Retrieves all deployments supporting the specified type.
    /// </summary>
    ValueTask<IEnumerable<AIDeployment>> GetByTypeAsync(AIDeploymentType type);

    /// <summary>
    /// Resolves the default deployment of a given type for a specific connection.
    /// </summary>
    ValueTask<AIDeployment> GetDefaultAsync(
        string clientName,
        string connectionName,
        AIDeploymentType type);

    /// <summary>
    /// Resolves a deployment using the full fallback chain:
    /// deploymentId → global default → first matching deployment.
    /// </summary>
    ValueTask<AIDeployment> ResolveOrDefaultAsync(
        AIDeploymentType type,
        string deploymentName = null,
        string clientName = null,
        string connectionName = null);

    /// <summary>
    /// Gets all deployments of a given type, optionally filtered by client.
    /// </summary>
    ValueTask<IEnumerable<AIDeployment>> GetAllByTypeAsync(
        AIDeploymentType type,
        string clientName = null);
}
```

### IAIProfileTemplateManager

Manages AI profile templates from both database and file-based sources.

- **Namespace**: `CrestApps.AI.Models`
- **Extends**: `INamedSourceCatalogManager<AIProfileTemplate>`
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAIProfileTemplateManager : INamedSourceCatalogManager<AIProfileTemplate>
{
    /// <summary>
    /// Gets all listable profile templates from all sources.
    /// </summary>
    ValueTask<IEnumerable<AIProfileTemplate>> GetListableAsync();
}
```

### IAIProfileTemplateProvider

Provides AI profile templates from a specific source (e.g., module files). Implement this to register templates from custom locations.

- **Namespace**: `CrestApps.AI.Models`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAIProfileTemplateProvider
{
    /// <summary>
    /// Gets all profile templates from this provider.
    /// </summary>
    Task<IReadOnlyList<AIProfileTemplate>> GetTemplatesAsync();
}
```

### IAIProviderConnectionHandler

Handles lifecycle events for AI provider connections during initialization and export.

- **Namespace**: `CrestApps.AI.Models`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAIProviderConnectionHandler
{
    void Initializing(InitializingAIProviderConnectionContext context);

    void Exporting(ExportingAIProviderConnectionContext context);
}
```

---

## Chat &amp; Sessions

Manage chat sessions, prompt history, response routing, and interaction settings.

### IAIChatSessionManager

Manages AI chat sessions — creating, saving, deleting, and paginating sessions with ownership checks.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAIChatSessionManager
{
    /// <summary>
    /// Retrieves a chat session by ID (no ownership check).
    /// </summary>
    Task<AIChatSession> FindByIdAsync(string id);

    /// <summary>
    /// Retrieves a chat session by ID with ownership check.
    /// </summary>
    Task<AIChatSession> FindAsync(string id);

    /// <summary>
    /// Paginates chat sessions based on query context.
    /// </summary>
    Task<AIChatSessionResult> PageAsync(
        int page,
        int pageSize,
        AIChatSessionQueryContext context = null);

    /// <summary>
    /// Creates a new chat session for the specified profile.
    /// </summary>
    Task<AIChatSession> NewAsync(AIProfile profile, NewAIChatSessionContext context);

    /// <summary>
    /// Saves or updates a chat session.
    /// </summary>
    Task SaveAsync(AIChatSession chatSession);

    /// <summary>
    /// Deletes a chat session by ID.
    /// </summary>
    Task<bool> DeleteAsync(string sessionId);

    /// <summary>
    /// Deletes all sessions for a profile and the current user.
    /// </summary>
    Task<int> DeleteAllAsync(string profileId);
}
```

### IAIChatSessionHandler

Handles lifecycle events for chat sessions, including a callback after a message exchange completes. Extends `ICatalogEntryHandler<AIChatSession>` for standard lifecycle hooks.

- **Namespace**: `CrestApps.AI`
- **Extends**: `ICatalogEntryHandler<AIChatSession>`
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAIChatSessionHandler : ICatalogEntryHandler<AIChatSession>
{
    /// <summary>
    /// Called after a user message has been processed and the assistant response
    /// has been fully generated.
    /// </summary>
    Task MessageCompletedAsync(ChatMessageCompletedContext context);
}
```

### IAIChatSessionPromptStore

Persistence store for AI chat session prompts. Supports listing, counting, and bulk deletion by session.

- **Namespace**: `CrestApps.AI`
- **Extends**: `ICatalog<AIChatSessionPrompt>`
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAIChatSessionPromptStore : ICatalog<AIChatSessionPrompt>
{
    /// <summary>
    /// Gets all prompts for a session, ordered by creation time.
    /// </summary>
    Task<IReadOnlyList<AIChatSessionPrompt>> GetPromptsAsync(string sessionId);

    /// <summary>
    /// Deletes all prompts for a session.
    /// </summary>
    Task<int> DeleteAllPromptsAsync(string sessionId);

    /// <summary>
    /// Counts the number of prompts for a session.
    /// </summary>
    Task<int> CountAsync(string sessionId);
}
```

### IChatInteractionPromptStore

Persistence store for chat interaction prompts. Supports listing and bulk deletion by interaction.

- **Namespace**: `CrestApps.AI.Chat`
- **Extends**: `ICatalog<ChatInteractionPrompt>`
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface IChatInteractionPromptStore : ICatalog<ChatInteractionPrompt>
{
    /// <summary>
    /// Gets all prompts for a chat interaction, ordered by creation time.
    /// </summary>
    Task<IReadOnlyCollection<ChatInteractionPrompt>> GetPromptsAsync(string chatInteractionId);

    /// <summary>
    /// Deletes all prompts for a chat interaction.
    /// </summary>
    Task<int> DeleteAllPromptsAsync(string chatInteractionId);
}
```

### IChatResponseHandler

A pluggable handler for processing chat prompts and producing responses. The default routes through the AI orchestration pipeline; custom implementations can route to live-agent platforms or other backends.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IChatResponseHandler
{
    /// <summary>
    /// Gets the unique technical name of this handler (e.g., "AI", "Genesys").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Processes a chat prompt and returns a result that is either streaming or deferred.
    /// </summary>
    Task<ChatResponseHandlerResult> HandleAsync(
        ChatResponseHandlerContext context,
        CancellationToken cancellationToken = default);
}
```

### IChatResponseHandlerResolver

Resolves the appropriate `IChatResponseHandler` by name. Falls back to the default AI handler when unrecognized.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IChatResponseHandlerResolver
{
    /// <summary>
    /// Resolves a handler by name. Returns the default AI handler if unrecognized.
    /// </summary>
    IChatResponseHandler Resolve(
        string handlerName = null,
        ChatMode chatMode = ChatMode.TextInput);

    /// <summary>
    /// Gets all registered chat response handlers.
    /// </summary>
    IEnumerable<IChatResponseHandler> GetAll();
}
```

### IChatInteractionSettingsHandler

Handles lifecycle events when chat interaction settings are saved from the client (e.g., SignalR hub). Implement this to validate or enrich settings before persistence.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IChatInteractionSettingsHandler
{
    /// <summary>
    /// Called while settings are being applied, before persistence.
    /// </summary>
    Task UpdatingAsync(ChatInteraction interaction, JsonElement settings);

    /// <summary>
    /// Called after the interaction has been persisted.
    /// </summary>
    Task UpdatedAsync(ChatInteraction interaction, JsonElement settings);
}
```

---

## Chat Hub (SignalR)

Client-side contracts for real-time chat communication over SignalR.

### IAIChatHubClient

Defines the SignalR client methods the AI Chat hub invokes. Covers text chat, conversation mode (STT/TTS), and notification system messages.

- **Namespace**: `CrestApps.AI.Chat.Hubs`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface IAIChatHubClient
{
    Task ReceiveError(string error);

    Task LoadSession(object data);

    Task MessageRated(string messageId, bool? userRating);

    Task ReceiveTranscript(string identifier, string text, bool isFinal);

    Task ReceiveAudioChunk(string identifier, string base64Audio, string contentType);

    Task ReceiveAudioComplete(string identifier);

    Task ReceiveConversationUserMessage(string identifier, string text);

    Task ReceiveConversationAssistantToken(
        string identifier,
        string messageId,
        string token,
        string responseId);

    Task ReceiveConversationAssistantComplete(string identifier, string messageId);

    /// <summary>
    /// Sends a notification system message to the client.
    /// </summary>
    Task ReceiveNotification(ChatNotification notification);

    /// <summary>
    /// Updates an existing notification on the client.
    /// </summary>
    Task UpdateNotification(ChatNotification notification);

    /// <summary>
    /// Removes a notification from the client by its type.
    /// </summary>
    Task RemoveNotification(string notificationType);
}
```

### IChatInteractionHubClient

Defines the SignalR client methods the Chat Interaction hub invokes for ad-hoc chat sessions.

- **Namespace**: `CrestApps.AI.Chat.Hubs`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface IChatInteractionHubClient
{
    Task ReceiveError(string error);

    Task LoadInteraction(object data);

    Task SettingsSaved(string itemId, string title);

    Task HistoryCleared(string itemId);
}
```

---

## Tools &amp; Agents

Interfaces for discovering, authorizing, and registering AI tools that the orchestrator can invoke.

### IAIToolsService

Retrieves a registered AI tool by name. Used by the orchestration pipeline to resolve tools before invocation.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAIToolsService
{
    /// <summary>
    /// Retrieves an AI tool by its name.
    /// </summary>
    ValueTask<AITool> GetByNameAsync(string name);
}
```

### IAIToolAccessEvaluator

Evaluates whether a user is authorized to invoke a specific AI tool. Implement this to add custom permission checks.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAIToolAccessEvaluator
{
    /// <summary>
    /// Determines whether the specified user is allowed to invoke the given tool.
    /// </summary>
    Task<bool> IsAuthorizedAsync(ClaimsPrincipal user, string toolName);
}
```

### IToolRegistry

A unified index of all available tools (local and MCP) that supports retrieval and relevance-based searching for tool scoping during orchestration.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IToolRegistry
{
    /// <summary>
    /// Gets all tool entries scoped to the given completion context.
    /// </summary>
    Task<IReadOnlyList<ToolRegistryEntry>> GetAllAsync(
        AICompletionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for the most relevant tools based on a capability query string.
    /// </summary>
    Task<IReadOnlyList<ToolRegistryEntry>> SearchAsync(
        string query,
        int topK,
        AICompletionContext context,
        CancellationToken cancellationToken = default);
}
```

### IToolRegistryProvider

Provides tool metadata entries to the unified tool registry. Implement this to supply tools from custom sources (local registrations, MCP servers, etc.).

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IToolRegistryProvider
{
    /// <summary>
    /// Retrieves all tool entries from this provider, scoped to the given context.
    /// </summary>
    Task<IReadOnlyList<ToolRegistryEntry>> GetToolsAsync(
        AICompletionContext context,
        CancellationToken cancellationToken = default);
}
```

---

## Notifications &amp; Relay

Real-time notification infrastructure and persistent relay connections to external chat systems.

### IChatNotificationSender

Sends transient UI notifications to chat clients via SignalR. Use this from webhooks, background tasks, or response handlers.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IChatNotificationSender
{
    /// <summary>
    /// Sends a notification to all clients connected to the specified session.
    /// </summary>
    Task SendAsync(
        string sessionId,
        ChatContextType chatType,
        ChatNotification notification);

    /// <summary>
    /// Updates an existing notification on all connected clients.
    /// </summary>
    Task UpdateAsync(
        string sessionId,
        ChatContextType chatType,
        ChatNotification notification);

    /// <summary>
    /// Removes a notification from all connected clients.
    /// </summary>
    Task RemoveAsync(
        string sessionId,
        ChatContextType chatType,
        string notificationType);
}
```

### IChatNotificationTransport

Low-level transport for delivering chat notifications to clients connected to a specific hub. Registered as a keyed service using `ChatContextType` as the key.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IChatNotificationTransport
{
    /// <summary>
    /// Sends a notification to all clients in the session group.
    /// </summary>
    Task SendNotificationAsync(string sessionId, ChatNotification notification);

    /// <summary>
    /// Updates an existing notification on all connected clients.
    /// </summary>
    Task UpdateNotificationAsync(string sessionId, ChatNotification notification);

    /// <summary>
    /// Removes a notification from all connected clients.
    /// </summary>
    Task RemoveNotificationAsync(string sessionId, string notificationType);
}
```

### IChatNotificationActionHandler

Handles user-initiated actions on chat notifications. Registered as keyed services where the key matches the action name.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IChatNotificationActionHandler
{
    /// <summary>
    /// Handles the notification action triggered by the user.
    /// </summary>
    Task HandleAsync(
        ChatNotificationActionContext context,
        CancellationToken cancellationToken = default);
}
```

### IExternalChatRelay

Defines a persistent relay connection to an external system (e.g., a live-agent platform) for real-time bidirectional communication. Protocol-agnostic — supports WebSocket, SSE, gRPC, or any transport.

- **Namespace**: `CrestApps.AI`
- **Extends**: `IAsyncDisposable`
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IExternalChatRelay : IAsyncDisposable
{
    /// <summary>
    /// Determines whether the relay is currently connected.
    /// </summary>
    Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Establishes the connection to the external system.
    /// </summary>
    Task ConnectAsync(
        ExternalChatRelayContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a user prompt to the external system.
    /// </summary>
    Task SendPromptAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a signal (e.g., typing indicator) to the external system.
    /// </summary>
    Task SendSignalAsync(
        string signalName,
        IDictionary<string, string> data = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gracefully disconnects from the external system.
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
```

### IExternalChatRelayManager

Manages the lifecycle of `IExternalChatRelay` instances. Registered as a singleton to persist relay connections across scoped service lifetimes.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IExternalChatRelayManager
{
    /// <summary>
    /// Gets or creates a relay for the specified session.
    /// </summary>
    Task<IExternalChatRelay> GetOrCreateAsync(
        string sessionId,
        ExternalChatRelayContext context,
        Func<IExternalChatRelay> factory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an existing relay for the session, or null.
    /// </summary>
    IExternalChatRelay Get(string sessionId);

    /// <summary>
    /// Gracefully disconnects and removes the relay for the session.
    /// </summary>
    Task CloseAsync(
        string sessionId,
        CancellationToken cancellationToken = default);
}
```

### IExternalChatRelayEventHandler

Handles events received from an external relay. The default resolves a keyed `IExternalChatRelayNotificationBuilder` by event type and delegates to `IExternalChatRelayNotificationHandler`.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IExternalChatRelayEventHandler
{
    /// <summary>
    /// Processes an event received from the external relay.
    /// </summary>
    Task HandleEventAsync(
        string sessionId,
        ChatContextType chatType,
        ExternalChatRelayEvent relayEvent,
        CancellationToken cancellationToken = default);
}
```

### IExternalChatRelayNotificationBuilder

Populates a `ChatNotification` for a specific relay event type. Registered as keyed scoped services where the key is the event type string.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IExternalChatRelayNotificationBuilder
{
    /// <summary>
    /// Gets the notification type this builder produces.
    /// </summary>
    string NotificationType { get; }

    /// <summary>
    /// Populates the notification and result for the given relay event.
    /// </summary>
    void Build(
        ExternalChatRelayEvent relayEvent,
        ChatNotification notification,
        ExternalChatRelayNotificationResult result,
        IStringLocalizer T);
}
```

### IExternalChatRelayNotificationHandler

Handles sending and removing chat notifications described by an `ExternalChatRelayNotificationResult`. This is the handler half of the builder/handler pattern.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IExternalChatRelayNotificationHandler
{
    /// <summary>
    /// Processes a notification result by removing and/or sending notifications.
    /// </summary>
    Task HandleAsync(
        string sessionId,
        ChatContextType chatType,
        ExternalChatRelayNotificationResult result,
        CancellationToken cancellationToken = default);
}
```

---

## Data Sources &amp; Search

Interfaces for managing search indexes, data sources, and vector similarity search across providers like Elasticsearch and Azure AI Search.

### IAIDataSourceStore

Persistence store for `AIDataSource` records.

- **Namespace**: `CrestApps.AI`
- **Extends**: `ICatalog<AIDataSource>`
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAIDataSourceStore : ICatalog<AIDataSource>
{
}
```

### IAIDataSourceSettingsProvider

Resolves the active data source retrieval settings for the current host.

- **Namespace**: `CrestApps.AI.Services`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAIDataSourceSettingsProvider
{
    /// <summary>
    /// Gets the current data source settings.
    /// </summary>
    Task<AIDataSourceSettings> GetAsync();
}
```

### IDataSourceContentManager

Searches for and deletes document chunks in a data source index using embedding vectors.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IDataSourceContentManager
{
    /// <summary>
    /// Searches for document chunks similar to the provided embedding vector.
    /// </summary>
    Task<IEnumerable<DataSourceSearchResult>> SearchAsync(
        IIndexProfileInfo indexProfile,
        float[] embedding,
        string dataSourceId,
        int topN,
        string filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all document chunks belonging to the specified data source.
    /// </summary>
    Task<long> DeleteByDataSourceIdAsync(
        IIndexProfileInfo indexProfile,
        string dataSourceId,
        CancellationToken cancellationToken = default);
}
```

### IDataSourceDocumentReader

Reads documents from a source index in batches. Registered as keyed services by provider name.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IDataSourceDocumentReader
{
    /// <summary>
    /// Reads documents from the source index in batches.
    /// </summary>
    IAsyncEnumerable<KeyValuePair<string, SourceDocument>> ReadAsync(
        IIndexProfileInfo indexProfile,
        string keyFieldName,
        string titleFieldName,
        string contentFieldName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads specific documents from the source index by their IDs.
    /// </summary>
    IAsyncEnumerable<KeyValuePair<string, SourceDocument>> ReadByIdsAsync(
        IIndexProfileInfo indexProfile,
        IEnumerable<string> documentIds,
        string keyFieldName,
        string titleFieldName,
        string contentFieldName,
        CancellationToken cancellationToken = default);
}
```

### ISearchDocumentManager

Manages documents within a search index (add, update, delete). Registered as keyed services by provider name.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface ISearchDocumentManager
{
    /// <summary>
    /// Adds or updates documents in the specified index.
    /// </summary>
    Task<bool> AddOrUpdateAsync(
        IIndexProfileInfo profile,
        IReadOnlyCollection<IndexDocument> documents,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes specific documents from the index by their IDs.
    /// </summary>
    Task DeleteAsync(
        IIndexProfileInfo profile,
        IEnumerable<string> documentIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all documents from the specified index.
    /// </summary>
    Task DeleteAllAsync(
        IIndexProfileInfo profile,
        CancellationToken cancellationToken = default);
}
```

### ISearchIndexManager

Manages the lifecycle of search indexes (create, check, delete). Registered as keyed services by provider name.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface ISearchIndexManager
{
    /// <summary>
    /// Checks whether the specified index exists.
    /// </summary>
    Task<bool> ExistsAsync(
        string indexFullName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new search index with the specified fields.
    /// </summary>
    Task CreateAsync(
        IIndexProfileInfo profile,
        IReadOnlyCollection<SearchIndexField> fields,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the specified search index.
    /// </summary>
    Task DeleteAsync(
        string indexFullName,
        CancellationToken cancellationToken = default);
}
```

### ISearchIndexProfileStore

Persistence store for `SearchIndexProfile` records with name and type look-ups.

- **Namespace**: `CrestApps.AI`
- **Extends**: `ICatalog<SearchIndexProfile>`
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface ISearchIndexProfileStore : ICatalog<SearchIndexProfile>
{
    /// <summary>
    /// Finds an index profile by its unique name.
    /// </summary>
    Task<SearchIndexProfile> FindByNameAsync(string name);

    /// <summary>
    /// Gets all index profiles of the specified type.
    /// </summary>
    Task<IReadOnlyCollection<SearchIndexProfile>> GetByTypeAsync(string type);
}
```

### IIndexProfileInfo

Provides index profile information for data source and vector search operations. Decouples the AI framework from specific indexing implementations.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IIndexProfileInfo
{
    /// <summary>
    /// Gets the unique identifier for the index profile.
    /// </summary>
    string IndexProfileId { get; }

    /// <summary>
    /// Gets the name of the index.
    /// </summary>
    string IndexName { get; }

    /// <summary>
    /// Gets the provider name (e.g., "Elasticsearch", "AzureAISearch").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the full name of the index including any tenant prefix.
    /// </summary>
    string IndexFullName { get; }
}
```

### IVectorSearchService

Searches document embeddings in an index provider. Registered as keyed services by provider name.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IVectorSearchService
{
    /// <summary>
    /// Searches for document chunks similar to the provided embedding vector.
    /// </summary>
    Task<IEnumerable<DocumentChunkSearchResult>> SearchAsync(
        IIndexProfileInfo indexProfile,
        float[] embedding,
        string referenceId,
        string referenceType,
        int topN,
        CancellationToken cancellationToken = default);
}
```

### IODataFilterTranslator

Translates OData filter expressions into provider-specific filter queries. Registered as keyed services by provider name.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IODataFilterTranslator
{
    /// <summary>
    /// Translates an OData filter expression into a provider-specific filter string.
    /// </summary>
    string Translate(string odataFilter);
}
```

### ITextTokenizer

Tokenizes text into normalized terms for matching and scoring. Handles code identifiers, stop words, stemming, and case normalization.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface ITextTokenizer
{
    /// <summary>
    /// Tokenizes the given text into a set of distinct, normalized tokens.
    /// </summary>
    HashSet<string> Tokenize(string text);
}
```

### IAIReferenceLinkResolver

Resolves links for AI completion references based on the reference type. Registered as keyed services by reference type.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAIReferenceLinkResolver
{
    /// <summary>
    /// Resolves a link URL for the given reference.
    /// </summary>
    string ResolveLink(
        string referenceId,
        IDictionary<string, object> metadata);
}
```

---

## Document Storage

Manage AI documents, document chunks, file processing, and tabular batch analysis.

### IAIDocumentStore

Persistence store for `AIDocument` records with reference-based retrieval.

- **Namespace**: `CrestApps.AI`
- **Extends**: `ICatalog<AIDocument>`
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAIDocumentStore : ICatalog<AIDocument>
{
    Task<IReadOnlyCollection<AIDocument>> GetDocumentsAsync(
        string referenceId,
        string referenceType);
}
```

### IAIDocumentChunkStore

Persistence store for `AIDocumentChunk` records with document-based and reference-based retrieval and deletion.

- **Namespace**: `CrestApps.AI`
- **Extends**: `ICatalog<AIDocumentChunk>`
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAIDocumentChunkStore : ICatalog<AIDocumentChunk>
{
    Task<IReadOnlyCollection<AIDocumentChunk>> GetChunksByAIDocumentIdAsync(string documentId);

    Task<IReadOnlyCollection<AIDocumentChunk>> GetChunksByReferenceAsync(
        string referenceId,
        string referenceType);

    Task DeleteByDocumentIdAsync(string documentId);
}
```

### IAIDocumentProcessingService

Processes uploaded files into AI documents and embedded chunks. Handles text extraction, chunking, and embedding generation.

- **Namespace**: `CrestApps.AI.Chat.Services`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface IAIDocumentProcessingService
{
    /// <summary>
    /// Creates an embedding generator for the given provider and connection.
    /// Returns null if no embedding deployment is configured.
    /// </summary>
    Task<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGeneratorAsync(
        string providerName,
        string connectionName);

    /// <summary>
    /// Processes an uploaded file by extracting text, chunking, generating
    /// embeddings, and creating an AI document.
    /// </summary>
    Task<DocumentProcessingResult> ProcessFileAsync(
        IFormFile file,
        string referenceId,
        string referenceType,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator);
}
```

### IInteractionDocumentSettingsProvider

Resolves the active document retrieval settings for the current host.

- **Namespace**: `CrestApps.AI.Services`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IInteractionDocumentSettingsProvider
{
    /// <summary>
    /// Gets the current interaction document settings.
    /// </summary>
    Task<InteractionDocumentSettings> GetAsync();
}
```

### ITabularBatchProcessor

Processes tabular data batches using LLM. Splits documents into batches, executes LLM calls with bounded concurrency, and aggregates results.

- **Namespace**: `CrestApps.AI.Chat`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface ITabularBatchProcessor
{
    /// <summary>
    /// Splits tabular content into batches based on the configured batch size.
    /// </summary>
    IList<TabularBatch> SplitIntoBatches(string content, string fileName);

    /// <summary>
    /// Processes multiple batches concurrently with the LLM.
    /// </summary>
    Task<IList<TabularBatchResult>> ProcessBatchesAsync(
        IList<TabularBatch> batches,
        string userPrompt,
        TabularBatchContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Merges batch results into a single output string, preserving row order.
    /// </summary>
    string MergeResults(
        IList<TabularBatchResult> results,
        bool includeHeader = true);
}
```

### ITabularBatchResultCache

Caches tabular batch processing results to avoid re-processing documents on every chat message.

- **Namespace**: `CrestApps.AI.Chat`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface ITabularBatchResultCache
{
    /// <summary>
    /// Generates a cache key based on interaction ID, document hash, and prompt.
    /// </summary>
    string GenerateCacheKey(
        string interactionId,
        string documentContentHash,
        string prompt);

    /// <summary>
    /// Computes a hash of the document contents for cache key generation.
    /// </summary>
    string ComputeDocumentContentHash(
        IEnumerable<(string FileName, string Content)> documents);

    /// <summary>
    /// Attempts to retrieve cached batch results.
    /// </summary>
    TabularBatchCacheEntry TryGet(string cacheKey);

    /// <summary>
    /// Stores batch results in the cache.
    /// </summary>
    void Set(
        string cacheKey,
        TabularBatchCacheEntry entry,
        TimeSpan? expiration = null);

    /// <summary>
    /// Removes a specific cache entry.
    /// </summary>
    void Remove(string cacheKey);

    /// <summary>
    /// Invalidates all cached results for a specific interaction.
    /// </summary>
    void InvalidateForInteraction(string interactionId);
}
```

---

## Memory

Interfaces for storing and validating per-user AI memory entries.

### IAIMemoryStore

Persistence store for `AIMemoryEntry` records with user-scoped queries.

- **Namespace**: `CrestApps.AI`
- **Extends**: `ICatalog<AIMemoryEntry>`
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAIMemoryStore : ICatalog<AIMemoryEntry>
{
    Task<int> CountByUserAsync(string userId);

    Task<AIMemoryEntry> FindByUserAndNameAsync(string userId, string name);

    Task<IReadOnlyCollection<AIMemoryEntry>> GetByUserAsync(
        string userId,
        int limit = 100);
}
```

### IAIMemorySafetyService

Validates AI memory entries for safety before storage.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface IAIMemorySafetyService
{
    bool TryValidate(
        string name,
        string description,
        string content,
        out string errorMessage);
}
```

---

## MCP (Model Context Protocol)

Interfaces for MCP client/server communication, capability resolution, resource handling, and authentication.

### IMcpCapabilityResolver

Resolves MCP capabilities semantically relevant to a user prompt using in-memory vector similarity over cached capability embeddings.

- **Namespace**: `CrestApps.AI.Mcp`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface IMcpCapabilityResolver
{
    /// <summary>
    /// Resolves MCP capabilities relevant to the given user prompt.
    /// </summary>
    Task<McpCapabilityResolutionResult> ResolveAsync(
        string prompt,
        string providerName,
        string connectionName,
        string[] mcpConnectionIds,
        CancellationToken cancellationToken = default);
}
```

### IMcpCapabilityEmbeddingCacheProvider

Caches embedding vectors for MCP capability metadata. Embeddings are recomputed when the underlying metadata is invalidated.

- **Namespace**: `CrestApps.AI.Mcp`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface IMcpCapabilityEmbeddingCacheProvider
{
    /// <summary>
    /// Gets or creates embedding entries for all capabilities across given servers.
    /// </summary>
    Task<IReadOnlyList<McpCapabilityEmbeddingEntry>> GetOrCreateEmbeddingsAsync(
        IReadOnlyList<McpServerCapabilities> capabilities,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates cached embeddings for a specific MCP connection.
    /// </summary>
    void Invalidate(string connectionId);
}
```

### IMcpClientTransportProvider

Provides transport instances for MCP client connections. Implement this to add support for custom MCP transports.

- **Namespace**: `CrestApps.AI.Mcp`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface IMcpClientTransportProvider
{
    /// <summary>
    /// Determines whether this provider can handle the specified connection.
    /// </summary>
    bool CanHandle(McpConnection connection);

    /// <summary>
    /// Gets an IClientTransport instance for the specified connection.
    /// </summary>
    Task<IClientTransport> GetAsync(McpConnection connection);
}
```

### IMcpFileProviderResolver

Resolves an `IFileProvider` by provider name. Register additional named providers (e.g., media, web root) for MCP resource access.

- **Namespace**: `CrestApps.AI.Mcp`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface IMcpFileProviderResolver
{
    /// <summary>
    /// Resolves an IFileProvider for the given provider name.
    /// </summary>
    IFileProvider Resolve(string providerName);
}
```

### IMcpMetadataPromptGenerator

Generates a structured system prompt describing MCP server capabilities. The prompt is injected into the model context so the AI can reason about when to invoke MCP capabilities.

- **Namespace**: `CrestApps.AI.Mcp`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface IMcpMetadataPromptGenerator
{
    /// <summary>
    /// Generates a system prompt describing the given MCP server capabilities.
    /// </summary>
    string Generate(IReadOnlyList<McpServerCapabilities> capabilities);
}
```

### IMcpResourceHandler

Handles MCP resource events like exporting. Implement this to redact sensitive data during resource export.

- **Namespace**: `CrestApps.AI.Mcp`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface IMcpResourceHandler
{
    /// <summary>
    /// Called during resource export to allow modification of export data.
    /// </summary>
    void Exporting(ExportingMcpResourceContext context);
}
```

### IMcpResourceTypeHandler

Defines a handler for reading MCP resource content based on its type. Each resource type (File, FTP, SQL, etc.) has its own implementation.

- **Namespace**: `CrestApps.AI.Mcp`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface IMcpResourceTypeHandler
{
    /// <summary>
    /// Gets the type of resource this handler supports.
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Reads the resource content and returns the result.
    /// </summary>
    Task<ReadResourceResult> ReadAsync(
        McpResource resource,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken = default);
}
```

### IMcpServerMetadataCacheProvider

Provides cached metadata about MCP server capabilities. Queries MCP servers and caches results with a configurable TTL.

- **Namespace**: `CrestApps.AI.Mcp`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface IMcpServerMetadataCacheProvider
{
    /// <summary>
    /// Gets the capabilities of the specified MCP server connection.
    /// </summary>
    Task<McpServerCapabilities> GetCapabilitiesAsync(McpConnection connection);

    /// <summary>
    /// Invalidates the cached metadata for a specific connection.
    /// </summary>
    Task InvalidateAsync(string connectionId);
}
```

### IMcpServerPromptService

Provides server-side MCP prompt listing and retrieval.

- **Namespace**: `CrestApps.AI.Mcp.Services`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface IMcpServerPromptService
{
    Task<IList<Prompt>> ListAsync();

    Task<GetPromptResult> GetAsync(
        RequestContext<GetPromptRequestParams> request,
        CancellationToken cancellationToken = default);
}
```

### IMcpServerResourceService

Provides server-side MCP resource listing and reading.

- **Namespace**: `CrestApps.AI.Mcp.Services`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface IMcpServerResourceService
{
    Task<IList<Resource>> ListAsync();

    Task<IList<ResourceTemplate>> ListTemplatesAsync();

    Task<ReadResourceResult> ReadAsync(
        RequestContext<ReadResourceRequestParams> request,
        CancellationToken cancellationToken = default);
}
```

### IOAuth2TokenService

Acquires OAuth 2.0 access tokens using various grant types. Used by MCP server authentication.

- **Namespace**: `CrestApps.AI.Mcp.Services`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface IOAuth2TokenService
{
    /// <summary>
    /// Acquires a token using the client credentials grant.
    /// </summary>
    Task<string> AcquireTokenAsync(
        string tokenEndpoint,
        string clientId,
        string clientSecret,
        string scopes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires a token using a private key JWT client assertion.
    /// </summary>
    Task<string> AcquireTokenWithPrivateKeyJwtAsync(
        string tokenEndpoint,
        string clientId,
        string privateKeyPem,
        string keyId = null,
        string scopes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires a token using mutual TLS (mTLS) client authentication.
    /// </summary>
    Task<string> AcquireTokenWithMtlsAsync(
        string tokenEndpoint,
        string clientId,
        byte[] clientCertificateBytes,
        string certificatePassword = null,
        string scopes = null,
        CancellationToken cancellationToken = default);
}
```

---

## A2A (Agent-to-Agent)

Interfaces for the Agent-to-Agent protocol — cached agent cards and connection authentication.

### IA2AAgentCardCacheService

Provides cached access to agent cards from remote A2A host connections.

- **Namespace**: `CrestApps.AI.A2A.Services`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface IA2AAgentCardCacheService
{
    /// <summary>
    /// Gets the agent card for the specified A2A connection, using a cached value if available.
    /// </summary>
    Task<AgentCard> GetAgentCardAsync(
        string connectionId,
        A2AConnection connection,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the cached agent card for the specified connection.
    /// </summary>
    void Invalidate(string connectionId);
}
```

### IA2AConnectionAuthService

Builds HTTP authentication headers for A2A connections.

- **Namespace**: `CrestApps.AI.A2A.Services`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface IA2AConnectionAuthService
{
    /// <summary>
    /// Builds the HTTP authentication headers for the given connection metadata.
    /// </summary>
    Task<Dictionary<string, string>> BuildHeadersAsync(
        A2AConnectionMetadata metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Configures an HttpClient with the authentication headers.
    /// </summary>
    Task ConfigureHttpClientAsync(
        HttpClient httpClient,
        A2AConnectionMetadata metadata,
        CancellationToken cancellationToken = default);
}
```

---

## Templates &amp; Prompting

Discover, parse, render, and compose AI prompt templates.

### IAITemplateParser

Parses AI template content into metadata and body. Implement this to add support for additional file formats.

- **Namespace**: `CrestApps.AI.Prompting.Parsing`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface IAITemplateParser
{
    /// <summary>
    /// Gets the file extensions this parser supports (e.g., ".md", ".yaml").
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Parses the raw content of a template file, separating metadata from the body.
    /// </summary>
    AITemplateParseResult Parse(string rawContent);
}
```

### IAITemplateProvider

Provides prompt templates from a specific source. Implement this to add custom template discovery.

- **Namespace**: `CrestApps.AI.Prompting.Providers`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface IAITemplateProvider
{
    /// <summary>
    /// Gets all prompt templates from this provider.
    /// </summary>
    Task<IReadOnlyList<AITemplate>> GetTemplatesAsync();
}
```

### IAITemplateEngine

Processes AI Liquid templates: renders them with arguments and validates their syntax.

- **Namespace**: `CrestApps.AI.Prompting.Rendering`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface IAITemplateEngine
{
    /// <summary>
    /// Renders a Liquid template string with the given arguments.
    /// </summary>
    Task<string> RenderAsync(
        string template,
        IDictionary<string, object> arguments = null);

    /// <summary>
    /// Validates that a Liquid template has valid syntax.
    /// </summary>
    bool TryValidate(string template, out IList<string> errors);
}
```

### IAITemplateService

High-level service for discovering, listing, rendering, and composing AI prompt templates.

- **Namespace**: `CrestApps.AI.Prompting.Services`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface IAITemplateService
{
    /// <summary>
    /// Lists all available prompt templates.
    /// </summary>
    Task<IReadOnlyList<AITemplate>> ListAsync();

    /// <summary>
    /// Gets a prompt template by its unique identifier.
    /// </summary>
    Task<AITemplate> GetAsync(string id);

    /// <summary>
    /// Renders a prompt template with the provided arguments.
    /// </summary>
    Task<string> RenderAsync(
        string id,
        IDictionary<string, object> arguments = null);

    /// <summary>
    /// Renders and merges multiple prompt templates into a single output.
    /// </summary>
    Task<string> MergeAsync(
        IEnumerable<string> ids,
        IDictionary<string, object> arguments = null,
        string separator = "\n\n");
}
```

---

## Speech

Interfaces for text-to-speech synthesis and speech voice resolution.

### ISpeechVoiceResolver

Resolves available speech voices for a deployment by delegating to the matching AI client provider.

- **Namespace**: `CrestApps.AI`
- **Extends**: None
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface ISpeechVoiceResolver
{
    /// <summary>
    /// Gets the available speech voices for the specified deployment.
    /// </summary>
    Task<SpeechVoice[]> GetSpeechVoicesAsync(AIDeployment deployment);
}
```

### ITextToSpeechClient

Synthesizes text into audio speech, supporting both one-shot and streaming modes. Thread-safe for concurrent use.

- **Namespace**: `CrestApps.AI`
- **Extends**: `IDisposable`
- **Project**: `CrestApps.AI.Abstractions`

```csharp
public interface ITextToSpeechClient : IDisposable
{
    /// <summary>
    /// Sends text and returns the generated audio speech.
    /// </summary>
    Task<TextToSpeechResponse> GetAudioAsync(
        string text,
        TextToSpeechOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends text and streams back the generated audio speech.
    /// </summary>
    IAsyncEnumerable<TextToSpeechResponseUpdate> GetStreamingAudioAsync(
        string text,
        TextToSpeechOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a service object of the specified type.
    /// </summary>
    object GetService(Type serviceType, object serviceKey = null);
}
```

---

## Copilot

Interfaces for the GitHub Copilot-style embedded chat experience.

### ICopilotCredentialStore

Abstracts storage and retrieval of GitHub OAuth credentials per user. Implement with your preferred user store.

- **Namespace**: `CrestApps.AI.Chat.Copilot`
- **Extends**: None
- **Project**: `CrestApps.OrchardCore.AI.Abstractions`

```csharp
public interface ICopilotCredentialStore
{
    /// <summary>
    /// Gets the protected credential for the specified user.
    /// </summary>
    Task<CopilotProtectedCredential> GetProtectedCredentialAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a protected credential for the specified user.
    /// </summary>
    Task SaveProtectedCredentialAsync(
        string userId,
        CopilotProtectedCredential credential,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the credential for the specified user.
    /// </summary>
    Task ClearCredentialAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
```
