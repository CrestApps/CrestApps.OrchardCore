# Contact Center Suite extraction plan

**Status:** proposal, written 2026-09-05. **Owner:** Mike Alhayek. **Executor:** an AI coding agent working workstream by workstream, with a manual review gate between phases.

## Goal

Extract every framework-level service, model, contract, store, SignalR hub base, background runner, and health check from the Omnichannel, Contact Center, Telephony, SMS Portal, and telephony-provider modules of `CrestApps.OrchardCore` into a **Contact Center Suite** of `CrestApps.Core.*` packages. The suite must be built the same way the AI suite in the `CrestApps.Core` repository is built:

- `Abstractions` / `Primitives` / `Stores` / `Resources` / `Utilities` / `Startup` project split.
- Builder-pattern registration (`AddCrestAppsCore(c => c.AddContactCenterSuite(cc => ...))`) with one extension method per feature, plus plain `AddCore*` `IServiceCollection` methods behind each builder method.
- YesSql index classes and `Create*IndexSchemaAsync` schema-builder extensions in a store package, consumed by Orchard Core data migrations.
- Hub base classes in the framework, concrete `[Authorize]` hubs in the host.
- Cycle services plus `IHostedService` runners in the framework, `IBackgroundTask` wrappers in Orchard Core.
- No `OrchardCore.*` package reference anywhere in `CrestApps.Core.*`.

The Orchard Core modules keep their **feature ids, feature dependencies, manifests, permissions, admin UI, recipes, deployment steps, workflows, and stored data exactly as they are today**. The only change on the Orchard Core side is where the services come from and how they are registered: each feature `Startup` becomes a thin call into the framework builder plus Orchard-specific glue (permissions, display drivers, menus, migrations, adapters).

## Scope

In scope (this repository, `src/`):

| Area | Projects |
| --- | --- |
| Omnichannel | `CrestApps.OrchardCore.Omnichannel.Core`, `...Omnichannel`, `...Omnichannel.Managements`, `...Omnichannel.Sms`, `...Omnichannel.Sms.Portal(.Core/.Abstractions)`, `...Omnichannel.Voice(.Core)`, `...Omnichannel.EventGrid` |
| Contact Center | `CrestApps.OrchardCore.ContactCenter(.Core/.Abstractions)` |
| Telephony | `CrestApps.OrchardCore.Telephony(.Core/.Abstractions)`, `...Telephony.Azure` |
| Providers | `CrestApps.OrchardCore.Telnyx(.Core)`, `...Asterisk`, `...DialPad` |
| Supporting | `CrestApps.OrchardCore.PhoneNumbers(.Core/.Abstractions)` (parsing service only), `...WebSockets(.Abstractions)`, `...SignalR.Core` helpers |
| Tests | `tests/CrestApps.OrchardCore.Tests` (Contact Center, Telephony, Omnichannel, Telnyx, Dialpad, SMS, PhoneNumbers, SignalR, WebSockets folders), `...ContactCenter.DistributedTests`, `...ContactCenter.FeatureActivationTests`, `...Telephony.PlaywrightTests` |
| Docs | `src/CrestApps.Docs/docs/{contact-center,telephony,omnichannel}` |

Out of scope: AI suite (already extracted), CRM content types and Orchard content-management concerns (contacts and subjects stay content items on the Orchard side and are reached through a new abstraction), Reports, Workflows, Deployment, Recipes, Audit Trail, Widgets, admin UI.

## Phases

| Phase | Where | Outcome | Gate before the next phase |
| --- | --- | --- | --- |
| **0 - Preparation** | This repository, existing projects | First milestone (P0.1, reviewed on its own): test baseline and coverage audit with a quantitative bar (every moving class covered, 80 percent line coverage per moving assembly, no public service below 60 percent) that becomes the enforced floor for Phases 0-1. Then every host seam, the CRM contracts with their Orchard content-type bindings, the SMS abstraction, options, authorization operations, cycles, hub bases, schema steps, endpoint methods, and `AddCore*` registration methods land inside the Orchard projects with behaviour unchanged. Phase 1 becomes a pure relocation | All suites green, coverage table complete, DI snapshot identical, snapshot upgrade test green, manual smoke; see [00](00-phase-0-preparation.md) |
| **1 - Transition** | This repository, new `Transitions` folders under `src/Abstractions`, `src/Core`, `src/Modules`, `tests` | All framework code lives in `CrestApps.Core.*`-named projects with final namespaces; Orchard modules consume them through builder extensions; framework tests live in an Orchard-free test project | Release build with warnings-as-errors, every test project green (including FeatureActivationTests and the Docker-backed DistributedTests), manual code review of the split, manual smoke test of the admin UI and soft phone |
| **2 - Move** | `CrestApps.Core` repository, then this repository | `Transitions` projects copied into the Core repository as-is, packaged and published to the preview feed, documented as the Contact Center Suite; this repository deletes `Transitions` and references NuGet packages | Core repo build/tests/docs green, preview packages published, this repository green on the packages, Orchard docs revised |
| **3 - Sample UI** | `CrestApps.Core` repository (`Mvc.Web`, later `Blazor.Web`) | The MVC sample host exposes every Contact Center Suite feature with standard MVC screens, including provider configuration (Telnyx voice and SMS, Twilio SMS, Asterisk, Dialpad), so the framework can be test-driven end to end without Orchard Core | Plan revisited and approved before work starts |
| **4 - Follow-up packages** | `CrestApps.Core` repository, then this repository | Reports framework (`CrestApps.Core.Reports.*`) with the Contact Center and Omnichannel report providers, do-not-call registries, Redis infrastructure, Azure integrations, time-zone map, contact import. Starts after Phase 2, overlaps with Phase 3; reports land before the Phase 3 reports screens | Per-package gates in [09](09-phase-4-follow-up-packages.md) |

## Documents (read in order)

0. [00-phase-0-preparation.md](00-phase-0-preparation.md) - the preparation phase: test baseline and coverage audit, then every seam and refactor landed inside the Orchard projects before anything moves.
1. [01-reference-architecture.md](01-reference-architecture.md) - how the AI suite is structured in `CrestApps.Core` and how Orchard Core consumes it today. Every rule in this plan derives from that model.
2. [02-inventory-and-target-layout.md](02-inventory-and-target-layout.md) - what exists today, the target package map, and the Phase 1 folder tree.
3. [03-host-seams.md](03-host-seams.md) - each Orchard Core dependency found inside the moving code and the framework abstraction plus adapter that replaces it.
4. [04-registration-api.md](04-registration-api.md) - the builder API, feature-by-feature service mapping, and before/after Orchard `Startup` shapes.
5. [05-phase-1-transition.md](05-phase-1-transition.md) - the ordered workstreams, tasks, definitions of done, and verification commands.
6. [06-phase-2-move-to-core.md](06-phase-2-move-to-core.md) - the move into the Core repository, packaging, docs for the suite, and the Orchard docs revision.
7. [07-phase-3-mvc-web-ui.md](07-phase-3-mvc-web-ui.md) - outline for the MVC sample UI parity (to be revisited before it starts).
8. [08-mvc-consumer-walkthrough.md](08-mvc-consumer-walkthrough.md) - the plan checked backward from a plain MVC consumer: a 30-step checklist of everything such an app needs, the gaps that check exposed (G1-G14), and how each was folded back into the plan.
9. [09-phase-4-follow-up-packages.md](09-phase-4-follow-up-packages.md) - the follow-up packages: reports framework, DNC registries, Redis, Azure, time-zone map, contact import, with ordering and gates.
10. [10-backward-review-framework-to-orchard.md](10-backward-review-framework-to-orchard.md) - from the finished framework to Orchard: the contract binding matrix, what Orchard must never register, what the framework must never know, cross-cutting compatibility checks.
11. [11-forward-review-orchard-features.md](11-forward-review-orchard-features.md) - from every Orchard feature forward: what it does, its startup after the split, what must stay identical, the tests that pin it, the cross-feature flows to smoke, and the easy-to-miss items.
12. [appendix-a-file-disposition.md](appendix-a-file-disposition.md) - file and folder disposition tables per project (move / stay / split / adapt).
13. [appendix-b-namespace-map-commands-checklists.md](appendix-b-namespace-map-commands-checklists.md) - namespace and assembly map, migration of stored type names, commands, grep gates, per-file checklist.
14. [appendix-c-startup-registration-inventory.md](appendix-c-startup-registration-inventory.md) - generated: every registration in every in-scope Orchard `Startup` (363 lines across 67 startup classes) with a heuristic stay/store/framework/map tag to confirm in Phase 0.
15. [appendix-d-test-coverage-gaps.md](appendix-d-test-coverage-gaps.md) - generated: the 356 of 570 moving classes with no same-named test file; the input to the Phase 0 coverage audit.

## Non-negotiables

1. **No Orchard Core in the framework.** No `CrestApps.Core.*` project may reference an `OrchardCore.*` package or a `CrestApps.OrchardCore.*` project. This is enforced by a grep gate and by the Phase 1 test project, which references only framework projects.
2. **Layer boundary preserved.** CRM (Omnichannel) owns business work data, Contact Center owns orchestration, Telephony owns media execution. `OmnichannelActivity` stays the universal work item; `Interaction` stays communication history for one attempt and never owns workflow or disposition.
3. **No competitor product names** in identifiers, comments, or docs.
4. **Orchard features unchanged.** Feature ids, names, categories, dependencies, `EnabledByDependencyOnly`, permissions, admin menus, recipes, deployment steps, workflows, and UI stay identical. FeatureActivationTests must pass unmodified except for assembly/namespace references.
5. **Stored data stays readable.** JSON shapes, property names, collection names, index table names, and index columns do not change. Every moved document type gets a stored type-name rewrite migration (the pattern already used by `AILegacyDocumentTypeNameMigrations`).
6. **Warnings are errors** and the `CrestApps.Core` coding conventions apply to every new or moved framework file (XML docs on public members, sealed by default, `TimeProvider` not `DateTime.UtcNow`, no `global using`, blank-line rules).
7. **Same registration style as the AI suite.** Builder types are sealed classes exposing `Services`; every builder method is sugar over an `AddCore*` `IServiceCollection` method; stores register through `AddYesSqlStores()`; defaults use `TryAdd*`; handlers use `TryAddEnumerable`.

## Decisions that need sign-off before Phase 1 starts

| Id | Decision | Recommendation | Alternative |
| --- | --- | --- | --- |
| D-1 | Suite entry point | `crestApps.AddContactCenterSuite(suite => suite.AddOmnichannel(...).AddTelephony(...).AddContactCenter(...).AddSmsPortal(...))`, mirroring `AddAISuite` | Separate top-level `AddOmnichannel` / `AddTelephony` / `AddContactCenter` on `CrestAppsCoreBuilder` |
| D-2 | Provider package names | `CrestApps.Core.Telephony.Telnyx`, `CrestApps.Core.Telephony.Asterisk`, `CrestApps.Core.Telephony.Dialpad` (like `CrestApps.Core.AI.OpenAI`) | Flat `CrestApps.Core.Telnyx` etc. |
| D-3 | YesSql store packaging | A separate `CrestApps.Core.Data.YesSql.ContactCenter` package in `src/Stores` that references `CrestApps.Core.Data.YesSql`, using the `CrestApps.Core.Data.YesSql` root namespace so a later merge is namespace-neutral. Provider-specific indexes and stores live inside the provider package under `Data/YesSql`. | Merge everything into `CrestApps.Core.Data.YesSql` (drags Contact Center dependencies into the base store package) |
| D-4 | Contacts, subjects, subject flows | The framework owns a complete default CRM data model: `ContactDefinition`, `OmnichannelContact` (with contact methods and communication preferences), `SubjectDefinition` (settings, AI settings, field schema = the subject flow), `OmnichannelSubject`, plus `OmnichannelDisposition`, `SubjectAction`, campaigns and cadences, all as catalog items with stores, managers, handlers, and YesSql persistence so any host can manage them. Framework services depend only on contracts (`IContactDefinitionProvider`, `IOmnichannelContactResolver/Writer`, `ISubjectDefinitionProvider`, `IOmnichannelSubjectAccessor`, `ISubjectFlowSettingsService`). Orchard does not register the default model; it registers its content-type implementations of the same contracts, so contacts and subjects stay content types and content items in Orchard | Framework declares contracts only with throwing defaults (leaves standalone hosts without CRM management) |
| D-12 | Schema upgrades for standalone hosts (G1) | Versioned `ISchemaMigration` steps in the store package with a runner and a `SchemaVersion` document; Orchard `DataMigration`s delegate to the same steps with the same version numbers | Keep create-only schema extensions and leave upgrades to each host |
| D-13 | Do-not-call screening without Orchard (G3) | Framework default registry over the contact's `DoNotCall` preference with a fail-closed option; national registries (local list, USA FTC, Canada DNCL) as an optional `CrestApps.Core.Omnichannel.Compliance.DncRegistry` package after Phase 2; Orchard keeps its DNC modules | Move the DNC modules in Phase 1 |
| D-14 | Contact time-zone detection (G4) | `IContactTimeZoneResolver` with a libphonenumber-based default and an optional `TimeZoneMap` catalog; Orchard keeps its TimeZones map and content handler | Leave detection Orchard-only |
| D-15 | Multi-node infrastructure (G9) | Single node in Phases 1-2; `CrestApps.Core.Redis` (lock provider, rendezvous owner store, presence) as a follow-up; SignalR backplane is the host's `AddStackExchangeRedis` | Build Redis variants in Phase 1 |
| D-16 | SMS and voice providers available to a standalone host from Phase 1 | Telnyx voice + Telnyx SMS (`CrestApps.Core.Telephony.Telnyx`, Phase 1 W7) and a new `CrestApps.Core.Sms.Twilio` package (sender over the Twilio REST API, inbound webhook, signature validator moved from `Omnichannel.Sms`, Phase 1 W7b) so the MVC sample can send and receive SMS and run calls with real providers; Azure Communication Services SMS optional in Phase 4 | Rely on host-provided `ISmsProvider` implementations |
| D-17 | Reports | Extract the reports framework (`CrestApps.Core.Reports.Abstractions`, `CrestApps.Core.Reports`, `CrestApps.Core.Reports.OpenXml`) in Phase 4 and move the Contact Center and Omnichannel report providers into the framework; Orchard keeps the reports admin module | Leave reporting Orchard-only |
| D-18 | Workflows, recipes, deployment steps, audit trail, content transfer, widgets | Never part of the framework; Orchard-only integrations over framework events and models | - |
| D-11 | Where the default model is registered | `omnichannel.AddContacts()` / `.AddSubjects()` (framework default model + stores through `AddYesSqlStores()`); Orchard calls `AddOrchardCoreOmnichannelContacts()` / `...Subjects()` instead, which bind the same contracts to content items. No `Replace` calls are needed because the default model is opt-in | Register the default model always and let Orchard `Replace` every binding |
| D-5 | Feature ids inside framework services | Introduce `ContactCenterCapabilities` constants (neutral values) used by `IContactCenterFeatureWorkManager` and lifecycle participants; Orchard maps feature id to capability | Keep the `CrestApps.OrchardCore.ContactCenter.*` strings inside framework code |
| D-6 | SMS provider abstraction | New `CrestApps.Core.Sms.Abstractions` (`ISmsProvider`, `ISmsProviderResolver`, `SmsMessage`, `SmsResult`); Orchard adapters in both directions | Keep `OrchardCore.Sms` types in the portal (blocks the portal from being a framework feature) |
| D-7 | Where host seams live | Phase 1: `CrestApps.Core.Hosting.Abstractions` + `CrestApps.Core.Hosting`. Phase 2: fold into `CrestApps.Core.Abstractions` / `CrestApps.Core` (the `AddContactCenterSuite` builder type joins `Builders/CrestAppsBuilder.cs`) | Keep a permanent `CrestApps.Core.Hosting` package |
| D-8 | Optional packages | `CrestApps.Core.Telephony.Azure` (blob recording store) and `CrestApps.Core.Omnichannel.Azure.EventGrid` are built in Phase 1 only if time allows; otherwise they stay Orchard-only until Phase 2 | Include from the start |
| D-9 | Tests | New Orchard-free test project `tests/Transitions/CrestApps.Core.ContactCenter.Tests` (Phase 2: folded into `tests/CrestApps.Core.Tests`) | Keep tests in the Orchard test project |
| D-10 | Redis-backed implementations | WebSocket rendezvous owner store, Contact Center Redis health check, Redis SignalR backplane stay Orchard-side in Phase 1 (they use `OrchardCore.Redis`); a framework `StackExchange.Redis` variant is a Phase 2 follow-up | Build the framework variants in Phase 1 |

## Success criteria

Phase 0 is done when the exit criteria in [00](00-phase-0-preparation.md) section 3 are met: every test suite green on `main` and on the branch, the coverage audit table complete with an architecture test enforcing it, no moving class referencing an Orchard-only type outside the listed binding classes, every Orchard startup reduced to framework calls plus `[stay]` lines with an identical DI snapshot, the snapshot upgrade test green, and the manual smoke flows in [11](11-forward-review-orchard-features.md) section 2 passing.

Phase 1 is done when all of the following are true:

- `dotnet build CrestApps.OrchardCore.slnx -c Release -warnaserror` succeeds.
- `tests/CrestApps.OrchardCore.Tests`, `tests/Transitions/CrestApps.Core.ContactCenter.Tests`, `tests/CrestApps.OrchardCore.ContactCenter.FeatureActivationTests`, and `tests/CrestApps.OrchardCore.ContactCenter.DistributedTests` (with Redis and PostgreSQL provisioned per the existing environment variables) pass.
- `grep -rlE "OrchardCore" src/*/Transitions --include=*.cs --include=*.csproj` returns nothing.
- Every Orchard feature id and dependency list in the manifests is byte-identical to `main`.
- A tenant created on `main` and upgraded to the branch keeps its agents, queues, entry points, dialer profiles, business hours, interactions, SMS conversations, and Telnyx credentials (stored type-name migrations ran).
- The public API baselines under `tests/CrestApps.OrchardCore.Tests/PublicApi/Baselines` are regenerated for the assemblies that remain governed, and the new framework assemblies have their own baselines in the transition test project.
- The Contact Center production-readiness changelog page records the split.
