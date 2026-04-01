---
title: Framework Overview
sidebar_position: 1
---

# CrestApps AI Framework

The CrestApps AI Framework is a modular, framework-agnostic set of .NET libraries for building AI-powered applications. It provides abstractions, core services, and default implementations that can be consumed by **any ASP.NET Core application** — not just Orchard Core CMS.

## Key Features

- **AI Completion Services** — Unified API for interacting with OpenAI, Azure OpenAI, Azure AI Inference, and Ollama
- **AI Chat Sessions** — Full session management with conversation history, prompt storage, and streaming responses
- **AI Profiles** — Define reusable AI configurations (system prompts, parameters, capabilities)
- **Orchestration** — Pluggable orchestrators for controlling how AI requests are processed
- **SignalR Integration** — Real-time streaming chat via SignalR hubs
- **Data Sources** — Connect AI to external knowledge bases (Azure AI Search, Elasticsearch)
- **MCP Protocol** — Model Context Protocol client/server support
- **YesSql Storage** — Default document store implementation using YesSql (SQLite, PostgreSQL, SQL Server)

## Architecture Layers

The framework follows a **three-layer architecture**:

| Layer | Purpose | Example Projects |
|-------|---------|-----------------|
| **Framework** | Core abstractions and services, no UI dependency | `CrestApps.AI.Abstractions`, `CrestApps.AI.Core` |
| **Display Management** *(optional)* | Shape-based UI rendering | OrchardCore.DisplayManagement integration |
| **Application** | UI implementation (MVC, Blazor, Orchard Core) | `CrestApps.Mvc.Web`, OC Modules |

### Framework Layer Projects

```
src/Framework/
├── Abstractions/
│   ├── CrestApps.Abstractions          # Core interfaces (ICatalog, INamedEntity, etc.)
│   └── CrestApps.AI.Abstractions       # AI interfaces (IAICompletionService, IAIProfileManager, etc.)
├── Core/
│   ├── CrestApps.Core                  # Core service implementations
│   ├── CrestApps.AI.Core              # AI service implementations (DefaultOrchestrator, etc.)
│   ├── CrestApps.AI.Chat.Core         # Chat session management and SignalR hub base
│   ├── CrestApps.AI.Mcp.Core          # MCP (Model Context Protocol) client/server services
│   ├── CrestApps.AI.OpenAI.Core       # OpenAI provider
│   ├── CrestApps.AI.OpenAI.Azure.Core # Azure OpenAI provider
│   ├── CrestApps.AI.Ollama.Core       # Ollama (local LLM) provider
│   ├── CrestApps.AI.AzureAIInference.Core # Azure AI Inference / GitHub Models provider
│   ├── CrestApps.Azure.Core           # Azure utilities
│   └── CrestApps.SignalR.Core         # SignalR hub abstractions
├── Stores/
│   └── CrestApps.Data.YesSql          # YesSql document store implementation
└── Utilities/
    ├── CrestApps.Support              # General utilities
    └── CrestApps.Templates         # Prompt template engine
```

## Getting Started

See the [ASP.NET Core Integration Guide](./getting-started-aspnet) for step-by-step instructions on adding CrestApps AI to your application.
