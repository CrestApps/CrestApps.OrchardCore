---
sidebar_label: Getting Started
sidebar_position: 2
title: Getting Started
description: How to get started with the CrestApps AI Framework or CrestApps Orchard Core modules.
---

# Getting Started

Choose the path that matches your project:

| Path | Description |
|------|-------------|
| **[Framework (any ASP.NET Core app)](#framework-quick-start)** | Use the AI framework in a standard MVC, Razor Pages, or Minimal API application |
| **[Orchard Core modules](#orchard-core-quick-start)** | Add AI capabilities to an existing Orchard Core CMS site |

## Framework Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later

### 1. Add NuGet Packages

```bash
dotnet add package CrestApps.AI
dotnet add package CrestApps.AI.Chat
dotnet add package CrestApps.AI.Markdown
dotnet add package CrestApps.AI.OpenAI        # or another provider
dotnet add package CrestApps.Data.YesSql       # or your preferred storage
```

### 2. Register Services

```csharp
builder.Services
    .AddCrestAppsCoreServices()
    .AddCrestAppsAI()
    .AddMarkdownServices()
    .AddOrchestrationServices()
    .AddChatInteractionHandlers()
    .AddDefaultDocumentProcessingServices()
    .AddOpenAIProvider();
```

### 3. Use the Orchestrator

```csharp
app.MapPost("/chat", async (IOrchestrator orchestrator, string message) =>
{
    // Build context and stream response
    // See the Framework documentation for complete examples
});
```

For a complete working example, see the [MVC Example walkthrough](framework/mvc-example.md) and the full [Framework documentation](framework/).

---

## Orchard Core Quick Start

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (version matching your Orchard Core target)
- An existing Orchard Core application, or use the CrestApps starter project

## Installation

### Install All Modules

Add the `CrestApps.OrchardCore.Cms.Core.Targets` package to include all modules at once:

```bash
dotnet add package CrestApps.OrchardCore.Cms.Core.Targets
```

### Install Individual Modules

Or install only the modules you need:

```bash
dotnet add package CrestApps.OrchardCore.AI
dotnet add package CrestApps.OrchardCore.AI.Chat
# ... add other modules as needed
```

## Running Locally

1. **Clone the Repository:**
   ```bash
   git clone https://github.com/CrestApps/CrestApps.OrchardCore.git
   ```

2. **Navigate to the Project Directory:**
   ```bash
   cd CrestApps.OrchardCore
   ```

3. **Install Frontend Dependencies and Rebuild Assets:**
   ```bash
   npm install
   npm run rebuild
   ```

   The Gulp asset pipeline emits both the standard and minified frontend outputs into each module's `wwwroot` folder, such as `ai-chat.js` and `ai-chat.min.js`.

4. **Build the Solution:**
   ```bash
   dotnet build
   ```

5. **Launch the Application:**
   ```bash
   dotnet run
   ```

6. **Enable Modules:**
   Access the **Orchard Core Admin Dashboard** to enable desired CrestApps modules.

## Package Feeds

### Production Packages

Stable releases are available on [NuGet.org](https://www.nuget.org/).

### Preview Packages

[![Hosted By: Cloudsmith](https://img.shields.io/badge/OSS%20hosting%20by-cloudsmith-blue?logo=cloudsmith&style=for-the-badge)](https://cloudsmith.com)

For the latest updates and preview packages, visit the [Cloudsmith CrestApps OrchardCore repository](https://cloudsmith.io/~crestapps/repos/crestapps-orchardcore).

### Adding the Preview Feed

#### In Visual Studio

1. Open **NuGet Package Manager Settings** (under *Tools*).
2. Add a new package source:
   - **Name:** `CrestAppsPreview`
   - **URL:** `https://nuget.cloudsmith.io/crestapps/crestapps-orchardcore/v3/index.json`

#### Via NuGet.config

Update your **NuGet.config** file:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="NuGet" value="https://api.nuget.org/v3/index.json" />
    <add key="CrestAppsPreview" value="https://nuget.cloudsmith.io/crestapps/crestapps-orchardcore/v3/index.json" />
  </packageSources>
  <disabledPackageSources />
</configuration>
```

## Contributing

We welcome contributions from the community! To contribute:

1. **Fork the repository.**
2. **Create a new branch** for your feature or bug fix.
3. **Make your changes** and commit them with clear messages.
4. **Push your changes** to your fork.
5. **Submit a pull request** to the main repository.

## License

CrestApps is licensed under the **MIT License**. See the [LICENSE](https://opensource.org/licenses/MIT) file for more details.
