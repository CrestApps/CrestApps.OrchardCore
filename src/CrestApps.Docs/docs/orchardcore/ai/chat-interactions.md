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
- User memory — persist private, non-sensitive preferences and durable background details for authenticated users
- Chat mode — configurable voice interaction modes (Text Only, Audio Input, Conversation) for speech-to-text dictation and two-way voice chat
- Prompt-template composition — add multiple reusable prompt templates from a searchable picker and provide per-template JSON parameters

## Getting Started

1. Enable the `AI Chat Interactions` feature in Orchard Core admin
2. Navigate to **Artificial Intelligence > Chat Interactions**
3. Click **+ New Chat**, then select your chat and utility deployments
4. Configure your chat settings and start chatting

:::tip
Deployment dropdowns are grouped by connection, making it easy to find the right model. If you don't select a deployment, the system uses the fallback chain: connection default → global default (configured in **Settings > Artificial Intelligence > Default Deployments**). For chat interactions, the global fallback is **Default Chat Deployment**.
:::

## Orchestration

Each chat interaction session is bound to an orchestrator that manages the execution pipeline. The orchestrator handles:

- **Planning** — breaking the request into steps and deciding what to do next
- **Tool scoping** — selecting and invoking the right tools based on context
- **MCP connections** — discovering and using capabilities from connected MCP servers
- **Document handling** — providing uploaded document context to the AI model (retrieval-augmented generation (RAG))
- **Iterative execution** — managing multi-step tool-call loops

The default orchestrator (`DefaultOrchestrator`) is our state-of-the-art orchestrator responsible for gluing together everything the model needs to do useful work: planning, tool selection and execution, document context, and multi-step reasoning loops. It is effectively the brain behind chat interactions and the overall model behavior, unless you select a different orchestrator (for example, the Copilot orchestrator).

## Chat Mode

Chat Interactions supports configurable chat modes that control how users interact with the AI. This is a site-level setting that applies globally to all chat interaction sessions.

### Chat Mode Options

| Mode | Description | UI Element |
| --- | --- | --- |
| **Text Only** (default) | Standard text-based chat. Users type prompts and receive text responses. | — |
| **Audio Input** | Adds a microphone button (🎤) for speech-to-text dictation. Users speak their prompts, review the transcribed text, and click send manually. | Microphone button |
| **Conversation** | Persistent two-way voice interaction like ChatGPT voice mode. A continuous audio stream stays open — the user speaks, the AI responds with both text and voice simultaneously. | Headset button |

### Prerequisites

- **Audio Input** requires a **Default Speech-to-Text Deployment** configured in **Settings → Artificial Intelligence → Default Deployments** (any deployment supporting the `ISpeechToTextClient` interface, such as Azure Speech or OpenAI Whisper).
- **Conversation** requires both a **Default Speech-to-Text Deployment** and a **Default Text-to-Speech Deployment** configured in default deployment settings.
- Optionally, set a **Default Text-to-Speech Voice** in **Settings → Artificial Intelligence → Default Deployments**. The voice list always includes the current culture, even if no site cultures are configured.

### Configuring Chat Mode

1. Navigate to **Settings → Artificial Intelligence → Chat Interactions**.
2. Select the desired option from the **Chat Mode** dropdown. The dropdown only appears when the required default deployments are configured.
3. Save the settings.

The selected chat mode applies to all Chat Interaction UIs. Unlike AI Profiles, there is no per-session voice selection — Conversation mode uses the default voice configured in site settings (or the provider's default).

Once configured:

- **Audio Input**: A microphone button (🎤) appears in the chat interaction interface. Click the microphone to start recording and speak your prompt. Audio is streamed to the server in real-time via SignalR, and transcript text is sent back as it becomes available — you see words appear while still speaking. Click the stop button when finished, then review or edit the transcribed text before sending.
- **Conversation**: A headset button appears in the Chat Interaction editor. Click it to start a persistent two-way voice conversation — the mic, send button, and text input are hidden. Speak naturally and your transcribed prompt appears as a user message and is automatically sent. The AI responds with streamed text **and** spoken audio simultaneously. If you speak while the AI is responding, the current response is interrupted to process your new prompt. Click the headset button again to end the conversation and restore the text interface.

:::info
If the speech-to-text service encounters an error during transcription, the error is reported immediately and the recording stops automatically.
:::

:::info
Text-to-speech synthesis occurs after the full response text has been received — it does not interrupt or delay the text streaming experience.
:::

## Related Features

### AI Memory

For persistent private memory across chat interactions, see the [AI Memory documentation](memory).

Chat Interaction memory is:

- **enabled by default**
- **available only to authenticated users**
- **always filtered by the current user ID**
- **disabled automatically when no AI Memory index profile is configured**

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
