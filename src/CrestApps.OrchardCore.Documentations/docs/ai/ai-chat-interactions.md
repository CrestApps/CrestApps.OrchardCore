---
sidebar_label: AI Chat Interactions
sidebar_position: 3
title: AI Chat Interactions Module
description: Ad-hoc AI chat interactions with configurable parameters, tool integration, prompt routing, and image/chart generation.
---

| | |
| --- | --- |
| **Feature Name** | AI Chat Interactions |
| **Feature ID** | `CrestApps.OrchardCore.AI.Chat.Interactions` |

Provides ad-hoc AI chat interactions with configurable parameters without predefined profiles.

## Overview

This module provides ad-hoc AI chat interactions with configurable parameters, enabling users to chat with AI models without requiring predefined AI Profiles.

## Capabilities

- Create and manage chat sessions with any configured AI provider
- Session persistence — all chat messages are saved and can be resumed later
- Configurable parameters — customize temperature, TopP, max tokens, frequency/presence penalties, and past messages count
- Tool integration — select from available AI tools and MCP connections
- Connection/deployment selection — choose specific connections and deployments for each interaction
- Prompt routing (intent detection + strategies) — automatically classifies the user prompt and routes it to a registered prompt-processing strategy
- Image generation — generate images from text prompts using AI image generation models
- Chart generation — generate chart specifications from prompts (for rendering as a chart)

## Getting Started

1. Enable the `AI Chat Interactions` feature in Orchard Core admin
2. Navigate to **Artificial Intelligence > Chat Interactions**
3. Click **+ New Chat** and select an AI provider
4. Configure your chat settings and start chatting

## Related Features

### AI Documents

For document upload and document-aware prompt processing (RAG and non-RAG strategies), see the [Documents feature documentation](documents/).

> Note: The `AI Documents` feature is provided on demand and is only enabled when another feature that requires it is enabled (for example one of the document indexing provider features). To configure document indexing you must enable either the `AI Documents (Azure AI Search)` feature or the `AI Documents (Elasticsearch)` feature in Orchard Core admin.

The Documents feature supports Elasticsearch and Azure AI Search as embedding and search providers, ensure you enable the corresponding feature for your chosen provider in Orchard Core admin.

## Prompt Routing (Intent-Based Processing)

Prompt routing is part of the base `CrestApps.OrchardCore.AI.Chat.Interactions` feature.

- **Intent detection** is performed by an AI classifier when available.
- When no intent model is configured/available, the system can fall back to a keyword-based detector.
- **Strategies** are registered services that decide whether to handle a prompt based on the detected intent.

### Built-in Intents

The base module ships with a small set of default intents that enable image and chart experiences.

| Intent | Description | Example Prompts |
|--------|-------------|-----------------|
| `GenerateImage` | Generate an image from a text description | "Generate an image of a sunset", "Create a picture of a cat" |
| `GenerateImageWithHistory` | Generate an image using conversation context | "Based on the above, draw a diagram", "Make a visual of what we discussed" |
| `GenerateChart` | Generate a chart/graph description or spec | "Create a bar chart", "Draw a pie chart of sales data" |

### Configuration

To enable image generation, configure the `DefaultImagesDeploymentName` in your AI provider connection settings.

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
              "DefaultDeploymentName": "gpt-4o",
              "DefaultUtilityDeploymentName": "gpt-4o-mini",
              "DefaultImagesDeploymentName": "dall-e-3"
            }
          }
        }
      }
    }
  }
}
```
