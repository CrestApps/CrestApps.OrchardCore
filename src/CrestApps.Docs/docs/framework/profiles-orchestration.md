---
title: AI Profiles & Orchestration
sidebar_position: 6
---

# AI Profiles & Orchestration

AI Profiles define reusable configurations for AI interactions — system prompts, model parameters, tool selections, and behavioral settings. The Orchestrator coordinates the full AI request pipeline including tool execution, RAG retrieval, and response streaming.

## AI Profiles

An `AIProfile` contains:

| Property | Description |
|----------|-------------|
| `Name` | Unique identifier (slug-friendly) |
| `DisplayText` | Human-readable display name |
| `Type` | `Chat`, `Utility`, or `TemplatePrompt` |
| `ConnectionName` | AI provider connection to use |
| `OrchestratorName` | Orchestrator to use (default: `DefaultOrchestrator`) |
| `SystemMessage` | Instructions that guide the AI's behavior |
| `WelcomeMessage` | Greeting shown when a chat session starts |
| `PromptTemplate` | Liquid template for generating prompts |
| `Temperature` | Controls randomness (0.0–2.0) |
| `TopP` | Nucleus sampling (0.0–1.0) |
| `FrequencyPenalty` | Penalizes repeated tokens (-2.0–2.0) |
| `PresencePenalty` | Penalizes already-present tokens (-2.0–2.0) |
| `MaxOutputTokens` | Maximum tokens in the response |
| `PastMessagesCount` | Number of previous messages to include in context |
| `UseCaching` | Enable response caching |
| `IsListable` | Whether the profile appears in the chat UI |
| `ToolNames` | List of AI tools attached to this profile |

### Managing Profiles Programmatically

```csharp
// Inject the profile manager
public class MyService
{
    private readonly IAIProfileManager _profiles;

    public MyService(IAIProfileManager profiles) => _profiles = profiles;

    public async Task CreateProfileAsync()
    {
        var profile = new AIProfile
        {
            Name = "customer-support",
            DisplayText = "Customer Support",
            Type = AIProfileType.Chat,
            ConnectionName = "my-openai",
            SystemMessage = "You are a helpful customer support agent.",
            WelcomeMessage = "Hello! How can I help you today?",
        };

        profile.AlterSettings<AICompletionProfileSettings>(s =>
        {
            s.Temperature = 0.7f;
            s.TopP = 0.9f;
            s.MaxOutputTokens = 2048;
            s.PastMessagesCount = 10;
        });

        await _profiles.CreateAsync(profile);
    }
}
```

### Profile Settings

Extended settings are stored in the profile's `Settings` dictionary:

| Settings Type | Purpose |
|--------------|---------|
| `AICompletionProfileSettings` | Temperature, TopP, MaxTokens, etc. |
| `AIChatProfileSettings` | PastMessagesCount, LockSystemMessage |
| `AIProfileDocumentSettings` | AllowSessionDocuments, DocumentTopN |
| `AIProfileToolsMetadata` | ToolNames, CapabilityNames |
| `AIProfileMcpMetadata` | MCP connection IDs |
| `AIProfileDataExtractionMetadata` | Data extraction settings |
| `AIProfileSessionMetricsMetadata` | Session metrics toggle |
| `AIProfilePostSessionProcessing` | Post-session analysis tasks |

## Orchestration

The **Orchestrator** coordinates the full AI interaction pipeline:

```
User Message
    ↓
┌─────────────────────────────────┐
│ Orchestrator                    │
│  1. Build context (history,     │
│     RAG documents, system msg)  │
│  2. Register tools              │
│  3. Call AI completion          │
│  4. Execute tool calls          │
│  5. Loop until complete         │
│  6. Stream response             │
└─────────────────────────────────┘
    ↓
AI Response (streamed)
```

### Default Orchestrator

The `DefaultOrchestrator` handles:
- **Context building** via `IOrchestrationContextBuilder`
- **Tool registration** via `IToolRegistry`
- **Preemptive RAG** via `PreemptiveRagOrchestrationHandler`
- **Function invocation** via `FunctionInvocationAICompletionServiceHandler`
- **Streaming** via `IAICompletionService`

### Custom Orchestrators

Create a custom orchestrator by implementing `IOrchestrator`:

```csharp
public sealed class MyOrchestrator : IOrchestrator
{
    public string Name => "MyOrchestrator";

    public async IAsyncEnumerable<StreamingChatCompletionUpdate> ChatAsync(
        AIProfile profile,
        IList<ChatMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Custom orchestration logic here
    }
}

// Register
builder.Services.AddOrchestrator<MyOrchestrator>("MyOrchestrator")
    .WithTitle("My Custom Orchestrator");
```

## Orchestration Pipeline Handlers

The pipeline is extensible via handlers registered in DI:

| Handler | Purpose |
|---------|---------|
| `IAICompletionContextBuilderHandler` | Adds context (system message, history) before AI call |
| `IOrchestrationContextBuilderHandler` | Pre-processes the orchestration context (e.g., RAG injection) |
| `IAICompletionServiceHandler` | Post-processes AI responses (e.g., tool invocation) |
| `IPreemptiveRagHandler` | Handles preemptive RAG search before orchestration |

### Adding a Custom Handler

```csharp
public sealed class MyContextHandler : IOrchestrationContextBuilderHandler
{
    public Task BuildContextAsync(OrchestrationContext context)
    {
        // Add custom context, modify messages, etc.
        context.Messages.Insert(0, new ChatMessage(
            ChatRole.System, "Additional context: ..."));
        return Task.CompletedTask;
    }
}

// Register
builder.Services.AddScoped<IOrchestrationContextBuilderHandler, MyContextHandler>();
```

## Data Extraction

When enabled, the system can automatically extract structured data from conversations:

```csharp
profile.AlterSettings<AIProfileDataExtractionMetadata>(s =>
{
    s.Enabled = true;
    s.ExtractionCheckInterval = 3; // Every 3 messages
    s.SessionInactivityTimeoutInMinutes = 30;
});
```

## Post-Session Processing

Configure AI-powered tasks that run after a session closes:

```csharp
profile.AlterSettings<AIProfilePostSessionProcessing>(s =>
{
    s.Enabled = true;
    s.Tasks =
    [
        new PostSessionTask
        {
            Name = "sentiment_analysis",
            Type = PostSessionTaskType.PredefinedOptions,
            Instructions = "Analyze the overall sentiment of the conversation.",
            Options =
            [
                new PostSessionTaskOption { Value = "Positive" },
                new PostSessionTaskOption { Value = "Neutral" },
                new PostSessionTaskOption { Value = "Negative" },
            ],
        },
        new PostSessionTask
        {
            Name = "summary",
            Type = PostSessionTaskType.Semantic,
            Instructions = "Provide a brief summary of the conversation.",
        },
    ];
});
```
