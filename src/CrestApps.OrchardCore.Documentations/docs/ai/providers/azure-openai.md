---
sidebar_label: Azure OpenAI
sidebar_position: 2
title: Azure OpenAI Integration
description: Azure OpenAI integration for AI chat profiles, deployments, and connections in Orchard Core.
---

| | |
| --- | --- |
| **Feature Name** | Azure OpenAI Chat |
| **Feature ID** | `CrestApps.OrchardCore.OpenAI.Azure` |

Provides AI services using Azure OpenAI models.

## Overview

The Azure OpenAI Chat feature integrates seamlessly with Azure OpenAI. Enable this feature to use Azure OpenAI models for AI chat profiles, deployments, and connections.

### Configuration

Add the following section to your `appsettings.json` to configure Azure OpenAI:

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "Azure": {
          "DefaultConnectionName": "<!-- Default connection name -->",
          "Connections": {
            "<!-- Unique connection name, ideally your Azure AccountName -->": {
              "Endpoint": "https://<!-- Your Azure Resource Name -->.openai.azure.com/",
              "AuthenticationType": "ApiKey",
              "ApiKey": "<!-- API Key for your Azure AI instance -->",
              "Deployments": [
                { "Name": "<!-- chat model deployment -->", "Type": "Chat", "IsDefault": true },
                { "Name": "<!-- utility model deployment -->", "Type": "Utility", "IsDefault": true },
                { "Name": "<!-- embedding model deployment -->", "Type": "Embedding", "IsDefault": true },
                { "Name": "<!-- image model deployment -->", "Type": "Image", "IsDefault": true }
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
The following format using `ChatDeploymentName`, `UtilityDeploymentName`, `EmbeddingDeploymentName`, and `ImagesDeploymentName` at both provider and connection levels is still supported but deprecated. Existing configurations will be auto-migrated at runtime.

```json
{
  "Connections": {
    "my-azure-account": {
      "Endpoint": "https://my-account.openai.azure.com/",
      "AuthenticationType": "ApiKey",
      "ApiKey": "...",
      "ChatDeploymentName": "gpt-4o",
      "UtilityDeploymentName": "gpt-4o-mini",
      "EmbeddingDeploymentName": "text-embedding-ada-002",
      "ImagesDeploymentName": "dall-e-3"
    }
  }
}
```
:::

Valid values for `AuthenticationType` are: `Default`, `ManagedIdentity`, or `ApiKey`. If using `ApiKey`, the `ApiKey` field is required.

When using `ManagedIdentity`, you can optionally provide an `IdentityId` to use a **user-assigned managed identity**. If `IdentityId` is omitted or empty, the **system-assigned managed identity** is used.

```json
{
  "Connections": {
    "my-azure-account": {
      "Endpoint": "https://my-account.openai.azure.com/",
      "AuthenticationType": "ManagedIdentity",
      "IdentityId": "<!-- Optional: client ID of a user-assigned managed identity -->"
    }
  }
}
```

### How to Retrieve Azure OpenAI Credentials

#### Get the API Key and Endpoint

1. Open the Azure Portal and navigate to your Azure OpenAI instance.
2. Go to **Resource Management** > **Keys and Endpoint**.
3. Copy the **Endpoint**.
4. Copy one of the two available **API keys**.

## Azure OpenAI Chat Feature

This feature allows the creation of AI profiles using Azure OpenAI chat capabilities.

### Recipe Configuration

Define an AI profile with the following step in your recipe:

```json
{
  "steps": [
    {
      "name": "AIProfile",
      "profiles": [
        {
          "Source": "Azure",
          "Name": "ExampleProfile",
          "DisplayText": "Example Profile",
          "WelcomeMessage": "What do you want to know?",
          "FunctionNames": [],
          "Type": "Chat",
          "TitleType": "InitialPrompt",
          "PromptTemplate": null,
          "ConnectionName": "<!-- Connection name (optional) -->",
          "ChatDeploymentId": "<!-- Chat Deployment ID (optional) -->",
          "UtilityDeploymentId": "<!-- Utility Deployment ID (optional) -->",
          "Properties": {
            "AIProfileMetadata": {
              "SystemMessage": "You are an AI assistant that helps people find information.",
              "Temperature": null,
              "TopP": null,
              "FrequencyPenalty": null,
              "PresencePenalty": null,
              "MaxTokens": null,
              "PastMessagesCount": null
            }
          }
        }
      ]
    }
  ]
}
```

:::tip
AI Profiles now use `ChatDeploymentId` and `UtilityDeploymentId` instead of the previous single `DeploymentId` field. This allows profiles to specify separate deployments for chat completions and auxiliary utility tasks.
:::

## RAG / Data Sources

Data sources and RAG are now implemented in the provider-agnostic `CrestApps.OrchardCore.AI.DataSources` module.

See: [AI Data Sources](../data-sources/)

## Azure Speech Deployments (Contained Connection)

The **Azure Speech** deployment provider allows you to register [Azure AI Speech Service](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/) deployments as standalone, self-contained speech-to-text endpoints. Unlike standard Azure OpenAI deployments that reference a shared connection, Azure Speech deployments embed their own connection parameters (endpoint, authentication, credentials) directly within the deployment configuration.

Under the hood, this provider uses the **Azure Speech REST API** (`/speech/recognition/conversation/cognitiveservices/v1`) for short audio recognition, which natively supports browser audio formats (WebM/Opus) without requiring any native SDK dependencies.

This is useful when:

- You want to use **Azure AI Speech Service** for speech-to-text rather than an Azure OpenAI Whisper deployment.
- Your speech-to-text service is on a **separate Azure resource** from your chat models.
- You want a **self-contained deployment** without creating a full provider connection.

### How to Create an Azure Speech Deployment

1. Navigate to **AI Services** → **Deployments** in the admin dashboard.
2. Click **Add Deployment** and select **Azure Speech** as the provider.
3. Enter a **deployment name** (a friendly identifier for this deployment).
4. Set the **deployment type** to **SpeechToText**.
5. Provide the **Endpoint URL** of your Azure Speech Service resource (e.g., `https://{region}.api.cognitive.microsoft.com/` or your custom domain endpoint).
6. Select the **Authentication type**: `Default`, `Managed Identity`, or `API Key`.
   - For **API Key**: provide the Speech Service subscription key.
   - For **Managed Identity**: optionally provide a **user-assigned identity client ID**. If omitted, the system-assigned identity is used.
7. Save the deployment.

### Setting as Default Speech-to-Text Deployment

After creating the deployment, go to **Configuration** → **Settings** → **AI** and select this deployment under **Default Speech-to-Text Deployment**. This enables the microphone button in AI Chat profiles and Chat Interactions that have speech-to-text enabled.
