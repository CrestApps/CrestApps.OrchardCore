---
title: Supply Chain Security
---

# Supply chain security

Every dependency this repository resolves is treated as code we ship. The controls below are enforced by the build itself, so they run on every local build, every pull request, and every push.

## Dependency vulnerability auditing

`NuGetAudit` is enabled for every build in `Directory.Build.props`, with `NuGetAuditMode` set to `all` and `NuGetAuditLevel` set to `low`, and the audit diagnostics `NU1901`-`NU1904` are promoted to errors. A published advisory against any package - direct or transitive, at any severity - fails the build.

This is deliberately strict. Previously every workflow passed `-p:NuGetAudit=false`, which meant advisories against transitive packages were never reported by any build.

When an advisory appears, pin the patched version in `Directory.Packages.props` rather than lowering the audit settings. Central transitive pinning is enabled, so a `PackageVersion` entry raises the resolved version everywhere without adding a direct dependency:

```xml
<!-- GHSA-pgww-w46g-26qg: patched in 1.5.0. -->
<PackageVersion Include="AngleSharp" Version="1.5.2" />
```

Record the advisory identifier next to the pin so the entry can be removed once the dependency resolves to a patched version on its own.

To review the whole graph rather than only what is broken, run `dotnet list package --vulnerable --include-transitive` locally.

## Dependency updates

Dependabot version update pull requests are enabled weekly for NuGet, npm, and GitHub Actions, grouped so routine minor and patch bumps arrive as a single review. Orchard Core packages and the bundled themes remain pinned and are never auto-updated.

Keeping updates flowing is part of the audit strategy rather than separate from it: because a published advisory is a hard build failure, a dependency left to drift eventually stops the pipeline outright.

## Hermetic builds

The GitHub Copilot SDK downloads a platform-specific CLI tarball from `registry.npmjs.org` during `BeforeBuild`, which made the whole solution unbuildable without public npm egress. That download is opt-in: `Directory.Build.props` defaults `CopilotSkipCliDownload` to `true`.

To build the Copilot chat orchestrator with its CLI, set one of:

- `-p:CopilotSkipCliDownload=false` - downloads the CLI from the configured npm registry.
- `-p:CopilotCliBinaryPath=<path>` - uses a vendored binary and skips the download entirely.
