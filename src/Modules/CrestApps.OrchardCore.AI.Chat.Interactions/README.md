# AI Chat Interactions Module

This module provides ad-hoc AI chat interactions with configurable parameters, enabling users to chat with AI models without requiring predefined AI Profiles.

## Features

- **AI Chat Interactions**: Create and manage chat sessions with any configured AI provider
- **Session Persistence**: All chat messages are saved and can be resumed later
- **Configurable Parameters**: Customize temperature, TopP, max tokens, frequency/presence penalties, and past messages count
- **Tool Integration**: Select from available AI tools and MCP connections
- **Connection/Deployment Selection**: Choose specific connections and deployments for each interaction

## Getting Started

1. Enable the `AI Chat Interactions` feature in Orchard Core admin
2. Navigate to **Artificial Intelligence > Chat Interactions**
3. Click **+ New Chat** and select an AI provider
4. Configure your chat settings and start chatting



## Related Features

### AI Chat Interactions - Documents

For document upload and RAG (Retrieval Augmented Generation) support, see the [Documents feature documentation](./README-Documents.md).

> Note: The `AI Chat Interactions - Documents` feature is provided on demand and is only enabled when another feature that requires it is enabled (for example one of the document indexing provider features). To configure document indexing you must enable either the `AI Chat Interactions - Documents - Azure AI Search` feature or the `AI Chat Interactions - Documents - Elasticsearch` feature in Orchard Core admin.

The Documents feature supports Elasticsearch and Azure AI Search as embedding and search providers, ensure you enable the corresponding feature for your chosen provider in Orchard Core admin.

## Intent-based processing

AI Chat Interactions uses intent detection to route a prompt to the most appropriate processing strategy.

### Image generation intents

The following intents are used for image generation:

- `GenerateImage`: The user is asking to generate an image (including chart/graph requests like "create a bar chart" or "draw a chart").
- `GenerateImageWithHistory`: The user is asking to generate an image based on prior context (for example: "use that data to create a chart", "based on the above", etc.).

When `GenerateImageWithHistory` is selected, the image generation prompt is augmented with the last N chat messages (configured by the interaction's **Past Messages** setting) so that prompts like "use that data" can be resolved.
