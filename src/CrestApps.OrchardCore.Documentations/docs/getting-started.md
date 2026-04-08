---
sidebar_label: Getting Started
sidebar_position: 2
title: Getting Started
description: Choose between the standalone CrestApps.Core framework and the Orchard Core modules.
---

# Getting Started

Choose the path that matches your project:

| Path | Description |
|------|-------------|
| **[CrestApps.Core framework](https://core.crestapps.com/docs)** | Use the shared AI framework in MVC, Razor Pages, Blazor, Minimal APIs, or MAUI hybrid apps |
| **[Orchard Core modules](#orchard-core-quick-start)** | Add AI, omnichannel, and related capabilities to an Orchard Core CMS site |

## Framework quick start

The standalone framework is now documented in the `CrestApps.Core` site:

- **[Framework overview](https://core.crestapps.com/docs/framework)** — package layout, capabilities, and extension methods
- **[ASP.NET Core integration](https://core.crestapps.com/docs/framework/getting-started-aspnet)** — how to register services in MVC, Pages, Blazor, Minimal APIs, and MAUI hybrid hosts
- **[MVC example](https://core.crestapps.com/docs/framework/mvc-example)** — feature-by-feature reference application

If you are building on Orchard Core, continue below.

---

## Orchard Core quick start

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) matching your Orchard Core target
- An existing Orchard Core application, or use the CrestApps starter project

### Install all modules

Add the `CrestApps.OrchardCore.Cms.Core.Targets` package to include all modules at once:

```bash
dotnet add package CrestApps.OrchardCore.Cms.Core.Targets
```

### Install individual modules

Or install only the modules you need:

```bash
dotnet add package CrestApps.OrchardCore.AI
dotnet add package CrestApps.OrchardCore.AI.Chat
# ... add other modules as needed
```

## Running locally

1. **Clone the repository**
   ```bash
   git clone https://github.com/CrestApps/CrestApps.OrchardCore.git
   cd CrestApps.OrchardCore
   ```

2. **Install frontend dependencies and rebuild assets**
   ```bash
   npm install
   npm run rebuild
   ```

3. **Build the solution**
   ```bash
   dotnet build
   ```

4. **Launch the application**
   ```bash
   dotnet run
   ```

5. **Enable modules**
   Open the Orchard Core admin dashboard and enable the desired CrestApps modules.

## Package feeds

### Production packages

Stable releases are available on [NuGet.org](https://www.nuget.org/).

### Preview packages

[![Hosted By: Cloudsmith](https://img.shields.io/badge/OSS%20hosting%20by-cloudsmith-blue?logo=cloudsmith&style=for-the-badge)](https://cloudsmith.com)

Preview packages are available from the [CrestApps OrchardCore Cloudsmith repository](https://cloudsmith.io/~crestapps/repos/crestapps-orchardcore).

### Adding the preview feed

#### In Visual Studio

1. Open **NuGet Package Manager Settings**.
2. Add a new source:
   - **Name:** `CrestAppsPreview`
   - **URL:** `https://nuget.cloudsmith.io/crestapps/crestapps-orchardcore/v3/index.json`

#### Via NuGet.config

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
