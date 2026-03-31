---
sidebar_label: AI Core
sidebar_position: 3
title: AI Core
description: Core AI services including completion clients, client factory, context building, and the deployment resolution chain.
---

# AI Core

> Provider-agnostic AI completion services, client factory, and context-building pipeline.

## Quick Start

```csharp
builder.Services
    .AddCrestAppsAI()
    .AddOpenAIProvider(); // or any other provider
```

This gives you access to `IAIClientFactory`, `IAICompletionService`, and `IAICompletionContextBuilder`.

## Problem & Solution

AI applications need to work with multiple LLM providers (OpenAI, Azure, Ollama, etc.) without coupling business logic to a specific SDK. The AI Core layer provides a **provider-agnostic abstraction** where you program against interfaces and swap providers through configuration.

## Core Concepts

### Deployment

A **deployment** maps a logical name to a specific model on a specific provider connection. For example, deployment `"gpt-4o"` might map to the `gpt-4o` model on your OpenAI connection. The orchestrator resolves deployments at runtime using a fallback chain:

1. Profile-level deployment override
2. Connection-level default deployment
3. Global default deployment

### Provider Connection

A **provider connection** stores credentials and endpoint information for a specific AI provider (API key, endpoint URL, provider name).

## Services Registered by `AddCrestAppsAI()`

| Service | Implementation | Lifetime | Purpose |
|---------|---------------|----------|---------|
| `IAIClientFactory` | `DefaultAIClientFactory` | Scoped | Creates typed AI clients |
| `IAICompletionService` | `DefaultAICompletionService` | Scoped | Deployment-aware completion |
| `IAICompletionContextBuilder` | `DefaultAICompletionContextBuilder` | Scoped | Builds context with handler pipeline |
| `IAITemplateService` | *(from AddAIPrompting)* | Scoped | Template rendering |

It also chains `AddAIPrompting()` and `AddCrestAppsCoreServices()` automatically.

## Key Interfaces

### `IAIClientFactory`

The lowest-level service. Creates typed AI clients from a provider connection entry.

```csharp
public interface IAIClientFactory
{
    IChatClient CreateChatClient(AIProviderConnectionEntry connection, string deploymentName);
    IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(
        AIProviderConnectionEntry connection, string deploymentName);
    // Also: CreateImageGenerator, CreateSpeechToTextClient, CreateTextToSpeechClient
}
```

**When to use:** Only when you need direct, low-level access to a specific client type.

### `IAICompletionService`

Mid-level service that resolves a deployment and sends a completion request.

```csharp
public interface IAICompletionService
{
    Task<ChatResponse> CompleteAsync(
        AIDeployment deployment,
        IList<ChatMessage> messages,
        ChatOptions options = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
        AIDeployment deployment,
        IList<ChatMessage> messages,
        ChatOptions options = null,
        CancellationToken cancellationToken = default);
}
```

**When to use:** When you have a deployment reference and want completion without the full orchestration loop.

### `IAICompletionContextBuilder`

Builds an `AICompletionContext` by running a handler pipeline that enriches the context before and after construction.

```csharp
public interface IAICompletionContextBuilder
{
    ValueTask<AICompletionContext> BuildAsync(
        AICompletionContextBuildingContext context,
        CancellationToken cancellationToken = default);
}
```

The builder invokes all registered `IAICompletionContextBuilderHandler` instances in sequence. See [Context Builders](./context-builders.md) for details.

### `IAICompletionClient`

Implement this interface to add a new AI provider. Each provider registers its own completion client.

```csharp
public interface IAICompletionClient
{
    Task<ChatResponse> CompleteAsync(
        AICompletionContext context,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
        AICompletionContext context,
        CancellationToken cancellationToken = default);
}
```

**When to implement:** When integrating an AI provider not already supported. See [Providers](./providers/index.md).

## Configuration

### `AIOptions`

Central options class for registering profile sources, deployment providers, connection sources, and template sources.

```csharp
services.Configure<AIOptions>(options =>
{
    options.AddProfileSource("MySource", configure => { /* ... */ });
    options.AddDeploymentProvider("MyProvider", configure => { /* ... */ });
    options.AddConnectionSource("MySource", configure => { /* ... */ });
});
```

### `DefaultAIDeploymentSettings`

Global default deployment settings, typically loaded from configuration:

```json
{
  "CrestApps": {
    "AI": {
      "DefaultDeploymentName": "gpt-4o",
      "DefaultConnectionName": "my-openai"
    }
  }
}
```

## Streaming Example

Use `CompleteStreamingAsync` to stream tokens as they are generated:

```csharp
public sealed class StreamingService(IAICompletionService completionService)
{
    public async IAsyncEnumerable<string> StreamAsync(
        AIDeployment deployment,
        string question,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant."),
            new(ChatRole.User, question),
        };

        await foreach (var update in completionService.CompleteStreamingAsync(
            deployment, messages, cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }
    }
}
```

### Using Streaming in an API Controller

```csharp
[ApiController]
[Route("api/[controller]")]
public sealed class ChatApiController : ControllerBase
{
    private readonly IAICompletionService _completionService;

    public ChatApiController(IAICompletionService completionService)
    {
        _completionService = completionService;
    }

    [HttpPost("stream")]
    public async Task StreamResponse(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant."),
            new(ChatRole.User, request.Message),
        };

        await foreach (var update in _completionService.CompleteStreamingAsync(
            request.Deployment, messages, cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                await Response.WriteAsync($"data: {update.Text}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
    }
}
```

## Error Handling

### Common Exceptions

| Exception | When | How to Handle |
|-----------|------|--------------|
| `InvalidOperationException` | No deployment found, no provider connection configured | Check AI configuration — this is a setup error |
| `HttpRequestException` | Provider API unreachable (network error, DNS failure) | Retry with exponential backoff, check network connectivity |
| `OperationCanceledException` | Request was cancelled (user navigated away, timeout) | Normal flow — let it propagate |
| Provider-specific rate limit errors | Too many requests to the AI provider | Implement retry policies at the HTTP client level |
| Provider-specific auth errors | Invalid API key or expired credentials | Check provider connection configuration |

### Handling Provider Failures

```csharp
public sealed class ResilientCompletionService
{
    private readonly IAICompletionService _completionService;
    private readonly ILogger<ResilientCompletionService> _logger;

    public ResilientCompletionService(
        IAICompletionService completionService,
        ILogger<ResilientCompletionService> logger)
    {
        _completionService = completionService;
        _logger = logger;
    }

    public async Task<string> SafeCompleteAsync(
        AIDeployment deployment,
        IList<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _completionService.CompleteAsync(
                deployment, messages, cancellationToken: cancellationToken);

            return response.Text;
        }
        catch (OperationCanceledException)
        {
            throw; // Always re-throw cancellation
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "AI configuration error — check deployment settings.");
            throw; // Configuration errors should not be silently swallowed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI completion failed for deployment '{Deployment}'.",
                deployment.Name);
            return null; // Or return a fallback message
        }
    }
}
```

:::warning
Never swallow `OperationCanceledException` — always re-throw it. Catching and ignoring it breaks the cancellation token contract and can cause resource leaks.
:::

## Implementing a Custom AI Provider

To integrate an AI provider that is not already supported (e.g., Anthropic, Mistral, Cohere), implement `IAICompletionClient`:

```csharp
public interface IAICompletionClient
{
    string Name { get; }

    Task<ChatResponse> CompleteAsync(
        IEnumerable<ChatMessage> messages,
        AICompletionContext context,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<ChatResponseUpdate> CompleteStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AICompletionContext context,
        CancellationToken cancellationToken = default);
}
```

### Example: Custom Provider Implementation

```csharp
public sealed class MyProviderCompletionClient : IAICompletionClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MyProviderCompletionClient> _logger;

    public MyProviderCompletionClient(
        IHttpClientFactory httpClientFactory,
        ILogger<MyProviderCompletionClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string Name => "MyProvider";

    public async Task<ChatResponse> CompleteAsync(
        IEnumerable<ChatMessage> messages,
        AICompletionContext context,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("MyProvider");

        // Convert messages to your provider's API format
        var request = new
        {
            model = context.Deployment.ModelName,
            messages = messages.Select(m => new
            {
                role = m.Role.Value,
                content = m.Text,
            }),
            max_tokens = context.Options?.MaxOutputTokens ?? 1024,
            temperature = context.Options?.Temperature ?? 0.7f,
        };

        var response = await client.PostAsJsonAsync("/v1/chat/completions", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<MyProviderResponse>(cancellationToken);

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, result.Content));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> CompleteStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AICompletionContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Similar to CompleteAsync but reads Server-Sent Events (SSE)
        // and yields ChatResponseUpdate for each token
        var client = _httpClientFactory.CreateClient("MyProvider");

        // Build request with stream: true
        var request = new
        {
            model = context.Deployment.ModelName,
            messages = messages.Select(m => new { role = m.Role.Value, content = m.Text }),
            stream = true,
        };

        using var response = await client.PostAsJsonAsync("/v1/chat/completions", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: "))
            {
                continue;
            }

            var data = line["data: ".Length..];
            if (data == "[DONE]")
            {
                break;
            }

            var chunk = JsonSerializer.Deserialize<MyProviderStreamChunk>(data);
            if (!string.IsNullOrEmpty(chunk?.Delta?.Content))
            {
                yield return new ChatResponseUpdate
                {
                    Text = chunk.Delta.Content,
                };
            }
        }
    }
}
```

### Registering the Provider

```csharp
services.AddScoped<IAICompletionClient, MyProviderCompletionClient>();
```

The `IAIClientFactory` uses the `Name` property to route requests to the correct provider. When a deployment's provider connection references `"MyProvider"`, the factory creates a client using your implementation.

## Example

```csharp
// Inject the high-level service
public class MyService(IAICompletionService completionService)
{
    public async Task<string> AskAsync(string question, AIDeployment deployment)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant."),
            new(ChatRole.User, question),
        };

        var response = await completionService.CompleteAsync(deployment, messages);
        return response.Text;
    }
}
```

## Orchard Core Integration

The [AI Services module](../ai/overview.md) registers `AddCrestAppsAI()` automatically when the feature is enabled and adds an admin UI for managing deployments, connections, and profiles.
