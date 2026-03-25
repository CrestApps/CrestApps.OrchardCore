---
sidebar_label: AI Services and Configuration
sidebar_position: 1
slug: /ai/overview
title: AI Services
description: Foundational infrastructure for the CrestApps AI Suite in Orchard Core, covering AI integration, management, profiles, orchestration, providers, deployments, MCP, Agent-to-Agent, and site-level settings.
---

| | |
| --- | --- |
| **Feature Name** | AI Services |
| **Feature ID** | `CrestApps.OrchardCore.AI` |

Provides the core AI services and management infrastructure. (`EnabledByDependencyOnly = true`)

## AI Services Feature

The **AI Services** feature provides the foundational infrastructure for the broader CrestApps AI Suite in Orchard Core. It covers configurable profiles, orchestration, provider integrations, connection and deployment management, prompt and tool infrastructure, and site-level AI administration.

Once enabled, a new **Artificial Intelligence** menu item appears in the admin dashboard, allowing administrators to create and manage **AI Profiles**, provider connections, deployments, templates, and related AI settings.

An **AI Profile** defines how the AI system interacts with users — including its welcome message, system message, response behavior, deployments, and selected tools or knowledge capabilities.

:::note
This feature does **not** include any AI completion client implementations such as **OpenAI**. It only provides the **user interface** and **core services** for managing AI profiles. You must install and configure a compatible provider module (e.g., `OpenAI`, `Azure`, `AzureAIInference`, or `Ollama`) separately.
:::
---

## Data Extraction (AI Profiles)

AI Profiles can be configured to **extract structured data** from the chat session as the conversation progresses (for example: name, email, product of interest, budget, meeting time, etc.).

To configure this, edit an AI Profile in the admin UI and open the **Data Extractions** tab (added by `AIProfileDataExtractionDisplayDriver`).

### Configuration

1. Go to **Artificial Intelligence → AI Profiles** and edit a profile.
2. Open the **Data Extractions** tab.
3. Check **Enable Data Extraction**.
4. Configure:
   - **Extraction Check Interval** — run extraction every N user messages (default: 1).
   - **Session Inactivity Timeout (minutes)** — sessions inactive longer than this are automatically closed and a final extraction is run (default: 30).
   - **Extraction Entries** — the fields to extract:
     - **Name** — a unique key (letters, digits, underscore), e.g. `customer_name`.
     - **Description** — what to extract (this is what the model uses as guidance).
     - **Allow Multiple Values** — accumulate multiple values over time (e.g., multiple mentioned products).
     - **Updatable** — allow replacing the previous value if the user corrects it later.

Extracted values are stored on the chat session (`ExtractedData`) and are updated after each qualifying message exchange. The extraction model uses the profile's **Utility deployment** when configured, and falls back to the chat deployment otherwise.

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
          "AbsoluteMaximumIterationsPerRequest": 100,
          "EnableOpenTelemetry": false,
          "EnableDistributedCaching": true
        },
      "Providers": {
        "<!-- Provider name goes here (valid values: 'OpenAI', 'Azure', 'AzureAIInference', or 'Ollama') -->": {
          "DefaultConnectionName": "<!-- The default connection name to use from the Connections list -->",
          "Connections": {
            "<!-- Connection name goes here -->": {
              // Provider-specific settings go here (e.g., ApiKey, Endpoint)
              "Deployments": [
                { "Name": "<!-- model name -->", "Type": "Chat", "IsDefault": true },
                { "Name": "<!-- lightweight model name -->", "Type": "Utility", "IsDefault": true },
                { "Name": "<!-- embedding model name -->", "Type": "Embedding", "IsDefault": true },
                { "Name": "<!-- image model name -->", "Type": "Image", "IsDefault": true }
              ]
            }
          }
        }
      }
    }
  }
}
```

:::warning Legacy Format (Deprecated)
The following configuration format using `ChatDeploymentName`, `UtilityDeploymentName`, `EmbeddingDeploymentName`, and `ImagesDeploymentName` at both the provider and connection levels is **deprecated**. It is still supported and will be auto-migrated at runtime, but new configurations should use the `Deployments` array format shown above.

```json
{
  "Providers": {
    "OpenAI": {
      "DefaultChatDeploymentName": "gpt-4o",
      "DefaultUtilityDeploymentName": "gpt-4o-mini",
      "DefaultEmbeddingDeploymentName": "text-embedding-3-large",
      "DefaultImagesDeploymentName": "dall-e-3",
      "Connections": {
        "openai-cloud": {
          "ChatDeploymentName": "gpt-4o",
          "UtilityDeploymentName": "gpt-4o-mini",
          "EmbeddingDeploymentName": "text-embedding-3-large",
          "ImagesDeploymentName": "dall-e-3"
        }
      }
    }
  }
}
```
:::

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
| `AbsoluteMaximumIterationsPerRequest` | Hard upper bound for `MaximumIterationsPerRequest`. This value is controlled by configuration providers such as `appsettings.json` and is not editable from the site settings UI. The effective maximum iterations value is always clamped to this ceiling. | `100` |
| `EnableOpenTelemetry` | Enables OpenTelemetry tracing for AI requests. | `false` |
| `EnableDistributedCaching` | Enables distributed caching for AI responses. | `true` |

### Site-level General AI overrides

In addition to `appsettings.json`, administrators can override selected AI defaults per tenant from **Settings → Artificial Intelligence → General**.

The **General** card currently supports:

- enabling or disabling **Preemptive Memory Retrieval**
- overriding `MaximumIterationsPerRequest`
- overriding `EnableDistributedCaching`
- overriding `EnableOpenTelemetry`

The appsettings-based `DefaultParameters` still provide the base values. Site settings only replace a value when the matching override toggle is enabled. `AbsoluteMaximumIterationsPerRequest` always stays configuration-owned and the effective `MaximumIterationsPerRequest` is always `Math.Min(MaximumIterationsPerRequest, AbsoluteMaximumIterationsPerRequest)`.

#### Typed AI Deployments

Each deployment is a first-class entity with a **Type** and an optional **IsDefault** flag. Deployments can be defined in the `Deployments` array on each connection in `appsettings.json`, or created through the admin UI. Deployments defined in configuration are automatically available at runtime across all tenants without requiring per-tenant setup.

| Property | Description | Required |
|----------|-------------|----------|
| `Name` | The model/deployment name (e.g., `gpt-4o`, `text-embedding-3-large`) | Yes |
| `Type` | The deployment type. Valid values: `Chat`, `Utility`, `Embedding`, `Image`, `SpeechToText` | Yes |
| `IsDefault` | Whether this is the default deployment for its type within the connection | No |

**Deployment Types:**

| Type | Purpose | Example Models |
|------|---------|----------------|
| `Chat` | Primary chat completions | `gpt-4o`, `gemini-pro`, `deepseek-chat` |
| `Utility` | Lightweight auxiliary tasks (query rewriting, planning, chart generation). Falls back to `Chat` when not set. | `gpt-4o-mini`, `gemini-flash` |
| `Embedding` | Generating embeddings for RAG / vector search | `text-embedding-3-large`, `text-embedding-3-small` |
| `Image` | Image generation | `dall-e-3`, `dall-e-2` |
| `SpeechToText` | Speech-to-text transcription | `whisper-1` |

#### Deployment Resolution

When an AI Profile or service requests a deployment, the system resolves it using the following fallback chain:

1. **Explicit deployment** — The deployment explicitly assigned to the profile/resource
2. **Connection default for type** — The deployment marked `IsDefault: true` for that type on the connection
3. **Global default** — The default deployment configured in **Default AI Deployment Settings** (see below)
4. **null/error** — No deployment found

#### Default AI Deployment Settings

A new settings page is available under **Settings → Artificial Intelligence → Default AI Deployment Settings**. This page allows administrators to configure global default deployments:

| Setting | Description |
|---------|-------------|
| `DefaultUtilityDeploymentId` | The global default deployment for utility tasks |
| `DefaultEmbeddingDeploymentId` | The global default deployment for embedding generation |
| `DefaultImageDeploymentId` | The global default deployment for image generation |
| `DefaultSpeechToTextDeploymentId` | The global default deployment for speech-to-text transcription |

These global defaults act as the final fallback when no explicit or connection-level default is configured.

:::tip
Chat deployments do not need a global default because they are always explicitly set on AI Profiles or Chat Interactions.
:::

#### Contained-Connection Deployments

Most AI deployments reference a shared **Provider Connection** (endpoint + credentials) that can host many models. However, some AI services — like [Azure AI Speech Service](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/) — have their own dedicated endpoint per service, not per model. For these, the standard connection-reference pattern doesn't apply.

**Contained-connection deployments** solve this by embedding connection parameters (endpoint, authentication type, credentials) directly within the deployment configuration, instead of referencing an external connection. This is indicated by the `SupportsContainedConnection` flag on the deployment provider.

**When to use contained connections:**
- The AI service has a **dedicated endpoint** (e.g., Azure Speech Service at `https://{region}.stt.speech.microsoft.com/`)
- The service does **not share an endpoint** with other model deployments
- You want a **self-contained deployment** without creating a separate provider connection

Currently, the **Azure Speech** provider is the built-in contained-connection provider. See the [Azure OpenAI documentation](./providers/azure-openai#azure-speech-deployments-contained-connection) for setup instructions.

#### Configuring Contained-Connection Deployments via appsettings.json

In addition to creating contained-connection deployments through the admin UI, you can define them in `appsettings.json` so they are automatically available across all tenants without per-tenant configuration.

Non-connection deployments are defined in the `CrestApps_AI:Deployments` section as an array of deployment objects:

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Deployments": [
        {
          "ClientName": "AzureSpeech",
          "Name": "my-speech-to-text",
          "Type": "SpeechToText",
          "IsDefault": true,
          "Endpoint": "https://eastus.api.cognitive.microsoft.com/",
          "AuthenticationType": "ApiKey",
          "ApiKey": "your-speech-service-api-key"
        }
      ]
    }
  }
}
```

| Property | Description | Required |
|----------|-------------|----------|
| `ProviderName` | The contained-connection deployment provider (for example `AzureSpeech`) | Yes |
| `Name` | A friendly name for the deployment | Yes |
| `Type` | The deployment type (e.g., `SpeechToText`, `TextToSpeech`) | Yes |
| `IsDefault` | Whether this is the default deployment for its type | No |
| Provider-specific fields | Connection settings such as `Endpoint`, `AuthenticationType`, `ApiKey`, and `IdentityId` | Provider-specific |

Deployments defined in configuration are **read-only** and **ephemeral** — they appear alongside database-managed deployments in dropdowns and API queries, but are not persisted to the database. Removing them from configuration removes them from the system.

For providers that need more complex metadata objects, you can also use a nested `Properties` object. Flat top-level fields are recommended for contained-connection providers like `AzureSpeech`.

:::tip
Use this approach when you want to share deployments across all tenants without configuring each tenant individually. API keys in configuration should be secured using environment variables or user secrets.
:::

:::warning Deprecated Settings
The following provider-level and connection-level settings are **deprecated** and will be auto-migrated:

- `DefaultChatDeploymentName`, `DefaultUtilityDeploymentName`, `DefaultEmbeddingDeploymentName`, `DefaultImagesDeploymentName` (provider-level)
- `ChatDeploymentName`, `UtilityDeploymentName`, `EmbeddingDeploymentName`, `ImagesDeploymentName` (connection-level)

Use the `Deployments` array on connections and the **Default AI Deployment Settings** page instead.
:::

---

### Provider Configuration

The following providers are supported **out of the box**:

* **OpenAI** — [View configuration guide](./providers/openai)
* **Azure** — [View configuration guide](./providers/azure-openai)
* **AzureAIInference** — [View configuration guide](./providers/azure-ai-inference)
* **Ollama** — [View configuration guide](./providers/ollama)

> **Tip:** Most modern AI providers offer APIs that follow the **OpenAI API standard**.
> For these providers, use the **`OpenAI`** provider type when configuring their connections and endpoints.

Each provider can define multiple connections, and the `DefaultConnectionName` determines which one is used when multiple connections are available.

---

### Microsoft.AI.Extensions

The AI module is built on top of [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI), making it easy to integrate AI services into your application. We provide the `IAIClientFactory` service, which allows you to easily create standard services such as `IChatClient`, `IEmbeddingGenerator` and `IImageGenerator` for any of your configured providers and connections.

Simply inject `IAIClientFactory` into your service and use the `CreateChatClientAsync` or `CreateEmbeddingGeneratorAsync` methods to obtain the required client.

### AI Deployments

Typed AI deployments are now part of the base **AI Services** feature (`CrestApps.OrchardCore.AI`).

Each deployment is a first-class entity with a `Type` property (`Chat`, `Utility`, `Embedding`, `Image`, `SpeechToText`) and an `IsDefault` flag. Deployments are associated with a provider connection and can be managed through the admin UI under **Artificial Intelligence → Deployments** without enabling a separate deployments feature.

UI dropdowns for deployment selection display deployments **grouped by connection**, making it easy to find the correct deployment without navigating a cascading connection → deployment hierarchy.

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

The **AI Connection Management** feature enhances **AI Services** by providing a user interface to manage provider connections. Connections are **pure connection configurations** — they define how to reach a provider (endpoint, API key, authentication). Deployments (models) are managed separately as typed entities associated with each connection.

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
  - **Model name**: e.g. `deepseek-chat`.
  - **Endpoint**: `https://api.deepseek.com/v1/`.
  - **API Key**: Generate one in [DeepSeek Platform](https://platform.deepseek.com).
- Google Gemini
  - **Model name**: e.g. `gemini-2.0-flash`.
  - **Endpoint**: `https://generativelanguage.googleapis.com/v1beta/openai/`.
  - **API Key**: Generate one in [Google AI Studio](https://aistudio.google.com).
- OpenAI
  - **Model name**: e.g. `gpt-4o-mini`.
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
          "DisplayText": "DeepSeek",
          "Deployments": [
            { "Name": "deepseek-chat", "Type": "Chat", "IsDefault": true }
          ],
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

This recipe ensures that a **DeepSeek** connection is added or updated within the AI provider settings, with a typed `Chat` deployment. Replace `<!-- DeepSeek API Key -->` with a valid API key to authenticate the connection.  

:::warning Legacy Recipe Format
The old recipe format using `ChatDeploymentName`, `UtilityDeploymentName`, etc. on the connection object is still supported but deprecated. Migrate to the `Deployments` array format shown above.
:::

If a connection with the same `Name` and `Source` already exists, the recipe updates its properties. Otherwise, it creates a new connection.

Data source (retrieval-augmented generation (RAG) / Knowledge Base) documentation is in the `CrestApps.OrchardCore.AI.DataSources` module: [README](data-sources/).

For managing AI tools, see [AI Tools](tools).

For consuming AI services programmatically, see [Consuming AI Services](consuming-ai-services).

---

### AI Chat with Workflows

See [AI Workflows](workflows) for details on using AI completion tasks in Orchard Core Workflows.

---

### Deployments with AI Chat

The **AI Services** feature integrates with the **Deployments** module, allowing profiles to be deployed to various environments through Orchard Core's Deployment UI.

---

## Compatibility  

This module is fully compatible with OrchardCore v2.1 and later. However, if you are using OrchardCore versions between `v2.1` and `3.0.0-preview-18562`, you must install the [CrestApps.OrchardCore.Resources module](../modules/resources) module into your web project. Then, enable the `CrestApps.OrchardCore.Resources` feature to ensure all required resource dependencies are available.
