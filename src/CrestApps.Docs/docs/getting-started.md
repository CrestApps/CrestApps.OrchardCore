---
sidebar_label: Getting Started
sidebar_position: 2
title: Getting Started
description: Install, build, and run the Orchard Core modules in this repository or consume the published packages in your own Orchard solution.
---

# Getting Started

Use this repository when you want the Orchard Core host applications, Orchard-specific modules, or the Orchard documentation site. For shared framework guidance, see **[core.crestapps.com](https://core.crestapps.com)**.

## Prerequisites

- **.NET 10 SDK**
- **Node.js** for the asset pipeline
- Network access to:
  - `https://api.nuget.org/v3/index.json`
  - `https://nuget.cloudsmith.io/orchardcore/preview/v3/index.json`
  - `https://nuget.cloudsmith.io/crestapps/crestapps-core/v3/index.json`

## Install packages in your Orchard solution

### Install all CrestApps Orchard modules

```bash
dotnet add package CrestApps.OrchardCore.Cms.Core.Targets
```

### Install individual modules

```bash
dotnet add package CrestApps.OrchardCore.AI
dotnet add package CrestApps.OrchardCore.AI.Chat
dotnet add package CrestApps.OrchardCore.OpenAI
```

After installing packages, enable the required features in **Tools -> Features** inside the Orchard admin.

## Upgrade guidance

If you are upgrading an existing Orchard solution, review these pages before you update package references:

1. [Version 2.0.0 Release Notes](changelog/v2.0.0)
2. [Migrating to Typed AI Deployments](ai/migration-typed-deployments)

The current repository version is the `2.0.0-preview` line on `.NET 10` and Orchard Core `3.0.0-preview`.

## Build this repository locally

```powershell
git clone https://github.com/CrestApps/CrestApps.OrchardCore.git
cd CrestApps.OrchardCore
npm install
npm run rebuild
dotnet build .\CrestApps.OrchardCore.slnx -c Release /p:NuGetAudit=false
dotnet test .\tests\CrestApps.OrchardCore.Tests\CrestApps.OrchardCore.Tests.csproj -c Release /p:NuGetAudit=false
```

> The .NET build depends on Orchard Core preview packages. If Cloudsmith is unreachable, asset builds still work but the .NET restore/build will not.
>
> The asset pipeline now waits for each generated CSS and JavaScript stream to finish before Gulp completes the task. This keeps `npm run rebuild` reliable on current Node.js and Gulp releases instead of failing with a premature stream-close error.

## Run the startup apps

### CMS host

```powershell
cd .\src\Startup\CrestApps.OrchardCore.Cms.Web
dotnet run
```

Use this app when you want to test modules inside a full Orchard Core site.

### Aspire host

```powershell
cd .\src\Startup\CrestApps.Aspire.AppHost
dotnet run
```

Use this when you want the local orchestration environment for the sample clients and supporting services.

### Sample clients

```powershell
cd .\src\Startup\CrestApps.OrchardCore.Samples.McpClient
dotnet run
```

```powershell
cd .\src\Startup\CrestApps.OrchardCore.Samples.A2AClient
dotnet run
```

## Build the docs site

```powershell
cd .\src\CrestApps.Docs
npm install
npm run build
```

## Package feeds

- **Stable packages:** [NuGet.org](https://www.nuget.org/)
- **Shared Core preview feed:** [Cloudsmith CrestApps Core feed](https://cloudsmith.io/~crestapps/repos/crestapps-core)
- **Shared Core preview source URL:** `https://nuget.cloudsmith.io/crestapps/crestapps-core/v3/index.json`
- **Orchard Core preview source URL:** `https://nuget.cloudsmith.io/orchardcore/preview/v3/index.json`
