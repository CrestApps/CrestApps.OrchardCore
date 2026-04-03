---
sidebar_label: Azure AI Inference
sidebar_position: 5
title: Azure AI Inference Provider
description: Connect to Azure AI Inference and GitHub Models for serverless model access.
---

# Azure AI Inference Provider

> Connect to [Azure AI Inference](https://learn.microsoft.com/azure/ai-studio/) and [GitHub Models](https://github.com/marketplace/models) for serverless access to a wide catalog of models.

## Quick Start

```csharp
builder.Services
    .AddCrestAppsAI()
    .AddOrchestrationServices()
    .AddAzureAIInferenceProvider();
```

## Services Registered

| Service | Implementation | Lifetime |
|---------|---------------|----------|
| `IAIClientProvider` | `AzureAIInferenceClientProvider` | Scoped |
| `IAICompletionClient` | `AzureAIInferenceCompletionClient` | Scoped |
| Connection source | — | Scoped |

## Configuration

### For GitHub Models

```json
{
  "CrestApps": {
    "AI": {
      "Providers": {
        "AzureAIInference": {
          "Endpoint": "https://models.inference.ai.azure.com",
          "ApiKey": "your-github-token"
        }
      }
    }
  }
}
```

### For Azure AI Studio

```json
{
  "CrestApps": {
    "AI": {
      "Providers": {
        "AzureAIInference": {
          "Endpoint": "https://your-project.inference.ai.azure.com",
          "ApiKey": "your-api-key"
        }
      }
    }
  }
}
```

### Constants

| Constant | Value |
|----------|-------|
| `AzureAIInferenceConstants.ProviderName` | `"AzureAIInference"` |
| `AzureAIInferenceConstants.ClientName` | `"AzureAIInference"` |

## Use Cases

- **GitHub Models** — Access models like GPT-4o, Llama, Mistral via GitHub token
- **Azure AI Studio** — Serverless model deployments without managing infrastructure
- **Model Catalog** — Access a wide variety of models through a single provider

## Capabilities

| Capability | Supported |
|-----------|-----------|
| Chat completions | ✅ |
| Streaming | ✅ |
| Embeddings | ✅ |
| Image generation | ❌ |
| Speech-to-text | ❌ |
| Text-to-speech | ❌ |

## GitHub Models

The Azure AI Inference provider is the gateway to the [GitHub Models marketplace](https://github.com/marketplace/models), which lets you experiment with and deploy models from multiple vendors through a single API:

1. **Get a GitHub token** — Generate a personal access token (PAT) at [github.com/settings/tokens](https://github.com/settings/tokens) with the `models:read` scope.
2. **Point to the GitHub Models endpoint** — Use `https://models.inference.ai.azure.com` as the endpoint.
3. **Select a model** — Use the model name from GitHub Marketplace as the deployment name.

```json
{
  "CrestApps": {
    "AI": {
      "Providers": {
        "AzureAIInference": {
          "Endpoint": "https://models.inference.ai.azure.com",
          "ApiKey": "ghp_your-github-token"
        }
      }
    }
  }
}
```

:::tip
GitHub Models is free for experimentation with rate limits. For production workloads, deploy the same models through Azure AI Studio for higher throughput and SLA guarantees.
:::

## Configuration

### Programmatic Registration

```csharp
builder.Services.AddAIConnectionSource("AzureAIInference", options =>
{
    options.Connections.Add(new AIProviderConnectionEntry
    {
        Name = "github-models",
        ProviderName = "AzureAIInference",
        // Endpoint and API key loaded from configuration
    });
});
```

### Environment-Specific Configuration

Use different endpoints for development vs. production:

```json title="appsettings.Development.json"
{
  "CrestApps": {
    "AI": {
      "Providers": {
        "AzureAIInference": {
          "Endpoint": "https://models.inference.ai.azure.com",
          "ApiKey": "ghp_your-github-token"
        }
      }
    }
  }
}
```

```json title="appsettings.Production.json"
{
  "CrestApps": {
    "AI": {
      "Providers": {
        "AzureAIInference": {
          "Endpoint": "https://your-project.inference.ai.azure.com",
          "ApiKey": "your-azure-key"
        }
      }
    }
  }
}
```

## Available Models

The Azure AI Inference / GitHub Models endpoint provides access to a broad catalog of models from multiple vendors:

| Vendor | Example Models | Strengths |
|--------|---------------|-----------|
| **OpenAI** | GPT-4o, GPT-4o-mini | General purpose, function calling, vision |
| **Meta** | Llama 3.2, Llama 3.1 | Open-weight, strong reasoning |
| **Mistral** | Mistral Large, Mistral Small | Multilingual, efficient |
| **Cohere** | Command R+, Command R | RAG-optimized, multilingual |
| **AI21** | Jamba 1.5 | Long context, document processing |

:::info
Model availability varies between GitHub Models (free tier) and Azure AI Studio (production tier). Check the respective marketplaces for the current catalog.
:::

## Orchard Core Integration

The [Azure AI Inference provider module](../../ai/providers/azure-ai-inference.md) adds admin UI for managing Azure AI Inference connections.
