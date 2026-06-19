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

Enable this feature when you want Orchard Core to use Azure OpenAI for chat, utility, embedding, or image workloads.

## Configuration

Add the following section to `appsettings.json`:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "Providers": {
          "Azure": {
            "DefaultConnectionName": "azure-openai",
            "Connections": {
              "azure-openai": {
                "Endpoint": "https://your-resource.openai.azure.com/",
                "AuthenticationType": "ApiKey",
                "ApiKey": "your-api-key",
                "Deployments": [
                  { "Name": "chat-deployment", "Purpose": "Chat" },
                  { "Name": "utility-deployment", "Purpose": "Utility" },
                  { "Name": "embedding-deployment", "Purpose": "Embedding" },
                  { "Name": "image-deployment", "Purpose": "Image" }
                ]
              }
            }
          }
        }
      }
    }
  }
}
```

Valid values for `AuthenticationType` are `Default`, `ManagedIdentity`, and `ApiKey`. If using `ApiKey`, the `ApiKey` field is required.

When using `ManagedIdentity`, you can optionally provide an `IdentityId` to use a user-assigned managed identity. If `IdentityId` is omitted or empty, the system-assigned managed identity is used.

```json
{
  "Connections": {
    "azure-openai": {
      "Endpoint": "https://my-account.openai.azure.com/",
      "AuthenticationType": "ManagedIdentity",
      "IdentityId": "optional-user-assigned-managed-identity-client-id"
    }
  }
}
```

## How to retrieve Azure OpenAI credentials

### Get the API key and endpoint

1. Open the Azure Portal and navigate to your Azure OpenAI resource.
2. Go to **Resource Management** -> **Keys and Endpoint**.
3. Copy the **Endpoint**.
4. Copy one of the available **API keys**.

## Recipe configuration

Use `AIProviderConnections` to create the connection, `AIDeployment` to create the deployments, and `AIProfile` when you want to provision a profile that references those deployments.

```json
{
  "steps": [
    {
      "name": "AIProviderConnections",
      "Connections": [
        {
          "Source": "Azure",
          "Name": "azure-openai",
          "DisplayText": "Azure OpenAI",
          "Properties": {
            "AzureConnectionMetadata": {
              "Endpoint": "https://my-account.openai.azure.com/",
              "AuthenticationType": "ApiKey",
              "ApiKey": "your-api-key"
            }
          }
        }
      ]
    },
    {
      "name": "AIDeployment",
      "Deployments": [
        {
          "Name": "chat-deployment",
          "ClientName": "Azure",
          "ConnectionName": "azure-openai",
          "Purpose": "Chat"
        },
        {
          "Name": "utility-deployment",
          "ClientName": "Azure",
          "ConnectionName": "azure-openai",
          "Purpose": "Utility"
        }
      ]
    },
    {
      "name": "AIProfile",
      "Profiles": [
        {
          "Source": "Azure",
          "Name": "support-assistant",
          "DisplayText": "Support Assistant",
          "WelcomeMessage": "What do you want to know?",
          "Type": "Chat",
          "TitleType": "InitialPrompt",
          "ChatDeploymentName": "chat-deployment",
          "UtilityDeploymentName": "utility-deployment",
          "Properties": {
            "AIProfileMetadata": {
              "SystemMessage": "You are an AI assistant that helps people find information."
            }
          }
        }
      ]
    }
  ]
}
```

## RAG / Data Sources

Data sources and RAG are implemented in the provider-agnostic `CrestApps.OrchardCore.AI.DataSources` module.

See: [AI Data Sources](../data-sources/)

## Azure Speech deployments

The Azure Speech deployment provider lets you register [Azure AI Speech Service](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/) deployments as standalone, self-contained speech-to-text endpoints. Unlike standard Azure OpenAI deployments that reference a shared connection, Azure Speech deployments embed their own connection parameters directly within the deployment configuration.

This is useful when:

- you want to use Azure AI Speech Service for speech-to-text
- your speech-to-text service is on a separate Azure resource from your chat models
- you want a self-contained deployment without creating a shared provider connection

### How to create an Azure Speech deployment

1. Navigate to **AI Services** -> **Deployments** in the admin dashboard.
2. Click **Add Deployment** and select **Azure Speech** as the provider.
3. Enter a deployment name.
4. Set the deployment purpose to **SpeechToText**.
5. Provide the endpoint URL of your Azure Speech Service resource.
6. Select the authentication type: `Default`, `ManagedIdentity`, or `ApiKey`.
7. Save the deployment.

:::tip
You can find your Speech Service endpoint and API key in the [Azure AI Foundry portal](https://ai.azure.com/) or the Azure Portal under your Speech Service resource's **Keys and Endpoint** section.
:::

### Setting the default speech-to-text deployment

After creating the deployment, go to **Settings** -> **Artificial Intelligence** and select this deployment under **Default Speech-to-Text Deployment**.

### Configuring Azure Speech via appsettings.json

Instead of creating Azure Speech deployments through the admin UI, you can define them in `appsettings.json`:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "Deployments": [
          {
            "ClientName": "AzureSpeech",
            "Name": "my-speech-to-text",
            "Purpose": "SpeechToText",
            "Endpoint": "https://eastus.api.cognitive.microsoft.com/",
            "AuthenticationType": "ApiKey",
            "ApiKey": "your-speech-service-api-key"
          }
        ]
      }
    }
  }
}
```

Deployments defined in configuration are read-only and appear alongside database-managed deployments in the UI and API.

### GStreamer requirement

The Azure Speech SDK uses [GStreamer](https://gstreamer.freedesktop.org) to decode compressed audio formats (OGG/Opus, WebM/Opus, MP3, FLAC). Because browsers send compressed audio through `MediaRecorder`, GStreamer must be installed on every platform where the application runs.

If GStreamer is missing, speech-to-text requests fail with error code `0x29 (SPXERR_GSTREAMER_NOT_FOUND_ERROR)`.

For more details, see the [Azure Speech SDK compressed audio documentation](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/how-to-use-codec-compressed-audio-input-streams).

#### Windows

1. Download the GStreamer installer from [https://gstreamer.freedesktop.org/download/](https://gstreamer.freedesktop.org/download/). Choose the **MSVC 64-bit** runtime installer.
2. Run the installer and add the installation directory to the system `PATH`.
3. Verify that the GStreamer `bin` directory is in your `PATH`.
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

#### Verifying GStreamer installation

Run the following command to confirm GStreamer is available:

```bash
gst-inspect-1.0 --version
```

You should see output like `gst-inspect-1.0 version 1.x.x`. If the command is not found, GStreamer is not installed correctly.
