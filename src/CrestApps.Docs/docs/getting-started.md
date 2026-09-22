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

## Release notes

Review the current [Version 3.0.0 Release Notes](changelog/3.0.0) before updating package references or tenant code. They list the breaking changes since the `2.x` line.

The repository currently builds the `3.0.0` line on `.NET 10` against Orchard Core `4.0.x` preview packages. The published `2.1.x` line is the latest stable release; its documentation is available from the version picker.

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
> The asset pipeline waits for each generated CSS and JavaScript stream to finish before Gulp completes the task, which keeps `npm run rebuild` reliable on current Node.js and Gulp releases.

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

The Aspire host starts Ollama, Redis, a PostgreSQL container running the [pgvector](https://github.com/pgvector/pgvector) image, and an Elasticsearch container. PostgreSQL provides a local vector store, and Elasticsearch backs the Orchard Core search indexes.

Both stores keep their data under `src\Startup\CrestApps.OrchardCore.Cms.Web\App_Data`, mounted into the containers instead of Docker volumes, so they sit beside the rest of the tenant data:

| Container | Data folder | Host port |
| --- | --- | --- |
| PostgreSQL (pgvector) | `App_Data\PostgreSQL` | 5432 |
| Elasticsearch | `App_Data\Elasticsearch` | 9200 |

The app host creates both folders on startup, and the first run initializes the data there. Delete a folder's contents to start that store over.

The credentials are app host parameters, so they can be overridden through user secrets:

```powershell
cd .\src\Startup\CrestApps.Aspire.AppHost
dotnet user-secrets set Parameters:PostgresPassword "<password>"
dotnet user-secrets set Parameters:ElasticsearchPassword "<password>"
```

PostgreSQL defaults to `postgres`/`postgres` with a `vectordb` database, and Elasticsearch to the built-in `elastic` user with the password `elasticsearch`.

The host passes both connections to the CMS as environment variables, so features pick them up without any per-feature setup:

```text
OrchardCore__CrestApps__PostgreSQL__ConnectionString
OrchardCore__CrestApps__Elasticsearch__Url
OrchardCore__CrestApps__Elasticsearch__Username
OrchardCore__CrestApps__Elasticsearch__Password
OrchardCore__OrchardCore_Elasticsearch__*
```

The `CrestApps` sections are the global connections described in [AI Data Sources - PostgreSQL](./ai/data-sources/postgresql.md) and [AI Data Sources - Elasticsearch](./ai/data-sources/elasticsearch.md). The `OrchardCore_Elasticsearch` section configures the Orchard Core Elasticsearch feature that owns the Orchard-managed indexes.

#### Share the local stores

Because the data lives in `App_Data`, sharing that folder shares the vector store and the indexes with it. Stop the Aspire host first so both containers shut down cleanly, then copy or zip `App_Data`; copying the files while the containers run produces torn data. The other developer drops the folder into their own `CrestApps.OrchardCore.Cms.Web` and starts the app host, which mounts it as is.

Leave `App_Data\logs` out of the copy. It holds the site logs rather than any state, and it grows to gigabytes on a long-running development site.

Each store is only readable by the major version that wrote it, so both sides must stay on the images pinned by the app host. Both must also use the same passwords, since PostgreSQL bakes its credentials into the cluster when it is first initialized.

To move individual databases instead of the whole folder, run `pg_dump` and `pg_restore` inside the container:

```powershell
docker exec -e PGPASSWORD=postgres <container> pg_dump --username postgres --dbname vectordb --format=custom --file /tmp/vectordb.dump
docker cp <container>:/tmp/vectordb.dump .\vectordb.dump
```

To work against shared servers instead of copies, host them somewhere both developers can reach and set the connections per developer rather than in source control:

```powershell
cd .\src\Startup\CrestApps.OrchardCore.Cms.Web
dotnet user-secrets set OrchardCore:CrestApps:PostgreSQL:ConnectionString "<connection string>"
dotnet user-secrets set OrchardCore:CrestApps:Elasticsearch:Url "<url>"
```

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
