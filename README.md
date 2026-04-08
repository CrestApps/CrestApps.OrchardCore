# CrestApps.OrchardCore

`CrestApps.OrchardCore` contains the Orchard Core modules built on top of the shared CrestApps framework.

## Documentation

- **Orchard Core docs:** <https://crestapps.crestapps.com>
- **Shared framework docs:** <https://core.crestapps.com>

## Repository Focus

This repository is focused on Orchard Core-specific packages and applications:

- Orchard Core abstractions
- Orchard Core core libraries
- Orchard Core modules
- Orchard Core startup applications and targets
- Orchard Core test and benchmark projects

The shared framework libraries are being split into the separate `CrestApps.Core` repository and will later be consumed here through NuGet packages instead of project references.

## Orchard Core Project Areas

```text
src/
├── OrchardCore/
│   ├── Abstractions/
│   ├── Core/
│   ├── Modules/
│   └── Targets/
├── Startup/
│   ├── CrestApps.OrchardCore.Cms.Web/
│   ├── CrestApps.OrchardCore.Samples.A2AClient/
│   └── CrestApps.OrchardCore.Samples.McpClient/
└── CrestApps.OrchardCore.Documentations/
```

## Getting Started

```powershell
git clone https://github.com/CrestApps/CrestApps.OrchardCore.git
cd CrestApps.OrchardCore
dotnet build .\CrestApps.OrchardCore.slnx -c Release /p:NuGetAudit=false
dotnet test .\tests\CrestApps.OrchardCore.Tests\CrestApps.OrchardCore.Tests.csproj -c Release /p:NuGetAudit=false
```

## Packages

- **Stable:** <https://www.nuget.org/>
- **Preview feed:** <https://cloudsmith.io/~crestapps/repos/crestapps-orchardcore>
- **Preview source URL:** `https://nuget.cloudsmith.io/crestapps/crestapps-orchardcore/v3/index.json`

## License

MIT
