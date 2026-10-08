---
sidebar_label: AI Chat Interactions
sidebar_position: 3
title: AI Chat Interactions Module
description: Ad-hoc AI chat interactions with configurable parameters, tool integration, and document support.
user_manual:
  - user-manual/ai/chat-interactions
---

| | |
| --- | --- |
| **Feature Name** | AI Chat Interactions |
| **Feature ID** | `CrestApps.OrchardCore.AI.Chat.Interactions` |

Provides ad-hoc AI chat interactions with configurable parameters without predefined profiles.

How to start, configure and use chat interactions in the admin, with screencasts, is described in the User Manual under [Chat interactions](../user-manual/ai/chat-interactions.md).

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
- Citation rendering — convert `[doc:N]` markers into superscript citations with a linked reference list when references include resolvable URLs
- User memory — persist private, non-sensitive preferences and durable background details for authenticated users
- Chat mode — configurable voice interaction modes (Text Only, Audio Input, Conversation) for speech-to-text dictation and two-way voice chat
- Prompt-template composition — add multiple reusable prompt templates from a searchable picker and provide per-template JSON parameters
- Upload controls driven by site settings — the Knowledge tab only exposes document and image extensions that are enabled in **Settings > Artificial Intelligence**, and image uploads also require a configured vision deployment

## Getting Started

Enable the **AI Chat Interactions** feature (`CrestApps.OrchardCore.AI.Chat.Interactions`). It adds **Artificial Intelligence > Chat Interactions**, which requires `ListChatInteractions` (`ListChatInteractionsForOthers` to see other users' interactions). **New Chat** creates and saves an interaction immediately, and settings changes are saved over SignalR through the `IChatInteractionSettingsHandler` pipeline. The screen is described in [Chat interactions](../user-manual/ai/chat-interactions.md).

:::tip
Deployment dropdowns are grouped by connection, making it easy to find the right model. If you don't select a deployment, the system uses the fallback chain: connection default → global default (configured in **Settings > Artificial Intelligence > Default Deployments**). For chat interactions, the global fallback is **Default Chat Deployment**.
:::

When a response cites uploaded or indexed content, the interaction UI renders `[doc:N]` markers as superscript citations and shows the resolved references beneath the assistant message.

The admin **Chat Interactions** list includes integrated search, multi-select, and bulk actions through the shared list management resource used across CrestApps admin catalogs.

When the **AI Documents** feature is enabled, the **Knowledge** tab shows the current supported upload formats directly under the file picker. The visible extensions follow the site-level **Allow document uploads in chat interactions** and **Allow image uploads in chat interactions** settings, and image formats only appear when a vision deployment is configured.

### Chatting with an Uploaded PDF

You can attach a document to a single interaction and ask questions about its contents. The uploaded file is chunked, embedded, and stored in an **AI Documents** knowledge base index, and the orchestrator retrieves the relevant passages to ground its answer.

To enable this experience:

1. Enable the **AI Documents (Elasticsearch)** (or **AI Documents (Azure AI Search)**) feature so a vector store is available.
2. In **Search > Indexing**, add an **AI Documents** index that uses an embedding deployment.
3. In **Settings > Artificial Intelligence**, select that index profile under the document settings, choose a **Document retrieval mode**, and enable **Allow document uploads in chat interactions**.
4. Start a new chat interaction, open the **Knowledge** tab, and upload your file.

The User Manual page [Chat interactions](../user-manual/ai/chat-interactions.md#knowledge-tab) shows this flow in a screencast.

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
| **Conversation** | Two-way voice interaction the user switches on and off beside an ordinary message box. Starting a session hands the turn to speech; ending it gives the message box back, and both kinds of turn land in the same interaction. | Soundwave button |

### Prerequisites

- **Audio Input** requires a **Default Speech-to-Text Deployment** configured in **Settings → Artificial Intelligence → Default Deployments** (any deployment supporting the `ISpeechToTextClient` interface, such as Azure Speech or OpenAI Whisper).
- **Conversation** is carried by a realtime (speech-to-speech) deployment when one resolves — the interaction's own **Conversation deployment**, then the site's default realtime deployment, then the first realtime-capable deployment. See [Realtime Voice](realtime-voice.md). When none resolves it falls back to the client-driven speech-to-text plus text-to-speech cascade, which requires both a **Default Speech-to-Text Deployment** and a **Default Text-to-Speech Deployment**.
- Optionally, set a **Default Text-to-Speech Voice** in **Settings → Artificial Intelligence → Default Deployments**. The voice list always includes the current culture, even if no site cultures are configured.

### Configuring Chat Mode

The chat mode is a site setting: **Chat mode** in the **Chat Interactions** section of **Settings → Artificial Intelligence** (options **Text input**, **Audio input**, **Conversation**; the voice options only appear when the required default deployments are configured). Changing it requires the `EditChatInteractions` permission. See [Site settings for chat interactions](../user-manual/ai/chat-interactions.md#site-settings-for-chat-interactions-administrators).

The selected chat mode applies to all Chat Interaction UIs. An interaction has no chat mode of its own, so naming a **Conversation deployment** on the interaction is how that interaction asks to speak while the site is in Conversation mode. The **Voice** picker beside it is per-interaction and lists the voices of whichever model resolves; leaving it empty uses the default voice configured in site settings (or the provider's default).

Audio input and conversation behave exactly as they do in AI Chat: audio is streamed to the server over SignalR and transcribed as it arrives, and a conversation keeps the stream open, sends each recognized utterance automatically and can be interrupted. See [AI Chat runtime behavior](chat.md#runtime-behavior) and, for what users see, [Talk instead of type](../user-manual/ai/chat.md#talk-instead-of-type).

:::info
If the speech-to-text service encounters an error during transcription, the error is reported immediately and the recording stops automatically.
:::

:::info
Text-to-speech synthesis occurs after the full response text has been received — it does not interrupt or delay the text streaming experience.
:::

### Text-to-Speech Playback

You can enable on-demand text-to-speech playback independently of the Conversation chat mode. When enabled, a playback button (🔊) appears on each AI-generated message, allowing users to click and listen to the response.

TTS playback for Chat Interactions is the **Enable text-to-speech playback** site setting in the **Chat Interactions** section of **Settings → Artificial Intelligence**. A **Default Text-to-Speech Deployment** must be configured in **Settings → Artificial Intelligence → Default Deployments** for the playback button to appear.

Playback behaves as in AI Chat: starting playback on a different message stops the current one, and in Conversation mode the per-message playback button is hidden so the live voice flow remains uninterrupted.

## Related Features

### AI Memory

For persistent private memory across chat interactions, see the [AI Memory documentation](memory).

Chat Interaction memory is controlled by **Enable user memory** in the **Memory** section of **Settings → Artificial Intelligence** (permission `ManageChatInteractionSettings`). It is:

- **enabled by default**
- **available only to authenticated users**
- **always filtered by the current user ID**
- **disabled automatically when no AI Memory index profile is configured**

### AI Documents

For document upload and retrieval-augmented generation (RAG) support, see the [Documents feature documentation](documents/).

:::note[Note]
The `AI Documents` feature is provided on demand and is only enabled when another feature that requires it is enabled (for example one of the document indexing provider features). To configure document indexing you must enable either the `AI Documents (Azure AI Search)` feature or the `AI Documents (Elasticsearch)` feature in Orchard Core admin.
:::

The Documents feature supports Elasticsearch and Azure AI Search as embedding and search providers. Ensure you enable the corresponding feature for your chosen provider in Orchard Core admin.

## Image and Chart Generation

Image and chart generation are handled by AI tools that the orchestrator can invoke based on the user's request.

### Configuration

To enable image generation, create an `AIDeployment` record for your image model (for example `dall-e-3`) and declare the `imageOutput` capability on it. You can set it as the site's default image deployment, or select it explicitly on each chat interaction.

**Option 1: Admin UI**

1. Navigate to **Artificial Intelligence > Deployments** and create a new deployment (for example, name `dall-e-3`, connection `openai-main`). On its **Model capabilities** card, enable **Image output**.
2. Optionally, set it as the **Default image deployment** in **Settings > Artificial Intelligence**.

**Option 2: Configuration (appsettings.json)**

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "Deployments": [
          {
            "Name": "gpt-4o",
            "ClientName": "OpenAI",
            "ConnectionName": "default",
            "Properties": {
              "AIDeploymentMetadata": {
                "Features": [ "textGeneration", "toolCalling", "streaming" ]
              }
            }
          },
          {
            "Name": "dall-e-3",
            "ClientName": "OpenAI",
            "ConnectionName": "default",
            "Properties": {
              "AIDeploymentMetadata": {
                "Features": [ "imageOutput" ]
              }
            }
          }
        ]
      }
    }
  }
}
```

The image deployment is the one declaring `imageOutput`. Assign it to the `image` slot by selecting it as the
**Default image deployment** under **Settings** -> **Artificial Intelligence** so image generation picks it
up. See [Model capabilities](model-capabilities.md) for the full list of features and slots.
