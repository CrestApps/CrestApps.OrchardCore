---
title: Architecture & Dependencies
sidebar_position: 2
---

# Architecture & Dependency Diagram

This page describes the project architecture and how the various layers depend on each other.

## Dependency Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Application Layer                            │
│                                                                     │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐  │
│  │  CrestApps.Core.Mvc   │  │  Orchard Core    │  │  Blazor / Other  │  │
│  │  .Web (MVC App)  │  │  Modules         │  │  (Future)        │  │
│  └────────┬─────────┘  └────────┬─────────┘  └────────┬─────────┘  │
│           │                     │                      │            │
└───────────┼─────────────────────┼──────────────────────┼────────────┘
            │                     │                      │
            ▼                     ▼                      ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Optional Middle Layer                             │
│                                                                     │
│  ┌──────────────────────────┐  ┌──────────────────────────────┐    │
│  │  CrestApps.Core.Data.YesSql  │  │  OrchardCore.DisplayManagement│    │
│  │  (Document Store)        │  │  (Shape-based UI)             │    │
│  └────────────┬─────────────┘  └──────────────┬───────────────┘    │
│               │                                │                    │
└───────────────┼────────────────────────────────┼────────────────────┘
                │                                │
                ▼                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│                       Framework Layer                               │
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                        Core Projects                         │   │
│  │                                                              │   │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐  │   │
│  │  │ CrestApps    │  │ CrestApps    │  │ CrestApps.Core.AI     │  │   │
│  │  │ .AI.Core     │  │ .AI.Chat     │  │ .OpenAI.Core     │  │   │
│  │  │              │  │ .Core        │  │                   │  │   │
│  │  └──────┬───────┘  └──────┬───────┘  └────────┬─────────┘  │   │
│  │         │                 │                    │             │   │
│  │  ┌──────┴───────┐  ┌─────┴────────┐  ┌───────┴─────────┐  │   │
│  │  │ CrestApps    │  │ CrestApps    │  │ CrestApps.Core.AI    │  │   │
│  │  │ .Core        │  │ .SignalR     │  │ .OpenAI.Azure   │  │   │
│  │  │              │  │ .Core        │  │ .Core           │  │   │
│  │  └──────┬───────┘  └──────┬───────┘  └────────┬────────┘  │   │
│  │         │                 │                    │            │   │
│  │  ┌──────┴───────┐  ┌─────┴────────┐  ┌───────┴─────────┐  │   │
│  │  │ CrestApps.Core.AI │  │ CrestApps.Core.AI │  │ CrestApps.Core.AI    │  │   │
│  │  │ .Ollama.Core │  │ .AzureAI     │  │ .Mcp.Core       │  │   │
│  │  │              │  │ Inference    │  │                   │  │   │
│  │  │              │  │ .Core        │  │                   │  │   │
│  │  └──────┬───────┘  └──────┬───────┘  └────────┬────────┘  │   │
│  │         │                 │                    │            │   │
│  │  ┌──────┴───────┐  ┌─────┴────────┐  ┌───────┴─────────┐  │   │
│  │  │ CrestApps.Core.AI │  │ CrestApps.Core.AI │  │ CrestApps.Core.AI    │  │   │
│  │  │ .Chat        │  │ .DataSources │  │ .DataSources    │  │   │
│  │  │ .Copilot     │  │ .AzureAI     │  │ .Elasticsearch  │  │   │
│  │  └──────────────┘  └──────────────┘  └─────────────────┘  │   │
│  │                                                              │   │
│  └─────────┼─────────────────┼────────────────────┼────────────┘   │
│            │                 │                    │                 │
│  ┌─────────┴─────────────────┴────────────────────┴────────────┐   │
│  │                    Abstractions                               │   │
│  │                                                              │   │
│  │  ┌──────────────────┐  ┌────────────────────────────────┐   │   │
│  │  │ CrestApps        │  │ CrestApps.Core.AI.Abstractions      │   │   │
│  │  │ .Abstractions    │  │ (IAICompletionService,          │   │   │
│  │  │ (ICatalog,       │  │  IAIProfileManager,             │   │   │
│  │  │  INamedEntity)   │  │  IOrchestrator, etc.)           │   │   │
│  │  └──────────────────┘  └────────────────────────────────┘   │   │
│  │                                                              │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                      Resources                                │   │
│  │  ┌──────────────────────────────────────────────────────┐     │   │
│  │  │ CrestApps.Core.AI.Resources (shared JS: ai-chat,          │     │   │
│  │  │  chat-interaction)                                    │     │   │
│  │  └──────────────────────────────────────────────────────┘     │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                      Utilities                                │   │
│  │  ┌────────────────┐  ┌──────────────────────┐                │   │
│  │  │ CrestApps      │  │ CrestApps.Core.AI         │                │   │
│  │  │ .Support       │  │ .Prompting           │                │   │
│  │  └────────────────┘  └──────────────────────┘                │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

## Layer Descriptions

### Framework Layer (Top Level)

The **Framework Layer** contains all abstractions, core services, and provider implementations. These projects have **no dependency on Orchard Core** and can be used by any .NET 10+ application.

| Project | Role |
|---------|------|
| `CrestApps.Core.Abstractions` | Core interfaces: `ICatalog<T>`, `INamedEntity`, `ExtensibleEntity`, `IODataValidator` |
| `CrestApps.Core.AI.Abstractions` | AI interfaces: `IAICompletionService`, `IAIProfileManager`, `IOrchestrator`, models |
| `CrestApps.Core` | Default implementations of core abstractions, `IServiceCollection` extensions |
| `CrestApps.Core.AI.Core` | AI orchestration, `DefaultOrchestrator`, tool execution, completion services |
| `CrestApps.Core.AI.Chat.Core` | Chat session management, prompt storage, `IAIChatSessionManager` |
| `CrestApps.Core.AI.OpenAI.Core` | OpenAI provider (`ChatClient`, streaming, tool calls) |
| `CrestApps.Core.AI.OpenAI.Azure.Core` | Azure OpenAI provider with data source integration |
| `CrestApps.Core.AI.Ollama.Core` | Ollama provider for locally hosted LLMs |
| `CrestApps.Core.AI.AzureAIInference.Core` | Azure AI Inference / GitHub Models provider |
| `CrestApps.Core.AI.Copilot` | GitHub Copilot chat orchestration, OAuth flow, credential management |
| `CrestApps.Core.Azure.AISearch` | Azure AI Search provider integration for indexing, document management, vector search, and OData filters |
| `CrestApps.Core.Elasticsearch` | Elasticsearch provider integration for indexing, document management, vector search, and query/filter translation |
| `CrestApps.Core.AI.Mcp.Core` | Model Context Protocol (MCP) client and server |
| `CrestApps.Core.Azure.Core` | Azure-specific utilities (data protection, connection settings) |
| `CrestApps.Core.SignalR.Core` | SignalR hub abstractions for real-time AI chat |
| `CrestApps.Core.Support` | General utility classes |
| `CrestApps.Core.AI.Resources` | Shared frontend JavaScript resources for AI chat UI |
| `CrestApps.Core.Templates` | Prompt template engine |

### Optional Middle Layer

| Project | Role |
|---------|------|
| `CrestApps.Core.Data.YesSql` | YesSql-based document catalog implementation (SQLite, PostgreSQL, SQL Server) |
| OrchardCore.DisplayManagement | Shape-based UI rendering (optional, for apps that want shape-driven UI) |

### Application Layer

| Project | Role |
|---------|------|
| `CrestApps.Core.Mvc.Web` | Standalone ASP.NET Core MVC application with full admin UI |
| Orchard Core Modules | CMS modules providing feature-gated UI, recipes, deployments, workflows |
| Blazor / Other | Future: Blazor Server/WASM, minimal APIs, etc. |

## Data Flow

```
User → UI (MVC/Blazor/OC) → SignalR Hub → Orchestrator → AI Provider → LLM
                                  ↓                              ↑
                          Session Manager ←──── Prompt Store ────┘
                                  ↓
                          YesSql / Custom Store
```

1. **User** sends a message via the UI (browser)
2. **SignalR Hub** receives the message and resolves the AI profile
3. **Orchestrator** builds the conversation context (system prompt, history, tools)
4. **AI Provider** (OpenAI, Azure, Ollama) streams the response
5. **Prompt Store** persists both user and assistant messages
6. **SignalR Hub** streams response chunks back to the client

## Extensibility Points

| Interface | Purpose | Default Implementation |
|-----------|---------|----------------------|
| `IAICompletionService` | AI provider abstraction | OpenAI, Azure OpenAI, Ollama |
| `IOrchestrator` | Controls AI request pipeline | `DefaultOrchestrator` |
| `ICatalog<T>` | CRUD for named entities | `NamedSourceDocumentCatalog<T>` (YesSql) |
| `IAIProfileManager` | Profile CRUD | Module-specific implementations |
| `IAIChatSessionManager` | Session lifecycle | YesSql-based implementation |
| `IAIChatSessionPromptStore` | Prompt persistence | YesSql-based implementation |
| `ICatalogEntryHandler<T>` | Entity lifecycle hooks | Per-provider handlers |
