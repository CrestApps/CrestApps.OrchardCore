---
sidebar_label: AI Services and Configuration
sidebar_position: 1
title: AI Services
description: Foundational infrastructure for interacting with AI models through configurable profiles and service integrations in Orchard Core.
---

| | |
| --- | --- |
| **Feature Name** | AI Services |
| **Feature ID** | `CrestApps.OrchardCore.AI` |

Provides AI services. (`EnabledByDependencyOnly = true`)

## AI Services Feature

The **AI Services** feature provides the foundational infrastructure for interacting with AI models through configurable profiles and service integrations.

Once enabled, a new **Artificial Intelligence** menu item appears in the admin dashboard, allowing administrators to create and manage **AI Profiles**.

An **AI Profile** defines how the AI system interacts with users — including its welcome message, system message, and response behavior.

> **Note:** This feature does **not** include any AI completion client implementations such as **OpenAI**. It only provides the **user interface** and **core services** for managing AI profiles. You must install and configure a compatible provider module (e.g., `OpenAI`, `Azure`, `AzureAIInference`, or `Ollama`) separately.

---

### Configuration

Before using the AI Services feature, ensure the required settings are properly configured. This can be done through the `appsettings.json` file or other configuration sources.

Below is an example configuration:

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "DefaultParameters": {
        "Temperature": 0,
        "MaxOutputTokens": 800,
        "TopP": 1,
        "FrequencyPenalty": 0,
        "PresencePenalty": 0,
        "PastMessagesCount": 10,
        "MaximumIterationsPerRequest": 10,
        "EnableOpenTelemetry": false,
        "EnableDistributedCaching": true
      },
      "Providers": {
        "<!-- Provider name goes here (valid values: 'OpenAI', 'Azure', 'AzureAIInference', or 'Ollama') -->": {
          "DefaultConnectionName": "<!-- The default connection name to use from the Connections list -->",
          "DefaultChatDeploymentName": "<!-- The default deployment name for chat completions -->",
          "DefaultUtilityDeploymentName": "<!-- Optional: a lightweight model for auxiliary tasks like query rewriting and planning -->",
          "DefaultEmbeddingDeploymentName": "<!-- The default embedding deployment name (optional, for embedding services) -->",
          "DefaultImagesDeploymentName": "<!-- The default deployment name for image generation (optional, e.g., 'dall-e-3') -->",
          "Connections": {
            "<!-- Connection name goes here -->": {
              "ChatDeploymentName": "<!-- The deployment name for this connection -->",
              "UtilityDeploymentName": "<!-- Optional: a lightweight model for auxiliary tasks -->",
              "EmbeddingDeploymentName": "<!-- The embedding deployment name (optional) -->",
              "ImagesDeploymentName": "<!-- The image generation deployment name (optional, e.g., 'dall-e-3') -->"
              // Provider-specific settings go here
            }
          }
        }
      }
    }
  }
}
```

#### Default Parameters

| Setting | Description | Default |
|---------|-------------|---------|
| `Temperature` | Controls randomness. Lower values produce more deterministic results. | `0` |
| `MaxOutputTokens` | Maximum number of tokens in the response. | `800` |
| `TopP` | Controls diversity via nucleus sampling. | `1` |
| `FrequencyPenalty` | Reduces repetition of token sequences. | `0` |
| `PresencePenalty` | Encourages the model to explore new topics. | `0` |
| `PastMessagesCount` | Number of previous messages included as conversation context. | `10` |
| `MaximumIterationsPerRequest` | Maximum number of tool-call round-trips the model can make per request. Set to a higher value (e.g., `10`) to enable agentic behavior where the model can call tools, evaluate results, and call additional tools as needed. A value of `1` limits the model to a single tool call with no follow-up. | `10` |
| `EnableOpenTelemetry` | Enables OpenTelemetry tracing for AI requests. | `false` |
| `EnableDistributedCaching` | Enables distributed caching for AI responses. | `true` |

#### Deployment Name Settings

**Provider-level settings** (apply to all connections under a provider):

| Setting | Description | Required |
|---------|-------------|----------|
| `DefaultChatDeploymentName` | The default model for chat completions | Yes |
| `DefaultUtilityDeploymentName` | The default lightweight model for auxiliary tasks (e.g., query rewriting, planning). Falls back to `DefaultChatDeploymentName` when not set. | No |
| `DefaultEmbeddingDeploymentName` | The model for generating embeddings (for RAG/vector search) | No |
| `DefaultImagesDeploymentName` | The model for image generation (e.g., `dall-e-3`). Required for image generation features. | No |

**Connection-level settings** (specific to an individual connection):

| Setting | Description | Required |
|---------|-------------|----------|
| `ChatDeploymentName` | The model for chat completions for this connection | Yes |
| `UtilityDeploymentName` | A lightweight model for auxiliary tasks such as query rewriting, planning, and chart generation. Falls back to `ChatDeploymentName` when not set. | No |
| `EmbeddingDeploymentName` | The model for generating embeddings (for RAG/vector search) | No |
| `ImagesDeploymentName` | The model for image generation (e.g., `dall-e-3`). Required for image generation features. | No |

---

### Provider Configuration

The following providers are supported **out of the box**:

* **OpenAI** — [View configuration guide](../providers/openai)
* **Azure** — [View configuration guide](../providers/azure-openai)
* **AzureAIInference** — [View configuration guide](../providers/azure-ai-inference)
* **Ollama** — [View configuration guide](../providers/ollama)

> **Tip:** Most modern AI providers offer APIs that follow the **OpenAI API standard**.
> For these providers, use the **`OpenAI`** provider type when configuring their connections and endpoints.

Each provider can define multiple connections, and the `DefaultConnectionName` determines which one is used when multiple connections are available.

---

### Microsoft.AI.Extensions

The AI module is built on top of [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI), making it easy to integrate AI services into your application. We provide the `IAIClientFactory` service, which allows you to easily create standard services such as `IChatClient` and `IEmbeddingGenerator` for any of your configured providers and connections.

Simply inject `IAIClientFactory` into your service and use the `CreateChatClientAsync` or `CreateEmbeddingGeneratorAsync` methods to obtain the required client.

### AI Deployments Feature

| | |
| --- | --- |
| **Feature Name** | AI Deployments |
| **Feature ID** | `CrestApps.OrchardCore.AI.Deployments` |

Manages AI model deployments.

The **AI Deployments** feature extends the **AI Services** feature by enabling AI model deployment capabilities.

### AI Chat Services Feature

| | |
| --- | --- |
| **Feature Name** | AI Chat Services |
| **Feature ID** | `CrestApps.OrchardCore.AI.Chat.Core` |

Provides all the necessary services to enable chatting with AI models using profiles. (`EnabledByDependencyOnly = true`)

The **AI Chat Services** feature builds upon the **AI Services** feature by adding AI chat capabilities. This feature is enabled on demand by other modules that provide AI completion clients.

### AI Chat WebAPI

| | |
| --- | --- |
| **Feature Name** | AI Chat WebAPI |
| **Feature ID** | `CrestApps.OrchardCore.AI.Chat.Api` |

Provides a RESTful API for interacting with the AI chat.

The **AI Chat WebAPI** feature extends the **AI Chat Services** feature by enabling a REST WebAPI endpoints to allow you to interact with the models.

### AI Connection Management

| | |
| --- | --- |
| **Feature Name** | AI Connection Management |
| **Feature ID** | `CrestApps.OrchardCore.AI.ConnectionManagement` |

Provides user interface to manage AI connections.

The **AI Connection Management** feature enhances **AI Services** by providing a user interface to manage provider connections.

---

#### Setting Up a Connection

1. **Navigate to AI Settings**  
   - Go to **"Artificial Intelligence"** in the admin menu.  
   - Click **"Provider Connections"** to configure a new connection.  

2. **Add a New Connection**  
   - Click **"Add Connection"**, select a provider, and enter the required details.  
   - Example configurations are in the next section.

#### Example Configurations for Common Providers

:::important

You need to use a paid plan for all of these even when using models that are free from the web. Otherwise, you'll get various errors along the lines of `insufficient_quota`.

:::

- DeepSeek
  - **Deployment name** (the model to use): e.g. `deepseek-chat`.
  - **Endpoint**: `https://api.deepseek.com/v1/`.
  - **API Key**: Generate one in [DeepSeek Platform](https://platform.deepseek.com).
- Google Gemini
  - **Deployment name**: e.g. `gemini-2.0-flash`.
  - **Endpoint**: `https://generativelanguage.googleapis.com/v1beta/openai/`.
  - **API Key**: Generate one in [Google AI Studio](https://aistudio.google.com).
- OpenAI
  - **Deployment name**: e.g. `gpt-4o-mini`.
  - **Endpoint**: `https://api.openai.com/v1/`.
  - **API Key**: Generate one in [OpenAI Platform](https://platform.openai.com/account/api-keys).

#### Creating AI Profiles  

After setting up a connection, you can create **AI Profiles** to interact with the configured model.  

#### Recipes

You can add or update a connection using **recipes**. Below is a recipe for adding or updating a connection to the **DeepSeek** service:  

```json
{
  "steps": [
    {
      "name": "AIProviderConnections",
      "connections": [
        {
          "Source": "OpenAI",
          "Name": "deepseek",
          "IsDefault": false,
          "ChatDeploymentName": "deepseek-chat",
          "UtilityDeploymentName": "deepseek-chat",
          "DisplayText": "DeepSeek",
          "Properties": {
            "OpenAIConnectionMetadata": {
              "Endpoint": "https://api.deepseek.com/v1",
              "ApiKey": "<!-- DeepSeek API Key -->"
            }
          }
        }
      ]
    }
  ]
}
```  

This recipe ensures that a **DeepSeek** connection is added or updated within the AI provider settings. Replace `<!-- DeepSeek API Key -->` with a valid API key to authenticate the connection.  

If a connection with the same `Name` and `Source` already exists, the recipe updates its properties. Otherwise, it creates a new connection.

Data source (RAG/Knowledge Base) documentation is in the `CrestApps.OrchardCore.AI.DataSources` module: [README](data-sources/).

For managing AI tools, see [AI Tools](ai-tools).

For consuming AI services programmatically, see [Consuming AI Services](consuming-ai-services).

---

### AI Chat with Workflows

See [AI Workflows](ai-workflows) for details on using AI completion tasks in Orchard Core Workflows.

---

### Deployments with AI Chat

The **AI Services** feature integrates with the **Deployments** module, allowing profiles to be deployed to various environments through Orchard Core's Deployment UI.

---

## Compatibility  

This module is fully compatible with OrchardCore v2.1 and later. However, if you are using OrchardCore versions between `v2.1` and `3.0.0-preview-18562`, you must install the [CrestApps.OrchardCore.Resources module](../modules/resources) module into your web project. Then, enable the `CrestApps.OrchardCore.Resources` feature to ensure all required resource dependencies are available.
