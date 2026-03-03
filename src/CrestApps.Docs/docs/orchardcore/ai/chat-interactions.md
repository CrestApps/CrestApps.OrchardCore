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
- Connection/deployment selection — choose specific connections and deployments for each interaction
- Orchestrator selection — choose which orchestrator runtime manages the session (e.g., Default, Copilot)
- Image generation — generate images from text prompts using AI image generation models
- Chart generation — generate chart specifications from prompts (for rendering as a chart)
- Document upload — upload documents and chat against your own data via retrieval-augmented generation (RAG)

## Getting Started

1. Enable the `AI Chat Interactions` feature in Orchard Core admin
2. Navigate to **Artificial Intelligence > Chat Interactions**
3. Click **+ New Chat** and select an AI provider
4. Configure your chat settings and start chatting

## Orchestration

Each chat interaction session is bound to an orchestrator that manages the execution pipeline. The orchestrator handles:

- **Planning** — breaking the request into steps and deciding what to do next
- **Tool scoping** — selecting and invoking the right tools based on context
- **MCP connections** — discovering and using capabilities from connected MCP servers
- **Document handling** — providing uploaded document context to the AI model (retrieval-augmented generation (RAG))
- **Iterative execution** — managing multi-step tool-call loops

The default orchestrator (`DefaultOrchestrator`) is our state-of-the-art orchestrator responsible for gluing together everything the model needs to do useful work: planning, tool selection and execution, document context, and multi-step reasoning loops. It is effectively the brain behind chat interactions and the overall model behavior, unless you select a different orchestrator (for example, the Copilot orchestrator).

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

To enable image generation, configure the `ImagesDeploymentName` in your AI provider connection settings.

**Option 1: Admin UI**

Navigate to **Artificial Intelligence > Provider Connections**, edit your connection, and set the **Images deployment name** field (e.g., `dall-e-3`).

**Option 2: Configuration (appsettings.json)**

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "OpenAI": {
          "Connections": {
            "default": {
              "ChatDeploymentName": "gpt-4o",
              "UtilityDeploymentName": "gpt-4o-mini",
              "EmbeddingDeploymentName": "",
              "ImagesDeploymentName": "dall-e-3"
            }
          }
        }
      }
    }
  }
}
```
