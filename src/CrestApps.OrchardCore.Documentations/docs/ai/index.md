---
sidebar_label: Overview
sidebar_position: 0
title: Artificial Intelligence Suite
description: Overview of the CrestApps AI Suite, a comprehensive and extensible Orchard Core ecosystem for AI integration, management, orchestration, chat, MCP, Agent-to-Agent, memory, and retrieval workflows.
---

# Artificial Intelligence Suite

The CrestApps AI Suite is a comprehensive and extensible AI ecosystem built on Orchard Core, designed to unify and streamline AI integration and management. It combines provider integrations, flexible deployment and connection management, AI profiles, and advanced orchestration into a cohesive platform.

The suite enables highly customizable chat experiences, along with robust prompt and tool management, retrieval workflows, and long-term memory capabilities. It also supports MCP and Agent-to-Agent integrations, delivering a powerful foundation for building intelligent, interconnected systems—all seamlessly managed within the Orchard Core admin experience.

It supports most AI providers including **OpenAI**, **Azure OpenAI**, **Azure AI Inference**, **Ollama**, and any provider that adheres to the OpenAI API standard.

## Architecture

The AI Suite is built on [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI), providing a standardized abstraction layer for AI services. The architecture follows a modular, feature-rich design, allowing you to enable only the components you need while still composing a larger AI integrations and solutions platform for your Orchard Core solution.

## Key Concepts

### AI Profiles

An **AI Profile** defines how the AI system interacts with users — including its welcome message, system message, response behavior, deployments, and which tools, data sources, and capabilities are available. Profiles can be managed through the admin UI or created programmatically via code and recipes.

### Orchestrator

The **Orchestrator** (`IOrchestrator`) is the central runtime that manages AI completion sessions. It handles:

- Planning and decomposing complex requests into steps
- Scoping relevant tools based on context
- Executing iterative agent loops (calling the LLM with scoped tools)
- Detecting capability gaps and expanding tool scope progressively
- Producing the final streaming response

### AI Providers

**Providers** are modules that implement the connection to specific AI services. Each provider knows how to create chat clients, embedding generators, and image generators for its platform. See [AI Providers](./providers/) for details.

### AI Management

The suite also includes rich management capabilities in Orchard Core, such as AI profile administration, deployment and connection management, prompt template management, site-level AI settings, MCP connection/resource management, and other operational surfaces required to run AI features in production.

## Modules

| Module | Description |
|--------|-------------|
| [AI](overview) | Foundation for the AI ecosystem with profiles, orchestration, connections, deployments, settings, and management |
| [AI Chat](chat) | Admin and frontend chat interfaces |
| [AI Chat Interactions](chat-interactions) | Ad-hoc chat with configurable parameters and document upload |
| [AI Prompt Templates](prompt-templates) | Centralized prompt management with Liquid templates and file-based discovery |
| [Consuming AI Services](consuming-ai-services) | Using AI services programmatically via code |
| [Copilot Integration](copilot) | GitHub Copilot SDK-based orchestration |
| [Data Sources](data-sources/) | Retrieval-augmented generation (RAG) / knowledge base indexing and vector search |
| [Documents](documents/) | Document upload, text extraction, and embedding |
| [AI Memory](memory) | Persistent, user-scoped memory for preferences, projects, topics, interests, and other durable background details |
| [MCP](mcp/) | Model Context Protocol client and server support |
| [Orchard Core Agent](agent) | Intelligent agents that perform tasks on your site |

## Getting Started

1. Install the `CrestApps.OrchardCore.Cms.Core.Targets` package (includes all modules), or install individual AI packages
2. Enable the desired AI features in the Orchard Core admin dashboard (**Tools → Features**)
3. Configure at least one [AI provider](./providers/) with connection credentials
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
          "Connections": {
            "my-connection": {
              "Endpoint": "https://api.openai.com/v1/",
              "ApiKey": "<your-openai-api-key>",
              "Deployments": [
                { "Name": "gpt-4o-mini", "Type": "Chat", "IsDefault": true }
              ]
            }
          }
        }
      }
    }
  }
}
```

For full configuration details, see [AI Services and Configuration](overview).
