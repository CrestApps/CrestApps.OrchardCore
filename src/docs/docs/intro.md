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

A comprehensive set of modules for integrating AI capabilities into your Orchard Core site, including:

- **[AI Services](ai/ai-services)** – Foundation for all AI modules with profile and connection management
- **[AI Chat](ai/ai-chat)** – Chat interfaces for interacting with AI models
- **[AI Agents](ai/ai-agent)** – Intelligent agents that perform tasks on your site
- **[Data Sources](ai/data-sources/)** – RAG/knowledge base indexing and vector search
- **[Documents](ai/documents/)** – Document upload, text extraction, and embedding
- **[MCP](ai/mcp/)** – Model Context Protocol client and server support

### AI Providers

Connect to various AI services:

- **[OpenAI](providers/openai)** – OpenAI-compatible providers (DeepSeek, Gemini, vLLM, etc.)
- **[Azure OpenAI](providers/azure-openai)** – Azure OpenAI integration
- **[Azure AI Inference](providers/azure-ai-inference)** – GitHub models via Azure AI
- **[Ollama](providers/ollama)** – Local model support

### Omnichannel Suite

Unified communication orchestration:

- **[Orchestrator](omnichannel/)** – Core orchestration services
- **[Management](omnichannel/management)** – Mini-CRM for contacts, campaigns, and activities
- **[SMS Automation](omnichannel/sms)** – AI-driven SMS automation
- **[Event Grid](omnichannel/event-grid)** – Azure Event Grid integration

### Standard Modules

Essential CMS enhancements:

- **[Users](modules/users)** – Enhanced user management
- **[SignalR](modules/signalr)** – Real-time communication
- **[Roles](modules/roles)** – Enhanced role management
- **[Content Access Control](modules/content-access-control)** – Role-based content restrictions
- **[Resources](modules/resources)** – Shared scripts and stylesheets
- **[Recipes](modules/recipes)** – JSON-Schema support for recipes

## Package Management

- **Orchard Core 2.1–2.3**: Use package version `1.2.x`
- **Orchard Core 3.0+**: Use version `2.0.0-beta` or newer

Stable releases are available on [NuGet.org](https://www.nuget.org/). Preview packages are available from the [CrestApps CloudSmith feed](https://cloudsmith.io/~crestapps/repos/crestapps-orchardcore).
