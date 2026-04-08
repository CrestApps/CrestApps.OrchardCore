---
sidebar_label: Overview
sidebar_position: 1
title: AI Providers
description: Provider architecture and how to connect to OpenAI, Azure OpenAI, Ollama, and Azure AI Inference.
---

:::info Canonical framework docs
The shared framework guidance now lives in **[CrestApps.Core](https://core.crestapps.com/docs/framework/providers/index)**. This Orchard Core page is kept for Orchard-specific integration context and cross-links.
:::

# AI Providers

> Connect to one or more LLM providers. Each provider registers an `IAIClientProvider` that creates typed AI clients.

## Quick Start

Register the providers you use:

```csharp
builder.Services
    .AddCrestAppsAI()
    .AddOrchestrationServices()
    .AddOpenAIProvider()          // OpenAI (api.openai.com)
    .AddAzureOpenAIProvider()     // Azure OpenAI Service
    .AddOllamaProvider()          // Ollama (local models)
    .AddAzureAIInferenceProvider(); // Azure AI Inference / GitHub Models
```

You only need to register the providers you actually use.

## Architecture

Each provider follows the same pattern:

1. **Registers an `IAIClientProvider`** — Creates chat clients, embedding generators, image generators, etc.
2. **Registers an `IAICompletionClient`** — Handles completion requests for that provider
3. **Registers a connection source** — Provides connection metadata (API keys, endpoints)

```text
IAIClientFactory
    │
    ├── OpenAIClientProvider
    │       └── Creates OpenAI.ChatClient
    │
    ├── AzureOpenAIClientProvider
    │       └── Creates AzureOpenAI.ChatClient
    │
    ├── OllamaAIClientProvider
    │       └── Creates Ollama ChatClient
    │
    └── AzureAIInferenceClientProvider
            └── Creates Azure.AI.Inference ChatClient
```

## Provider Connection

Each provider needs at least one **connection** that stores credentials:

```csharp
public class AIProviderConnectionEntry
{
    public string Name { get; set; }           // Unique connection name
    public string ProviderName { get; set; }   // "OpenAI", "Azure", "Ollama", etc.
    public string GetApiKey();                 // API key
    public Uri GetEndpoint();                  // Endpoint URL (optional for OpenAI)
}
```

Connections are typically stored in a configuration file or database and loaded at startup. See the [MVC Example](../mvc-example.md) for a complete setup.

## Adding a Custom Provider

Implement these interfaces:

1. **`IAIClientProvider`** — Creates client instances
2. **`IAICompletionClient`** — Handles completions

```csharp
public sealed class MyProviderClientProvider : IAIClientProvider
{
    public string ProviderName => "MyProvider";

    public IChatClient CreateChatClient(
        AIProviderConnectionEntry connection, string deploymentName)
    {
        // Create and return your chat client
    }

    // Implement other client creation methods...
}

public sealed class MyProviderCompletionClient : IAICompletionClient
{
    public async Task<ChatResponse> CompleteAsync(
        AICompletionContext context,
        CancellationToken cancellationToken = default)
    {
        // Send completion request to your provider
    }
}
```

Register:

```csharp
builder.Services.AddScoped<IAIClientProvider, MyProviderClientProvider>();
builder.Services.AddAICompletionClient<MyProviderCompletionClient>("MyProvider");
builder.Services.AddAIConnectionSource("MyProvider", configure => { /* ... */ });
```

## Available Providers

| Provider | Extension | Provider Name | Documentation |
|----------|-----------|--------------|---------------|
| OpenAI | `AddOpenAIProvider()` | `"OpenAI"` | [OpenAI](./openai.md) |
| Azure OpenAI | `AddAzureOpenAIProvider()` | `"Azure"` | [Azure OpenAI](./azure-openai.md) |
| Ollama | `AddOllamaProvider()` | `"Ollama"` | [Ollama](./ollama.md) |
| Azure AI Inference | `AddAzureAIInferenceProvider()` | `"AzureAIInference"` | [Azure AI Inference](./azure-ai-inference.md) |

## Provider Comparison

| Capability | OpenAI | Azure OpenAI | Ollama | Azure AI Inference |
|-----------|--------|-------------|--------|-------------------|
| Chat completions | ✅ | ✅ | ✅ | ✅ |
| Streaming | ✅ | ✅ | ✅ | ✅ |
| Function calling | ✅ | ✅ | ⚠️ Model-dependent | ⚠️ Model-dependent |
| Embeddings | ✅ | ✅ | ✅ | ✅ |
| Image generation | ✅ (DALL·E) | ✅ (DALL·E) | ❌ | ❌ |
| Speech-to-text | ✅ (Whisper) | ✅ (via Azure Speech) | ❌ | ❌ |
| Text-to-speech | ✅ | ✅ (via Azure Speech) | ❌ | ❌ |
| Vision (image input) | ✅ | ✅ | ⚠️ Model-dependent | ⚠️ Model-dependent |
| Managed identity | ❌ | ✅ | N/A | ✅ |
| Data residency | ❌ | ✅ (per region) | ✅ (local) | ✅ (per region) |
| Cost tier | Pay-per-token | Pay-per-token | Free (self-hosted) | Pay-per-token |

## When to Choose Which Provider

| Scenario | Recommended Provider | Why |
|----------|---------------------|-----|
| **Prototyping / getting started** | OpenAI | Simplest setup — just an API key |
| **Enterprise production** | Azure OpenAI | Data residency, SLAs, managed identity, VNET support |
| **Local development** | Ollama | No API costs, fast iteration, offline capable |
| **Privacy-sensitive workloads** | Ollama | Data never leaves your infrastructure |
| **Multi-model exploration** | Azure AI Inference | Access GPT, Llama, Mistral, Cohere through a single endpoint |
| **GitHub-integrated workflows** | Azure AI Inference | Use your GitHub token to access models via GitHub Models |
| **Image generation** | OpenAI or Azure OpenAI | Only providers with DALL·E support |
| **Speech capabilities** | OpenAI or Azure OpenAI | Only providers with Whisper/TTS support |

:::tip
You can register **multiple providers simultaneously** and assign different profiles to different providers. For example, use Ollama for development and Azure OpenAI for production by switching connection names per environment.
:::

## Custom Provider Walkthrough

To add a provider for a service not covered by the built-in providers, implement three components:

### Step 1: Implement `IAIClientProvider`

The client provider creates typed AI clients (chat, embedding, image) from a connection entry:

```csharp
public sealed class MyProviderClientProvider : IAIClientProvider
{
    public string ProviderName => "MyProvider";

    public IChatClient CreateChatClient(
        AIProviderConnectionEntry connection,
        string deploymentName)
    {
        var apiKey = connection.GetApiKey();
        var endpoint = connection.GetEndpoint()
            ?? new Uri("https://api.myprovider.com");

        // Use Microsoft.Extensions.AI abstractions
        return new MyProviderChatClient(endpoint, apiKey, deploymentName);
    }

    public IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(
        AIProviderConnectionEntry connection,
        string deploymentName)
    {
        var apiKey = connection.GetApiKey();
        var endpoint = connection.GetEndpoint()
            ?? new Uri("https://api.myprovider.com");

        return new MyProviderEmbeddingGenerator(endpoint, apiKey, deploymentName);
    }

    // Return null for capabilities the provider does not support
    public object CreateImageGenerator(
        AIProviderConnectionEntry connection,
        string deploymentName)
        => null;
}
```

### Step 2: Implement `IAICompletionClient`

The completion client handles the request/response cycle:

```csharp
public sealed class MyProviderCompletionClient(
    IAIClientFactory clientFactory,
    ILogger<MyProviderCompletionClient> logger) : IAICompletionClient
{
    public async Task<ChatResponse> CompleteAsync(
        AICompletionContext context,
        CancellationToken cancellationToken = default)
    {
        var chatClient = clientFactory.GetChatClient(context);

        if (chatClient is null)
        {
            logger.LogWarning("No chat client available for connection '{Name}'.",
                context.ConnectionName);
            return ChatResponse.Empty;
        }

        var options = new ChatOptions
        {
            Temperature = context.Profile.Temperature,
            MaxOutputTokens = context.Profile.MaxOutputTokens,
        };

        // Delegate to the Microsoft.Extensions.AI IChatClient
        return await chatClient.GetResponseAsync(
            context.Messages,
            options,
            cancellationToken);
    }
}
```

### Step 3: Register Connection Source and Services

```csharp
public static class MyProviderServiceExtensions
{
    public static AIServiceBuilder AddMyProvider(this AIServiceBuilder builder)
    {
        var services = builder.Services;

        // Register the client provider
        services.AddScoped<IAIClientProvider, MyProviderClientProvider>();

        // Register the completion client for this provider name
        services.AddAICompletionClient<MyProviderCompletionClient>("MyProvider");

        // Register the connection source (how credentials are loaded)
        services.AddAIConnectionSource("MyProvider", options =>
        {
            // Connections can be loaded from configuration, database, etc.
            options.Connections.Add(new AIProviderConnectionEntry
            {
                Name = "my-connection",
                ProviderName = "MyProvider",
            });
        });

        return builder;
    }
}
```

Use it:

```csharp
builder.Services
    .AddCrestAppsAI()
    .AddOrchestrationServices()
    .AddMyProvider();
```

## Fallback Strategies

The framework does not include automatic provider failover, but you can implement fallback logic at the application level:

### Connection-Level Fallback

Register multiple connections for different providers and switch on failure:

```csharp
public sealed class FallbackCompletionService(
    IEnumerable<IAICompletionClient> clients,
    ILogger<FallbackCompletionService> logger)
{
    private readonly string[] _providerOrder = ["Azure", "OpenAI", "Ollama"];

    public async Task<ChatResponse> CompleteWithFallbackAsync(
        AICompletionContext context,
        CancellationToken cancellationToken)
    {
        foreach (var providerName in _providerOrder)
        {
            var client = clients.FirstOrDefault(
                c => c.GetType().Name.Contains(providerName));

            if (client is null)
            {
                continue;
            }

            try
            {
                return await client.CompleteAsync(context, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Provider '{Provider}' failed, trying next.", providerName);
            }
        }

        throw new InvalidOperationException("All AI providers failed.");
    }
}
```

### Profile-Level Fallback

Assign a primary and fallback connection at the profile level:

```json
{
  "Profiles": {
    "my-chat": {
      "ConnectionName": "azure-primary",
      "FallbackConnectionName": "openai-backup"
    }
  }
}
```

:::warning
When implementing fallback logic, be mindful of token format differences between providers. A conversation started with one provider's tokenizer may behave differently when sent to another provider mid-stream.
:::
