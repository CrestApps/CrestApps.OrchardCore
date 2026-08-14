# Orchard Core Modules by CrestApps

`CrestApps.OrchardCore` is a collection of modules and extensions that expand the capabilities of the Orchard Core framework and accelerate the development of modern, enterprise-ready applications.

## Documentation

- **CrestApps Orchard Core Documentation:** https://orchardcore.crestapps.com
- **CrestApps Core Documentation:** https://core.crestapps.com
- **Orchard Core Documentation:** https://docs.orchardcore.net

Use the CrestApps Orchard Core documentation for module installation, feature configuration, administration, and application development guidance.

Refer to the Orchard Core documentation for framework concepts, CMS features, and core platform documentation.

The CrestApps Core documentation covers shared infrastructure, reusable APIs, orchestration components, and cross-platform libraries used across the CrestApps ecosystem.

## Repository Scope

This repository includes:

- Orchard Core modules and features
- Framework extensions and enhancements
- Orchard Core integration libraries
- Startup applications and reference implementations
- Package targets and module bundles
- Tests, benchmarks, and documentation

## Solution Structure

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

## Project Areas

| Area | Description |
| --- | --- |
| `src/Abstractions` | Shared abstractions for Orchard Core integrations |
| `src/Common` | Common utilities and supporting components |
| `src/Core` | Core Orchard Core integration libraries |
| `src/Modules` | Orchard Core modules and framework extensions |
| `src/Startup` | Sample applications, CMS hosts, and development environments |
| `src/Targets` | Package targets that simplify module installation and composition |
| `src/CrestApps.Docs` | Documentation source for Orchard Core modules and extensions |

## Getting Started

```powershell
git clone https://github.com/CrestApps/CrestApps.OrchardCore.git
cd CrestApps.OrchardCore

npm install
npm run rebuild

dotnet build .\CrestApps.OrchardCore.slnx -c Release /p:NuGetAudit=false
dotnet test .\tests\CrestApps.OrchardCore.Tests\CrestApps.OrchardCore.Tests.csproj -c Release /p:NuGetAudit=false
```

> Note: Some builds may restore preview packages from the CrestApps Cloudsmith feed. Ensure the required NuGet sources are configured and accessible.

## Package Feeds

### Stable Packages

- https://www.nuget.org/

### Preview Packages

- https://cloudsmith.io/~crestapps/repos/crestapps-orchardcore
- https://nuget.cloudsmith.io/crestapps/crestapps-orchardcore/v3/index.json

## License

MIT
