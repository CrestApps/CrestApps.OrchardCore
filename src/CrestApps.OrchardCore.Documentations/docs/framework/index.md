---
sidebar_label: Overview
sidebar_position: 1
title: CrestApps AI Framework
description: A modular .NET framework for building AI-powered applications with support for multiple providers, orchestration, chat, MCP, A2A, and more.
---

# CrestApps AI Framework

The **CrestApps AI Framework** is a modular .NET library for building AI-powered applications. It provides a provider-agnostic abstraction over large language models, a pluggable orchestration pipeline, real-time chat, document processing, and protocol support for [MCP](https://modelcontextprotocol.io/) and [A2A](https://google.github.io/A2A/).

The framework ships as a set of NuGet packages that you compose via dependency injection — pick only the features you need.

:::tip Orchard Core Users
If you use **Orchard Core CMS**, the [AI Suite modules](../ai/index.md) wrap this framework with admin UI, recipes, deployment steps, and multi-tenant support. You do **not** need to call the extension methods shown here — the Orchard modules register them for you.
:::

## Quick Start

```csharp
builder.Services
    // Foundation
    .AddCrestAppsCoreServices()
    .AddCrestAppsAI()

    // Orchestration & chat
    .AddOrchestrationServices()
    .AddChatInteractionHandlers()
    .AddDefaultDocumentProcessingServices()

    // At least one AI provider
    .AddOpenAIProvider();
```

That is enough to resolve `IOrchestrator` or `IAICompletionService` and start sending prompts.

## Architecture

```text
┌──────────────────────────────────────────────────────────┐
│                    Your Application                      │
├──────────────────────────────────────────────────────────┤
│  Orchestration    Chat    MCP    A2A    SignalR    Tools  │
├──────────────────────────────────────────────────────────┤
│                  AI Core Services                        │
│   IAIClientFactory · IAICompletionService · Context      │
├──────────────────────────────────────────────────────────┤
│ Infrastructure: CrestApps.Core · CrestApps.Infrastructure    │
│ Abstractions: CrestApps.Infrastructure.Abstractions          │
│ Shared host-agnostic helpers, indexing contracts, constants  │
├──────────────────────────────────────────────────────────┤
│  Providers: OpenAI │ Azure OpenAI │ Ollama │ Azure AI    │
├──────────────────────────────────────────────────────────┤
│  Data Sources: Elasticsearch │ Azure AI Search           │
├──────────────────────────────────────────────────────────┤
│  Storage: YesSql (SQLite/SQL Server/PostgreSQL) or EF    │
└──────────────────────────────────────────────────────────┘
```

Each layer is an independent NuGet package. You only reference the packages you use.

## Feature Catalog

| Feature | Extension Method | Package | Description |
|---------|-----------------|---------|-------------|
| [Core Services](./core-services.md) | `AddCrestAppsCoreServices()` | `CrestApps.Core` | OData validation and shared utilities |
| Infrastructure abstractions | — | `CrestApps.Infrastructure.Abstractions` | Search, indexing, and data-source contracts consumed by provider packages without depending on the AI layer |
| Infrastructure helpers | — | `CrestApps.Infrastructure` | Shared non-AI constants and helpers consumed by higher layers |
| [AI Core](./ai-core.md) | `AddCrestAppsAI()` | `CrestApps.AI` | Completion clients, client factory, context building |
| [Orchestration](./orchestration.md) | `AddOrchestrationServices()` | `CrestApps.AI` | Tool management, orchestrator pipeline, RAG |
| [Chat](./chat.md) | `AddChatInteractionHandlers()` | `CrestApps.AI.Chat` | Chat sessions, response handlers, interaction history |
| [Document Processing](./document-processing.md) | `AddDefaultDocumentProcessingServices()` | `CrestApps.AI.Chat` | Document readers, semantic search, tabular data |
| [AI Templates](./ai-templates.md) | `AddTemplating()` | `CrestApps.Templates` | Liquid-based prompt templates |
| [Custom Tools](./tools.md) | `AddAITool<T>()` | `CrestApps.AI` | Register AI-callable functions |
| [AI Agents](./agents.md) | *via orchestration* | `CrestApps.AI` | Sub-agents as tools for task delegation |
| [GitHub Copilot](./copilot.md) | `AddCopilotOrchestrator()` | `CrestApps.AI.Copilot` | GitHub Copilot SDK orchestrator with OAuth and BYOK |
| [Response Handlers](./response-handlers.md) | *implement interface* | `CrestApps.AI` | Route chat to external systems |
| [Context Builders](./context-builders.md) | *implement interface* | `CrestApps.AI` | Enrich AI context per request |
| [SignalR](./signalr.md) | `AddCrestAppsSignalR()` | `CrestApps.SignalR` | Real-time hub route management |
| [Data Storage](./data-storage.md) | `AddDocumentCatalog<,>()` | `CrestApps.Data.YesSql` | Pluggable catalog/store pattern |
| [AI Documents](./ai-documents.md) | `AddDefaultDocumentProcessingServices()` | `CrestApps.AI.Chat` | Document upload, chunking, embedding, and RAG search |
| [AI Memory](./ai-memory.md) | *implement store* | `CrestApps.AI` | Persistent user-scoped memory with vector search |
| [Providers](./providers/index.md) | per-provider | various | OpenAI, Azure, Ollama, Azure AI Inference |
| [Data Sources](./data-sources/index.md) | per-backend | various | Elasticsearch, Azure AI Search |
| [MCP](./mcp/index.md) | `AddCrestAppsMcpClient()` / `AddCrestAppsMcpServer()` | `CrestApps.AI.Mcp` | Model Context Protocol client & server |
| [A2A](./a2a/index.md) | `AddCrestAppsA2AClient()` | `CrestApps.AI.A2A` | Agent-to-Agent protocol |
| [MVC Example](./mvc-example.md) | — | — | Full walkthrough of the example MVC application |

## Service Consumption Hierarchy

The framework exposes AI capabilities at multiple abstraction levels:

| Level | Service | Use When |
|-------|---------|----------|
| **High** | `IOrchestrator` | Full agentic loop — tool calls, RAG, streaming |
| **High** | `IOrchestrationContextBuilder` | Build orchestration context then call `IOrchestrator` |
| **Mid** | `IAICompletionService` | Single completion call with deployment resolution |
| **Mid** | `IAICompletionContextBuilder` | Build completion context with handler pipeline |
| **Low** | `IAIClientFactory` | Direct access to `IChatClient`, embedding, image, speech clients |

Most applications should use `IOrchestrator` or `IOrchestrationContextBuilder`. Drop to lower levels only when you need fine-grained control.

## NuGet Packages

| Package | Description |
|---------|-------------|
| `CrestApps.Core` | Foundation services |
| `CrestApps.Infrastructure.Abstractions` | Search, indexing, and data-source contracts for lower-level providers |
| `CrestApps.Infrastructure` | Shared non-AI infrastructure helpers and constants |
| `CrestApps.AI` | AI core + orchestration |
| `CrestApps.AI.Chat` | Chat interaction system |
| `CrestApps.Templates` | Template engine |
| `CrestApps.AI.Tools` | Tool abstractions |
| `CrestApps.AI.OpenAI` | OpenAI provider |
| `CrestApps.AI.OpenAI.Azure` | Azure OpenAI provider |
| `CrestApps.AI.Ollama` | Ollama provider |
| `CrestApps.AI.AzureAIInference` | Azure AI Inference / GitHub Models |
| `CrestApps.AI.Mcp` | MCP client & server |
| `CrestApps.AI.A2A` | A2A client |
| `CrestApps.AI.Copilot` | GitHub Copilot orchestrator |
| `CrestApps.Elasticsearch` | Elasticsearch vector search and indexing services |
| `CrestApps.Azure.AISearch` | Azure AI Search vector search and indexing services |
| `CrestApps.SignalR` | SignalR hub management |
| `CrestApps.Data.YesSql` | YesSql persistence catalogs |

## Next Steps

- **[AI Core](./ai-core.md)** — Understand the completion pipeline
- **[Orchestration](./orchestration.md)** — Build agentic workflows
- **[Providers](./providers/index.md)** — Connect to an LLM
- **[MVC Example](./mvc-example.md)** — See a complete working application
