# CrestApps AI Framework

A modular, framework-independent AI services library for ASP.NET Core applications. Provides AI orchestration, chat services, provider integrations, and more — consumable by **any ASP.NET Core application**.

## Overview

The CrestApps AI Framework is the foundation layer for building AI-powered applications in .NET. It includes:

- **AI Orchestration** — Tool execution, RAG retrieval, and streaming completions
- **Chat Services** — SignalR hubs for real-time AI conversations
- **Provider Integrations** — OpenAI, Azure OpenAI, Ollama, Azure AI Inference
- **MCP Support** — Model Context Protocol client and server
- **Pluggable Stores** — Default YesSql stores; bring your own ORM

## Quick Start

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCrestAppsCoreServices();
builder.Services.AddCrestAppsAI();
builder.Services.AddOrchestrationServices();
builder.Services.AddOpenAIProvider();
```

## Documentation

For full documentation, guides, and API reference, visit the **[CrestApps Documentation](https://crestapps.com/docs)**.

## License

MIT
