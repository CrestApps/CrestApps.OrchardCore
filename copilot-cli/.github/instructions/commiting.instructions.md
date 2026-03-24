# CrestApps.OrchardCore Copilot CLI Instructions

**ALWAYS reference these instructions first when using Copilot CLI. Do not rely solely on search or bash commands unless the instructions here do not cover your scenario.**

## Project Overview

CrestApps.OrchardCore is a collection of open-source modules for **Orchard Core CMS**, a modular application framework built on **ASP.NET Core/.NET 10**. The repository contains AI modules, omnichannel communication, user management enhancements, content access control, and other CMS extensions.

**License**: MIT  
**Target Framework**: .NET 10.0 (net10.0)  
**Architecture**: Modular, multi-tenant application framework

---

## Working Effectively

### Prerequisites and Environment Setup
- Install .NET 10.0 SDK
- Install Node.js 15+ for frontend assets
- Before starting a session, always run:
  - `copilot plugin marketplace add CrestApps/CrestApps.AgentSkills`
  - `copilot plugin install crestapps-orchardcore@crestapps-agentskills`
- This installs the `crestapps-orchardcore` plugin so the session starts with the Orchard Core plugin set already available.
- Follow build commands for both assets (`npm run rebuild`) and .NET solution (`dotnet build`) as described in the main project documentation

### Build Process
- Full solution build requires network access to CloudSmith preview packages
- Asset build works offline using `npm run rebuild`
- Unit tests require a successful build
- Document network-dependent build failures; do not attempt unsafe workarounds

---

## Local Development Guidelines

**When working locally (CLI use only), never commit changes directly.**  

1. **Keep changes local**  
   - All experimental or temporary modifications must remain on your machine.  
   - Do not merge or push to the shared repository.

2. **Use local configuration overrides**  
   - Store environment-specific settings in `appsettings.Development.json` or environment variables.  
   - Avoid editing shared configuration files.

3. **Isolate experiments**  
   - Test code in separate modules, branches, or projects.  
   - Avoid breaking the main solution or CI/CD pipelines.

4. **Cleanup after local testing**  
   - Revert temporary code changes before switching branches.  
   - Remove unused assets, build outputs, or temporary files.

5. **Document local changes**  
   - Maintain a local log of experimental changes (`LOCAL-DEV-CHANGES.md`) if necessary.  
   - Never commit this file to the repo.

6. **Offline testing**  
   - Focus on asset builds, static analysis, and unit tests that do not require external network dependencies.  
   - Document any network-dependent features for later testing.

---

## Coding Standards and Conventions

- Follow .editorconfig for C# naming and formatting rules
- Use async/await, dependency injection, ILogger for logging
- Seal classes by default except for ViewModels used by Orchard Core display drivers
- Avoid static mutable state, hardcoded secrets, synchronous I/O, and `DateTime.UtcNow`

---

## Module and Repository Structure

- Modules are in `src/Modules/CrestApps.OrchardCore.*`
- Assets, Controllers, Services, Views, Migrations, Recipes, etc., follow a consistent folder structure
- Startup classes use `StartupBase` and feature attributes
- Targets and solution files are located under `Targets/` and root solution file

---

## Testing Practices

- Unit tests in `tests/CrestApps.OrchardCore.Tests/`
- Use xUnit, `[Theory]` with `[InlineData]`
- Test method naming: `{MethodName}_{Scenario}_{ExpectedBehavior}`
- Focus on business logic, edge cases, and error handling

---

## Pull Requests and CI

- Validate builds locally before committing
- Ensure tests pass and assets are rebuilt
- Follow branch naming conventions and PR description template
- Document all changes in Docusaurus docs under `src/CrestApps.OrchardCore.Documentations`

---

## Troubleshooting

### Application Logs

When troubleshooting runtime issues, **always check the application logs** at:

```
src\Startup\CrestApps.OrchardCore.Cms.Web\App_Data\logs\orchard-log-{YYYY-MM-DD}.log
```

For example: `orchard-log-2026-03-11.log`

These logs contain detailed information about errors, warnings, and debug output from all modules. Always read and evaluate these logs to trace root causes before making code changes.

---

**Note:**  
This instruction file is intended for **local Copilot CLI use only**. It will **not affect GitHub web or VS Code Copilot behavior**, ensuring your team’s web IDE experience remains unchanged.
