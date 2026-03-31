---
sidebar_label: MVC Example
sidebar_position: 20
title: MVC Example Application
description: Complete walkthrough of the CrestApps.Mvc.Web example application showing how to bootstrap a full AI-powered MVC application.
---

# MVC Example Application

> A complete walkthrough of the `CrestApps.Mvc.Web` example project that demonstrates every framework feature in a standard ASP.NET Core MVC application.

The source code is at `src/Startup/CrestApps.Mvc.Web/`. It serves as the canonical example for consuming the CrestApps AI Framework without Orchard Core.

## Application Structure

```text
CrestApps.Mvc.Web/
├── Program.cs                  ← Full startup configuration
├── BackgroundTasks/            ← Hosted services for maintenance
├── Controllers/                ← MVC controllers
├── Hubs/                       ← SignalR hubs for real-time chat
├── Indexes/                    ← YesSql index providers
├── Services/                   ← Application-specific service implementations
├── Tools/                      ← Custom AI tools
├── Views/                      ← Razor views
├── App_Data/                   ← Runtime data (DB, logs, documents, settings)
└── wwwroot/                    ← Static files
```

## Startup Configuration Walkthrough

The `Program.cs` file is organized into numbered sections. Here is what each section does:

### Section 1 — Logging

Configures NLog with daily log file rotation in `App_Data/logs/`. Replaceable with Serilog, Application Insights, or any logging provider.

### Section 2 — Application Configuration

Loads settings from JSON files in `App_Data/` with automatic reload-on-change:

| Service | Purpose |
|---------|---------|
| `JsonFileDeploymentDefaultsService` | Default AI deployment settings |
| `JsonFileInteractionDocumentSettingsService` | Chat document settings |
| `JsonFileAIDataSourceSettingsService` | Vector search configuration |
| `JsonFileMcpServerSettingsService` | MCP server authentication |

### Section 3 — Authentication & Authorization

Cookie-based authentication with an `"Admin"` authorization policy requiring the Administrator role.

### Section 4 — CrestApps AI Framework

The core framework registration chain:

```csharp
builder.Services
    .AddCrestAppsCoreServices()
    .AddCrestAppsAI()
    .AddOrchestrationServices()
    .AddChatInteractionHandlers()
    .AddDefaultDocumentProcessingServices()
    .AddCrestAppsA2AClient()
    .AddCrestAppsSignalR();
```

### Section 5 — AI Providers

Registers all supported AI providers:

```csharp
builder.Services
    .AddOpenAIProvider()
    .AddAzureOpenAIProvider()
    .AddOllamaProvider()
    .AddAzureAIInferenceProvider();
```

Plus application-specific provider configuration via `MvcAIProviderOptionsStore`.

### Section 6 — Data Sources

Registers vector search backends with configuration binding:

```csharp
builder.Services
    .AddElasticsearchDataSourceServices(
        builder.Configuration.GetSection("CrestApps:Search:Elasticsearch"))
    .AddAzureAISearchDataSourceServices(
        builder.Configuration.GetSection("CrestApps:Search:AzureAISearch"));
```

### Section 7 — Model Context Protocol (MCP)

Full bidirectional MCP setup:

- **Client**: `AddCrestAppsMcpClient()` for connecting to remote MCP servers
- **Server**: `AddCrestAppsMcpServer()` plus `MapMcpSse()` endpoint with handlers for tools, prompts, and resources

The MCP server endpoint at `/mcp` includes configurable authentication middleware supporting API key, cookie auth, and admin role checks.

### Section 8 — Custom AI Tools

Registers application-specific tools:

```csharp
builder.Services
    .AddAITool<CalculatorTool>(CalculatorTool.TheName)
        .WithTitle("Calculator")
        .WithDescription("Performs basic arithmetic.")
        .WithCategory("Utilities")
        .Selectable();

builder.Services
    .AddAITool<DataSourceSearchTool>(DataSourceSearchTool.TheName)
        .WithPurpose(AIToolPurposes.DataSourceSearch);
```

### Section 9 — Data Store (YesSql + SQLite)

Configures YesSql with SQLite for persistent storage:

- Creates the SQLite database in `App_Data/crestapps.db`
- Registers 17 index providers for all framework models
- Registers catalog services for each model type
- Sets up manager and store implementations

This entire section is replaceable with Entity Framework Core or another persistence layer.

### Section 10 — Background Tasks

Three hosted services for ongoing maintenance:

| Service | Purpose |
|---------|---------|
| `AIChatSessionCloseBackgroundService` | Closes idle/expired chat sessions |
| `DataSourceSyncBackgroundService` | Synchronizes vector search data |
| `DataSourceAlignmentBackgroundService` | Aligns indices after config changes |

### Section 11 — MVC & SignalR

Standard MVC controller and SignalR hub registration with camelCase JSON.

### Section 12 — Middleware Pipeline

The middleware pipeline includes:

1. Exception handling and HSTS
2. HTTPS redirection and static files
3. Routing, authentication, and authorization
4. MCP authentication middleware (conditional on `/mcp` path)
5. SignalR hub endpoints (`/hubs/ai-chat`, `/hubs/chat-interaction`)
6. MCP SSE endpoint (`/mcp`)
7. MVC route patterns

## Key Takeaways

1. **Each framework feature is a single extension method call** — compose only what you need
2. **Providers are independent** — register only the ones you use
3. **Storage is pluggable** — the YesSql section can be replaced entirely
4. **Configuration is file-based** — JSON files in `App_Data/` with reload-on-change
5. **MCP and A2A are optional** — add them only if you need protocol interop
6. **Background tasks handle maintenance** — no manual cleanup needed

## Running the Example

```bash
cd src/Startup/CrestApps.Mvc.Web
dotnet run
```

The application starts on `https://localhost:5001`. Configure AI provider connections in `App_Data/` JSON files before using AI features.
