---
sidebar_label: Introduction
sidebar_position: 1
title: CrestApps AI Platform
description: Orchard Core modules built on top of the shared CrestApps.Core framework.
---

# CrestApps AI Platform

CrestApps provides two complementary layers for building AI-powered .NET applications:

1. **[CrestApps.Core](https://core.crestapps.com)** — The shared framework for AI completions, orchestration, chat, document processing, MCP, A2A, providers, and storage abstractions.
2. **[CrestApps.OrchardCore](#orchard-core-modules)** — Orchard Core modules that wrap that framework with admin UI, recipes, deployment steps, content integration, and multi-tenant support.

## Start in the right place

| I want to... | Start here |
|--------------|-----------|
| Build an AI app without Orchard Core | [CrestApps.Core docs](https://core.crestapps.com/docs) |
| Understand the shared framework packages | [Framework overview in CrestApps.Core](https://core.crestapps.com/docs/framework) |
| Add AI to an Orchard Core site | [Getting Started](getting-started.md) |
| Explore Orchard-specific modules | [AI Suite docs](ai/index.md) |

## About the shared framework

`CrestApps.OrchardCore` uses the same `CrestApps.Core.*` libraries that are documented in the standalone framework site. Those libraries provide:

- provider-agnostic AI services
- orchestration and tool execution
- chat and response handling
- document processing and retrieval
- MCP and A2A protocol support
- storage and indexing abstractions

The Orchard Core modules build on top of that shared runtime and add CMS-specific capabilities such as settings screens, recipes, deployment steps, permissions, tenancy, and content integration.

## Orchard Core modules

### Artificial Intelligence Suite

The CrestApps AI Suite is a comprehensive and extensible AI ecosystem built on Orchard Core, designed to unify and streamline AI integration and management. It combines provider integrations, flexible deployment and connection management, AI profiles, and advanced orchestration into a cohesive platform.

The suite enables highly customizable chat experiences, along with robust prompt and tool management, retrieval workflows, and long-term memory capabilities. It also supports MCP and Agent-to-Agent integrations, delivering a powerful foundation for building intelligent, interconnected systems—all seamlessly managed within the Orchard Core admin experience.

Integrate most AI providers like **OpenAI**, **Azure OpenAI**, **Azure AI Inference**, **Ollama**, and more into your Orchard Core website. The AI suite includes:

- **[Overview](ai/)** – Introduction to the AI integrations and solutions ecosystem
- **[AI](ai/overview)** – Foundation for the AI ecosystem with profiles, orchestration, connection management, deployment management, and settings
- **[AI Chat](ai/chat)** – Chat interfaces for interacting with AI models
- **[AI Chat Interactions](ai/chat-interactions)** – Ad-hoc chat with configurable parameters and document upload
- **[Consuming AI Services](ai/consuming-ai-services)** – Using AI services programmatically via code
- **[Copilot Integration](ai/copilot)** – GitHub Copilot SDK-based orchestration
- **[Data Sources](ai/data-sources/)** – Retrieval-augmented generation (RAG) / knowledge base indexing and vector search
- **[Documents](ai/documents/)** – Document upload, text extraction, and embedding
- **[AI Memory](ai/memory)** – Durable private memory for preferences, projects, topics, interests, and reusable background context
- **[MCP](ai/mcp/)** – Model Context Protocol client and server support
- **[Orchard Core Agent](ai/agent)** – Intelligent agents that perform tasks on your site
- **[AI Providers](ai/providers/)** – Connect to OpenAI, Azure OpenAI, Azure AI Inference, Ollama, and more

### Standard Modules

Essential CMS enhancements:

- **[Content Access Control](modules/content-access-control)** – Role-based content restrictions
- **[Recipes](modules/recipes)** – JSON-Schema support for recipes
- **[Resources](modules/resources)** – Shared scripts and stylesheets
- **[Roles](modules/roles)** – Enhanced role management
- **[SignalR](modules/signalr)** – Real-time communication
- **[Users](modules/users)** – Enhanced user management

### Omnichannel Communications

Unified communication orchestration:

- **[Overview](omnichannel/)** – Core orchestration services
- **[Event Grid](omnichannel/event-grid)** – Azure Event Grid integration
- **[Management](omnichannel/management)** – Mini-CRM for contacts, campaigns, and activities
- **[SMS Automation](omnichannel/sms)** – AI-driven SMS automation
