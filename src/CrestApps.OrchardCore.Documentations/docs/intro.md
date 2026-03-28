---
sidebar_label: Introduction
sidebar_position: 1
title: CrestApps Orchard Core Modules
description: Overview of CrestApps open-source modules for Orchard Core CMS
---

# CrestApps Orchard Core Modules

CrestApps provides a collection of open-source modules designed to enhance **[Orchard Core](https://orchardcore.net/)**, a powerful application framework built on **ASP.NET Core**.

## Key Focus Areas

- **Modularity** – Independent modules allow for seamless integration based on project requirements.
- **Security** – Designed following industry best practices to ensure application safety.
- **Performance** – Optimized for speed and efficiency to maximize Orchard Core's potential.

## Module Categories

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

## Package Management

- **Orchard Core 2.1–2.3**: Use package version `1.2.x`
- **Orchard Core 3.0+**: Use version `2.0.0-preview-0001` or newer

Stable releases are available on [NuGet.org](https://www.nuget.org/). Preview packages are available from the [CrestApps CloudSmith feed](https://cloudsmith.io/~crestapps/repos/crestapps-orchardcore).
