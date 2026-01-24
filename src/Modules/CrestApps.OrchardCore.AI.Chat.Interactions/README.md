# AI Chat Interactions Module

This module provides ad-hoc AI chat interactions with configurable parameters, enabling users to chat with AI models without requiring predefined AI Profiles.

## Features

- **AI Chat Interactions**: Create and manage chat sessions with any configured AI provider
- **Session Persistence**: All chat messages are saved and can be resumed later
- **Configurable Parameters**: Customize temperature, TopP, max tokens, frequency/presence penalties, and past messages count
- **Tool Integration**: Select from available AI tools and MCP connections
- **Connection/Deployment Selection**: Choose specific connections and deployments for each interaction
- **Image Generation**: Generate images from text prompts using AI image generation models

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

## Intent-Based Processing

AI Chat Interactions uses intent detection to route prompts to the most appropriate processing strategy. This allows the system to automatically determine whether a user wants to generate an image, chat with documents, or perform other specialized tasks.

### Image Generation

The module supports AI-powered image generation through prompts. When a user asks to generate, create, or draw an image, the system automatically detects this intent and routes the request to the image generation strategy.

#### Supported Intents

| Intent | Description | Example Prompts |
|--------|-------------|-----------------|
| `GenerateImage` | Generate an image from a text description | "Generate an image of a sunset", "Create a picture of a cat", "Draw a mountain landscape" |
| `GenerateImageWithHistory` | Generate an image using conversation context | "Use that data to create a chart", "Based on the above, draw a diagram", "Make a visual of what we discussed" |
| `GenerateChart` | Generate charts and graphs | "Create a bar chart", "Draw a pie chart of sales data", "Make a line graph" |

#### Configuration

To enable image generation, configure the `DefaultImagesDeploymentName` in your AI provider connection settings:

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
              "DefaultImagesDeploymentName": "dall-e-3"
            }
          }
        }
      }
    }
  }
}
```

#### Supported Providers

Image generation is supported by:
- **OpenAI**: Models like `dall-e-2`, `dall-e-3`
- **Azure OpenAI**: DALL-E deployments

> **Note**: Ollama and Azure AI Inference do not currently support image generation and will return an error if image generation is attempted.

#### How It Works

1. **Intent Detection**: When a user sends a prompt, the system analyzes it to detect if it's an image generation request
2. **Context Augmentation**: For `GenerateImageWithHistory`, the prompt is augmented with recent chat history
3. **Image Generation**: The request is sent to the configured image generation model
4. **Response**: The generated image is displayed inline in the chat with a download option

### Intent Detection Model

By default, intent detection uses the same model configured for chat (`DefaultDeploymentName`). For cost optimization, you can configure a separate lightweight model for intent classification:

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "OpenAI": {
          "Connections": {
            "default": {
              "DefaultDeploymentName": "gpt-4o",
              "DefaultIntentDeploymentName": "gpt-4o-mini",
              "DefaultImagesDeploymentName": "dall-e-3"
            }
          }
        }
      }
    }
  }
}
```

> **Recommendation**: Use a lightweight model like `gpt-4o-mini` for intent detection. Intent classification is a simple task that doesn't require the full capabilities of larger models.
