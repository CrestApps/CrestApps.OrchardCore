---
sidebar_label: Consuming AI Services
sidebar_position: 9
title: Consuming AI Services via Code
description: How to use IAIClientFactory, IAICompletionService, IOrchestrator, and other AI services programmatically in Orchard Core.
---

# Consuming AI Services via Code

This guide explains how to use the CrestApps AI services programmatically in your Orchard Core modules.

## Service Overview

The AI infrastructure provides several key services at different abstraction levels:

| Service | Level | Purpose |
|---------|-------|---------|
| `IAIClientFactory` | Low | Creates raw AI clients (`IChatClient`, `IEmbeddingGenerator`, `IImageGenerator`) |
| `IAICompletionService` | Mid | Sends messages to AI and returns completions, with handler pipeline support |
| `IAICompletionContextBuilder` | Mid | Builds `AICompletionContext` from a resource (profile or chat interaction) |
| `IOrchestrator` | High | Full orchestration runtime with planning, tool scoping, and execution loops |
| `IOrchestrationContextBuilder` | High | Builds `OrchestrationContext` from a resource for use with an orchestrator |

## IAIClientFactory

The lowest-level service. Use it when you need direct access to AI clients without any orchestration or context management. It creates standard [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI) clients.

```csharp
public class MyService
{
    private readonly IAIClientFactory _clientFactory;

    public MyService(IAIClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<string> GetCompletionAsync()
    {
        // Create a chat client for a specific provider, connection, and model
        var chatClient = await _clientFactory.CreateChatClientAsync(
            providerName: "OpenAI",
            connectionName: "default",
            deploymentName: "gpt-4o-mini");

        var response = await chatClient.GetResponseAsync("Hello, world!");
        return response.Text;
    }

    public async Task<Embedding<float>> GetEmbeddingAsync(string text)
    {
        var generator = await _clientFactory.CreateEmbeddingGeneratorAsync(
            providerName: "OpenAI",
            connectionName: "default",
            deploymentName: "text-embedding-3-small");

        var embedding = await generator.GenerateEmbeddingAsync(text);
        return embedding;
    }
}
```

**When to use**: Simple, one-off AI calls where you don't need profiles, tools, or orchestration. Ideal for embedding generation, simple completions, or building custom pipelines.

## IAICompletionService

A mid-level service that sends messages to AI models and returns completions. It includes a handler pipeline (`IAICompletionServiceHandler`) that can modify requests and responses.

```csharp
public class MyService
{
    private readonly IAICompletionService _completionService;

    public MyService(IAICompletionService completionService)
    {
        _completionService = completionService;
    }

    public async Task<ChatResponse> CompleteAsync(
        string clientName,
        IEnumerable<ChatMessage> messages,
        AICompletionContext context)
    {
        return await _completionService.CompleteAsync(clientName, messages, context);
    }

    public IAsyncEnumerable<ChatResponseUpdate> StreamAsync(
        string clientName,
        IEnumerable<ChatMessage> messages,
        AICompletionContext context,
        CancellationToken cancellationToken)
    {
        return _completionService.CompleteStreamingAsync(
            clientName, messages, context, cancellationToken);
    }
}
```

**When to use**: When you need to send messages to an AI model with context management, but don't need full orchestration (tool loops, planning, etc.).

## IAICompletionContextBuilder

Builds an `AICompletionContext` from a resource object (such as an `AIProfile` or `ChatInteraction`). The builder runs a handler pipeline that populates the context with tools, MCP connections, data sources, and other configuration.

```csharp
public class MyService
{
    private readonly IAICompletionContextBuilder _contextBuilder;
    private readonly IAIProfileManager _profileManager;

    public MyService(
        IAICompletionContextBuilder contextBuilder,
        IAIProfileManager profileManager)
    {
        _contextBuilder = contextBuilder;
        _profileManager = profileManager;
    }

    public async Task<AICompletionContext> BuildContextAsync(string profileName)
    {
        var profile = await _profileManager.FindByNameAsync(profileName);

        var context = await _contextBuilder.BuildAsync(profile, ctx =>
        {
            // Optionally customize the context
            ctx.SystemMessage = "Custom system message override";
        });

        return context;
    }
}
```

**When to use**: When you need a fully configured completion context built from an AI Profile or Chat Interaction, but want to manage the completion call yourself.

## IOrchestrator

The highest-level service. The orchestrator is a pluggable runtime that manages the full lifecycle of an AI completion session, including:

- **Planning** — Decomposing complex requests into steps
- **Tool scoping** — Selecting a subset of available tools relevant to the request
- **Execution loops** — Calling the LLM with scoped tools, evaluating results, and iterating
- **Capability gap detection** — Expanding tool scope when the current tools can't handle the request
- **Streaming** — Producing the final streaming response

```csharp
public class MyService
{
    private readonly IOrchestrationContextBuilder _orchestrationContextBuilder;
    private readonly IAIProfileManager _profileManager;

    public MyService(
        IOrchestrationContextBuilder orchestrationContextBuilder,
        IAIProfileManager profileManager)
    {
        _orchestrationContextBuilder = orchestrationContextBuilder;
        _profileManager = profileManager;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> ExecuteAsync(
        string profileName,
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var profile = await _profileManager.FindByNameAsync(profileName);

        // Build orchestration context from the profile
        var context = await _orchestrationContextBuilder.BuildAsync(profile, ctx =>
        {
            ctx.UserMessage = userMessage;
        });

        // Resolve the orchestrator for this profile
        var orchestrator = context.Orchestrator;

        // Execute and stream results
        await foreach (var update in orchestrator.ExecuteStreamingAsync(context, cancellationToken))
        {
            yield return update;
        }
    }
}
```

**When to use**: When you want the full AI experience — tool invocation, MCP integration, document handling, multi-step reasoning — managed automatically.

## IOrchestrationContextBuilder

Builds an `OrchestrationContext` from a resource object. Similar to `IAICompletionContextBuilder`, but produces a richer context that includes orchestrator selection, tool registry, and extensible properties.

The builder runs a handler pipeline (`IOrchestrationContextBuilderHandler`) with `BuildingAsync` and `BuiltAsync` phases:

```csharp
public class MyOrchestrationHandler : IOrchestrationContextBuilderHandler
{
    public Task BuildingAsync(OrchestrationContextBuildingContext context)
    {
        // Add custom tools or modify context during building phase
        return Task.CompletedTask;
    }

    public Task BuiltAsync(OrchestrationContextBuiltContext context)
    {
        // Post-processing after context is built
        return Task.CompletedTask;
    }
}
```

Register your handler in `Startup`:

```csharp
services.AddScoped<IOrchestrationContextBuilderHandler, MyOrchestrationHandler>();
```

## Choosing the Right Service

| Scenario | Service |
|----------|---------|
| Simple completion or embedding call | `IAIClientFactory` |
| Send messages with handler pipeline | `IAICompletionService` |
| Build context from a profile for custom use | `IAICompletionContextBuilder` |
| Full AI experience with tools, MCP, and documents | `IOrchestrator` via `IOrchestrationContextBuilder` |
| Extend orchestration context building | `IOrchestrationContextBuilderHandler` |
| Extend completion context building | `IAICompletionContextBuilderHandler` |

## Implementing a Custom Orchestrator

You can create a custom orchestrator by implementing `IOrchestrator`:

```csharp
public sealed class MyCustomOrchestrator : IOrchestrator
{
    public string Name => "MyCustomOrchestrator";

    public async IAsyncEnumerable<ChatResponseUpdate> ExecuteStreamingAsync(
        OrchestrationContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Custom orchestration logic here
        // Access tools via context.Tools
        // Access messages via context.Messages
        // Access completion context via context.CompletionContext
    }
}
```

Register your orchestrator in `Startup`:

```csharp
services.AddOrchestrator<MyCustomOrchestrator>("MyCustomOrchestrator", options =>
{
    options.DisplayName = S["My Custom Orchestrator"];
    options.Description = S["A custom orchestration runtime."];
});
```
