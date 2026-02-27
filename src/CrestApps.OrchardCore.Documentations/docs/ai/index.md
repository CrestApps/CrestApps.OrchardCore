---
sidebar_label: Overview
sidebar_position: 0
title: Artificial Intelligence Suite
description: Overview of the CrestApps AI modules for Orchard Core, providing comprehensive AI capabilities.
---

# Artificial Intelligence Suite

The CrestApps AI Suite is a comprehensive collection of modules that integrate AI capabilities into your Orchard Core website. It supports most AI providers including **OpenAI**, **Azure OpenAI**, **Azure AI Inference**, **Ollama**, and any provider that adheres to the OpenAI API standard.

## Architecture

The AI Suite is built on [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI), providing a standardized abstraction layer for AI services. The architecture follows a modular feature-rich design, allowing you to enable only the components you need.

## Key Concepts

### AI Profiles

An **AI Profile** defines how the AI system interacts with users — including its welcome message, system message, response behavior, and which tools and data sources are available. Profiles can be managed through the admin UI or created programmatically via code and recipes.

### Orchestrator

The **Orchestrator** (`IOrchestrator`) is the central runtime that manages AI completion sessions. It handles:

- Planning and decomposing complex requests into steps
- Scoping relevant tools based on context
- Executing iterative agent loops (calling the LLM with scoped tools)
- Detecting capability gaps and expanding tool scope progressively
- Producing the final streaming response

### AI Providers

**Providers** are modules that implement the connection to specific AI services. Each provider knows how to create chat clients, embedding generators, and image generators for its platform. See [AI Providers](../providers/) for details.

## Modules

| Module | Description |
|--------|-------------|
| [AI](ai) | Foundation for all AI modules with profile and connection management |
| [AI Chat](ai-chat) | Admin and frontend chat interfaces |
| [AI Chat Interactions](ai-chat-interactions) | Ad-hoc chat with configurable parameters and document upload |
| [AI Prompt Templates](ai-prompt-templates) | Centralized prompt management with Liquid templates and file-based discovery |
| [Consuming AI Services](consuming-ai-services) | Using AI services programmatically via code |
| [Copilot Integration](ai-copilot) | GitHub Copilot SDK-based orchestration |
| [Data Sources](data-sources/) | Retrieval-augmented generation (RAG) / knowledge base indexing and vector search |
| [Documents](documents/) | Document upload, text extraction, and embedding |
| [MCP](mcp/) | Model Context Protocol client and server support |
| [Orchard Core Agent](ai-agent) | Intelligent agents that perform tasks on your site |

## Getting Started

1. Install the `CrestApps.OrchardCore.Cms.Core.Targets` package (includes all modules), or install individual AI packages
2. Enable the desired AI features in the Orchard Core admin dashboard (**Tools → Features**)
3. Configure at least one [AI provider](../providers/) with connection credentials
4. Create an AI Profile and start interacting with AI models

### Quick Start Configuration

Add the following to your `appsettings.json` to get started with OpenAI:

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "DefaultParameters": {
        "Temperature": 0,
        "MaxOutputTokens": 800
      },
      "Providers": {
        "OpenAI": {
          "DefaultConnectionName": "my-connection",
          "DefaultChatDeploymentName": "gpt-4o-mini",
          "Connections": {
            "my-connection": {
              "Endpoint": "https://api.openai.com/v1/",
              "ApiKey": "<your-openai-api-key>",
              "ChatDeploymentName": "gpt-4o-mini",
            }
          }
        }
      }
    }
  }
}
```

For full configuration details, see [AI Services and Configuration](ai).
