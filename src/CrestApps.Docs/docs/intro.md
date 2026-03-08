---
sidebar_label: Introduction
sidebar_position: 1
title: CrestApps Orchard Core Modules
description: Overview of CrestApps open-source modules for Orchard Core CMS
---

# CrestApps Orchard Core Modules

CrestApps provides a collection of open-source modules designed to enhance **[Orchard Core](https://orchardcore.net/)**, a powerful application framework built on **ASP.NET Core**.

The project is built on the **CrestApps AI Framework** — a reusable set of .NET libraries that can power AI in any ASP.NET Core application. See [Framework Overview](framework/) to learn more.

## Key Focus Areas

- **Modularity** – Independent modules allow for seamless integration based on project requirements.
- **Security** – Designed following industry best practices to ensure application safety.
- **Performance** – Optimized for speed and efficiency to maximize Orchard Core's potential.
- **Framework-First** – Core AI services are framework-agnostic, reusable by any .NET application.

## Module Categories

### Artificial Intelligence Suite

Integrate most AI providers like **OpenAI**, **Azure OpenAI**, **Azure AI Inference**, **Ollama**, and more into your Orchard Core website. The AI suite includes:

- **[Overview](orchardcore/ai/)** – Introduction to the AI Suite
- **[AI](orchardcore/ai/overview)** – Foundation for all AI modules with profile and connection management
- **[AI Chat](orchardcore/ai/chat)** – Chat interfaces for interacting with AI models
- **[AI Chat Interactions](orchardcore/ai/chat-interactions)** – Ad-hoc chat with configurable parameters and document upload
- **[Consuming AI Services](orchardcore/ai/consuming-ai-services)** – Using AI services programmatically via code
- **[Copilot Integration](orchardcore/ai/copilot)** – GitHub Copilot SDK-based orchestration
- **[Data Sources](orchardcore/ai/data-sources/)** – Retrieval-augmented generation (RAG) / knowledge base indexing and vector search
- **[Documents](orchardcore/ai/documents/)** – Document upload, text extraction, and embedding
- **[MCP](orchardcore/ai/mcp/)** – Model Context Protocol client and server support
- **[Orchard Core Agent](orchardcore/ai/agent)** – Intelligent agents that perform tasks on your site
- **[AI Providers](orchardcore/ai/providers/)** – Connect to OpenAI, Azure OpenAI, Azure AI Inference, Ollama, and more

### Standard Modules

Essential CMS enhancements:

- **[Content Access Control](orchardcore/modules/content-access-control)** – Role-based content restrictions
- **[Recipes](orchardcore/modules/recipes)** – JSON-Schema support for recipes
- **[Resources](orchardcore/modules/resources)** – Shared scripts and stylesheets
- **[Roles](orchardcore/modules/roles)** – Enhanced role management
- **[SignalR](orchardcore/modules/signalr)** – Real-time communication
- **[Users](orchardcore/modules/users)** – Enhanced user management

### Omnichannel Communications

Unified communication orchestration:

- **[Overview](orchardcore/omnichannel/)** – Core orchestration services
- **[Event Grid](orchardcore/omnichannel/event-grid)** – Azure Event Grid integration
- **[Management](orchardcore/omnichannel/management)** – Mini-CRM for contacts, campaigns, and activities
- **[SMS Automation](orchardcore/omnichannel/sms)** – AI-driven SMS automation

## Package Management

- **Orchard Core 2.1–2.3**: Use package version `1.2.x`
- **Orchard Core 3.0+**: Use version `2.0.0-preview-0001` or newer

Stable releases are available on [NuGet.org](https://www.nuget.org/). Preview packages are available from the [CrestApps CloudSmith feed](https://cloudsmith.io/~crestapps/repos/crestapps-orchardcore).
