# 06 - Phase 2: move the `Transitions` projects into `CrestApps.Core` and consume them as packages

Phase 2 starts only after the Phase 1 gate and the manual review in [05-phase-1-transition.md](05-phase-1-transition.md) section 3 are complete. The move is a copy of finished projects; no logic changes are expected. Anything that needs a logic change is a Phase 1 defect and goes back to Phase 1.

## 1. In the `CrestApps.Core` repository

### 1.1 Copy projects into their final folders

| From (this repository) | To (`CrestApps.Core`) |
| --- | --- |
| `src/Abstractions/Transitions/CrestApps.Core.Hosting.Abstractions/**` | folded into `src/Abstractions/CrestApps.Core.Abstractions` (`Locking/`, `Hosting/`, `Security/IUserDirectory`, `Builders/CrestAppsBuilder.cs` gains `CrestAppsContactCenterSuiteBuilder`) and `src/Abstractions/CrestApps.Core.Infrastructure.Abstractions/Diagnostics/LogDataClassifications.cs` (D-7) |
| `src/Core/Transitions/CrestApps.Core.Hosting/**` | folded into `src/Primitives/CrestApps.Core` (`Locking/`, `Hosting/`, `BackgroundWork/`, `AddContactCenterSuite` in `ServiceCollectionExtensions.cs` next to `AddIndexingServices`) and `src/Primitives/CrestApps.Core.SignalR` (`TenantSignalRGroupName`, `HubConnectionWork`) |
| `src/Abstractions/Transitions/CrestApps.Core.Sms.Abstractions` | `src/Abstractions/CrestApps.Core.Sms.Abstractions` |
| `src/Abstractions/Transitions/CrestApps.Core.PhoneNumbers.Abstractions` | `src/Abstractions/CrestApps.Core.PhoneNumbers.Abstractions` |
| `src/Abstractions/Transitions/CrestApps.Core.Omnichannel.Abstractions` | `src/Abstractions/CrestApps.Core.Omnichannel.Abstractions` |
| `src/Abstractions/Transitions/CrestApps.Core.Telephony.Abstractions` | `src/Abstractions/CrestApps.Core.Telephony.Abstractions` |
| `src/Abstractions/Transitions/CrestApps.Core.ContactCenter.Abstractions` | `src/Abstractions/CrestApps.Core.ContactCenter.Abstractions` |
| `src/Abstractions/Transitions/CrestApps.Core.Omnichannel.Sms.Portal.Abstractions` | `src/Abstractions/CrestApps.Core.Omnichannel.Sms.Portal.Abstractions` |
| `src/Core/Transitions/CrestApps.Core.PhoneNumbers`, `...WebSockets`, `...Omnichannel`, `...Omnichannel.Sms`, `...Omnichannel.Voice`, `...Omnichannel.Azure.EventGrid`, `...Telephony`, `...Telephony.Azure`, `...Telephony.Telnyx`, `...Telephony.Asterisk`, `...Telephony.Dialpad`, `...ContactCenter`, `...Omnichannel.Sms.Portal` | `src/Primitives/<same name>` |
| `src/Core/Transitions/CrestApps.Core.Data.YesSql.ContactCenter` | `src/Stores/CrestApps.Core.Data.YesSql.ContactCenter` (or merged into `src/Stores/CrestApps.Core.Data.YesSql` if D-3 is decided the other way; the namespaces already match) |
| `src/Modules/Transitions/CrestApps.ContactCenter.Resources` | `src/Resources/CrestApps.ContactCenter.Resources` |
| `tests/Transitions/CrestApps.Core.ContactCenter.Tests/**` | `tests/CrestApps.Core.Tests/<area>/**` (folders `ContactCenter`, `Telephony`, `Omnichannel`, `SmsPortal`, `Providers/Telnyx|Asterisk|Dialpad`, `PhoneNumbers`, `WebSockets`, `Hosting`); doubles into `tests/CrestApps.Core.Tests/Support`; public API baselines into `tests/CrestApps.Core.Tests/PublicApi/Baselines` (add a `PublicApiApprovalTests` to the Core test project if it does not exist) |

Steps:

1. Create a branch in `CrestApps.Core`; copy the folders; fix the relative `ProjectReference` paths (they become `../../Abstractions/...`, `../CrestApps.Core.Telephony/...` etc.); replace every `PackageReference` to a `CrestApps.Core.*` package with the matching `ProjectReference`.
2. Remove the `Transitions` `Directory.Build.props` overrides; the Core repository's root props apply (`IsPackable` true).
3. Register every project in `CrestApps.Core.slnx` under the matching solution folder.
4. Add package versions to `Directory.Packages.props` (`libphonenumber-csharp`, `BouncyCastle.Cryptography`, `Microsoft.Extensions.Http.Resilience`, `Microsoft.Extensions.Compliance.Redaction`, `Microsoft.Extensions.Compliance.Abstractions`, `Dapper`, `Azure.Storage.Blobs`, `Azure.Messaging.EventGrid`, `Microsoft.Extensions.Diagnostics.HealthChecks`, `Microsoft.Extensions.TimeProvider.Testing`) with the versions used in this repository.
5. Fold `CrestApps.Core.Hosting(.Abstractions)` into the base packages (D-7): move files, delete the two transitional projects, update `using` directives (namespaces were already final: `CrestApps.Core.Locking`, `CrestApps.Core.Hosting`, `CrestApps.Core.Security`, `CrestApps.Core.SignalR`).
6. Build and test:
   ```bash
   dotnet build .\CrestApps.Core.slnx -c Release -warnaserror /p:TreatWarningsAsErrors=true /p:RunAnalyzers=true /p:NuGetAudit=false
   dotnet test .\tests\CrestApps.Core.Tests\CrestApps.Core.Tests.csproj -c Release /p:NuGetAudit=false
   ```
7. `README.md` (root and `src/README.md`): add the Contact Center Suite to "What you get", "Common use cases" (contact center, soft phone, SMS portal, outbound dialer), and the package list.
8. `AGENTS.md` / `.github/copilot-instructions.md`: extend "Project overview" with the suite and add the layer-boundary rule and the no-competitor-names rule from this repository's `AGENTS.md`.

### 1.2 Documentation for the Contact Center Suite (mirror of the AI suite docs)

Create `src/CrestApps.Core.Docs/docs/contact-center/` and register a `Contact Center Suite` category in `sidebars.js` after `Features`:

| Page | Content |
| --- | --- |
| `contact-center/index.md` | Overview, value proposition, capability table (Omnichannel CRM data model, Telephony, Contact Center orchestration, SMS Portal, providers), the layer-boundary diagram from the Orchard docs (`contact-center/index.md` "Layer boundaries"), quick start snippet with `AddContactCenterSuite` |
| `contact-center/getting-started.md` | Minimal MVC host: packages, `AddCrestAppsCore(...AddContactCenterSuite(...))`, YesSql store, SignalR hubs, endpoint mapping, appsettings sections, first queue and agent |
| `contact-center/architecture.md` | Package map (from [02](02-inventory-and-target-layout.md) section 2), dependency graph, builder pattern, host seams (S1-S20) and how a host implements them, background workers, hubs |
| `contact-center/omnichannel.md` | Activities, campaigns, groups, dispositions, channel endpoints, cadences, subject flows, automation options, handoff, the contact/subject abstraction and how to implement it for your CRM |
| `contact-center/telephony.md` | Provider model (`ITelephonyProvider` + capability contracts), `ITelephonyService`, command executor, hub base, soft phone registration config, recording media stores, extensions; adapted from the Orchard `telephony/index.md` and `custom-providers.md` |
| `contact-center/contact-center.md` | Agents, presence, entitlements, business hours, queues and routing strategies, reservations, work state, voice routing, inbound entry points and IVR, dialer modes, recording governance, secure capture, supervision, real-time notifications, metrics/retention, health checks; adapted from `contact-center/agents-queues-dialer.md`, `routing-work-state.md`, `voice-routing.md`, `live-call-topology.md` |
| `contact-center/sms-portal.md` | Conversations, routing modes, broadcasts, templates, keyword replies, SLA, quiet hours, provider dispatch through `ISmsProvider`; adapted from `omnichannel/sms-portal.md` |
| `contact-center/sms-automation.md` and `contact-center/automated-voice.md` | AI SMS automation and Automated Voice over the AI suite; adapted from `omnichannel/sms.md` and the Automated Voice sections |
| `contact-center/providers/telnyx.md`, `asterisk.md`, `dialpad.md` | Configuration sections, webhooks/endpoints to map, capability matrix; adapted from `telephony/telnyx.md`, `asterisk.md`, `dialpad.md` with Orchard-specific screens removed |
| `contact-center/data-storage.md` | YesSql store package, collections, schema extensions, the `ConcurrentDocumentCatalog`, migration helpers, EF Core status (not provided; how to implement the store interfaces) |
| `contact-center/host-integration.md` | Implementing `ITenantAccessor`, `IDistributedLockProvider`, `IUserDirectory`, `IScopedWorkExecutor`, authorization operations, SMS providers, contact resolver, startup checks; the Orchard Core implementation as the reference downstream (link to `orchardcore.crestapps.com`) |
| `contact-center/mvc-example.md` | Walkthrough of the Phase 3 MVC areas (written in Phase 3; stub in Phase 2) |
| `changelog/2.0.0.md` | "Contact Center Suite" section listing the new packages |
| `glossary.md` | Add activity, interaction, queue item, reservation, entry point, dialer profile, work state, capability |

Docs build: `cd src\CrestApps.Core.Docs && npm install && npm run build`. Follow the MDX rules (no `{#id}` heading ids).

### 1.3 Packaging and publishing

- `preview_ci.yml` already packs and pushes every `src/**/*.nupkg`; the new projects are picked up automatically. Verify `dotnet pack` succeeds for each new project (README/icon inclusion comes from `Directory.Build.props`).
- Merge to the preview branch, wait for the Cloudsmith feed to show `2.0.0-preview.<n>` for `CrestApps.Core.ContactCenter` and friends.
- `e2e_samples.yml` should start `Mvc.Web` with the suite registered (Phase 3 makes the screens exist; in Phase 2 the registration is enough to prove DI resolves).

## 2. In this repository

1. Delete `src/Abstractions/Transitions`, `src/Core/Transitions`, `src/Modules/Transitions`, `tests/Transitions` and their solution folders.
2. `Directory.Packages.props`: bump `CrestAppsCoreVersion` to the published preview; add `PackageVersion` entries for `CrestApps.Core.Sms.Abstractions`, `CrestApps.Core.PhoneNumbers.Abstractions`, `CrestApps.Core.PhoneNumbers`, `CrestApps.Core.WebSockets`, `CrestApps.Core.Omnichannel.Abstractions`, `CrestApps.Core.Omnichannel`, `CrestApps.Core.Omnichannel.Sms`, `CrestApps.Core.Omnichannel.Voice`, `CrestApps.Core.Omnichannel.Azure.EventGrid`, `CrestApps.Core.Omnichannel.Sms.Portal.Abstractions`, `CrestApps.Core.Omnichannel.Sms.Portal`, `CrestApps.Core.Telephony.Abstractions`, `CrestApps.Core.Telephony`, `CrestApps.Core.Telephony.Azure`, `CrestApps.Core.Telephony.Telnyx`, `CrestApps.Core.Telephony.Asterisk`, `CrestApps.Core.Telephony.Dialpad`, `CrestApps.Core.ContactCenter.Abstractions`, `CrestApps.Core.ContactCenter`, `CrestApps.Core.Data.YesSql.ContactCenter`, `CrestApps.ContactCenter.Resources`, `CrestApps.Core.SignalR`. `NuGet.config` already maps `CrestApps.Core*` to the Cloudsmith preview source; add a mapping for `CrestApps.ContactCenter.Resources`.
3. Replace every `ProjectReference` to a `Transitions` project with the `PackageReference`. Remove the `CrestApps.Core.Hosting*` references (types now live in `CrestApps.Core.Abstractions`/`CrestApps.Core`; the namespaces did not change so no code edits are expected).
4. Rebuild, run every test project, run FeatureActivationTests and DistributedTests, run the Playwright soft phone tests.
5. Switch the Telephony, Contact Center, and SMS Portal resource manifests to the `CrestApps.ContactCenter.Resources` static web assets (`_content/CrestApps.ContactCenter.Resources/scripts/...`) and delete the module `Assets`/`wwwroot` copies once the Playwright tests pass. Keep the `Assets.json` entries for Orchard-only bundles (agent bar, external transfer settings) if they are not moved.
6. `CrestApps.OrchardCore.Cms.Core.Targets`: no change unless projects were deleted in Phase 1.
7. Public API: the Orchard baselines now cover only Orchard assemblies; delete baselines for deleted projects and update `PublicApiApprovalTests.GetGovernedAssemblyNames` accordingly. The framework assemblies are governed in the Core repository.
8. Update `AGENTS.md` (Contact Center section) to state that framework services come from the `CrestApps.Core` Contact Center Suite packages and that framework changes are made in the Core repository first.

### 2.1 Orchard docs revision (`src/CrestApps.Docs/docs`)

Revise, do not rewrite. Each page keeps its feature-focused content and gains a short "Framework" note linking to the Core docs page that now owns the concept.

| Page | Change |
| --- | --- |
| `contact-center/index.md` | Add a "Built on the CrestApps.Core Contact Center Suite" section: what the framework owns (orchestration services, stores, hub bases, providers) and what the Orchard modules add (features, admin, permissions, recipes, deployments, workflows, reports). Keep feature/administration tables and the layer diagram |
| `contact-center/public-api-surface.md` | Update the governed-assembly list (framework assemblies are governed upstream); explain that changing a framework surface happens in the Core repository |
| `contact-center/production-readiness-changelog.md` | Entry for the split (Phase 1) and the package switch (Phase 2) |
| `contact-center/runbooks.md`, `production-support.md` | Package versions matter now: add "check the `CrestApps.Core.*` package version" to the diagnostics checklist |
| `contact-center/configuration-deployment.md` | Options sections unchanged; note which options classes are framework types |
| `contact-center/agents-queues-dialer.md`, `routing-work-state.md`, `voice-routing.md`, `live-call-topology.md`, `workflows.md`, `report-catalog.md`, `agent-desktop.md`, `user-manual.md` | Replace `CrestApps.OrchardCore.ContactCenter.Core.*` type references with the `CrestApps.Core.ContactCenter.*` names; link to Core docs for service contracts |
| `telephony/index.md`, `custom-providers.md` | Provider contracts now live in `CrestApps.Core.Telephony.Abstractions`; a provider can be an Orchard module or a plain package; show both registration styles (`AddTelephonyProvider<T>` in a framework builder vs. an Orchard `Startup`) |
| `telephony/telnyx.md`, `asterisk.md`, `dialpad.md`, `extension-dialing.md`, `recording-azure-blob-storage.md` | Settings screens unchanged; note options classes and framework packages |
| `telephony/*-project-plan.md`, `production-readiness-soft-phone-telnyx.md`, `soft-phone-endpoint-implementation-plan.md` | Historical; add a banner that the implementation moved to the framework |
| `omnichannel/index.md`, `management.md`, `cadences.md`, `sms.md`, `sms-portal.md`, `event-grid.md`, `azure-communication-services.md`, `ai-agent-handoff-project-plan.md`, `production-readiness-sms-portal.md` | Same treatment: framework note, type-name updates, link to `contact-center/omnichannel.md` and `sms-portal.md` in the Core docs |
| `feature-reference.md` | Unchanged feature ids; add package column if it lists assemblies |
| `sidebars.js` | Unchanged unless pages are added |

Build the site (`npm run build` in `src/CrestApps.Docs`) as the acceptance check.

## 3. Phase 2 gate

- Core repository: build, tests, docs build green; packages on the preview feed.
- This repository: build, all test projects, FeatureActivationTests, DistributedTests, Playwright green on packages; no `Transitions` folder left; manifests unchanged; upgrade test from a `main` database still passes.
