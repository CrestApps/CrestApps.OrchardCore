---
title: ASP.NET Core Integration
sidebar_position: 3
---

# Getting Started with ASP.NET Core

This guide shows how to add CrestApps AI services to any ASP.NET Core application (MVC, Razor Pages, Blazor, Minimal APIs, etc.).

## Prerequisites

- .NET 10.0 SDK or later
- An AI provider API key (OpenAI, Azure OpenAI, Azure AI Inference, or a local Ollama instance)

## 1. Add NuGet Packages

Add the framework packages your application needs:

```xml
<!-- Required: Core AI services -->
<PackageReference Include="CrestApps.AI.Core" />

<!-- Required: Orchestration (DefaultOrchestrator, tool execution) -->
<!-- Already included transitively by CrestApps.AI.Core -->

<!-- Optional: OpenAI provider -->
<PackageReference Include="CrestApps.AI.OpenAI.Core" />

<!-- Optional: Azure OpenAI provider -->
<PackageReference Include="CrestApps.AI.OpenAI.Azure.Core" />

<!-- Optional: Real-time chat via SignalR -->
<PackageReference Include="CrestApps.SignalR.Core" />

<!-- Optional: YesSql document store (for profile/connection/session persistence) -->
<PackageReference Include="CrestApps.Data.YesSql" />

<!-- Optional: Chat session management -->
<PackageReference Include="CrestApps.AI.Chat.Core" />
```

## 2. Register Services

In your `Program.cs`, register the CrestApps AI services:

```csharp
using CrestApps.AI;
using CrestApps.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Step 1: Add core CrestApps services (ICatalog, IODataValidator, etc.)
builder.Services.AddCrestAppsCoreServices();

// Step 2: Add AI services (IAICompletionService, IAIClientFactory)
builder.Services.AddCrestAppsAI();

// Step 3: Add orchestration (DefaultOrchestrator, tool registry)
builder.Services.AddOrchestrationServices();

// Step 4 (optional): Add SignalR for real-time streaming chat
builder.Services.AddCrestAppsSignalR();
builder.Services.AddSignalR();

// Step 5: Register AI providers via appsettings.json
builder.Services.Configure<AIProviderOptions>(
    builder.Configuration.GetSection("CrestApps:AI:Providers"));
```

## 3. Configure AI Providers

Add provider configuration to `appsettings.json`:

```json
{
  "CrestApps": {
    "AI": {
      "Providers": {
        "Connections": [
          {
            "Name": "my-openai",
            "ProviderName": "OpenAI",
            "DefaultDeploymentName": "gpt-4o",
            "ApiKey": "sk-..."
          }
        ]
      }
    }
  }
}
```

### Provider Configuration Options

#### OpenAI
```json
{
  "Name": "my-openai",
  "ProviderName": "OpenAI",
  "DefaultDeploymentName": "gpt-4o",
  "ApiKey": "sk-..."
}
```

#### Azure OpenAI
```json
{
  "Name": "my-azure",
  "ProviderName": "Azure",
  "DefaultDeploymentName": "gpt-4o",
  "Endpoint": "https://your-resource.openai.azure.com/",
  "ApiKey": "..."
}
```

#### Azure AI Inference (GitHub Models)
```json
{
  "Name": "my-inference",
  "ProviderName": "AzureAIInference",
  "DefaultDeploymentName": "gpt-4o",
  "Endpoint": "https://models.inference.ai.azure.com",
  "ApiKey": "..."
}
```

#### Ollama (Local)
```json
{
  "Name": "my-ollama",
  "ProviderName": "Ollama",
  "DefaultDeploymentName": "llama3.2",
  "Endpoint": "http://localhost:11434"
}
```

## 4. Data Persistence with YesSql

If you want to persist AI profiles, connections, and chat sessions, use the YesSql store:

```csharp
using CrestApps.AI.Models;
using CrestApps.Data.YesSql;
using YesSql;
using YesSql.Provider.Sqlite;

// Configure YesSql with SQLite
builder.Services.AddSingleton(sp =>
{
    var dbPath = Path.Combine(
        builder.Environment.ContentRootPath, "App_Data", "crestapps.db");
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

    var store = StoreFactory.CreateAndInitializeAsync(
        new Configuration()
            .UseSqLite($"Data Source={dbPath};Cache=Shared")
            .SetTablePrefix("CA_")
    ).GetAwaiter().GetResult();

    // Register indexes
    store.RegisterIndexes<AIProfileIndexProvider>();
    store.RegisterIndexes<AIProviderConnectionIndexProvider>();
    store.RegisterIndexes<AIDeploymentIndexProvider>();

    return store;
});

builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IStore>().CreateSession());

// Register YesSql-based catalogs
builder.Services.AddNamedSourceDocumentCatalog<AIProfile, AIProfileIndex>();
builder.Services.AddNamedSourceDocumentCatalog<AIProviderConnection, AIProviderConnectionIndex>();
builder.Services.AddNamedSourceDocumentCatalog<AIDeployment, AIDeploymentIndex>();
```

:::tip
YesSql supports SQLite, PostgreSQL, SQL Server, and MySQL. Simply swap the provider:
```csharp
.UsePostgreSql("Host=localhost;Database=mydb;...")
.UseSqlServer("Server=localhost;Database=mydb;...")
```
:::

## 5. Real-Time Chat with SignalR

### Create a SignalR Hub

```csharp
using System.Threading.Channels;
using CrestApps.AI;
using CrestApps.AI.Chat.Hubs;
using CrestApps.AI.Chat.Models;
using Microsoft.AspNetCore.SignalR;

public class AIChatHub : Hub<IAIChatHubClient>
{
    private readonly ICatalog<AIProfile> _profiles;
    private readonly IOrchestratorResolver _orchestratorResolver;
    private readonly IOrchestrationContextBuilder _contextBuilder;

    public AIChatHub(
        ICatalog<AIProfile> profiles,
        IOrchestratorResolver orchestratorResolver,
        IOrchestrationContextBuilder contextBuilder)
    {
        _profiles = profiles;
        _orchestratorResolver = orchestratorResolver;
        _contextBuilder = contextBuilder;
    }

    public ChannelReader<CompletionPartialMessage> SendMessage(
        string sessionId, string message, string[] fileNames)
    {
        var channel = Channel.CreateUnbounded<CompletionPartialMessage>();

        _ = Task.Run(async () =>
        {
            // 1. Resolve profile and orchestrator
            // 2. Build conversation context
            // 3. Stream response chunks to channel
            // 4. Save prompts to store
            channel.Writer.Complete();
        });

        return channel.Reader;
    }
}
```

### Map the Hub

```csharp
var app = builder.Build();
app.MapHub<AIChatHub>("/hubs/ai-chat");
```

### Connect from JavaScript

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/ai-chat')
    .withAutomaticReconnect()
    .build();

await connection.start();

// Stream a message
connection.stream('SendMessage', sessionId, 'Hello!', [])
    .subscribe({
        next: (chunk) => console.log(chunk.content),
        complete: () => console.log('Done'),
        error: (err) => console.error(err),
    });
```

## 6. Using the AI Completion Service Directly

For simpler use cases without SignalR, inject `IAICompletionService` directly:

```csharp
public class MyService
{
    private readonly IAICompletionService _aiService;

    public MyService(IAICompletionService aiService)
    {
        _aiService = aiService;
    }

    public async Task<string> GetResponseAsync(string prompt)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, prompt)
        };

        var response = await _aiService.CompleteAsync(
            connectionName: "my-openai",
            deploymentName: "gpt-4o",
            messages);

        return response?.Text ?? "No response";
    }
}
```

## 7. Frontend Chat Widget

The framework includes a ready-to-use chat widget (`chat-widget.js` + `chat-widget.css`) that provides a floating chat interface with:

- Profile selection dropdown
- SignalR streaming with markdown rendering
- Auto-growing input textarea
- Responsive design (mobile-friendly)

Include it in your layout:

```html
<link rel="stylesheet" href="~/css/chat-widget.css" />
<script src="~/js/chat-widget.js"></script>
<script>
    CrestAppsChatWidget.initialize({
        hubUrl: '/hubs/ai-chat',
        profiles: [
            { id: 'profile-id', displayText: 'My AI Assistant', welcomeMessage: 'Hello!' }
        ]
    });
</script>
```

## Complete Example

See the `CrestApps.Mvc.Web` project in the repository for a fully working standalone MVC application that demonstrates all of the above, including:

- Admin UI for managing AI profiles, connections, and deployments
- Full chat interface with SignalR streaming
- Floating chat widget
- YesSql with SQLite persistence
- Cookie-based authentication
