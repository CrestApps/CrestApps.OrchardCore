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

### Test against local `CrestApps.Core` packages

The repository now includes a repo-local feed at `.\.nupkgs\crestapps-core-local`, and `NuGet.config` maps all `CrestApps.Core*` restores to that feed before the shared preview feed.

When you need to test changes from the sibling `CrestApps.Core` repository, pack that solution into the local feed with the version used by this repo:

```powershell
dotnet pack ..\CrestApps.Core\CrestApps.Core.slnx -c Release `
  -o .\.nupkgs\crestapps-core-local `
  -p:Version=1.0.0-local-preview-49 `
  /p:NuGetAudit=false
```

After packing, run restore or build from this repository and the OrchardCore projects will consume the locally packed `CrestApps.Core` packages.

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
- **Preview packages:** [Cloudsmith CrestApps OrchardCore feed](https://cloudsmith.io/~crestapps/repos/crestapps-orchardcore)
- **Shared Core preview feed:** [Cloudsmith CrestApps Core feed](https://cloudsmith.io/~crestapps/repos/crestapps-core)
- **Shared Core preview source URL:** `https://nuget.cloudsmith.io/crestapps/crestapps-core/v3/index.json`
- **Repo-local test feed:** `.\.nupkgs\crestapps-core-local`
