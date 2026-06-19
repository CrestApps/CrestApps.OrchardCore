---
sidebar_label: Overview
sidebar_position: 0
title: AI Providers
description: Overview of AI provider modules and how to implement custom providers for Orchard Core.
---

# AI Providers

AI providers are modules that connect the CrestApps AI infrastructure to specific AI services. Each provider knows how to create chat clients, embedding generators, image generators, and other provider-specific runtime services for its platform.

## What Is a Provider?

A provider is a module that implements the connection layer between CrestApps AI Services and a specific AI platform. Providers handle:

- **Authentication** — Managing API keys, tokens, or managed identity credentials
- **Client creation** — Creating `IChatClient`, `IEmbeddingGenerator`, and `IImageGenerator` instances
- **Connection configuration** — Defining endpoints, deployment names, and provider-specific settings
- **Deployment management** — Supporting multiple named deployments under a single connection, each with one or more `Purpose` values such as `Chat`, `Utility`, `Embedding`, `Image`, `SpeechToText`, `TextToSpeech`, or `Vision`

## Built-in Providers

| Provider | Module | Description |
|----------|--------|-------------|
| [Azure AI Inference](azure-ai-inference) | `CrestApps.OrchardCore.AzureAIInference` | GitHub models via Azure AI Inference |
| [Azure OpenAI](azure-openai) | `CrestApps.OrchardCore.OpenAI.Azure` | Azure OpenAI Service integration |
| [Ollama](ollama) | `CrestApps.OrchardCore.Ollama` | Local model support via Ollama |
| [OpenAI](openai) | `CrestApps.OrchardCore.OpenAI` | OpenAI and any OpenAI-compatible provider |

> **Tip:** Most modern AI providers offer APIs that follow the **OpenAI API standard**. For these providers, use the **OpenAI** provider type when configuring their connections and endpoints. This includes DeepSeek, Google Gemini, Together AI, vLLM, and many more.

## Deployment model

AI deployments are first-class records. Each deployment has:

- a **Name** used throughout Orchard editors, recipes, and settings
- an optional **ModelName** when the Orchard deployment name should differ from the vendor model name
- a **ConnectionName** that points at the provider connection
- a **Purpose** that describes what the deployment is used for

### Deployment purposes

| Purpose | Description |
|---------|-------------|
| `Chat` | Primary chat completions |
| `Utility` | Lightweight auxiliary tasks such as query rewriting or planning |
| `Embedding` | Vector embeddings for RAG and semantic search |
| `Image` | Image generation |
| `SpeechToText` | Speech-to-text transcription |
| `TextToSpeech` | Text-to-speech synthesis |
| `Vision` | Vision and image-understanding workloads |

When configuring connections through `appsettings.json`, each connection can define a `Deployments` array:

```json
{
  "Connections": {
    "my-connection": {
      "ApiKey": "your-api-key",
      "Deployments": [
        {
          "Name": "chat-default",
          "ModelName": "gpt-4o",
          "Purpose": "Chat"
        },
        {
          "Name": "utility-default",
          "ModelName": "gpt-4.1-mini",
          "Purpose": "Utility"
        },
        {
          "Name": "embedding-default",
          "ModelName": "text-embedding-3-large",
          "Purpose": "Embedding"
        }
      ]
    }
  }
}
```

Assign deployments directly on profiles and interactions when you need explicit model selection. For tenant-wide fallbacks, configure **Settings -> Artificial Intelligence -> Default Deployments**.

## Implementing a Custom Provider

To create a custom AI provider, you need to implement two key interfaces:

### 1. Implement `IAIClientProvider`

This interface is responsible for creating AI clients for your provider:

```csharp
public sealed class CustomAIClientProvider : IAIClientProvider
{
    public string ProviderName => "CustomProvider";

    public ValueTask<IChatClient> CreateChatClientAsync(
        AIProviderConnection connection, string deploymentName)
    {
        // Create and return an IChatClient for your provider
    }

    public ValueTask<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGeneratorAsync(
        AIProviderConnection connection, string deploymentName)
    {
        // Create and return an embedding generator
    }
}
```

### 2. Implement `IAICompletionClient`

Use the `NamedAICompletionClient` base class for standard providers, or `DeploymentAwareAICompletionClient` if your provider supports multiple deployments. Provider connections come from the active provider connection catalog:

```csharp
public sealed class CustomCompletionClient : NamedAICompletionClient
{
    public CustomCompletionClient(
        IAIClientFactory aIClientFactory,
        ILoggerFactory loggerFactory,
        IDistributedCache distributedCache,
        IEnumerable<IAICompletionServiceHandler> handlers,
        DefaultAIOptions defaultOptions
    ) : base(
        "CustomSource",
        aIClientFactory,
        distributedCache,
        loggerFactory,
        defaultOptions,
        handlers)
    {
    }

    protected override string ProviderName => "CustomProvider";

    protected override IChatClient GetChatClient(
        AIProviderConnection connection,
        AICompletionContext context,
        string deploymentName)
    {
        return new YourAIClient(connection.GetApiKey())
            .AsChatClient(deploymentName);
    }
}
```

### 3. Register Services

```csharp
public sealed class Startup : StartupBase
{
    private readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IAIClientProvider, CustomAIClientProvider>()
            .AddAIProfile<CustomCompletionClient>("CustomSource", "CustomProvider", o =>
            {
                o.DisplayName = S["Custom Provider"];
                o.Description = S["Provides AI profiles using custom source."];
            });
    }
}
```

### Supporting multiple deployments

If your provider supports multiple models, register a deployment provider:

```csharp
services.AddAIDeploymentProvider("CustomProvider", options =>
{
    options.DisplayName = _localizer["Custom Provider"];
    options.Description = _localizer["Custom provider deployments."];
});
```

When you create or edit a deployment in the admin UI, the connection picker is populated from the provider connection catalog for that provider, so deployments always point at the stored connection record by name.
