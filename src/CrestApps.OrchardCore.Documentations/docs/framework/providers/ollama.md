---
sidebar_label: Ollama
sidebar_position: 4
title: Ollama Provider
description: Connect to locally hosted Ollama models for private, self-hosted AI completions.
---

:::info Canonical framework docs
The shared framework guidance now lives in **[CrestApps.Core](https://core.crestapps.com/docs/framework/providers/ollama)**. This Orchard Core page is kept for Orchard-specific integration context and cross-links.
:::

# Ollama Provider

> Connect to locally hosted [Ollama](https://ollama.ai/) models for private, self-hosted AI completions.

## Quick Start

```csharp
builder.Services
    .AddCrestAppsAI()
    .AddOrchestrationServices()
    .AddOllamaProvider();
```

## Services Registered

| Service | Implementation | Lifetime |
|---------|---------------|----------|
| `IAIClientProvider` | `OllamaAIClientProvider` | Scoped |
| `IAICompletionClient` | `OllamaCompletionClient` | Scoped |
| Connection source | — | Scoped |

## Configuration

### Connection Setup

Point to your Ollama instance:

```json
{
  "CrestApps": {
    "AI": {
      "Providers": {
        "Ollama": {
          "Endpoint": "http://localhost:11434"
        }
      }
    }
  }
}
```

### Constants

| Constant | Value |
|----------|-------|
| `OllamaConstants.ProviderName` | `"Ollama"` |
| `OllamaConstants.ImplementationName` | `"Ollama"` |

## Use Cases

- **Development** — Run models locally without API costs
- **Privacy** — Keep data on-premises
- **Air-gapped environments** — No internet required after model download
- **Testing** — Fast iteration without rate limits

## Capabilities

| Capability | Supported |
|-----------|-----------|
| Chat completions | ✅ |
| Streaming | ✅ |
| Embeddings | ✅ |
| Image generation | ❌ |
| Speech-to-text | ❌ |
| Text-to-speech | ❌ |

## Docker Setup

The fastest way to run Ollama locally is with Docker:

```bash
# Run Ollama with CPU only
docker run -d -v ollama:/root/.ollama -p 11434:11434 --name ollama ollama/ollama

# Run Ollama with NVIDIA GPU support
docker run -d --gpus=all -v ollama:/root/.ollama -p 11434:11434 --name ollama ollama/ollama
```

Verify Ollama is running:

```bash
curl http://localhost:11434/api/tags
```

:::tip
The CrestApps.OrchardCore repository includes an Aspire AppHost that can orchestrate Ollama alongside the CMS application for local development. Run `dotnet run` from `src/Startup/CrestApps.Core.Aspire.AppHost/` to start everything together.
:::

## Model Management

Pull models before using them:

```bash
# Pull a chat model
docker exec -it ollama ollama pull llama3.2

# Pull an embedding model
docker exec -it ollama ollama pull nomic-embed-text

# List downloaded models
docker exec -it ollama ollama list

# Remove a model
docker exec -it ollama ollama rm llama3.2
```

Use the pulled model name as the `deploymentName` when configuring profiles. For example, after pulling `llama3.2`, reference it as:

```csharp
// The deployment name matches the Ollama model name
var profile = new AIProfile
{
    DeploymentName = "llama3.2",
    ConnectionName = "ollama-local",
};
```

## Configuration

Full `appsettings.json` configuration:

```json
{
  "CrestApps": {
    "AI": {
      "Providers": {
        "Ollama": {
          "Endpoint": "http://localhost:11434"
        }
      }
    }
  }
}
```

Or register programmatically:

```csharp
builder.Services.AddAIConnectionSource("Ollama", options =>
{
    options.Connections.Add(new AIProviderConnectionEntry
    {
        Name = "ollama-local",
        ProviderName = "Ollama",
        // Endpoint defaults to http://localhost:11434 if not set
    });
});
```

:::info
Ollama does not require an API key. The connection only needs an endpoint URL.
:::

## Limitations

Compared to cloud providers, Ollama has several differences to be aware of:

| Feature | Ollama | Cloud Providers |
|---------|--------|----------------|
| **Function calling** | Supported by some models (e.g., `llama3.2`, `mistral`), not all | Universally supported |
| **Image generation** | ❌ Not supported | ✅ OpenAI, Azure OpenAI |
| **Speech services** | ❌ Not supported | ✅ OpenAI, Azure OpenAI |
| **Vision (image input)** | Supported by multimodal models (e.g., `llava`) | Broadly supported |
| **Max context window** | Model-dependent (typically 4K–128K) | Up to 1M tokens |
| **Concurrent requests** | Limited by local hardware | Auto-scaling |
| **Response speed** | Depends on hardware (GPU recommended) | Optimized infrastructure |

:::warning
Not all Ollama models support function calling. If your application relies on [Custom AI Tools](../tools.md), verify that your chosen model supports tool use before deploying. Models like `llama3.2` and `mistral` have good function calling support.
:::

## Orchard Core Integration

The [Ollama provider module](../../ai/providers/ollama.md) adds admin UI for managing Ollama connections and model deployments.
