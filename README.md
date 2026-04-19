# CrestApps.OrchardCore

`CrestApps.OrchardCore` contains the Orchard Core packages, modules, startup apps, and docs that build on top of the shared **CrestApps.Core** framework.

## Documentation

- **Orchard Core docs:** <https://crestapps.crestapps.com>
- **CrestApps.Core docs:** <https://core.crestapps.com>

Use the Orchard site for Orchard module setup, admin configuration, startup apps, and CMS integration guidance. Use the Core site for shared framework concepts, APIs, orchestration internals, and reusable .NET building blocks.

## Repository scope

This repository focuses on:

- Orchard Core abstractions and integration libraries
- Orchard Core modules and feature wiring
- Orchard-specific startup applications and samples
- Orchard Core targets, tests, benchmarks, and documentation

The underlying framework also powers non-Orchard hosts and is documented separately in the `CrestApps.Core` repository.

## Current solution structure

```text
src/
├── Abstractions/
├── Common/
├── Core/
├── CrestApps.Docs/
├── Modules/
├── Startup/
└── Targets/

tests/
├── CrestApps.OrchardCore.Benchmarks/
└── CrestApps.OrchardCore.Tests/
```

## Main project areas

| Area | Purpose |
| --- | --- |
| `src\Abstractions` | Shared Orchard Core abstractions |
| `src\Common` | Shared support code used by Orchard projects |
| `src\Core` | Orchard-focused core libraries |
| `src\Modules` | Orchard Core modules such as AI, Omnichannel, Roles, Users, and SignalR |
| `src\Startup` | Runnable apps including the CMS host, Aspire host, and sample clients |
| `src\Targets` | Package targets that bundle module references |
| `src\CrestApps.Docs` | Docusaurus site for Orchard-specific documentation |

## Getting started

```powershell
git clone https://github.com/CrestApps/CrestApps.OrchardCore.git
cd CrestApps.OrchardCore
npm install
npm run rebuild
dotnet build .\CrestApps.OrchardCore.slnx -c Release /p:NuGetAudit=false
dotnet test .\tests\CrestApps.OrchardCore.Tests\CrestApps.OrchardCore.Tests.csproj -c Release /p:NuGetAudit=false
```

The .NET build restores Orchard Core preview packages from Cloudsmith, so network access to the configured feeds is required.

## Package feeds

- **Stable:** <https://www.nuget.org/>
- **Preview feed:** <https://cloudsmith.io/~crestapps/repos/crestapps-orchardcore>
- **Preview source URL:** `https://nuget.cloudsmith.io/crestapps/crestapps-orchardcore/v3/index.json`

## License

MIT
