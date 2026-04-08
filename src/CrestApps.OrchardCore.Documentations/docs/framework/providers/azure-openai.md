---
sidebar_label: Azure OpenAI
sidebar_position: 3
title: Azure OpenAI Provider
description: Connect to Azure OpenAI Service for enterprise-grade AI completions with Azure-specific optimizations.
---

:::info Canonical framework docs
The shared framework guidance now lives in **[CrestApps.Core](https://core.crestapps.com/docs/framework/providers/azure-openai)**. This Orchard Core page is kept for Orchard-specific integration context and cross-links.
:::

# Azure OpenAI Provider

> Connect to Azure OpenAI Service for enterprise-grade completions with Azure-specific optimizations for `max_tokens` and stream handling.

## Quick Start

```csharp
builder.Services
    .AddCrestAppsAI()
    .AddOrchestrationServices()
    .AddAzureOpenAIProvider();
```

## Services Registered

| Service | Implementation | Lifetime |
|---------|---------------|----------|
| `IAIClientProvider` | `AzureOpenAIClientProvider` | Scoped |
| `IAICompletionClient` | `AzureOpenAICompletionClient` | Scoped |
| `IOpenAIChatOptionsConfiguration` | `AzurePatchOpenAIDataSourceHandler` | Scoped |
| Connection source | — | Scoped |

## Configuration

### Connection Setup

Azure OpenAI requires an endpoint URL and either an API key or Azure AD credentials:

```json
{
  "CrestApps": {
    "AI": {
      "Providers": {
        "Azure": {
          "Endpoint": "https://my-resource.openai.azure.com/",
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
| `AzureOpenAIConstants.ProviderName` | `"Azure"` |
| `AzureOpenAIConstants.ClientName` | `"Azure"` |

## Azure-Specific Behavior

The Azure provider includes `AzurePatchOpenAIDataSourceHandler` which automatically:

- Maps `max_tokens` to Azure-compatible format
- Handles Azure-specific stream options
- Patches `ChatCompletionOptions` for Azure API compatibility

## Capabilities

| Capability | Supported |
|-----------|-----------|
| Chat completions | ✅ |
| Streaming | ✅ |
| Embeddings | ✅ |
| Image generation | ✅ |
| Speech-to-text | ✅ (via Azure Speech) |
| Text-to-speech | ✅ (via Azure Speech) |

## Azure Setup

Before configuring the provider, create the required Azure resources:

1. **Create an Azure OpenAI resource** in the [Azure Portal](https://portal.azure.com/#create/Microsoft.CognitiveServicesOpenAI).
2. **Deploy a model** — In your Azure OpenAI resource, go to **Model deployments** → **Create new deployment**. Choose a model (e.g., `gpt-4o`) and give the deployment a name.
3. **Copy the endpoint and key** — Found under **Keys and Endpoint** in the Azure Portal.

:::info
The deployment name in Azure OpenAI is what you pass as the `deploymentName` parameter when creating profiles. It does **not** need to match the model name (e.g., you can name a `gpt-4o` deployment `"my-chat-model"`).
:::

## Configuration

Full `appsettings.json` configuration with endpoint, API key, and deployment:

```json
{
  "CrestApps": {
    "AI": {
      "Providers": {
        "Azure": {
          "Endpoint": "https://my-resource.openai.azure.com/",
          "ApiKey": "your-api-key-here"
        }
      }
    }
  }
}
```

Or register programmatically:

```csharp
builder.Services.AddAIConnectionSource("Azure", options =>
{
    options.Connections.Add(new AIProviderConnectionEntry
    {
        Name = "azure-production",
        ProviderName = "Azure",
        // Endpoint and API key are loaded from configuration
    });
});
```

## Authentication

### API Key

The simplest authentication method. Suitable for development and testing:

```json
{
  "CrestApps": {
    "AI": {
      "Providers": {
        "Azure": {
          "Endpoint": "https://my-resource.openai.azure.com/",
          "ApiKey": "your-api-key-here"
        }
      }
    }
  }
}
```

:::warning
API keys grant full access to your Azure OpenAI resource. Rotate keys regularly and never commit them to source control.
:::

### DefaultAzureCredential

For production environments, use `DefaultAzureCredential` from the Azure Identity SDK. This supports managed identity, Azure CLI, Visual Studio, and other credential sources without storing secrets:

```csharp
builder.Services.AddAIConnectionSource("Azure", options =>
{
    options.Connections.Add(new AIProviderConnectionEntry
    {
        Name = "azure-production",
        ProviderName = "Azure",
        // When no API key is set, the provider uses DefaultAzureCredential
    });
});
```

When no API key is configured, the `AzureOpenAIClientProvider` automatically falls back to `DefaultAzureCredential`, which tries these credential sources in order:

1. Environment variables (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_CLIENT_SECRET`)
2. Workload identity (Kubernetes)
3. Managed identity
4. Azure CLI / Azure PowerShell
5. Visual Studio / VS Code credentials

## Managed Identity

To use managed identity in production (Azure App Service, Azure Container Apps, Azure VMs):

1. **Enable managed identity** on your hosting resource (System-assigned or User-assigned).
2. **Assign the role** `Cognitive Services OpenAI User` to the identity on your Azure OpenAI resource.
3. **Remove the API key** from configuration — the provider uses `DefaultAzureCredential` automatically.

```json
{
  "CrestApps": {
    "AI": {
      "Providers": {
        "Azure": {
          "Endpoint": "https://my-resource.openai.azure.com/"
        }
      }
    }
  }
}
```

:::tip
Managed identity eliminates the need to manage and rotate API keys. It is the recommended authentication method for all Azure-hosted production workloads.
:::

## Orchard Core Integration

The [Azure OpenAI provider module](../../ai/providers/azure-openai.md) adds admin UI for managing Azure OpenAI connections and deployments.
