# PSA Implementation Guide

Status: proposal, revision 2 (27 November 2026). This guide turns [`psa-platform-plan.md`](psa-platform-plan.md) into 17 work packages and their tasks, in order. It contains no code. The plan says **what** each module is (specifications M01–M23) and **why** (decisions D-01 to D-28 in its section 25); this guide says **how to build it, in which order, and how to know each step is done**.

Every decision is taken. If a step still seems to need a decision that is not written here or in the plan, stop and ask (section 1.7). Do not choose.

---

## Contents

1. How to work (read first, every time)
2. Environment and commands
3. Recipes: the repeatable "how to" for each kind of change
4. WP00 Readiness
5. WP01 Foundations
6. WP02 Boards
7. WP03 Rates and PSA skeleton
8. WP04 Service operations
9. WP05 E-mail (after #817)
10. WP06 Agreements
11. WP07 Quotes
12. WP08 Time approval
13. WP09 Projects
14. WP10 Resource planning
15. WP11 CMDB and change management (after #1379)
16. WP12 Procurement
17. WP13 Billing review and expenses
18. WP14 Accounting sync and portal v2
19. WP15 Analytics, AI and RMM
20. WP16 Enterprise readiness and migration toolkit
21. Appendix A: Done checklist for every work package
22. Appendix B: Glossary
23. Appendix C: Progress

---

## 1. How to work (read first, every time)

### 1.1 One work package = one branch = one pull request

- This is the repository's convention (decision D-17): one branch and one pull request per plan, with **one commit per task (slice)**.
- Branch: `psa/wp<NN>-<short-name>`, for example `psa/wp03-rates`. Pull request title: `PSA WP<NN>: <work package title>`.
- Commit message: `WP<NN>-T<MM> <task title>` on the first line, then what the commit does.
- Start a work package only when every work package in its **Needs** line is merged into `main`, and rebase on `main` first.
- In the pull request description, copy the work package's **Done when** list and Appendix A, and tick each item.
- Update the work package's row in Appendix C (Progress) in the last commit of the pull request.

### 1.2 Read before you write

Before the first task of a work package:

1. Read the whole work package section in this guide.
2. Read the plan sections it names (specifications, decisions, section 5.3 for the existing plans it builds on).
3. Read `AGENTS.md` sections *Building Blocks and Reuse*, *Code Guidelines* (including *Admin Forms and Menus*, *Data Portability*, *YesSql Index Declarations*), *Common Development Tasks → Adding a New Module*, *Coding Standards and Conventions*, *Testing Practices*, *Anti-Patterns to Avoid*.
4. Read `CLAUDE.md`.
5. Read `docs/engineering/building-blocks.md` and confirm every concept the work package touches has the owner the work package names.
6. Read every existing plan the work package lists under **Builds on**.
7. Read every file listed under **Read first** in each task: they are the templates you copy the shape of.
8. If a task names a skill under `.agents/skills/`, read its `SKILL.md` and follow it.

### 1.3 The task loop (do this for every task, then commit)

1. Write a failing test for one rule from the task's **Tests first** list. Run that test class and confirm it fails for the right reason.
2. Write the smallest code that makes it pass. Refactor with the test green.
3. Repeat until every test in the list exists and passes.
4. Run the **affected test classes** only (filter, section 2.3). Do not run the whole suite for every task.
5. Commit with the task's message.

### 1.4 The end-of-work-package routine (once, before asking for review)

1. Build with warnings as errors (section 2.2).
2. Start the site and check in a browser every page the work package added or changed, in both list layouts (List and Grid) and both row-action modes (Buttons and Menu), as `AGENTS.md` requires.
3. Run the work package's end-to-end workflow test (Playwright) and the PSA integration tests.
4. Run the **whole** unit test project once, including the 36 architecture tests (Appendix A lists them).
5. Run the docs checks (section 2.4).
6. Fill in Appendix A in the pull request.

### 1.5 Rules you must never break

- **Never** put a concept in a second place. Use or extend the owner named in `building-blocks.md`.
- **Never** let a `.Core` or `.Abstractions` project reference a module project; module code never uses a type from another module's assembly.
- **Never** reference anything named `CrestApps.OrchardCore.Psa*` from outside the PSA modules.
- **Never** read another module's index tables; call its service through its `.Abstractions` interface.
- **Never** change money, usage, stock or time without an idempotency key and a distributed lock (Recipe R11), and prove it on a real database (Recipe R16).
- **Never** return a record without the record family's scope filter (Recipe R12).
- **Never** edit a golden fixture under `tests/fixtures/psa/` (D-18). If you think one is wrong, stop and ask.
- **Never** write a migration that changes or deletes existing data in place; use expand-and-contract (Recipe R7, D-19).
- **Never** add a per-entry "billable" flag to time: billable is the Time Tracker stage's Billable category (D-11).
- **Never** add a PSA menu group. Put pages where decision D-16 says (section 1.6).
- **Never** use `DateTime.UtcNow`; inject `IClock`. Never show UTC to a person; use `ILocalClock`.
- **Never** hand-write an admin list or button row: use `<admin-list>` and `action-bar-bottom`; group editor fields in cards declared by placement.
- **Never** write a competitor product name in code, comments, identifiers or docs (`AGENTS.md`).
- **Never** add Claude or any AI as a commit author or co-author. Commit only as the configured human author.
- **Never** document behaviour that is not implemented.

### 1.6 Where pages go (decision D-16)

| Kind of page | Where |
| --- | --- |
| Work queues of an existing area | That area's existing menu: Service Desk, Field Service, Workforce, Time Tracker, Approvals, Assets, Commerce (invoices, estimates, inventory, billing review) |
| Projects | One new top-level **Projects** menu with an icon (it is a work area like Service Desk) |
| Shared business records | Content (Agreements already lives there; Rates and expense categories and policies go there) |
| Settings | Settings (a settings group with `AddSiteSettingsPermission` when it uses a module permission) or Configuration > Tickets for ticket settings |
| Reports | Reports, never the module's own menu |
| Self-service | My Work (staff) and the portal (customers) |
| Get Started and other one-time setup pages | Not in the menu; linked from the module's pages |

### 1.7 When to stop and ask

Stop, write the question in the pull request, and wait, when:

- a step needs a type, service or feature that does not exist and this guide does not tell you to create it;
- an existing test fails and the task does not say it should change;
- the owner named here does not match `building-blocks.md`, or an existing plan in plan section 5.3 says something different;
- a golden fixture disagrees with the rule as written;
- a prerequisite pull request (#817, #1379) changed after this guide was written in a way that affects the task;
- the build cannot restore packages (report it; do not work around it).

---

## 2. Environment and commands

### 2.1 Tools

- .NET SDK as pinned in `global.json` (`11.0.100`, `rollForward: latestMajor`). `AGENTS.md` mentions .NET 10 in places; `global.json` wins.
- Node.js and npm for assets. Package feeds per `NuGet.config` (Orchard Core preview feed, CrestApps feed).

### 2.2 Build

```
npm install
npm run rebuild
dotnet build -c Release -warnaserror /p:TreatWarningsAsErrors=true /p:RunAnalyzers=true /p:NuGetAudit=false
```

`NU1301` errors about `nuget.cloudsmith.io` mean the environment cannot reach the feeds: report it. After the first successful build, list `.agents/skills/` again (a package may add skills).

### 2.3 Test

```
dotnet test -c Release --no-build ./tests/CrestApps.OrchardCore.Tests/CrestApps.OrchardCore.Tests.csproj --filter "FullyQualifiedName~CrestApps.OrchardCore.Tests.<Area>"
dotnet test -c Release --no-build ./tests/CrestApps.OrchardCore.Tests/CrestApps.OrchardCore.Tests.csproj
dotnet test -c Release --no-build ./tests/CrestApps.OrchardCore.Psa.IntegrationTests/CrestApps.OrchardCore.Psa.IntegrationTests.csproj
```

- First line: affected tests during a task. Second: the whole unit project, once per work package. Third: PSA integration tests (exists after WP01).
- Public API baselines: `tests/CrestApps.OrchardCore.Tests/PublicApi/Baselines/*.approved.txt`. Update them in the same commit as a public type change and review the diff.

### 2.4 Docs

```
cd src/CrestApps.Docs
npm run check:manuals
npm run build
```

Read `src/CrestApps.Docs/README.md` first. User Manual pages: `src/CrestApps.Docs/docs/user-manual/`. Technical Manual: the rest of `src/CrestApps.Docs/docs/`.

### 2.5 Run the site

```
cd src/Startup/CrestApps.OrchardCore.Cms.Web
dotnet run
```

---

## 3. Recipes

Each task refers to these by number. "Copy the shape of" means: open that file and follow its structure, naming and documentation; do not copy unrelated logic.

### R1. Create a new building block

Template: the Agreements block.

1. Folders `src/Abstractions/CrestApps.OrchardCore.<Name>.Abstractions/`, `src/Core/CrestApps.OrchardCore.<Name>.Core/`, `src/Modules/CrestApps.OrchardCore.<Name>/`.
2. `.csproj` files copying the shape of the three Agreements project files; keep only needed references.
3. Add the three projects to `CrestApps.OrchardCore.slnx` (Abstractions, Core and Modules sections, alphabetical).
4. Add the module to `src/Targets/CrestApps.OrchardCore.Cms.Core.Targets/CrestApps.OrchardCore.Cms.Core.Targets.csproj` as `<ProjectReference ... PrivateAssets="none" />`, alphabetical (this file is where modules are listed, even though `AGENTS.md` mentions the `.targets` file).
5. `.Abstractions`: `<Name>Constants.cs` (feature IDs, `CollectionName`, open kind strings; shape of `AgreementsConstants.cs`), `<Name>Permissions.cs` (shape of `AgreementsPermissions.cs`), `Models/`, `Services/`.
6. `.Core`: `<Name>ServiceCollectionExtensions.cs` with `Add<Name>Core` (shape of `AgreementsServiceCollectionExtensions.cs`), `Indexes/`, `Services/`.
7. Module: `Manifest.cs`, `Startup.cs`, `README.md`, `Migrations/`, `Controllers/`, `Drivers/`, `Views/`, `Recipes/`, `Deployments/`, `Navigation/`, `ViewModels/` (shape of the Agreements module).
8. Add the block to `docs/engineering/building-blocks.md`.
9. Technical Manual and User Manual pages, both added to `sidebars.js`.

### R2. Add a catalog record

Template chain: `Agreement` → `AgreementIndex` → `AgreementIndexProvider` → `AgreementsIndexSchema` → `AgreementStore` → `AgreementManager` → `AgreementHandler` → `AgreementsMigrations`.

1. Model in `.Abstractions/Models/<Record>.cs`, sealed, deriving from `CatalogItem`; XML docs on every property; a `Properties` bag when other modules add terms.
2. Index with only the columns lists, filters and lookups need.
3. Index provider: `ExclusiveToDocumentType()` always; `SingleRecordPerDocument()` when it maps at most one row; a multi-row index is added to the list in `IndexDeclarationArchitectureTests` (`AGENTS.md`, *YesSql Index Declarations*).
4. Index schema with database indexes for every `WHERE`/`ORDER BY` column.
5. Store deriving from `DocumentCatalog<TRecord, TIndex>` with `CollectionName` from constants.
6. Catalog manager (`ICatalogManager<T>`) so handlers run; validation in the handler.
7. Handler deriving from `AuditedCatalogEntryHandlerBase<T>` for created/modified/owner stamps.
8. Migration: new numbered step only; never edit a released step (`MigrationAdditiveOnlyGuardTests`).
9. Numbers through `AddNumberSeries` and `INumberSeries` in the same unit of work.
10. Register everything in `Add<Name>Core` and call it from `Startup`.

### R3. Admin list and editor

1. Follow `.agents/skills/crestapps-admin-index-views/SKILL.md` and `.agents/skills/crestapps-admin-edit-views/SKILL.md`.
2. Controller deriving from `CatalogAdminController<T>` (`src/Core/CrestApps.OrchardCore.Core/Controllers/CatalogAdminController.cs`).
3. Rows from a display driver with `SummaryAdmin` and `SummaryAdminGrid` locations; editor shapes placed in cards by placement (`.Card(...)`), never card markup in views.
4. `Index.cshtml` uses `<admin-list>` with `<admin-list-grid>` columns; forms end with `<div class="action-bar-bottom">`.
5. Menu entry at the place section 1.6 gives, guarded by the view permission; a new top-level item has an icon.
6. Every action checks a permission.

### R4. Extension point

1. Interface in `.Abstractions/Services/I<Thing>.cs` with XML docs including every `<param>`.
2. Registration helper `Add<Thing><T>(this IServiceCollection services, ...)` in `.Abstractions` (shape: `AddApprovalType`, `AddRecordSource`, `AddCalendarType`, `AddPartySource`).
3. Open kinds are strings owned by the consumer.
4. The block resolves `IEnumerable<I<Thing>>` and works with none registered.
5. A test with a fake implementation proves the block calls it.

### R5. Optional integration between two blocks

Code in the consumer, in a startup marked `[RequireFeatures(<OtherBlock>Constants.Feature.Id)]` (or a bridge feature); reference the other block's `.Abstractions`; feature IDs from constants (`FeatureIdLiteralRatchetTests`; Orchard IDs from `OrchardCoreFeatureIds`).

### R6. Recipe and deployment steps

`NamedCatalogRecipeStep` with an `IRecipeStep` schema; `NamedCatalogDeploymentSource`, step, step display driver and deployment schema (shape: `src/Modules/CrestApps.OrchardCore.Agreements/Recipes/*`, `Deployments/*`); schemas registered in a startup marked `[RequireFeatures("CrestApps.OrchardCore.Recipes")]` via the constant. `RecipeSchemaCoverageArchitectureTests`, `DeploymentStepSerializationArchitectureTests` and `DeploymentStepViewsArchitectureTests` check this. Test: export then import gives identical records.

### R7. Data migration: expand and contract (D-19)

1. **Expand:** an additive migration adds the new fields or index columns.
2. **Move:** an `IResumableStep` registered with `AddResumableStep<T>()` and requested from the migration with `ResumableSteps.RequestAsync(name)` writes the new shape beside the old one, in batches, idempotently (`ResumableStepRequestTests` applies).
3. **Dual-read:** readers use the new shape when present and fall back to the old.
4. **Verify:** at the end, the step writes a verification report (counts, totals, balances before and after) to the log and as an admin notification; a mismatch stops the step and keeps reads on the old shape.
5. **Contract:** removing old fields is a separate task in a later release, never in the same work package.
6. **Rollback:** turning the new feature off returns to the old reads; the old data was never changed.
7. Tests in the PSA integration project: run the step twice on the migration fixture; the report matches the golden expected report.

### R8. Report data set

Follow `.agents/skills/crestapps-report-data-source/SKILL.md`; derive from `IndexedReportDataSet`; apply the scope filter; reports go under Reports.

### R9. Workflow events, webhooks, audit

Copy the shape of Tickets' `WorkflowsStartup.cs`, `Workflows/`, and `Integrations/TicketWebhooksAndAudit.cs`. Webhooks implement `IWebhookEventProvider`; names `{entity}.{verb}`; payloads carry IDs and changed fields only. `WorkflowActivityViewsArchitectureTests` checks the activity views.

### R10. JSON API

`MapModuleApi` route group `api/<module>/v1`, `Api` scheme, `ApiProblems`, `IdempotencyEndpointFilter` on writes; template `src/Modules/CrestApps.OrchardCore.Tickets/Api/TicketApiEndpoints.cs` and `ApiStartup.cs`; permission and scope on every endpoint; page size at most 200.

### R11. Money, usage, stock or time changes

1. Lock name including the aggregate ID (`DistributedLockExtensions`; `LockAllAsync` for several, in a fixed order).
2. Re-read state inside the lock.
3. Idempotency key from the source record.
4. One YesSql session per command.
5. Observers notified after the save.

### R12. Scope filter

Template: `ITicketAccessService` + `TicketScopeFilter`. An `I<Record>AccessService` in `.Abstractions` (can view, can edit, restrict a query), applied in SQL; used by every list, API, data set, search, attachment and timeline access provider, record source and AI tool; probes added to the isolation harness (WP01-T04).

### R13. Personal data

`IPersonalDataContributor` per record holding personal data; erasure clears personal fields and keeps financial facts (shape of `InvoicingPersonalDataContributor`).

### R14. Telemetry

Background tasks run through `BackgroundTaskTelemetry`; meters and activity sources from `CrestAppsOrchardCoreTelemetry` (`CrestApps.OrchardCore.<Area>`); no IDs or names in metric tags.

### R15. Unit tests

`tests/CrestApps.OrchardCore.Tests/<Area>/<ClassUnderTest>Tests.cs`, sealed, xUnit; the Kit style of `tests/CrestApps.OrchardCore.Tests/Agreements/AgreementsTests.cs` with doubles from `tests/CrestApps.OrchardCore.Tests/Doubles`; fixed clock (Monday 16 November 2026, noon New York); Arrange/Act/Assert separated.

### R16. Real-database tests

In `tests/CrestApps.OrchardCore.Psa.IntegrationTests` (created in WP01-T05): each test gets a fresh tenant from the `Psa` recipe (after WP03; before that, from the features under test) on SQLite; CI also runs them on SQL Server. Concurrency tests start parallel tasks against the real store and assert the effect happened once.

### R17. Golden fixtures

Read the fixture and its entry in `tests/fixtures/psa/README.md`; load it in the test; compare outputs to its expected values. Never edit it (D-18). The manifest test fails if a fixture changes without a manifest change.

---

## 4. WP00 Readiness

- **Needs:** nothing. **Builds on:** plan sections 23 and 25.
- **Who:** a maintainer or an agent that will **not** implement the business rules (golden data must be independent, D-18).

| Task | What | Done when |
| --- | --- | --- |
| T01 Baseline | Build and run all tests; record in `docs/engineering/psa/baseline.md`: date, commit, results, failing test names | File merged |
| T02 Golden fixtures | Write under `tests/fixtures/psa/`: `rates/precedence.json` (5 scopes × 4 line specificities × Fixed/Multiplier with expected rates), `agreements/coverage.json` (overlaps, exclusions, umbrella, default, after-hours, site), `agreements/balances.json` (each entitlement kind, purchases, rollover, period edges, overage split), `agreements/migration/` (current agreements and expected balances), `time/approval-posting.json`, `projects/networks/*.json` (20 networks, expected dates computed with an independent critical-path tool, tool and version recorded), `resource-planning/capacity.json`, `billing/month-end/` (3 accounts, 5 agreements, 400 entries, expected invoices), `analytics/kpis.json`; a `README.md` recording author and method per file | Each file reviewed by a maintainer |
| T03 Fixture manifest | `tests/fixtures/psa/MANIFEST.sha256` and a unit test that fails when a fixture's hash differs from the manifest; create `.github/CODEOWNERS` (the repository has none) owning `tests/fixtures/psa/` by the maintainers, so fixture changes need their approval | Test passes; CODEOWNERS merged |
| T04 Benchmark data set | In `tests/CrestApps.OrchardCore.Benchmarks`, a generator for the reference data set of plan section 23 and benchmarks for the budgets listed there (coverage, approval, recalculation, board, posting are added as their work packages land) | Generator runs; budgets file `docs/engineering/psa/budgets.md` copied from plan section 23 |

**Done when:** all four tasks merged.

---

## 5. WP01 Foundations

- **Needs:** WP00. **Builds on:** `ticket-support-plan.md` §4.1 (record links), Approvals README. **Plan:** M01, M03, sections 16.2, 23; D-15, D-18, D-19.

### T01 Record links: contracts and store (M01)

- **Read first:** `src/Abstractions/CrestApps.OrchardCore.Abstractions/Records/` (`RecordRef`, `IRecordSource`), `src/Core/CrestApps.OrchardCore.Tickets.Core/Indexes/TicketLinkIndex.cs`, where `IQuantityLedger` keeps its index in `CrestApps.OrchardCore.YesSql.Core` (pattern for a platform index), R2, R4.
- **Create:** in `CrestApps.OrchardCore.Abstractions/Records/`: `RecordLink`, `IRecordLinkService`, `RecordLinkKind`, `AddRecordLinkKind`. In `CrestApps.OrchardCore.YesSql.Core/Records/`: `RecordLinkIndex` (multi-row: no; one row per link document), provider, schema helper, store, `RecordLinkService`. In the Resources module: a migration creating the table through the schema helper, and a `RecordLinks` view component.
- **Rules:** unique per (From, To, Kind); listing from the target returns the inverse kind; link and unlink need view permission on both ends through each kind's `IRecordSource`; unknown kinds refused.
- **Tests first:** `Link_SameTripleTwice_StoresOnce`; `List_FromTarget_ReturnsInverseKind`; `Link_WithoutViewPermissionOnTarget_IsRefused`; `Link_UnknownKind_IsRefused`; `Unlink_RemovesBothDirections`.
- **Commit:** `WP01-T01 Typed record links`.

### T02 Ticket links on record links (M01)

- **Read first:** Tickets' link code and `TicketLinkKinds`; R7.
- **Steps:** characterization tests of today's ticket links first (problem solves incidents, links both ways, merge); register Tickets' kinds with `AddRecordLinkKind`; resumable step copying ticket links to record links with a verification report (link counts per kind); dual-read (Tickets reads record links when the step has finished and verified, else `TicketLinkIndex`); new links written to both until the contract task in a later release.
- **Tests first:** the characterization tests; in the integration project `TicketLinkStep_RunTwice_CopiesEachOnce_ReportMatches`.
- **Commit:** `WP01-T02 Ticket links on record links`.

### T03 Approvals additions (M03)

- **Read first:** Approvals README, `IApproverRuleProvider`, `ApprovalTypeHandlerBase` (in `IApprovalTypeHandler.cs`), `ITeamMembershipService`, `SignedTokenService`.
- **Steps:** (1) Team approver rule in a startup requiring the Teams feature (members of a team; optional minimum); (2) approve/reject links in approver notifications built with `SignedTokenService` (single use, expiry from Approvals settings; approval types may require a signed-in decision); (3) delegation (delegate and date range; decision records "on behalf of"); (4) policy simulation page and service method: given type, amount and scope, show the policy and approvers without creating a request.
- **Tests first:** `TeamRule_ReturnsCurrentMembers`; `TeamRule_MinimumRespected`; `DecisionLink_UsedTwice_SecondRefused`; `DecisionLink_Expired_Refused`; `Delegation_InRange_AsksDelegate`; `Simulation_ReturnsPolicyAndApprovers_CreatesNothing`; `Requester_CannotApprove` still passes.
- **Commit:** `WP01-T03 Approvals: team rule, e-mail decisions, delegation, simulation`.

### T04 Isolation harness and scope convention

- **Read first:** `ITicketAccessService`, `TicketScopeFilter`, `SourceAccess` types, plan 16.2, R12.
- **Steps:** document the convention in `building-blocks.md` (platform table); create `tests/CrestApps.OrchardCore.Tests/Architecture/Isolation/` with a helper building accounts A and B, a staff user scoped to A's desk, portal users of A and B, records of both; each record family registers probes (list service, API handler, data set, search, attachment access, AI tool) that list records as a user; the helper asserts no foreign record appears; add probes for Tickets, Invoicing (my invoices) and Agreements (account tab). Add a two-tenant variant to the PSA integration project in T05.
- **Tests first:** the probes; prove the harness catches a removed filter locally, then restore.
- **Commit:** `WP01-T04 Isolation harness`.

### T05 PSA test projects

- **Read first:** `tests/CrestApps.OrchardCore.Commerce.IntegrationTests/` (fixture and host), `tests/CrestApps.OrchardCore.Telephony.PlaywrightTests/`.
- **Steps:** create `tests/CrestApps.OrchardCore.Psa.IntegrationTests` (SQLite by default, SQL Server through an environment variable used in CI) with a tenant fixture that enables named features (the `Psa` recipe is used from WP03); create `tests/CrestApps.OrchardCore.Psa.PlaywrightTests` with one smoke test (sign in, open Service Desk); add both to the solution and CI.
- **Tests first:** `TenantFixture_CreatesTenantWithFeatures`; `TwoTenants_DoNotSeeEachOthersRecords` (tickets).
- **Commit:** `WP01-T05 PSA integration and browser test projects`.

### T06 PSA architecture guards

- **Read first:** `LayeringRatchetTests.cs`, `ProjectReferenceRulesTests.cs`, `ModuleDependencyGraphTests.cs`. `ModuleDependencyGraphTests` only guards commerce and payment assemblies, so it does not cover PSA or feature manifests; this task adds what is missing.
- **Steps:** a rule that no assembly outside `CrestApps.OrchardCore.Psa*` references one inside; a test that reads every manifest's feature dependencies and fails on a cycle.
- **Tests first:** both rules, each proven to fail with a temporary bad reference.
- **Commit:** `WP01-T06 PSA guards`.

**Done when:** all tasks committed; existing Tickets and Approvals tests unchanged; isolation harness and integration projects run in CI; end-of-work-package routine done.

---

## 6. WP02 Boards

- **Needs:** WP00. **Builds on:** `ticket-boards-plan.md`. **Plan:** M02; A5.

| Task | Steps | Tests first |
| --- | --- | --- |
| T01 Characterize ticket boards | Tests pinning current behaviour: columns of statuses, move calls `ITicketService.UpdateAsync`, cards filtered by `ITicketAccessService.ApplyScopeAsync`, shared/team/personal boards, recipes | The characterization tests |
| T02 Boards block | Create `CrestApps.OrchardCore.Boards` (R1): `IBoardSource` (kind, columns, cards for a viewer, move), `Board` record (name, source kind, columns, filter, sharing, WIP limits, swimlane field), admin pages (R3), board page, move endpoint; generic board script in Resources | `Move_CallsSourceMove_NeverWritesStatus`; `Cards_FilteredBySourceAccess`; `WipLimitExceeded_Warns` |
| T03 Tickets on Boards | Tickets registers a board source; resumable step moves `TicketBoard` records to `Board` records with verification (R7); Service Desk > Boards keeps its menu place | All T01 tests pass unchanged; `BoardStep_RunTwice_SameBoards` |
| T04 Clean-up and docs | Private board code removed only after T03 passes; `building-blocks.md` row updated (Boards is the owner) | Architecture tests |

**Done when:** ticket boards behave as before; Boards documented.

---

## 7. WP03 Rates and PSA skeleton

- **Needs:** WP01. **Builds on:** `staff-plan.md`, `accounts-plan.md`, `time-tracker-people-teams-review.md`. **Plan:** M05, M04 (1–3), M21; D-11, D-16, D-25.

### T01 Work roles (M05)

- Create the Rates block (R1); `WorkRole` record (R2); Content > Rates > Work Roles (R3); recipe and deployment (R6); permissions `ManageRates`, `ViewBillRates`, `ViewCostRates`; `IRateUsage` delete guard.
- **Tests first:** `WorkRole_CodeUnique`; `Delete_InUse_Refused`; recipe round-trip.

### T02 Rate cards (M05)

- `RateCard` with `RateLine {WorkRoleId?, TimeStageId?, Kind(Fixed, Multiplier), Value}`, `ScopeKind`/`Scope`, currency, dates, `DefaultRate`, status by `TransitionTable`; `IRateScopeProvider` with `AddRateScope` and built-in `Default` and `Party` scopes; card editor as a role × stage grid; copy, activate, retire. Stages are read through Time Tracker's abstractions.
- **Tests first:** `Activate_OverlappingSameScopeAndCurrency_Refused`; `Activate_Retired_Refused`; `Line_UnknownStage_Refused`.

### T03 Rate resolver (M05)

- `IRateResolver` exactly as plan M05 D rules 1–6; cost from Staff (`StaffPayRates.CostOn`); rate preview page; `GET api/rates/v1/resolve` (R10).
- **Tests first:** golden `rates/precedence.json` (R17); `NonBillableStage_BillZero_CostKept`; `ForeignCurrency_ConvertedOnWorkDate`; `EffectiveDate_LocalDateInNewYork`; `Api_WithoutViewCostRates_OmitsCost`.

### T04 Rates on Staff and Account pages; bulk import (M05)

- Display drivers: default work role card on the Staff editor; assigned rate card on the Account page; ContentTransfer record importer for rate cards (`AddContentTransferImportTarget<T>()`).
- **Tests first:** `StaffDefaultRole_Saved`; `Import_InvalidRow_Fails_WithReason`.

### T05 Account health, roll-ups, account-team routing (M04)

- `IAccountHealthSignalProvider`, `IAccountHealthService` (weighted average of present signals, equal default weights, editable under Settings > Accounts); providers in Tickets (open tickets and breaches, satisfaction) and Invoicing (overdue) through R5 (Agreements adds its signals in WP06); health card on the account page; parent account roll-up cards (children's open tickets and receivables, scoped); notifications addressed to "account manager" resolve through the Account Team feature's roles.
- **Tests first:** `Score_WeightedAverageOfPresentSignals`; `Score_NoSignals_Null`; `RollUp_HidesChildWithoutPermission`.

### T06 PSA module, recipe and role templates (M21)

- Create `src/Modules/CrestApps.OrchardCore.Psa` (module only), add to slnx and Targets; feature depending on the features available now; `Psa` recipe creating the defaults listed in plan M21 (1) that exist now (desk, ticket types, statuses, priorities and matrix, business hours, SLA policies, Time Tracker stages with categories, work roles, default rate card); recipe roles for the 13 role templates with only existing permissions; PSA settings under Settings > PSA (default stage per desk, time approval period default; billing grouping comes in WP13).
- **Tests first (integration):** `PsaRecipe_FreshTenant_CreatesDefaults`; `RoleTemplates_NoUnknownPermissions`; `PsaSettings_RequirePermission` (`SiteSettingsGroupPermissionRegistrationTests` passes).

### T07 Get Started page (M21)

- A Get Started page (not in the menu) with the checklist of plan M21 (4), each item linking to the page that does it and showing done or not; linked from the PSA settings page and empty states.
- **Tests first:** `Checklist_ItemDone_WhenRecordExists`.

**Done when:** the PSA integration tests now build their tenant from the `Psa` recipe (update R16 fixture); rates resolve on a fresh tenant; end-of-work-package routine done.

---

## 8. WP04 Service operations

- **Needs:** WP03. **Builds on:** `ticket-support-plan.md`, `ticket-intake-plan.md` (decisions §9 bind), Service Levels docs. **Plan:** M07 (templates, recurring, catalog additions, SLA attainment and escalation), M08, M22 v1; D-14.

| Task | Steps | Tests first |
| --- | --- | --- |
| T01 Ticket templates | `TicketTemplate` (type, category, field values, checklist, child-ticket definitions, default stage, estimate); Service Desk > Templates; "New from template"; `ITicketTemplateService.ApplyAsync` | `Apply_SetsFieldsAndChildren`; `Apply_OtherType_Refused` |
| T02 Recurring tickets | `RecurringTicketSchedule` (template, RRULE via Calendars `RecurrenceRule`, time zone, account, desk, team, assignee, lead days, end rule, when previous open); background task creates each occurrence once under a lock (key = schedule + date), R14 | `Occurrence_OncePerDate_AfterRestart` (integration); `Dst_OccurrenceAtLocalTime`; `SkipWhenPreviousOpen` |
| T03 Catalog additions | On `TicketCategoryPart`: approval policy (Ticket Approvals before work), fulfilment template (child tickets), audience by account, displayed price; portal flow unchanged (types, then categories); audience enforced on submit | `Submit_CategoryOutsideAudience_Refused`; `Submit_WithPolicy_StartsApproval`; `Approved_CreatesFulfilmentChildren` |
| T04 Escalation chains | `EscalationChain` per desk (steps: trigger warning/breach of a metric; actions notify team lead, user or role, reassign team, raise priority, raise level), editor under Configuration > Tickets; an `IServiceLevelObserver` in Tickets runs each step once per timer | `Breach_RunsStepOnce`; `Warning_NotifiesTeamLead`; `Reassign_UsesTicketService` |
| T05 SLA attainment | Data set: met ÷ completed by policy, metric, priority, account, period (R8); goal % per policy for reporting; report under Reports | Data set isolation probe; `Attainment_FromTimers_MatchesFixture` |
| T06 Knowledge Base lifecycle | Owner, review date, states Draft → In review → Published → Retired with approval type `KnowledgeArticle` for governed categories; review reminder once; "Create article from ticket" with link kind `DocumentedBy`; known-error flag linking a problem ticket | `GovernedPublish_NeedsApproval`; `ReviewReminder_Once`; `InternalArticle_NotInSuggestApiForAnonymous`; KB probes |
| T07 Portal v1 | Create `CrestApps.OrchardCore.Psa.Portal`; portal shell built from registered sections: My tickets (existing Tickets Portal), Request a service (intake catalog), Help center (KB); portal administrator role per account (all the account's tickets, manage its portal users, approve catalog requests); rate limits | Portal probes in the isolation harness; `PortalAdmin_SeesAccountTickets`; `PortalUser_OwnTicketsOnly_WhenSharingOff` |
| T08 End-to-end W4 | Playwright: ticket → SLA warning → escalation → solve | The test |

**Done when:** each feature can be enabled alone; existing Tickets tests unchanged; W4 passes.

---

## 9. WP05 E-mail (after #817)

- **Needs:** WP03 and pull request #817 merged. **Builds on:** `ticket-support-plan.md` §6.4; #817's `docs/engineering/omnichannel/email-channel-plan.md`. **Plan:** M06, M07 `Tickets.Email`; D-13.
- First action: re-read the merged #817 code; if any contract named below changed, stop and ask.

| Task | Steps | Tests first |
| --- | --- | --- |
| T01 Sender throttling | `IInboundSenderThrottle` in the receiver pipeline: a sender above N messages in M minutes (address settings) is quarantined with a cause | `OverThreshold_Quarantined`; `UnderThreshold_Passes` |
| T02 OAuth mailbox sign-in | OAuth 2.0 authorization code for Microsoft and Google on the address's Email card; refresh tokens stored through Data Connectors in Orchard Core Secrets; IMAP and SMTP use XOAUTH2; failed refresh disables the source and notifies | `ExpiredRefresh_DisablesAndNotifies`; secret never appears in logs |
| T03 Microsoft Graph source | Change notifications (client state validated) trigger a delta query; each message becomes `InboundEmail` for `IEmailInboundReceiver`; cursor advances after commit | Recorded payloads: `DuplicateNotification_OnceOnly`; `ForgedNotification_ReadsNothing` |
| T04 Gmail source | Pub/Sub push (token validated) triggers a history read; same rules as T03 | Same as T03 with Gmail payloads |
| T05 Tickets.Email | Feature `CrestApps.OrchardCore.Tickets.Email`: address → desk and type mapping (Configuration > Tickets); threading by signed token, then `In-Reply-To`/`References`; new thread → ticket (requester through Parties; unknown sender → contact when allowed, else `RequesterUnverified`); reply → public reply; agent address → agent reply; closed beyond window → follow-up; CC → participants of the same account only; automatic mail → nothing, quarantined; outbound e-mails carry token and headers | `NewThread_CreatesTicket`; `Reply_PublicReply`; `AgentReply`; `ClosedBeyondWindow_FollowUp`; `CcOtherAccount_NotParticipant`; `AutoReply_Nothing` |
| T06 End-to-end W2 (to solved) | Playwright with the raw-MIME webhook: e-mail → ticket → reply → solve | The test |

**Done when:** M06 and `Tickets.Email` acceptance criteria pass.

---

## 10. WP06 Agreements

- **Needs:** WP03. **Builds on:** `ticket-time-agreements-reports-plan.md` (decisions §9 bind). **Plan:** M09; D-06, D-19, D-22.

| Task | Steps | Tests first |
| --- | --- | --- |
| T01 Characterize | Tests pinning current balances, warnings, ticket coverage selection, charge-when-logged, work-order usage | The characterization tests |
| T02 Usage in units | `AgreementUsage` gains `Unit` (Minutes, Money, Incidents) and `Quantity`; existing providers report minutes as before | All T01 tests pass |
| T03 Entitlements with dual-read | `Agreement.Entitlements` (Hours, Money, Incidents, Unlimited; period, rollover, purchases); balance service computes per entitlement; when `Entitlements` is empty it reads `AgreementHours` as one Hours entitlement; resumable step writes entitlements from `AgreementHours` with a verification report (R7) | Golden `agreements/balances.json`; migration fixture report matches (integration) |
| T04 Kinds and types | `AddAgreementKind` with the seven kinds; `AgreementType` templates and "New from type" (Content > Agreements > Types) | `NewFromType_CopiesTemplate`; `UnknownKind_Refused` |
| T05 Coverage service | `CoverageRule`s, `ICoverageSubjectFilter` (Tickets registers desk, type, category filters), `IAgreementCoverageService` per plan M09 D rules 2–3; `AgreementCoverageIndex` (multi-row, add to `IndexDeclarationArchitectureTests`); resumable step converting `TicketAgreementTerms` to rules with verification; `ITicketAgreementResolver` becomes an adapter; `TicketTimeChargeResolver` unchanged in behaviour; `Agreement` registered as a Rates scope | Golden `agreements/coverage.json`; `TermsMigration_SameAgreementAsBefore` (integration); benchmark within budget |
| T06 Money and incident usage | Tickets reports incidents for per-incident agreements (one per ticket solved under it); Invoicing Billing reports money for retainers (billable amounts charged to the agreement) through R5 | `PerIncident_CountsSolvedTickets`; `Retainer_DrawsBilledAmounts` |
| T07 Additions and quantity sources | `Addition`s; `IAdditionQuantitySource` with a contact-count source in Accounts (asset counts come in WP11) | `Quantity_ReadAtRunDate`; `ProRata_MidPeriod` |
| T08 Lifecycle, renewals, amendments, top-ups | States per plan M09 G; versions; renewal with uplift; amendments; threshold notices and automatic top-up in the existing background task; failed top-up → payment issue (Checkout on) or notice | State machine tests; `AutoRenew_NewVersionWithUplift`; `TopUp_OncePerThresholdAndPeriod` (integration) |
| T09 Agreements.Billing | Bridge feature: one recurring plan per agreement with additions, synced on change; quantities frozen per run; milestones and overage as `BillableItem`s keyed by source; `IEstimateConversionTarget` (recurring section → agreement); grouping policy hook | `Activate_OneRecurringPlan`; `RunTwice_InvoicesOnce` (integration); `Overage_ItemOnce` |
| T10 Health signals and profitability | Agreements' account health signals (consumption, renewal due); profitability read model (revenue vs cost) | Golden `analytics/kpis.json` agreement rows |
| T11 End-to-end W3 and W8 | Integration and Playwright tests for both workflows | The tests |

**Done when:** all existing Agreements, Tickets and Work Orders tests pass unchanged; acceptance criteria of plan M09 pass.

---

## 11. WP07 Quotes

- **Needs:** WP06. **Builds on:** `invoicing-plan.md` §14 (estimates), `lead-management-plan.md`. **Plan:** M10, M04 (4).

| Task | Steps | Tests first |
| --- | --- | --- |
| T01 Sections and recurring lines | Sections with optional lines; line kinds Labor (priced by `IRateResolver`) and Recurring (frequency, term); one-time and recurring totals | `OptionalLinesSelected_TotalsCorrect` |
| T02 Margin approval | `IEstimateMarginService`; approval type `Estimate` above margin/discount thresholds (settings) | `LowMargin_RequiresApproval` |
| T03 Signature | Accept page captures typed name, time, IP hash, document SHA-256; public page shows no cost | `SignatureHash_MatchesDocument`; `PublicPage_NoCost` |
| T04 Conversion targets | `IEstimateConversionTarget`; conversions recorded and run once per (version, section); Agreements.Billing target used | `Convert_Twice_Once` (integration) |
| T05 Opportunity links | Quote links, forecast category, recurring and one-time amounts on opportunities; "Create quote" | `CreateQuote_LinksBoth` |
| T06 End-to-end W1 (to agreement) | Integration and Playwright | The test |

---

## 12. WP08 Time approval

- **Needs:** WP06. **Builds on:** `ticket-time-agreements-reports-plan.md`, `time-tracker-people-teams-review.md`. **Plan:** M13; D-11, D-12.

| Task | Steps | Tests first |
| --- | --- | --- |
| T01 Work role on entries | `TimeEntry.WorkRoleId` (additive), defaulted from the Staff default role; editable; `ITimeEntryDefaultsProvider` with a Tickets implementation; index column | `Default_FromStaff`; existing Time Tracker tests unchanged |
| T02 Time approval | `TimeApproval` per person and period (pay schedule or weekly); approval type `TimeApproval` (manager rule; project-manager rule added by Projects in WP09); submit, recall; lock entries; reversals through the Time entries page; My time week view with Submit | `Submit_Locks`; `Recall_Unlocks`; `EditApproved_Reversal`; `Over24h_BlocksSubmit` |
| T03 Workforce link | When Workforce Time Clock is on, approving the Workforce timesheet approves the same period's `TimeApproval` (observer, R5) | `WorkforceApproval_ApprovesTimeApproval` |
| T04 Posting | On approval: `RateSnapshot` per billable-stage entry; `BillableItem` for minutes not covered or beyond the balance (overage in entry start order), keyed by entry ID; entries whose source is a work order are skipped (Work Orders bills its labor); optional pay inputs | Golden `time/approval-posting.json`; `Reapprove_NoDuplicates` and parallel approvals (integration) |
| T05 End-to-end W7 and W2 to invoice | Integration and Playwright | The tests |

---

## 13. WP09 Projects

- **Needs:** WP08, WP02. **Plan:** M11; D-08, D-16.

| Task | Steps | Tests first |
| --- | --- | --- |
| T01 Projects core | Create the block (R1); `Project` with phases inside the document (max 50), `ProjectTask` as separate documents, project types, states, numbering, `IProjectAccessService` + scope filter, Projects top-level menu with icon, project page Overview and Tasks, record sources, timeline and attachment access, isolation probes | State machine; `TaskInClosedProject_Refused`; probes |
| T02 Scheduler | Pure `IProjectScheduler` (FS, SS, FF, SF, lag, working calendar from Business Hours, pins), rollups, recalculation under the project lock writing changed tasks only, cycle refusal, read-only Gantt | Golden `projects/networks/*`; property tests; `Cycle_Refused`; `Pin_KeepsAndWarns`; benchmark |
| T03 Templates and conversion | `ProjectTemplate` (roles, not people); create from template; Estimates conversion target (labor section → project) | `Template_CopyFidelity`; `QuoteConvert_OneProject` (integration) |
| T04 Budgets, baselines, change orders | Budget lines; actuals through `IProjectCostSource` (Time Tracker registers approved time at cost); thresholds; baselines (max 10); change orders with approval type `ChangeOrder`; project-manager approver rule for time approvals | `Actuals_EqualApprovedTimeAtCost`; `ChangeOrder_UpdatesBudget` |
| T05 Kanban and charge resolver | Boards source for tasks; Time Tracker charge resolver for project-task time (project's agreement, else coverage) | `TaskBoard_MoveCallsTaskService`; `ProjectTaskTime_ChargedToProjectAgreement` |
| T06 Projects.Billing | Milestone achieved → agreement milestone billable (fixed price); close blockers with override reason | `Milestone_PostsOnce` (integration); `Close_WithUnapprovedTime_Blocked` |
| T07 End-to-end W5 | Integration and Playwright | The test |

---

## 14. WP10 Resource planning

- **Needs:** WP09. **Builds on:** `workforce-management-plan.md`. **Plan:** M12.

| Task | Steps | Tests first |
| --- | --- | --- |
| T01 Work item sources | Create the block; `IWorkItemSource` + `AddWorkItemSource`; sources in Projects (tasks, role demands), Tickets (tickets with planned dates), Dispatch (visits) | Each source with fixtures |
| T02 Capacity | `ICapacityService` from Staff hours, Workforce shifts and time off, Business Hours holidays, calendar absences | Golden `resource-planning/capacity.json` |
| T03 Allocations | `Allocation` (soft, hard), book, move, release; hard over capacity needs `OverrideOverallocation` and a reason; cancelled work releases; time-off approval re-evaluates | `HardOverCapacity_RefusedWithoutPermission`; `Cancelled_Releases` |
| T04 Board | Workforce > Resource Planning: people × time heat map, role-demand rows, filters, masking of titles the viewer cannot see | `Board_MasksTitles`; benchmark |

---

## 15. WP11 CMDB and change management (after #1379)

- **Needs:** WP06 and pull request #1379 merged. **Plan:** M15, M07 (`Tickets.Assets`, `Tickets.Changes`). First action: re-read merged #1379.

| Task | Steps | Tests first |
| --- | --- | --- |
| T01 Relationships | Link kinds (DependsOn, RunsOn, ConnectedTo, InstalledOn, Contains, BackedUpBy) on record links; relationship card on the asset page (`DetailAdmin` driver); impact query (depth 10); `IAssetUsage` for linked CIs | `Impact_DepthLimited`; `LinkedCi_CannotBeDeleted` |
| T02 Lifecycle, keys, licenses | `ExternalKeys`, `LifecycleState`, `ManualFields`; `SoftwareLicense`; expiry notices once | `ExpiryNotice_Once` |
| T03 Discovery seam and quantities | `IAssetDiscoverySource` upsert by external key respecting manual fields; `IAdditionQuantitySource` (active assets of types per account) | `UpsertTwice_NoChange`; `ManualField_Kept`; `Quantity_ActiveOfTypes` |
| T04 Tickets.Assets | Link kind `AffectsAsset`; ticket panel; asset page card listing tickets | `Panel_HidesOtherAccountAsset` |
| T05 Tickets.Changes | `ChangePart`, change type flag, state machine mapped to status categories, risk questionnaire (`ConditionRule`), approval type `Change` with policies by type and risk (CAB = team rule), standard changes from templates, emergency post-approval, change calendar type with overlap warnings (`.agents/skills/crestapps-calendar-types`) | State machine; `Normal_AllCabStages`; `Standard_SkipsApproval`; `Emergency_PostApproval`; `OverlapSameCi_Warns` |
| T06 End-to-end | Change with CAB on a CI (Playwright) | The test |

---

## 16. WP12 Procurement

- **Needs:** WP07. **Builds on:** `commerce/inventory-and-shipping-plan.md`. **Plan:** M16; D-23.

| Task | Steps | Tests first |
| --- | --- | --- |
| T01 Decouple from the storefront | Manifest: depend on Commerce Foundation and Products instead of `CommerceConstants.Features.Area`; move order code (`InventoryOrderEventHandler`, availability for checkout, location service parts that read orders) into a startup marked `[RequireFeatures(Orders)]`; API endpoints and webhooks into their own `[RequireFeatures]` startups; types needed from Commerce.Core moved to the owner's `.Abstractions` | `Inventory_RunsWithoutStorefront` (integration); all existing Inventory tests unchanged |
| T02 Suppliers | Supplier part on Accounts; purchase orders name a supplier account | `PurchaseOrder_SupplierIsAccount` |
| T03 Requisitions | `PurchaseRequisition` with demand `RecordRef`; aggregate into purchase orders by supplier; approval type `PurchaseOrder` by amount; Estimates conversion target (hardware section → requisitions) | `QuoteHardware_ToRequisitions` |
| T04 Serials and assets | Serial and lot tracking on receipt and issue; delivered serialized items create assets; reservations | `SerialIssuedTwice_Refused`; `Delivered_CreatesAssets` |
| T05 End-to-end | Quote hardware → requisition → PO → receipt → assets | The test |

---

## 17. WP13 Billing review and expenses

- **Needs:** WP08, WP09. **Builds on:** `invoicing-plan.md` §18 (billing sources). **Plan:** M17, M14.

| Task | Steps | Tests first |
| --- | --- | --- |
| T01 Review states | `BillableItem` gains review state, adjustments, subject, agreement ID (additive) | Existing Invoicing tests unchanged |
| T02 Review page | Commerce > Billing Review: filters (account, agreement, project, period, source); approve, adjust with reason, non-billable, defer | `Adjust_RecordsBeforeAfter` |
| T03 Posting and grouping | Post to drafts grouped by bill-to, currency and `IInvoiceGroupingPolicy` (Agreements.Billing and Projects.Billing register policies; default from PSA settings); cut-off date; reverse before issue; automatic posting rules per source | `Post_Twice_Once` (integration); golden `billing/month-end` |
| T04 Expenses block | Create `CrestApps.OrchardCore.Expenses`: categories, policies, mileage rates, reports and items, receipts via Attachments, My Work > Expenses and Content > Expenses | `Policy_RequiresJustification`; `Receipt_AccessDeniedToOthers` |
| T05 Expense approval and outputs | Approval type `ExpenseReport` with a policy-exception stage; `IBillableSource`, `IProjectCostSource`, `IPayInputSource`, reimbursement export; currency on expense date; duplicate warning; personal data | `Approve_Mixed_OutputsOnce` (integration); `ForeignCurrency_OnExpenseDate` |
| T06 End-to-end W9 | Integration and Playwright | The test |

---

## 18. WP14 Accounting sync and portal v2

- **Needs:** WP13. **Plan:** M19 (accounting), M22, M18 (deferred-revenue export only); D-09.

| Task | Steps | Tests first |
| --- | --- | --- |
| T01 AccountingSync block | `IAccountingProvider` (customers, items, invoices, credit notes out; payments in), mapping tables, sync log, leased queue with backoff, Settings > Integrations | Fake provider: `IssuedInvoice_ExportsOnce`; `Payment_ImportsOnce`; `ProviderDown_Retries` |
| T02 Providers | One feature per accounting package, credentials in Data Connectors, egress-guarded | Recorded API payload tests |
| T03 Deferred revenue export | Prepaid entitlement invoice lines post to a configurable deferred-revenue item; per period, entitlement usage of prepaid agreements exported for release in the accounting package | `PrepaidUsage_ExportedPerPeriod` |
| T04 Portal v2 | Sections: My invoices (existing), Agreements and balances (no costs), Projects (customer-visible fields, milestones, change order approval), Quotes | Portal probes for each section |

---

## 19. WP15 Analytics, AI and RMM

- **Needs:** WP14, WP10, WP11. **Plan:** M20, M23, plan section 20, M19 (RMM); D-10.

| Task | Steps | Tests first |
| --- | --- | --- |
| T01 Data sets | Each block's data sets (Rates, agreements and balances, projects, allocations, time approval compliance, expenses, billing review, changes) with scope filters | One probe per data set |
| T02 Role dashboards | Default dashboards per PSA role on Orchard Core's Admin Dashboard with the existing report widgets, created by the `Psa` recipe; KPI tile and trend widgets in Reports; drill-through to lists | `Dashboard_HidesWidgetsUserCannotRun` |
| T03 Psa.Analytics | Create the module; KPIs of plan section 19 by joining published data sets; nightly snapshots (R14); `ViewPsaFinancials` | Golden `analytics/kpis.json` |
| T04 AI use cases | One commit each: triage (per-desk confidence threshold, audited auto-apply), summary, suggested resolution and articles (scope-filtered retrieval), draft reply, time entry suggestions, receipt extraction, article from ticket, project plan draft; prompts as templates; every tool acts as the user | Per tool: isolation probe and an evaluation set with a recorded baseline |
| T05 RMM connectors | `CrestApps.OrchardCore.Integrations.Rmm`: device sync via `IAssetDiscoverySource`, alerts via `IProviderWebhookInbox` with alert-key de-duplication, auto-resolve option, account mapping; first provider feature | `SameAlertTwice_OneTicket`; `Cleared_ResolvesWhenEnabled`; `Unmapped_GoesToTriageDesk` |

---

## 20. WP16 Enterprise readiness and migration toolkit

- **Needs:** WP15. **Plan:** sections 16, 23, 24; M21 (7).

| Task | Steps | Done when |
| --- | --- | --- |
| T01 Threat models | STRIDE per bounded context in `docs/engineering/psa/security/` | Reviewed |
| T02 ASVS 5.0 L2 | Checklist per module with the test proving each requirement | No unproven L2 item |
| T03 Penetration test | External test of a staging tenant | No high finding open |
| T04 Load and soak | Reference data set; budgets of plan section 23 | All budgets met |
| T05 Disaster recovery | Point-in-time restore of one tenant | RPO 15 min, RTO 4 h met |
| T06 Evidence exports | Access review, audit, configuration history, backup logs | Usable by a reviewer |
| T07 Localization and accessibility | Every new string localizable (`ViewLocalizerFormatArgumentsArchitectureTests`, `LocalizationSourceJsonIgnoreArchitectureTests` pass); WCAG 2.2 AA check of new pages (keyboard, labels, contrast) | Issues fixed or listed |
| T08 Migration toolkit | ContentTransfer CSV import templates for accounts, contacts, assets, agreements with entitlements, open tickets, rate cards; docs page "Moving from another PSA" (no product names) | Each template imports a sample file |
| T09 Demo data | Demo-data recipe for trials | Fresh demo tenant works |
| T10 Final end-to-end | Fresh `Psa` tenant runs W1–W9 | All green |

---

## 21. Appendix A: Done checklist for every work package

- [ ] Every task's **Tests first** list exists and passes; each test failed first.
- [ ] The whole unit test project passed once at the end, including all 36 architecture tests: `AdminPostAntiforgeryArchitectureTests`, `AggregateLifecycleArchitectureTests`, `BinaryAssetIntegrityTests`, `ContactCenterArchitectureGuardTests`, `ContentsAdminListTermNameArchitectureTests`, `CreateViewTitleConventionTests`, `DependencyInjectionArchitectureTests`, `DeploymentStepSerializationArchitectureTests`, `DeploymentStepViewsArchitectureTests`, `DuplicatePatternRatchetTests`, `EditorRootViewArchitectureTests`, `EmptyCatchRatchetTests`, `FeatureIdLiteralRatchetTests`, `FileSizeRatchetTests`, `IndexDeclarationArchitectureTests`, `LayeringRatchetTests`, `LearningNotificationArchitectureTests`, `LocalizationSourceJsonIgnoreArchitectureTests`, `MigrationAdditiveOnlyGuardTests`, `ModuleDependencyGraphTests`, `OrderEventHandlerDependencyTests`, `PaymentProviderCapabilityTruthfulnessTests`, `PdfLibraryLicenseTests`, `PhoneNumberCanonicalizationArchitectureTests`, `ProjectReferenceRulesTests`, `ProviderHttpRetryArchitectureTests`, `ProviderWriteIdempotencyArchitectureTests`, `RecipeSchemaCoverageArchitectureTests`, `ResumableStepRequestTests`, `SiteSettingsGroupPermissionRegistrationTests`, `TenantHierarchyArchitectureTests`, `TimeZonePickerArchitectureTests`, `UnboundedCollectionLoadTests`, `UserListLoadArchitectureTests`, `ViewLocalizerFormatArgumentsArchitectureTests`, `WorkflowActivityViewsArchitectureTests`, plus the PSA guards from WP01-T06.
- [ ] PSA integration tests pass (SQLite locally; SQL Server in CI).
- [ ] The work package's end-to-end workflow passes (Playwright).
- [ ] Changed pages checked in a browser in both list layouts and both row-action modes.
- [ ] Build passes with warnings as errors.
- [ ] Golden fixtures unchanged (manifest test passes).
- [ ] Public API baselines updated and reviewed.
- [ ] New projects in `CrestApps.OrchardCore.slnx`; new modules in the Targets project.
- [ ] Feature IDs are constants; permissions on every action and endpoint.
- [ ] Scope filters and isolation probes for every new record family.
- [ ] Locks and idempotency keys for money, usage, stock and time.
- [ ] Recipe and deployment steps with schemas for every record type; round-trip test.
- [ ] Migrations additive; data moves by expand-and-contract with a verification report.
- [ ] Personal-data contributors; telemetry on background tasks.
- [ ] Pages placed per section 1.6; top-level menu items have icons.
- [ ] Module README, both manuals, `sidebars.js`, docs checks pass; `building-blocks.md` updated when a block or extension point changed.
- [ ] No unused usings or services; no commented-out code; no competitor names.
- [ ] Commits authored by the configured human author only, one per task, messages `WP<NN>-T<MM> ...`.
- [ ] Appendix C row updated.

## 22. Appendix B: Glossary

| Term | Meaning |
| --- | --- |
| Work package (WP) | One plan slice delivered as one branch and one pull request |
| Task (T) | One commit inside a work package |
| Building block | A module that owns one concept (`docs/engineering/building-blocks.md`) |
| `.Abstractions` / `.Core` | Contracts / shared implementations |
| Catalog record | YesSql document (`CatalogItem`, `DocumentCatalog` store) |
| `RecordRef` / `PartyRef` | Reference to any record / to who a record belongs to |
| Stage | Time Tracker stage; the industry's "work type"; its categories decide billable and payable |
| `ChargedTo` | The record (agreement) time counts against, set when logged |
| Scope filter | SQL restriction to what the viewer may see |
| Golden fixture | Expected test data written independently of the implementation |
| Expand and contract | Additive migration, move beside, dual-read, verify, remove old later |

## 23. Appendix C: Progress

| Work package | Phase | Needs | State | Pull request |
| --- | --- | --- | --- | --- |
| WP00 Readiness | P0 | — | Not started | — |
| WP01 Foundations | P1 | WP00 | Not started | — |
| WP02 Boards | P1 | WP00 | Not started | — |
| WP03 Rates and PSA skeleton | P2 | WP01 | Not started | — |
| WP04 Service operations | P3 | WP03 | Not started | — |
| WP05 E-mail | P3 | WP03, #817 | Waiting for #817 | — |
| WP06 Agreements | P4 | WP03 | Not started | — |
| WP07 Quotes | P4 | WP06 | Not started | — |
| WP08 Time approval | P5 | WP06 | Not started | — |
| WP09 Projects | P5 | WP08, WP02 | Not started | — |
| WP10 Resource planning | P5 | WP09 | Not started | — |
| WP11 CMDB and changes | P6 | WP06, #1379 | Waiting for #1379 | — |
| WP12 Procurement | P6 | WP07 | Not started | — |
| WP13 Billing review and expenses | P7 | WP08, WP09 | Not started | — |
| WP14 Accounting and portal v2 | P7 | WP13 | Not started | — |
| WP15 Analytics, AI, RMM | P8 | WP14, WP10, WP11 | Not started | — |
| WP16 Enterprise readiness | P9 | WP15 | Not started | — |
