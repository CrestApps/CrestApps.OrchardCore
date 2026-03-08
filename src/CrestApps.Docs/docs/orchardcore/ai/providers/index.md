---
sidebar_label: Overview
sidebar_position: 0
title: AI Providers
description: Overview of AI provider modules and how to implement custom providers for Orchard Core.
---

# AI Providers

AI Providers are modules that connect the CrestApps AI infrastructure to specific AI services. Each provider knows how to create chat clients, embedding generators, and image generators for its platform.

## What Is a Provider?

A provider is a module that implements the connection layer between CrestApps AI Services and a specific AI platform. Providers handle:

- **Authentication** — Managing API keys, tokens, or managed identity credentials
- **Client creation** — Creating `IChatClient`, `IEmbeddingGenerator`, and `IImageGenerator` instances
- **Connection configuration** — Defining endpoints, deployment names, and provider-specific settings
- **Deployment management** — Supporting multiple models/deployments under a single connection

## Built-in Providers

| Provider | Module | Description |
|----------|--------|-------------|
| [Azure AI Inference](azure-ai-inference) | `CrestApps.OrchardCore.AzureAIInference` | GitHub models via Azure AI Inference |
| [Azure OpenAI](azure-openai) | `CrestApps.OrchardCore.OpenAI.Azure` | Azure OpenAI Service integration |
| [Ollama](ollama) | `CrestApps.OrchardCore.Ollama` | Local model support via Ollama |
| [OpenAI](openai) | `CrestApps.OrchardCore.OpenAI` | OpenAI and any OpenAI-compatible provider |

> **Tip:** Most modern AI providers offer APIs that follow the **OpenAI API standard**. For these providers, use the **OpenAI** provider type when configuring their connections and endpoints. This includes DeepSeek, Google Gemini, Together AI, vLLM, and many more.

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

Use the `NamedAICompletionClient` base class for standard providers, or `DeploymentAwareAICompletionClient` if your provider supports multiple deployments:

```csharp
public sealed class CustomCompletionClient : NamedAICompletionClient
{
    public CustomCompletionClient(
        IAIClientFactory aIClientFactory,
        ILoggerFactory loggerFactory,
        IDistributedCache distributedCache,
        IOptions<AIProviderOptions> providerOptions,
        IEnumerable<IAICompletionServiceHandler> handlers,
        IOptions<DefaultAIOptions> defaultOptions
    ) : base(
        "CustomSource",
        aIClientFactory, distributedCache,
        loggerFactory,
        providerOptions.Value,
        defaultOptions.Value,
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

### Supporting Multiple Deployments

If your provider supports multiple models, register a deployment provider:

```csharp
services.AddAIDeploymentProvider("CustomProvider", options =>
{
    options.DisplayName = _localizer["Custom Provider"];
    options.Description = _localizer["Custom provider deployments."];
});
```
