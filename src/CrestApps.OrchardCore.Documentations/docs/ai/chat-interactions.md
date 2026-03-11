---
sidebar_label: AI Chat Interactions
sidebar_position: 3
title: AI Chat Interactions Module
description: Ad-hoc AI chat interactions with configurable parameters, tool integration, and document support.
---

| | |
| --- | --- |
| **Feature Name** | AI Chat Interactions |
| **Feature ID** | `CrestApps.OrchardCore.AI.Chat.Interactions` |

Provides ad-hoc AI chat interactions with configurable parameters without predefined profiles.

## Overview

This module provides ad-hoc AI chat interactions with configurable parameters, enabling users to chat with AI models without requiring predefined AI Profiles. The orchestrator manages all AI dependencies including tools, MCP connections, and document handling.

## Capabilities

- Create and manage chat sessions with any configured AI provider
- Session persistence — all chat messages are saved and can be resumed later
- Configurable parameters — customize temperature, TopP, max tokens, frequency/presence penalties, and past messages count
- Tool integration — select from available AI tools and MCP connections
- Deployment selection — choose specific chat and utility deployments for each interaction (grouped by connection in the dropdown)
- Orchestrator selection — choose which orchestrator runtime manages the session (e.g., Default, Copilot)
- Image generation — generate images from text prompts using AI image generation models
- Chart generation — generate chart specifications from prompts (for rendering as a chart)
- Document upload — upload documents and chat against your own data via retrieval-augmented generation (RAG)
- Speech-to-text — speak your prompts via a microphone button using a configured speech-to-text model

## Getting Started

1. Enable the `AI Chat Interactions` feature in Orchard Core admin
2. Navigate to **Artificial Intelligence > Chat Interactions**
3. Click **+ New Chat** and select your chat and utility deployments
4. Configure your chat settings and start chatting

:::tip
Deployment dropdowns are grouped by connection, making it easy to find the right model. If you don't select a deployment, the system uses the fallback chain: connection default → global default (configured in **Settings > Artificial Intelligence > Default Deployments**).
:::

## Orchestration

Each chat interaction session is bound to an orchestrator that manages the execution pipeline. The orchestrator handles:

- **Planning** — breaking the request into steps and deciding what to do next
- **Tool scoping** — selecting and invoking the right tools based on context
- **MCP connections** — discovering and using capabilities from connected MCP servers
- **Document handling** — providing uploaded document context to the AI model (retrieval-augmented generation (RAG))
- **Iterative execution** — managing multi-step tool-call loops

The default orchestrator (`DefaultOrchestrator`) is our state-of-the-art orchestrator responsible for gluing together everything the model needs to do useful work: planning, tool selection and execution, document context, and multi-step reasoning loops. It is effectively the brain behind chat interactions and the overall model behavior, unless you select a different orchestrator (for example, the Copilot orchestrator).

## Speech-to-Text (Voice Input)

Chat Interactions supports speech-to-text input, allowing users to speak their prompts using a microphone button.

### Prerequisites

- A **Default Speech-to-Text Deployment** must be configured in **Settings → Artificial Intelligence → Default Deployments**. This can be any deployment that supports the `ISpeechToTextClient` interface, such as an Azure Speech contained-connection deployment or an OpenAI Whisper deployment.
- The AI provider must support the `ISpeechToTextClient` interface.

### Enabling Speech-to-Text

1. Navigate to **Settings → Artificial Intelligence → Chat Interactions**.
2. Check the **Enable speech-to-text in chat interactions** checkbox. This checkbox only appears when a default speech-to-text deployment is configured.
3. Save the settings.

Once enabled, a microphone button (🎤) appears in the chat interaction interface. Click the microphone to start recording and speak your prompt. Audio is streamed to the server in real-time via SignalR, and transcript text is sent back as it becomes available — you see words appear while still speaking. Click the stop button when finished, then review or edit the transcribed text before sending.

:::info
If the speech-to-text service encounters an error during transcription, the error is reported immediately and the recording stops automatically.
:::

## Related Features

### AI Documents

For document upload and retrieval-augmented generation (RAG) support, see the [Documents feature documentation](documents/).

:::note Note
The `AI Documents` feature is provided on demand and is only enabled when another feature that requires it is enabled (for example one of the document indexing provider features). To configure document indexing you must enable either the `AI Documents (Azure AI Search)` feature or the `AI Documents (Elasticsearch)` feature in Orchard Core admin.
:::

The Documents feature supports Elasticsearch and Azure AI Search as embedding and search providers. Ensure you enable the corresponding feature for your chosen provider in Orchard Core admin.

## Image and Chart Generation

Image and chart generation are handled by AI tools that the orchestrator can invoke based on the user's request.

### Configuration

To enable image generation, create an `AIDeployment` record with type `Image` for your image model (e.g., `dall-e-3`). You can set it as the default Image deployment globally, or select it explicitly on each chat interaction.

**Option 1: Admin UI**

1. Navigate to **Artificial Intelligence > Deployments** and create a new deployment with type **Image** (e.g., name `dall-e-3`, connection `openai-main`).
2. Optionally, set it as the default Image deployment in **Settings > Artificial Intelligence > Default Deployments**.

**Option 2: Configuration (appsettings.json)**

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "OpenAI": {
          "Connections": {
            "default": {
              "Deployments": [
                {
                  "Name": "gpt-4o",
                  "Type": "Chat",
                  "IsDefault": true
                },
                {
                  "Name": "gpt-4o-mini",
                  "Type": "Utility",
                  "IsDefault": true
                },
                {
                  "Name": "dall-e-3",
                  "Type": "Image",
                  "IsDefault": true
                }
              ]
            }
          }
        }
      }
    }
  }
}
```

:::info
The legacy format with `ChatDeploymentName`, `UtilityDeploymentName`, `EmbeddingDeploymentName`, and `ImagesDeploymentName` on the connection is still supported for backward compatibility but is deprecated. See the [migration guide](migration-typed-deployments) for details.
:::
