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
          "Name": "ExampleProfile",
          "DisplayText": "Example Profile",
          "WelcomeMessage": "What do you want to know?",
          "Type": "Chat",
          "TitleType": "InitialPrompt",
          "PromptTemplate": null,
          "ConnectionName": "<!-- Optional connection fallback -->",
          "ChatDeploymentName": "<!-- Optional chat deployment technical name -->",
          "UtilityDeploymentName": "<!-- Optional utility deployment technical name -->",
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
AI Profiles now use `ChatDeploymentName` and `UtilityDeploymentName` instead of the previous single `DeploymentId` field. This allows profiles to specify separate deployments for chat completions and auxiliary utility tasks.
:::

## RAG / Data Sources

Data sources and RAG are now implemented in the provider-agnostic `CrestApps.OrchardCore.AI.DataSources` module.

See: [AI Data Sources](../data-sources/)

## Azure Speech Deployments (Contained Connection)

The **Azure Speech** deployment provider allows you to register [Azure AI Speech Service](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/) deployments as standalone, self-contained speech-to-text endpoints. Unlike standard Azure OpenAI deployments that reference a shared connection, Azure Speech deployments embed their own connection parameters (endpoint, authentication, credentials) directly within the deployment configuration.

Under the hood, this provider uses the [Azure Speech SDK for .NET](https://learn.microsoft.com/en-us/dotnet/api/microsoft.cognitiveservices.speech) (`Microsoft.CognitiveServices.Speech`) with continuous recognition for real-time streaming transcription. The SDK automatically handles audio format detection, WebSocket connections, and supports multiple authentication methods (API Key, Managed Identity, Default Azure credentials).

This is useful when:

- You want to use **Azure AI Speech Service** for speech-to-text rather than an Azure OpenAI Whisper deployment.
- Your speech-to-text service is on a **separate Azure resource** from your chat models.
- You want a **self-contained deployment** without creating a full provider connection.

### How to Create an Azure Speech Deployment

1. Navigate to **AI Services** → **Deployments** in the admin dashboard.
2. Click **Add Deployment** and select **Azure Speech** as the provider.
3. Enter a **deployment name** (a friendly identifier for this deployment).
4. Set the **deployment type** to **SpeechToText**.
5. Provide the **Endpoint URL** of your Azure Speech Service resource (e.g., `https://{region}.stt.speech.microsoft.com/`, `https://{region}.api.cognitive.microsoft.com/`, or your custom domain endpoint). The region is automatically extracted from the endpoint to configure the Speech SDK.
6. Select the **Authentication type**: `Default`, `Managed Identity`, or `API Key`.
   - For **API Key**: provide the Speech Service subscription key.
   - For **Managed Identity**: optionally provide a **user-assigned identity client ID**. If omitted, the system-assigned identity is used.
7. Save the deployment.

:::tip
You can find your Speech Service endpoint and API key in the [Azure AI Foundry portal](https://ai.azure.com/) or the Azure Portal under your Speech Service resource's **Keys and Endpoint** section.
:::

### Setting as Default Speech-to-Text Deployment

After creating the deployment, go to **Configuration** → **Settings** → **AI** and select this deployment under **Default Speech-to-Text Deployment**. This enables the microphone button in AI Chat profiles and Chat Interactions that have speech-to-text enabled.

### Configuring Azure Speech via appsettings.json

Instead of creating Azure Speech deployments through the admin UI, you can define them in `appsettings.json`. This is useful for sharing speech-to-text deployments across all tenants without per-tenant configuration.

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

Deployments defined in configuration are read-only and appear alongside database-managed deployments in the UI and API.

### GStreamer Requirement

The Azure Speech SDK uses [GStreamer](https://gstreamer.freedesktop.org) to decode compressed audio formats (OGG/Opus, WebM/Opus, MP3, FLAC). Because browsers send compressed audio (typically WebM/Opus or OGG/Opus) via MediaRecorder, **GStreamer must be installed on every platform** where the application runs — including Windows, Linux, and macOS.

If GStreamer is missing, speech-to-text requests will fail with error code `0x29 (SPXERR_GSTREAMER_NOT_FOUND_ERROR)`.

For more details, see the [Azure Speech SDK compressed audio documentation](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/how-to-use-codec-compressed-audio-input-streams).

#### Windows

1. Download the GStreamer installer from [https://gstreamer.freedesktop.org/download/](https://gstreamer.freedesktop.org/download/). Choose the **MSVC 64-bit** runtime installer (e.g., `gstreamer-1.0-msvc-x86_64-X.X.X.msi`).
2. Run the installer. During setup, ensure the installation directory is added to the system `PATH` (the installer offers this option).
3. Verify that the GStreamer `bin` directory (e.g., `C:\gstreamer\1.0\msvc_x86_64\bin`) is in your `PATH`. The Speech SDK looks for `libgstreamer-1.0-0.dll` or `gstreamer-1.0-0.dll` at runtime.
4. Restart any running applications or terminals after installation.

To verify:

```powershell
gst-inspect-1.0.exe --version
```

#### Ubuntu / Debian

```bash
sudo apt-get update
sudo apt-get install -y \
  libgstreamer1.0-0 \
  gstreamer1.0-plugins-base \
  gstreamer1.0-plugins-good \
  gstreamer1.0-plugins-bad \
  gstreamer1.0-plugins-ugly
```

#### RHEL / CentOS / Fedora

```bash
sudo dnf install -y \
  gstreamer1 \
  gstreamer1-plugins-base \
  gstreamer1-plugins-good \
  gstreamer1-plugins-bad-free \
  gstreamer1-plugins-ugly-free
```

#### Alpine Linux

```bash
apk add --no-cache \
  gstreamer \
  gst-plugins-base \
  gst-plugins-good \
  gst-plugins-bad \
  gst-plugins-ugly
```

#### macOS

```bash
brew install gstreamer
```

#### Docker

Add GStreamer installation to your `Dockerfile` before the application layer:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base

# Install GStreamer for Azure Speech SDK compressed audio support
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
      libgstreamer1.0-0 \
      gstreamer1.0-plugins-base \
      gstreamer1.0-plugins-good \
      gstreamer1.0-plugins-bad \
      gstreamer1.0-plugins-ugly && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CrestApps.OrchardCore.Cms.Web.dll"]
```

#### Verifying GStreamer Installation

Run the following command to confirm GStreamer is available:

```bash
gst-inspect-1.0 --version
```

You should see output like `gst-inspect-1.0 version 1.x.x`. If the command is not found, GStreamer is not installed correctly.
