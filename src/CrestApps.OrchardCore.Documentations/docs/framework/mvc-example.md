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
├── Areas/
│   ├── Admin/                  ← Settings, articles, and shared admin-only pages
│   ├── AI/                     ← AI connections, deployments, profiles, templates
│   ├── AIChat/                 ← AI chat sessions and Copilot auth
│   ├── ChatInteractions/       ← Interactive chat workflows
│   ├── A2A/                    ← A2A host management
│   ├── Mcp/                    ← MCP hosts, prompts, and resources
│   ├── DataSources/            ← Data source CRUD and storage
│   └── Indexing/               ← Index profiles and AI document indexing
├── BackgroundTasks/            ← Hosted services for maintenance
├── Controllers/                ← Non-area MVC controllers such as Home and Account
├── Hubs/                       ← SignalR hubs for real-time chat
├── Indexes/                    ← YesSql index providers
├── Tools/                      ← Custom AI tools
├── Views/                      ← Non-area Razor views
├── App_Data/                   ← Runtime data (DB, logs, documents, settings)
└── wwwroot/                    ← Static files
```

The sample now keeps feature-specific controllers, Razor views, and related MVC-only services or models close to the owning area instead of accumulating under a single `Areas/Admin` catch-all folder.

## Startup Configuration Walkthrough

The `Program.cs` file is organized into numbered sections. Here is what each section does:

### Section 1 — Logging

Configures NLog with daily log file rotation in `App_Data/logs/`. Replaceable with Serilog, Application Insights, or any logging provider.

### Section 2 — Application Configuration

Loads settings from the normal appsettings chain plus `App_Data/appsettings.json` as the highest-priority local override file with automatic reload-on-change:

| Service | Purpose |
|---------|---------|
| `App_Data/appsettings.json` | Local machine overrides for admin-managed AI, MCP, Copilot, and pagination settings |
| `AppDataConfigurationFileService` | Writes admin-managed sections back into the same `App_Data/appsettings.json` file that ASP.NET Core watches, so changes persist and reload through `IConfiguration` without a restart |
| `AppDataSettingsService<T>` | Reads merged `IConfiguration` and persists nested sections back into `App_Data/appsettings.json` through `AppDataConfigurationFileService` |

### Section 3 — ASP.NET Core MVC Setup

Registers the standard host services first so the file starts with familiar ASP.NET Core concerns before any CrestApps-specific registrations:

- `AddLocalization()`
- `AddControllersWithViews()`
- `AddSignalR()` with camelCase JSON payloads

### Section 4 — Authentication & Authorization

Cookie-based authentication with an `"Admin"` authorization policy requiring the Administrator role.

The MVC admin Copilot settings follow the same configured/not-configured behavior as the Orchard Core host: leaving Copilot at `NotConfigured` keeps the Copilot orchestrator unavailable until the required OAuth or BYOK fields are completed.

That same Copilot status now flows through the MVC AI Profile, AI Profile Template, and Chat Interaction editors as well. When admins choose the Copilot orchestrator, the sample app shows the same not-configured warning, GitHub sign-in prompt, connected-as status, and Copilot model picker behavior that Orchard Core uses.

### Section 5 — CrestApps Foundation + AI Services

The core framework registration chain:

```csharp
builder.Services
    .AddCrestAppsCoreServices()
    .AddCrestAppsAI()
    .AddOrchestrationServices()
    .AddCopilotOrchestrator()
    .AddChatInteractionHandlers()
    .AddDefaultDocumentProcessingServices()
    .AddCrestAppsA2AClient()
    .AddCrestAppsMcpClient()
    .AddCrestAppsMcpServer()
    .AddCrestAppsSignalR();
```

### Section 6 — AI Providers

Registers all supported AI providers:

```csharp
builder.Services
    .AddOpenAIProvider()
    .AddAzureOpenAIProvider()
    .AddOllamaProvider()
    .AddAzureAIInferenceProvider();
```

Plus application-specific provider configuration via `MvcAIProviderOptionsStore`.

### Section 7 — Elasticsearch Services

Keeps the Elasticsearch registrations together so you can remove that provider by deleting a single block:

```csharp
builder.Services
    .AddElasticsearchServices(
        builder.Configuration.GetSection("CrestApps:Elasticsearch"))
    .AddElasticsearchDataSource(IndexProfileTypes.AIDocuments, ...)
    .AddElasticsearchDataSource(IndexProfileTypes.DataSource, ...)
    .AddElasticsearchDataSource(IndexProfileTypes.AIMemory, ...)
    .AddElasticsearchDataSource(IndexProfileTypes.Articles, ...);
```

### Section 8 — Azure AI Search Services

Mirrors the Elasticsearch block so Azure AI Search is equally easy to enable or remove:

```csharp
builder.Services
    .AddAzureAISearchServices(
        builder.Configuration.GetSection("CrestApps:AzureAISearch"))
    .AddAzureAISearchDataSource(IndexProfileTypes.AIDocuments, ...)
    .AddAzureAISearchDataSource(IndexProfileTypes.DataSource, ...)
    .AddAzureAISearchDataSource(IndexProfileTypes.AIMemory, ...)
    .AddAzureAISearchDataSource(IndexProfileTypes.Articles, ...);
```

### Section 9 — Model Context Protocol (MCP)

Full bidirectional MCP setup:

- **Client**: `AddCrestAppsMcpClient()` for connecting to remote MCP servers
- **Server**: `AddCrestAppsMcpServer()` plus `MapMcp("mcp")` and `AddMcpServer(...)` handlers for tools, prompts, and resources

The MCP server endpoint at `/mcp` includes configurable authentication middleware supporting API key, cookie auth, and admin role checks.

### Section 10 — Custom AI Tools

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

### Section 11 — Data Store (YesSql + SQLite)

Configures YesSql with SQLite for persistent storage:

- Creates the SQLite database in `App_Data/crestapps.db`
- Registers 17 index providers for all framework models
- Registers catalog services for each model type
- Sets up manager and store implementations

This entire section is replaceable with Entity Framework Core or another persistence layer.

## Area Layout

The sample admin UI is intentionally split by feature so each area is easy to remove or understand in isolation:

| Area | Responsibility |
|------|----------------|
| `Admin` | Shared settings, articles, and general admin navigation |
| `AI` | AI connections, deployments, profiles, and templates |
| `AIChat` | Session-based AI chat plus Copilot OAuth callbacks |
| `ChatInteractions` | Long-lived interactive chat experiences |
| `A2A` | A2A host connections |
| `Mcp` | MCP host connections, prompts, and resources |
| `DataSources` | AI data sources and their MVC-specific store implementation |
| `Indexing` | Search index profiles, AI documents, and MVC indexing services |

### Section 12 — Background Tasks

Three hosted services for ongoing maintenance:

| Service | Purpose |
|---------|---------|
| `AIChatSessionCloseBackgroundService` | Closes idle/expired chat sessions |
| `DataSourceSyncBackgroundService` | Synchronizes vector search data |
| `DataSourceAlignmentBackgroundService` | Aligns indices after config changes |

### Section 13 — Middleware Pipeline

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
4. **Configuration supports local overrides** — `App_Data/appsettings.json` sits on top of `appsettings.json` and `appsettings.{Environment}.json`
5. **MCP and A2A are optional** — add them only if you need protocol interop
6. **Background tasks handle maintenance** — no manual cleanup needed

## Running the Example

```bash
cd src/Startup/CrestApps.Mvc.Web
dotnet run
```

The application starts on `https://localhost:5001`. Configure AI provider connections in `App_Data/appsettings.json` before using AI features.
