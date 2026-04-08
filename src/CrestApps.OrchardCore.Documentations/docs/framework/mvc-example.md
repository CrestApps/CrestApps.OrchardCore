---
sidebar_label: MVC Example
sidebar_position: 20
title: MVC Example Application
description: Complete walkthrough of the CrestApps.Core.Mvc.Web example application showing how to bootstrap a full AI-powered MVC application.
---

:::info Canonical framework docs
The shared framework guidance now lives in **[CrestApps.Core](https://core.crestapps.com/docs/framework/mvc-example)**. This Orchard Core page is kept for Orchard-specific integration context and cross-links.
:::

# MVC Example Application

> A complete walkthrough of the `CrestApps.Core.Mvc.Web` example project that demonstrates every framework feature in a standard ASP.NET Core MVC application.

The source code is at `src/Startup/CrestApps.Core.Mvc.Web/`. It serves as the canonical example for consuming the CrestApps AI Framework without Orchard Core.

## Application Structure

```text
CrestApps.Core.Mvc.Web/
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

MVC admin forms also keep placeholder dropdown options in the Razor views instead of injecting fake empty `SelectListItem` entries from controllers. The option collections now contain only real persisted values, while the views render plain placeholders such as `Select provider`, `Use default orchestrator`, or `None` when an empty selection is allowed.

The MVC admin AI settings screen now exposes the shared `GeneralAISettings` values used by the framework runtime, including preemptive memory retrieval. It also now stores the site-wide Chat Interactions chat mode and resolves the default text-to-speech voice from the selected speech deployment instead of relying on free-form voice IDs. Profile editors also expose Orchard-style chat mode selection plus a deployment-driven voice picker, while the MVC AI Chat page, Chat Interactions page, and admin widget honor those chat mode settings for text input, microphone dictation, and conversation mode when speech deployments are configured. Speech input now prefers the browser's full locale (for example `en-US`) and the shared Azure Speech client normalizes neutral culture names such as `en` before sending speech requests. Conversation mode now also keeps the same start/end button treatment across MVC AI Chat and Chat Interactions, hides the Chat Interactions mic/send controls while live conversation mode is active, streams speech chunks more frequently to reduce transcription latency, and auto-dismisses the conversation-ended notice after a short delay.

The MVC sample now also reuses the same Orchard-style drag-and-drop knowledge upload treatment in the **AI Profile**, **Chat Interaction**, and **AI Template** editors. Profile-source templates can upload and remove template documents directly from the MVC admin UI, and those stored template documents are cloned into new AI Profiles when the template is applied during profile creation. The MVC Chat Interactions sidebar now validates number inputs such as **Strictness** against each field's `min`/`max` attributes before autosaving, marks invalid fields inline, and keeps the existing `Saved ✓` feedback when the save succeeds.

Because the MVC sample stores runtime state under `App_Data`, the project now excludes `App_Data/**` from `.NET 10` `dotnet watch` input discovery. That prevents Aspire and other watch-based local runs from restarting `MvcWeb` when uploads create files under `App_Data/Documents` or runtime services update logs and local data files.

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

The project file also excludes the broader `App_Data` folder from `dotnet watch` so watch-based local hosts do not mistake uploaded documents or SQLite/log writes for source changes and restart the app in the middle of a request.

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
    .AddMarkdownServices()

    .AddOrchestrationServices()

    .AddCopilotOrchestrator()

    .AddChatInteractionHandlers()

    .AddDefaultDocumentProcessingServices()
    .AddCrestAppsA2AClient()
    .AddCrestAppsMcpClient()
    .AddCrestAppsMcpServer()
    .AddFtpMcpResourceServices()
    .AddSftpMcpResourceServices()
    .AddCrestAppsSignalR();
```

`AddChatInteractionHandlers()` now registers the shared `DataSourceChatInteractionSettingsHandler`, so Chat Interactions persist the selected data source and RAG metadata through the framework settings pipeline instead of MVC-only wiring. The provider service blocks also pull in `AddDataSourceRagServices()`, which now registers both `DataSourceOrchestrationHandler` and `DataSourcePreemptiveRagHandler` at the framework level so source availability instructions and preemptive RAG stay aligned with the saved chat settings.

Documents, memory, and data sources now remain fully independent orchestration sources in the shared framework. Each source injects its own availability instructions and preemptive-RAG context, so the orchestrator can compose them together without the document prompts needing to know whether memory or data sources are also attached.

The MVC sample explicitly calls `AddMarkdownServices()` after `AddCrestAppsAI()`. That keeps Markdown-aware normalization opt-in at the host level instead of making `CrestApps.Core.AI` depend on the Markdig-backed package automatically.

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

`AddAzureOpenAIProvider()` also registers the `AzureSpeech` deployment provider used by MVC speech-to-text and text-to-speech selectors, so standalone Azure AI Services deployments from `CrestApps:AI:Deployments` participate in the same merged deployment catalog as UI-managed deployments.

The shared AI options pipeline now also reads connection definitions from `CrestApps:AI:Connections`, merges them with any UI-managed MVC connections, and exposes the combined set everywhere the runtime resolves provider connections. Each configured connection must provide a `Name` plus `ClientName`, and the MVC AI Deployment editor now uses that merged options source too, so appsettings-defined connections appear alongside admin-created connections when creating or editing deployments.

```json
{
  "CrestApps": {
    "AI": {
      "Connections": [
        {
          "Name": "primary",
          "ClientName": "OpenAI",
          "ApiKey": "YOUR_API_KEY",
          "DefaultDeploymentName": "gpt-4.1"
        }
      ]
    }
  }
}
```

Provider-grouped connection settings under `CrestApps:Providers:{ProviderName}:Connections:{ConnectionName}` still work too. The framework now keeps those provider-defined connections and the `CrestApps:AI:Connections` array in the same runtime options graph, then merges UI-managed MVC connections on top without duplicating host-specific merge code.

AI deployments now follow the same pattern. `CrestApps:AI:Deployments` can be defined either as a flat array of deployment records or as a provider-grouped object, and those configuration deployments are merged with the persisted deployment catalog for both MVC and Orchard Core read operations. Connection-scoped `Deployments` arrays nested under provider connections continue to work as well.

```json
{
  "CrestApps": {
    "AI": {
      "Deployments": [
        {
          "ClientName": "AzureSpeech",
          "Name": "whisper",
          "Type": "SpeechToText",
          "IsDefault": true,
          "Endpoint": "https://eastus.stt.speech.microsoft.com",
          "AuthenticationType": "ApiKey",
          "ApiKey": "YOUR_API_KEY"
        }
      ]
    }
  }
}
```

When a connection or deployment comes from system configuration, the MVC admin keeps it visible in separate read-only cards below the user-defined records and blocks edit/delete actions. Only records created through the UI remain editable there.

### Section 7 — Elasticsearch Services

Keeps the Elasticsearch registrations together so you can remove that provider by deleting a single block:

```csharp
builder.Services
    .AddElasticsearchServices(

        builder.Configuration.GetSection("CrestApps:Elasticsearch"))
    .AddElasticsearchAIDocumentSource()
    .AddElasticsearchAIDataSource()
    .AddElasticsearchAIMemorySource()
    .AddElasticsearchArticleSource();

```

When users create MVC index profiles, `AI Documents`, `AI Memory`, and `Data Source` profiles must select an embedding deployment, while `Articles` hides that selector entirely. That validation now runs through source-specific `IIndexProfileHandler` implementations registered by provider-owned extensions such as `AddElasticsearchAIDocumentSource()` and `AddAzureAISearchAIMemorySource()`, so each provider/type pair owns its own embedding requirements and field schema.

The MVC sample provisions the remote index during profile creation by resolving the keyed `ISearchIndexManager` for the selected provider, composing `IndexFullName` from the provider's configured `IndexPrefix` plus the user-entered index name, rejecting the create when that remote index already exists, and only persisting the local profile after the remote index is created successfully.

After creation, the MVC admin keeps the index name, provider, type, and embedding deployment immutable so the remote index contract cannot drift from the saved profile.

### Section 8 — Azure AI Search Services

Mirrors the Elasticsearch block so Azure AI Search is equally easy to enable or remove:

```csharp
builder.Services

    .AddAzureAISearchServices(

        builder.Configuration.GetSection("CrestApps:AzureAISearch"))

    .AddAzureAISearchAIDocumentSource()
    .AddAzureAISearchAIDataSource()
    .AddAzureAISearchAIMemorySource()
    .AddAzureAISearchArticleSource();
```

Deleting an MVC index profile now also deletes the remote Elasticsearch or Azure AI Search index through the keyed `ISearchIndexManager` registered for that provider, preventing orphaned indexes from lingering after the profile is removed. The same handler pipeline is reused for synchronization and type-specific validation so the controller stays focused on the Orchard-style CRUD flow.

If an administrator already deleted the remote index directly in Elasticsearch or Azure AI Search, the MVC app now still allows deleting the local index profile. The same local delete is also allowed when the stored profile no longer has a resolvable remote index name or the original provider registration is no longer available. The delete flow only blocks local removal when the remote index still exists and the provider fails to delete it.

`Articles` remains the only MVC-specific source registration. The sample app adds that descriptor directly in `Program.cs` and pairs it with `ArticleIndexProfileHandler`, because the article catalog and indexing logic belong only to the MVC sample rather than the reusable provider packages.

MVC chat responses now also collect and surface data-source citations the same way Orchard Core does. The sample host registers an `IAIReferenceLinkResolver` keyed to `IndexProfileTypes.Articles`, then both AI Chat and Chat Interactions use an MVC citation collector to resolve `[doc:N]` references into public `/articles/{id}` links while streaming and when existing chat history is reloaded. Citation links now keep their resolved labels/URLs after hydration and open in a new browser tab. Article management remains behind the admin policy, but the article display route itself is anonymous so end users can open citation links without signing in.

The MVC sample now also keeps article-backed knowledge bases synchronized the same way Orchard Core does. `AIDataSourceController` writes through `ICatalogManager<AIDataSource>` so lifecycle handlers run, article create/update/delete events queue knowledge-base reindex or removal work, and the background synchronization services now call the shared `IAIDataSourceIndexingService` instead of only logging placeholders. Each data source row in the admin list exposes a **Sync** action that queues a full rebuild immediately.

Strict in-scope prompting is now enforced more aggressively too. When `IsInScope` is enabled for a chat interaction or AI profile data-source RAG configuration, the shared strict prompt templates explicitly forbid both answering from general knowledge and offering a general-knowledge fallback, so the MVC host matches the Orchard Core behavior more closely.

The MVC admin chat widget now stays bound to the configured admin-chat profile instead of exposing a profile picker, restores its open/closed state and active session across page navigation, and reuses the stored session automatically when the next admin page loads. **Settings → AI Settings** now includes an **Admin widget** card where administrators choose that profile; leaving it empty disables the widget entirely. The same card also lets administrators change the widget accent color, which now defaults to the admin theme secondary color (`#6c757d`) instead of a hard-coded green. The widget now boots a real chat session immediately, so profiles with an **Initial prompt** show that assistant message first; otherwise it falls back to the welcome message and then **What do you want to know?** when no welcome text is configured.

The MVC sample also now records provider usage in a dedicated **AI Usage Analytics** report. The report groups tracked completion calls by authenticated username or **Anonymous**, then breaks usage down by completion client and resolved model/deployment while showing total calls, distinct sessions, distinct chat interactions, token totals, and average latency. Session analytics now keep token totals and user-visible response latency separate so the main chat analytics page still shows per-session performance while the usage report captures provider activity more directly.

`Program.cs` also registers a sample `sendEmail` AI function in the MVC host. The sample tool does not deliver real mail — it logs the requested recipient, subject, and message so you can see how host-specific tools plug into the shared framework.

### Section 9 — Model Context Protocol (MCP)

Full bidirectional MCP setup:

- **Client**: `AddCrestAppsMcpClient()` for connecting to remote MCP servers

- **Server**: `AddCrestAppsMcpServer()` plus `AddFtpMcpResourceServices()`, `AddSftpMcpResourceServices()`, and `MapMcp("mcp")` / `AddMcpServer(...)` handlers for tools, prompts, and resources

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

## Validation Feedback

The shared MVC layout now renders a Bootstrap validation summary whenever a request returns with invalid `ModelState`, so CRUD pages consistently show server-side validation errors even when the controller adds them dynamically. The same shared layout also surfaces `TempData["ErrorMessage"]` as a top-level alert for redirected error flows such as a failed remote index delete.

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
| `AIChatSessionCloseBackgroundService` | Runs every 5 minutes to close idle/expired chat sessions and trigger post-session/reporting work |
| `DataSourceSyncBackgroundService` | Synchronizes vector search data |
| `DataSourceAlignmentBackgroundService` | Aligns indices after config changes |

The AI chat close service now also keeps the MVC chat analytics and extracted-data reports aligned with closed sessions, while the data-source hosted services treat timer cancellation as a normal shutdown path and the alignment service safely handles an empty data-source store instead of dereferencing a null collection during a periodic run.






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
cd src/Startup/CrestApps.Core.Mvc.Web
dotnet run
```

The application starts on `https://localhost:5001`. Configure AI provider connections in `App_Data/appsettings.json` before using AI features.
