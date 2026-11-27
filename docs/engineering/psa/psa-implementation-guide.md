# PSA Implementation Guide

Status: proposal, 27 November 2026. This guide turns [`psa-platform-plan.md`](psa-platform-plan.md) into small, ordered tasks that a coding agent can carry out one at a time. It contains no code. Read it together with the plan: the plan says **what** each module is (its specification, sections M01–M23); this guide says **how to build it, in which order, and how to know each step is done**.

The guide is written for an agent that should not have to make architecture decisions. If a step seems to need a decision that is not written here or in the plan, stop and ask (section 1.6).

---

## Contents

1. How to work (read first, every time)
2. Environment and commands
3. Recipes: the repeatable "how to" for each kind of change
4. Phase 0: Discovery and validation
5. Phase 1: Platform foundations
6. Phase 2: Core business blocks
7. Phase 3: Service operations
8. Phase 4: Commercial management
9. Phase 5: Projects and time
10. Phase 6: Field, assets and procurement
11. Phase 7: Financial operations
12. Phase 8: Intelligence and automation
13. Phase 9: Enterprise readiness
14. Appendix A: Done checklist for every pull request
15. Appendix B: Glossary of repository terms
16. Appendix C: Task index

---

## 1. How to work (read first, every time)

### 1.1 One task, one pull request

- Every task in sections 4–13 has an ID such as `P2-03`. Do exactly one task per pull request, in the order listed, unless the task says it can run in parallel.
- Branch name: `psa/<task-id>-<short-name>`, for example `psa/p2-03-rates-resolver`.
- Pull request title: `[<task-id>] <task title>`. In the description, copy the task's **Done when** list and tick each item.
- Do not start a task until every task in its **Needs** line is merged.

### 1.2 Read before you write

For every task, before changing anything:

1. Read the task in this guide completely.
2. Read the module's specification in the plan (the section named in the task, for example "Plan M05").
3. Read `AGENTS.md` sections *Building Blocks and Reuse*, *Common Development Tasks → Adding a New Module*, *Coding Standards and Conventions*, *Testing Practices* and *Anti-Patterns to Avoid*.
4. Read `CLAUDE.md` at the repository root.
5. Read `docs/engineering/building-blocks.md` and check that every concept the task touches has the owner the task names.
6. Read every file the task lists under **Read first**. They are the templates you will copy the shape of.
7. If the task names a skill under `.agents/skills/`, read its `SKILL.md` and follow it.

### 1.3 The work loop (do this for every behaviour)

1. **Write a failing test** for one rule from the task's **Tests first** list. Run it and confirm it fails for the right reason (not a compile error in unrelated code).
2. **Write the smallest code** that makes it pass.
3. **Refactor** while the test stays green.
4. Repeat until every test in the list exists and passes.
5. Run the whole test project (section 2.3) and the architecture tests.
6. Update docs, README, `building-blocks.md` and public API baselines (Appendix A).
7. Fill in the Done checklist (Appendix A) in the pull request.

### 1.4 Rules you must never break

- **Never** put the same concept in two places. If the concept already has an owner in `building-blocks.md`, use or extend that owner.
- **Never** let a `.Core` or `.Abstractions` project reference a module project. Module code never uses a type from another module's assembly. Shared types go to `.Abstractions` (contracts) or `.Core` (implementations).
- **Never** reference anything named `CrestApps.OrchardCore.Psa*` from outside the PSA modules.
- **Never** read another module's index tables directly. Call its service through its `.Abstractions` interface.
- **Never** change money, entitlements, stock or time without an idempotency key and a distributed lock (Recipe R11).
- **Never** return a record to a user without passing it through the record family's scope filter (Recipe R12).
- **Never** use `DateTime.UtcNow`; inject `IClock`. Never show a UTC time to a person; use `ILocalClock`.
- **Never** hand-write an admin list or button row: use `<admin-list>` and `action-bar-bottom` (skills `crestapps-admin-index-views`, `crestapps-admin-edit-views`).
- **Never** add Claude (or any AI) as a commit author or co-author. Commit only as the configured human author.
- **Never** write documentation that describes behaviour that is not implemented yet.

### 1.5 Size limits

- Keep a pull request under about 1,500 changed lines excluding tests, generated baselines and docs. If a task grows past that, split it at a step boundary and say so in the pull request.
- Each public type in its own file named after the type (repository rule).

### 1.6 When to stop and ask

Stop, write down the question in the pull request (or the conversation) and wait, when:

- a step needs a type, service or feature that does not exist and this guide does not tell you to create it;
- an existing test fails and the task does not say it should change;
- a building block owner listed here does not match `building-blocks.md`;
- a migration would delete or rewrite existing data in a way the task does not describe;
- the build cannot restore packages (see section 2.2): report it, do not work around it.

---

## 2. Environment and commands

### 2.1 Tools

- .NET SDK as pinned in `global.json` (currently `11.0.100` with `rollForward: latestMajor`). `AGENTS.md` still mentions .NET 10; `global.json` wins.
- Node.js and npm for assets (`npm install`, `npm run rebuild`).
- Orchard Core packages come from the Orchard Core preview feed and CrestApps.Core packages from the CrestApps feed (see `NuGet.config`).

### 2.2 Build

```
npm install
npm run rebuild
dotnet build -c Release -warnaserror /p:TreatWarningsAsErrors=true /p:RunAnalyzers=true /p:NuGetAudit=false
```

If the build fails with `NU1301` errors about `nuget.cloudsmith.io`, the environment cannot reach the package feeds. Report this in the pull request; do not add other feeds or vendor packages.

After the first successful build, list `.agents/skills/` again: a package may add skills on first build (see `AGENTS.md`).

### 2.3 Test

```
dotnet test -c Release --no-build ./tests/CrestApps.OrchardCore.Tests/CrestApps.OrchardCore.Tests.csproj
```

- While iterating, filter to your test class: add `--filter "FullyQualifiedName~CrestApps.OrchardCore.Tests.<Area>"`.
- Always run the architecture tests before finishing: filter `FullyQualifiedName~CrestApps.OrchardCore.Tests.Architecture`.
- Public API approval tests live in `tests/CrestApps.OrchardCore.Tests/PublicApi` with baselines in `PublicApi/Baselines/*.approved.txt`. When you add or change a public type in a tracked assembly, update its baseline in the same pull request and review the diff.

### 2.4 Docs

```
cd src/CrestApps.Docs
npm run check:manuals
npm run build
```

Read `src/CrestApps.Docs/README.md` before editing any page. User Manual pages are under `src/CrestApps.Docs/docs/user-manual/`; Technical Manual pages are the rest of `src/CrestApps.Docs/docs/`.

### 2.5 Run the site

```
cd src/Startup/CrestApps.OrchardCore.Cms.Web
dotnet run
```

---

## 3. Recipes: the repeatable "how to"

Every task refers to these recipes by number. They describe the shape of the work using existing code as templates. When a recipe says "copy the shape of", open that file and follow its structure, naming and documentation style; do not copy unrelated logic.

### R1. Create a new building block (three projects)

Template: the Agreements block.

1. Create the folders:
   - `src/Abstractions/CrestApps.OrchardCore.<Name>.Abstractions/`
   - `src/Core/CrestApps.OrchardCore.<Name>.Core/`
   - `src/Modules/CrestApps.OrchardCore.<Name>/`
2. Create each `.csproj` by copying the shape of the matching Agreements project file (`src/Abstractions/CrestApps.OrchardCore.Agreements.Abstractions/CrestApps.OrchardCore.Agreements.Abstractions.csproj`, the `.Core` one and the module one). Keep only the package and project references the new block needs.
3. Add all three projects to `CrestApps.OrchardCore.slnx` in the matching sections (Abstractions, Core, Modules), in alphabetical order.
4. Add the module project to `src/Targets/CrestApps.OrchardCore.Cms.Core.Targets/CrestApps.OrchardCore.Cms.Core.Targets.csproj` as a `ProjectReference ... PrivateAssets="none"`, in alphabetical order (that is where the existing modules are listed).
5. In `.Abstractions`, create:
   - `<Name>Constants.cs` with `Feature` IDs, the catalog `CollectionName`, and any open "kind" strings. Copy the shape of `AgreementsConstants.cs`.
   - `<Name>Permissions.cs` with every permission. Copy the shape of `AgreementsPermissions.cs`.
   - `Models/` and `Services/` folders for records and interfaces.
6. In `.Core`, create:
   - `<Name>ServiceCollectionExtensions.cs` with an `Add<Name>Core` method. Copy the shape of `AgreementsServiceCollectionExtensions.cs`.
   - `Indexes/` (index, index provider, index schema) and `Services/` (store, manager, handler).
7. In the module, create `Manifest.cs`, `Startup.cs`, `README.md`, `Migrations/`, `Controllers/`, `Drivers/`, `Views/`, `Recipes/`, `Deployments/`, `Navigation/`, `ViewModels/`. Copy the shape of the Agreements module.
8. Add the block to `docs/engineering/building-blocks.md` in the right catalog table, with "Reuse it through" and "Do not" columns filled in.
9. Add a Technical Manual page and a User Manual page, and add both to `sidebars.js` (section 2.4).

### R2. Add a catalog record (a stored business record)

Template: `Agreement` → `AgreementIndex` → `AgreementIndexProvider` → `AgreementsIndexSchema` → `AgreementStore` → `AgreementManager` → `AgreementHandler` → `AgreementsMigrations`.

1. **Model** in `.Abstractions/Models/<Record>.cs`: a sealed class deriving from `CatalogItem` (as `Agreement` does). Document every property with XML comments. Keep extension data in a `Properties` bag if other modules must add their own terms.
2. **Index** in `.Core/Indexes/<Record>Index.cs`: one column per field that lists, filters or lookups need. Nothing else.
3. **Index provider** in `.Core/Indexes/<Record>IndexProvider.cs`: maps the record to the index row.
4. **Index schema** in `.Core/Indexes/<Name>IndexSchema.cs`: creates the index table and its database indexes. Add a database index for every column used in a `WHERE` or `ORDER BY`.
5. **Store** in `.Core/Services/<Record>Store.cs`: derive from `DocumentCatalog<TRecord, TIndex>` and set `CollectionName` from the constants (as `AgreementStore` does).
6. **Manager**: use the catalog manager pattern (`ICatalogManager<T>`) so handlers run. Add validation in the handler.
7. **Handler**: derive from `AuditedCatalogEntryHandlerBase<T>` (in `CrestApps.OrchardCore.Core/Handlers`) so created/modified/owner stamps are set. Put validation rules here.
8. **Migration** in the module's `Migrations/`: create the index table through the schema class, in a numbered `CreateAsync`/`UpdateFromNAsync` method. Never change a released migration; add a new step.
9. **Numbering** if the record has a human number: register a series with `AddNumberSeries` and get numbers from `INumberSeries` in the same unit of work (see how Agreements numbers `SA-00001`).
10. **Register** store, manager, handler and index provider in `Add<Name>Core`, and call it from the module `Startup`.

### R3. Add an admin list and editor for a catalog record

1. Read and follow `.agents/skills/crestapps-admin-index-views/SKILL.md` and `.agents/skills/crestapps-admin-edit-views/SKILL.md`.
2. Controller: derive from `CatalogAdminController<T>` (`src/Core/CrestApps.OrchardCore.Core/Controllers/CatalogAdminController.cs`). Do not write your own index/create/edit/delete actions unless the record needs a command that is not CRUD (then add only that action).
3. Display driver for the record with `SummaryAdmin` and `SummaryAdminGrid` locations for rows and an `Edit` location for the form (copy the shape of `AgreementDisplayDriver.cs`).
4. Views: `Index.cshtml` uses `<admin-list>`; edit views end with `<div class="action-bar-bottom">`.
5. Admin menu entry in `Navigation/<Name>AdminMenu.cs`, guarded by the view permission.
6. Every action checks a permission from `<Name>Permissions`.

### R4. Add an extension point (a provider interface other modules implement)

1. Put the interface in `.Abstractions/Services/I<Thing>.cs` with full XML docs, including every `<param>`.
2. Add a registration helper `Add<Thing><T>(this IServiceCollection services, ...)` in `.Abstractions` (shape: `AddApprovalType`, `AddRecordSource`, `AddCalendarType`).
3. If consumers register an open kind, the kind is a string constant owned by the consumer, never an enum in the building block.
4. The building block resolves all registered implementations with `IEnumerable<I<Thing>>` and must work when none are registered.
5. Write a test with a fake implementation proving the block calls it.

### R5. Add an optional integration between two blocks

- Put the code in the consumer module, in a startup class marked `[RequireFeatures(<OtherBlock>Constants.Feature.Id)]`, or in a bridge feature (for example `Agreements.Billing`).
- Reference the other block's `.Abstractions` (and `.Core` only to reuse a base class).
- Feature IDs come from constants, never string literals (`FeatureIdLiteralRatchetTests`).

### R6. Add recipe and deployment steps for a record type

- Recipe step: derive from `NamedCatalogRecipeStep` (`src/Core/CrestApps.OrchardCore.Core/Recipes/NamedCatalogRecipeStep.cs`); add a JSON schema class (shape: `AgreementsRecipeStepSchema.cs`).
- Deployment: `NamedCatalogDeploymentSource` plus a step, step display driver and schema (shape: `src/Modules/CrestApps.OrchardCore.Agreements/Deployments/*`) with `CatalogDeploymentSerializer`.
- Test: export then import produces identical records.

### R7. Migrate existing data in a long-running way

- Use resumable steps: implement `IResumableStep`, register with `AddResumableStep<T>()`, and request it from the migration with `ResumableSteps.RequestAsync(name)`. The background task runs it in batches and keeps a cursor.
- The step must be idempotent: running it twice gives the same result.
- Write a test that runs the step on fixture data twice and compares results with the expected data.

### R8. Add a report data set or report

- Read and follow `.agents/skills/crestapps-report-data-source/SKILL.md`.
- Derive data sets from `IndexedReportDataSet` and register with the Reports helpers; reports with `AddReport<T>`.
- The data set must apply the record family's scope filter (R12).

### R9. Add workflow events and tasks, webhooks and audit events

- Workflow events and tasks: copy the shape of Tickets' `WorkflowsStartup.cs` and `Workflows/` folder; register them in a `[RequireFeatures("OrchardCore.Workflows")]`-style startup using the Orchard feature ID constant from `OrchardCoreFeatureIds`.
- Webhooks: implement `IWebhookEventProvider` (Webhooks.Abstractions) in a startup that requires the Webhooks feature. Event names are `{entity}.{verb}`. Payload carries IDs and changed fields only.
- Audit: copy the shape of `src/Modules/CrestApps.OrchardCore.Tickets/Integrations/TicketWebhooksAndAudit.cs`.

### R10. Add a JSON API

- Use `MapModuleApi` (module API helper in `CrestApps.OrchardCore.Core`): route group `api/<module>/v1`, `Api` auth scheme, problem details via `ApiProblems`, `IdempotencyEndpointFilter` on POST/PUT/PATCH.
- Template: `src/Modules/CrestApps.OrchardCore.Tickets/Api/TicketApiEndpoints.cs` and `ApiStartup.cs`.
- Every endpoint checks a permission and the scope filter. Lists use cursor or page parameters with a maximum page size of 200.

### R11. Change money, entitlements, stock or time safely

1. Choose a lock name that includes the aggregate ID (for example `agreement:<id>`). Use the distributed lock helpers (`DistributedLockExtensions`, `LockAllAsync` when you need several, in a fixed order).
2. Inside the lock, read the current state again.
3. Use an idempotency key derived from the source record (for example the time entry ID) so a second call does nothing.
4. Save in one YesSql session (one unit of work).
5. Notify observers **after** the save.
6. Test: call the command twice, and call it from two parallel tasks; the effect must happen once.

### R12. Scope filter (who may see which records)

Template: `ITicketAccessService` and `TicketScopeFilter` in Tickets.

1. Add an access service interface in `.Abstractions` (for example `IProjectAccessService`) with methods "can view", "can edit" and a way to restrict an index query.
2. Apply the restriction in SQL on the index (never load everything and filter in memory).
3. Use it in every list, API, data set, search result, attachment access provider, timeline access provider, record source and AI tool for that record family.
4. Add the record family to the isolation harness (task P1-05).

### R13. Add a personal-data contributor

- Implement `IPersonalDataContributor` (PersonalData.Abstractions) for every record that holds personal data: export the person's data; on erasure, clear personal fields but keep financial facts (copy the shape of `InvoicingPersonalDataContributor`).

### R14. Add telemetry to a background task

- Background tasks run their body through `BackgroundTaskTelemetry` (`this.RunAsync(run => ...)`).
- Meters and activity sources come from `CrestAppsOrchardCoreTelemetry.CreateMeter` / `CreateActivitySource("CrestApps.OrchardCore.<Area>")`. Never put IDs, names, phone numbers or tenant names in metric tags.

### R15. Tests: the shape to copy

- Tests live in `tests/CrestApps.OrchardCore.Tests/<Area>/<ClassUnderTest>Tests.cs`, sealed classes, xUnit.
- Copy the "Kit" style from `tests/CrestApps.OrchardCore.Tests/Agreements/AgreementsTests.cs`: a small kit that builds a harness with the services under test and test doubles from `tests/CrestApps.OrchardCore.Tests/Doubles`.
- Name tests `{Method}_{Scenario}_{Expected}` or as a sentence (the Agreements tests use sentence style); separate Arrange, Act and Assert with blank lines and comments.
- Use a fixed clock time (the Agreements tests use Monday 16 November 2026, noon in New York) so dates are stable.

---

## 4. Phase 0: Discovery and validation

Goal: remove the plan's assumptions before building. Output is documents, not features. Plan section 22, row P0.

### P0-01 Baseline the repository

- **Needs:** nothing.
- **Steps:**
  1. Build and run all tests (section 2).
  2. Record in `docs/engineering/psa/p0-baseline.md`: date, commit, build result, test counts (passed, failed, skipped), and any environment problems.
- **Done when:** the baseline file is merged; failing tests (if any) are listed with their names.

### P0-02 Accept the architecture decisions

- **Needs:** P0-01.
- **Steps:**
  1. Create `docs/engineering/psa/adr/` with one file per decision ADR-01 to ADR-10 from plan section 25.1, using this layout: *Status*, *Context*, *Decision*, *Alternatives*, *Consequences*.
  2. Leave *Status: Proposed*. A maintainer changes it to *Accepted*.
- **Done when:** ten ADR files exist and a maintainer has marked each Accepted or written a change.

### P0-03 Spike: Inventory without the storefront Commerce feature

- **Needs:** P0-01.
- **Steps:**
  1. List every type Inventory (module, `.Core`) uses from Commerce, Carts, Checkout and Orders, using the compiler (remove the dependency in a throwaway branch and record the errors). Do not merge the throwaway branch.
  2. For each, record whether it lives in Commerce Foundation, Products, or the storefront Commerce feature.
  3. Write `docs/engineering/psa/p0-inventory-commerce.md` with the list and a proposed change.
- **Done when:** the report says clearly "Inventory can / cannot run with only Products and Commerce Foundation" and lists the changes task P6-04 must make.

### P0-04 Spike: YesSql volume

- **Needs:** P0-01.
- **Steps:**
  1. In the benchmarks project (`tests/CrestApps.OrchardCore.Benchmarks`), add a benchmark that inserts 1,000,000 time-entry-like documents with an index of 8 columns and 100,000 task-like documents with 6 columns, on SQLite and SQL Server.
  2. Measure: insert throughput, a filtered page query on two columns, a count by one column.
  3. Write `docs/engineering/psa/p0-yessql-volume.md` with numbers and the index columns that must exist.
- **Done when:** the report gives p95 times for the three queries and a recommendation for the TimeEntry and ProjectTask index columns.

### P0-05 Decide where contact contracts live (ADR-07)

- **Needs:** P0-01.
- **Steps:** list every public type in `src/Core/CrestApps.OrchardCore.Omnichannel.Core` that Tickets, a portal or e-mail intake would need for contacts (contact lookup by e-mail, phone, account membership). Propose either `Omnichannel.Abstractions` or a new `Contacts.Abstractions`. Write the proposal into ADR-07.
- **Done when:** ADR-07 names the target project and the list of types to move.

### P0-06 Confirm open assumptions

- **Needs:** P0-02.
- **Steps:** copy plan section 25.3 into `docs/engineering/psa/assumptions.md` and get an answer from a maintainer for each line (performance targets, accounting stance, offline field support, session and MFA defaults, RPO/RTO).
- **Done when:** every assumption is answered or explicitly deferred with a phase.

---

## 5. Phase 1: Platform foundations

Goal: shared mechanisms every later task uses. Plan section 22, row P1; specifications M01, M02, M03.

### P1-01 Record links: contracts and store (M01)

- **Needs:** P0-02.
- **Read first:** `src/Abstractions/CrestApps.OrchardCore.Abstractions/Records/IRecordSource.cs`, the `RecordRef` type next to it, `src/Core/CrestApps.OrchardCore.Tickets.Core/Indexes/TicketLinkIndex.cs`, Recipe R2, R4.
- **Create:**
  - In `src/Abstractions/CrestApps.OrchardCore.Abstractions/Records/`: `RecordLink.cs` (From, To, Kind, CreatedById, CreatedUtc, Properties), `IRecordLinkService.cs`, `RecordLinkKind.cs` (Name, InverseName, DisplayName), registration helper `AddRecordLinkKind`.
  - In `src/Core/CrestApps.OrchardCore.YesSql.Core` (or `CrestApps.OrchardCore.Core` if indexes live there; follow where `RecordRef` helpers live): `RecordLinkIndex`, provider, schema, store, `RecordLinkService`.
- **Rules:**
  1. A link is unique per (From, To, Kind).
  2. Creating a link also makes it findable from the other end under the inverse kind.
  3. Link and unlink require view permission on both records, checked through each record kind's `IRecordSource`.
  4. Unknown kinds are refused.
- **Tests first:** `Link_SameTripleTwice_StoresOnce`; `List_FromTarget_ReturnsInverseKind`; `Link_WithoutViewPermissionOnTarget_IsRefused`; `Link_UnknownKind_IsRefused`; `Unlink_RemovesBothDirections`.
- **Done when:** tests pass; public API baselines updated; `building-blocks.md` platform table has a "Links between records" row.
- **Don't:** migrate Tickets yet (P1-02).

### P1-02 Record links: view component and Tickets migration (M01)

- **Needs:** P1-01.
- **Read first:** `src/Modules/CrestApps.OrchardCore.Tickets` links UI and `TicketLinkKinds`, plan M01, Recipe R7.
- **Steps:**
  1. Add a `RecordLinks` view component in the Resources module that lists links of a record with add/remove for users allowed to.
  2. Register Tickets' link kinds (Related, Parent/Child, Duplicate, FollowUpOf, Problem) through `AddRecordLinkKind` in Tickets.
  3. Write a resumable step in Tickets that copies existing ticket links into record links.
  4. Switch Tickets to read and write links through `IRecordLinkService`; keep `TicketLinkKinds` constants.
  5. Keep the behaviour "solving a problem solves its incidents" unchanged.
- **Tests first:** characterization tests for current Tickets link behaviour **before** changing code (problem solves incidents, links visible both ways, merge); then `ResumableStep_RunTwice_CopiesEachLinkOnce`.
- **Done when:** every existing Tickets test passes unchanged; the new tests pass; the Tickets docs page mentions nothing new to users (behaviour is the same).

### P1-03 Boards building block (M02)

- **Needs:** P0-02.
- **Read first:** `ITicketBoardService`, `TicketBoard`, `TicketBoardColumn`, `ticket-board.js`, `building-blocks.md` row "A board of cards in columns", Recipe R1, R4.
- **Create:** `CrestApps.OrchardCore.Boards` (Abstractions, Core, module) with `IBoardSource` (Kind, columns, cards for a viewer, move), `Board` record (name, source kind, columns as status sets, filter, sharing: everyone/team/personal, WIP limit per column, swimlane field), board admin pages (R3), the board page, the move endpoint. Move the script to the Resources module as a generic board script.
- **Steps:**
  1. Build the block with a fake source in tests.
  2. Make Tickets register a ticket board source; its `MoveAsync` calls the ticket service (never writes status directly).
  3. Migrate existing ticket boards into `Board` records (resumable step, R7).
  4. Remove the private board code from Tickets once its tests pass on the new block.
- **Tests first:** characterization tests of ticket boards first; then `Move_CallsSourceMove_NotStatusWrite`, `Cards_AreFilteredBySourceAccess`, `WipLimit_Exceeded_ReturnsWarning`.
- **Done when:** ticket boards work as before (same tests); Boards is in `building-blocks.md` replacing the "Tickets keeps the only one" note.
- **Don't:** add project or CRM boards yet.

### P1-04 Approvals: team approver rule, signed e-mail decisions, delegation (M03)

- **Needs:** P0-02.
- **Read first:** `src/Modules/CrestApps.OrchardCore.Approvals/README.md`, `IApproverRuleProvider`, `ApprovalTypeHandlerBase`, `ITeamMembershipService`, `SignedTokenService`.
- **Steps:**
  1. Add a `Team` approver rule provider in a `[RequireFeatures]` startup on the Teams feature: approvers are the team's members; optional "at least N".
  2. Add decision links: each approver notification can carry an approve and a reject link built with `SignedTokenService` (single use, expiry from settings). The link opens a confirmation page; for approval types marked `RequireSignedInDecision`, the person must be signed in.
  3. Add delegation: a user sets a delegate and a date range; during it, the delegate is asked instead and the decision records "on behalf of".
- **Tests first:** `TeamRule_ReturnsCurrentMembers`; `TeamRule_MinimumApprovals_Respected`; `DecisionLink_UsedTwice_SecondIsRefused`; `DecisionLink_Expired_IsRefused`; `Delegation_InRange_AsksDelegate`; built-in rules still hold (`Requester_CannotApprove`).
- **Done when:** tests pass; Approvals docs (both manuals) updated.

### P1-05 Isolation harness and scope-filter convention

- **Needs:** P0-02.
- **Read first:** `ITicketAccessService`, `TicketScopeFilter`, `SourceAccess` types in `CrestApps.OrchardCore.Abstractions`, plan section 16.2, Recipe R12.
- **Steps:**
  1. Write the convention in `building-blocks.md` (platform table): every record family has an access service and an SQL scope filter; name pattern `I<Record>AccessService` and `<Record>ScopeFilter`.
  2. Create a test helper in `tests/CrestApps.OrchardCore.Tests/Architecture/Isolation/`: it builds two accounts (A, B), one staff user scoped to A's desk, one portal user of A, one portal user of B, and records for both accounts. Each record family registers a "probe" with the helper: a function that lists records through a given channel (list service, API handler, data set, AI tool) as a given user.
  3. The helper asserts no probe returns a record of the other account.
  4. Add probes for Tickets, Invoicing (my invoices) and Agreements (account tab).
- **Tests first:** the probes themselves are the tests; make one fail on purpose locally (remove a filter) to prove the harness catches it, then restore.
- **Done when:** the harness runs in the normal test run; the convention is documented.

### P1-06 Architecture guards for PSA

- **Needs:** P0-02.
- **Read first:** `tests/CrestApps.OrchardCore.Tests/Architecture/LayeringRatchetTests.cs`.
- **Steps:**
  1. Add a rule: no assembly whose name does not start with `CrestApps.OrchardCore.Psa` may reference one that does.
  2. Add a test that reads every `Manifest.cs` feature dependency graph and fails on a cycle (use the same assembly scan the layering test uses).
- **Tests first:** both rules as tests; prove each fails with a temporary bad reference, then remove it.
- **Done when:** both tests pass on main.

---

## 6. Phase 2: Core business blocks

Goal: contacts abstraction, rates and the PSA skeleton. Plan row P2; specifications M04, M05, M21.

### P2-01 Move contact contracts to an Abstractions project (M04, D3)

- **Needs:** P0-05, P1-06.
- **Read first:** ADR-07, `docs/engineering/reusability-audit-2026-11.md` item D3, `src/Core/CrestApps.OrchardCore.Omnichannel.Core`.
- **Steps:**
  1. Create the target project named in ADR-07 if it does not exist (R1 steps 1–3 only).
  2. Move the listed interfaces and records there, keeping namespaces stable if ADR-07 says so; otherwise add the new namespace and update usings.
  3. Make `Omnichannel.Core` reference the new project and implement the interfaces.
  4. Do it in small commits: one interface per commit, building after each.
- **Tests first:** existing Omnichannel tests must pass unchanged; add `ContactFinder_ByEmail_ReturnsAccountContact` against the moved interface if no such test exists.
- **Done when:** a grep shows no consumer outside Omnichannel references `Omnichannel.Core` for contacts; public API baselines updated; audit section 0 note for D3 updated to "done".

### P2-02 Account health signals (M04)

- **Needs:** P2-01.
- **Steps:**
  1. In `Accounts.Abstractions`: `IAccountHealthSignalProvider` (name, weight default, `GetSignalAsync(accountId)` returning 0–100 and a short reason) and `AccountHealthSettings` (weights per signal).
  2. In `Accounts.Core`: `IAccountHealthService` combining signals as a weighted average of present signals.
  3. In Tickets (R5): a signal from open tickets and SLA breaches in the last 30 days; in Invoicing (R5): overdue receivables; in Tickets: CSAT average.
  4. A card on the account page (display driver) showing score and reasons, only to users who can view the account.
- **Tests first:** `Score_IsWeightedAverageOfPresentSignals`; `Score_NoSignals_IsNull`; each provider with fixture data.
- **Done when:** card shows on the account page; docs updated.

### P2-03 Rates: work roles and work types (M05)

- **Needs:** P1-06.
- **Read first:** plan M05 (all of it), R1, R2, R3, R6.
- **Steps:**
  1. Create the Rates block (R1).
  2. Add `WorkRole` and `WorkType` catalog records (R2) with fields from plan M05 E.
  3. Admin pages (R3), recipe and deployment steps (R6), permissions `ManageRates`, `ViewBillRates`, `ViewCostRates`.
  4. Usage guard interface `IRateUsage` so a role or type in use cannot be deleted.
- **Tests first:** `WorkRole_CodeMustBeUnique`; `WorkType_MultiplierAndFixedRate_AreMutuallyExclusive`; `Delete_InUse_IsRefused`; recipe round-trip.
- **Done when:** admins can manage roles and types; docs pages exist.

### P2-04 Rates: rate cards (M05)

- **Needs:** P2-03.
- **Steps:**
  1. `RateCard` aggregate with `RateLine`s, `ScopeKind` + `Scope` (`RecordRef`), currency, effective dates, status Draft/Active/Retired using `TransitionTable<RateCardStatus>`.
  2. `IRateScopeProvider` with `AddRateScope(kind, displayName)` (R4); register a built-in `Default` scope and a `Party` scope (accounts) in Rates.
  3. Overlap rule: two active cards in the same scope and currency cannot have overlapping dates.
  4. Card editor as a grid of work role × work type (R3), copy card, activate, retire.
- **Tests first:** `Activate_OverlappingSameScopeAndCurrency_IsRefused`; `Retire_ActiveCard_Succeeds`; `Activate_RetiredCard_IsRefused`.
- **Done when:** cards can be created and activated in the UI and recipes.

### P2-05 Rates: resolver and snapshots (M05)

- **Needs:** P2-04.
- **Steps:**
  1. `IRateResolver.ResolveAsync(RateRequest)` → `RateSnapshot` exactly as plan M05 D "Business rules" (precedence 1–5, line specificity, multiplier vs fixed, non-billable → bill 0, currency conversion with Exchange Rates on the work date).
  2. Cost rate from Staff: `StaffPayRates.CostOn(date)` through the Staff abstractions.
  3. Snapshots are value objects; nothing stores them yet (consumers do in later tasks).
  4. A rate preview page (pick account, role, type, date) for `ViewBillRates` users; cost column only with `ViewCostRates`.
  5. API `GET api/rates/v1/resolve` (R10).
- **Tests first:** a `[Theory]` precedence matrix covering 5 scopes × 4 line specificities × (multiplier, fixed); `NonBillableType_BillRateZero_CostKept`; `ForeignCurrencyCard_ConvertsOnWorkDate`; `EffectiveDate_BoundaryInNewYork_UsesLocalDate`; `Api_WithoutViewCostRates_OmitsCost`.
- **Done when:** all plan M05 O criteria pass; Rates registered in `building-blocks.md`.

### P2-06 PSA module skeleton (M21)

- **Needs:** P1-06, P2-03.
- **Read first:** plan M21, an existing setup recipe in a module's `Recipes/` folder (for example Tickets' "Help desk starter" recipe), Users/Roles recipe steps.
- **Steps:**
  1. Create `src/Modules/CrestApps.OrchardCore.Psa` (module only; it needs no `.Abstractions`/`.Core` yet). Add to slnx and Targets.
  2. Feature `CrestApps.OrchardCore.Psa` depending on the features available so far (Tickets, Service Levels, Agreements, Accounts, Staff, Teams, Rates, Invoicing, Time Tracker).
  3. A `Psa` recipe that enables those features and creates: desks (Service Desk), ticket types (Incident, Service Request), statuses, priorities with an impact × urgency matrix, business hours, SLA policies Gold/Silver/Bronze, work roles (Technician, Senior Engineer, Project Manager), work types (Remote, Onsite, After hours, Internal), a default rate card.
  4. Role templates as recipe roles: the 13 roles from plan M21 with only permissions that exist today. Leave a TODO list in the README of permissions to add when later modules land (not in code).
  5. Admin menu groups: Service, Projects, Field, Sales, Finance, Resources, Insights, Setup (only items that exist).
- **Tests first:** `PsaRecipe_OnFreshTenant_CreatesExpectedRecords` (integration-style via the harness); `RoleTemplates_HaveNoUnknownPermissions`.
- **Done when:** a fresh tenant set up with the recipe shows the menu and works for tickets; the layering guard (P1-06) passes.

---

## 7. Phase 3: Service operations

Goal: e-mail intake, templates, recurring tickets, service catalog, knowledge lifecycle, portal v1. Plan row P3; specifications M06, M07, M08, M22. Can run in parallel with Phase 4.

### P3-01 E-mail channel: mailboxes and providers (M06)

- **Needs:** P2-01.
- **Read first:** `.agents/skills/crestapps-messaging-channel/SKILL.md`, `IMessagingChannel`, Data Connectors (`IDataConnector`, `DataConnection`), `ILeasedWorkQueue<T>`, `IProviderWebhookInbox`, Attachments `IAttachmentService`.
- **Steps:**
  1. New feature `CrestApps.OrchardCore.Omnichannel.Messaging.Email` in the Messaging module family, following the channel skill.
  2. `Mailbox` catalog record (R2) and admin pages (R3) with "Test connection" and "Last sync".
  3. `IEmailMailboxProvider` (R4) with two implementations as separate features: Microsoft Graph (push subscription + delta query) and IMAP (polling with UID cursor).
  4. Polling background task per enabled mailbox, enqueuing work on the leased work queue (R14 telemetry).
  5. Push notifications arrive through the provider webhook inbox.
- **Tests first:** provider-neutral tests with a fake provider: `Poll_SameMessageTwice_ProcessedOnce`; `Cursor_AdvancesOnlyAfterSave`.
- **Done when:** a mailbox can be configured and synced against the fake provider in tests; credentials only via Data Connectors; docs for both manuals.

### P3-02 E-mail channel: parsing, threading, loops (M06)

- **Needs:** P3-01.
- **Steps:**
  1. Parse MIME: text and HTML bodies (sanitized), inline images, attachments through `IAttachmentService` (policy, byte sniffing, scanner).
  2. `IEmailThreadResolver`: match by `In-Reply-To`/`References` first, then a subject token, then none.
  3. `IEmailLoopGuard`: skip auto-replies (`Auto-Submitted` not `no`, `Precedence: bulk|auto_reply|list`, `X-Auto-Response-Suppress`), throttle a sender over N messages in M minutes (settings).
  4. `IEmailSenderResolver`: find the party by address, then by account e-mail domain (Accounts E-mail Domains).
  5. Outbound: send replies with `Message-ID`, `In-Reply-To`, `References` and the subject token.
- **Tests first:** fixture `.eml` files under the test project: `Reply_WithReferences_ThreadsToConversation`; `OutOfOffice_IsIgnored`; `DisguisedExecutable_IsQuarantined`; `UnknownSender_KnownDomain_ResolvesAccount`; `Loop_OverThreshold_IsThrottled`.
- **Done when:** all M06 O criteria pass.

### P3-03 Tickets.Email bridge (M07)

- **Needs:** P3-02.
- **Read first:** plan M07 "Tickets.Email", Tickets' messaging integration (`TicketConversationSource`), `TicketPart` (`RequesterUnverified`, `FollowUpOfId`).
- **Steps:** new feature `CrestApps.OrchardCore.Tickets.Email` (R5). Mailbox → desk and ticket type mapping. New thread → ticket through `ITicketService`; reply → public reply; agent address → agent reply; closed beyond reopen window → follow-up ticket; CC → participants only when same account. Requester e-mails include the token and threading headers.
- **Tests first:** `NewThread_CreatesTicket_WithRequester`; `Reply_AddsPublicReply`; `AgentReply_FromAgentAddress_IsAgentReply`; `ReplyToClosedBeyondWindow_CreatesFollowUp`; `CcOtherAccount_IsNotParticipant`; `AutoReply_CreatesNothing`.
- **Done when:** end-to-end test e-mail → ticket → reply → solve passes; Tickets docs updated.

### P3-04 Ticket templates (M07)

- **Needs:** P2-06.
- **Steps:** `TicketTemplate` catalog record (ticket type, field values, checklist items, child ticket definitions, default work type, estimate); admin pages; "New from template" action on the ticket list; `ITicketTemplateService.ApplyAsync`.
- **Tests first:** `Apply_SetsFieldsAndCreatesChildren`; `Apply_TemplateOfOtherTicketType_IsRefused`.
- **Done when:** agents can create tickets from templates; recipe/deployment steps exist.

### P3-05 Recurring tickets (M07)

- **Needs:** P3-04.
- **Read first:** Calendars `RecurrenceRule`, `RecurrenceExpander`, `CalendarTime`.
- **Steps:** `RecurringTicketSchedule` record (template, RRULE, time zone, account, desk, assignee/team, lead days, end rule, "skip if previous open"). Background task: for each due occurrence, create a ticket keyed by (schedule ID, occurrence date) under a lock (R11). Admin pages.
- **Tests first:** `Occurrence_CreatedOncePerDate_EvenAfterRestart`; `DstTransition_OccurrenceAtLocalTime`; `SkipIfPreviousOpen_SkipsAndLogs`.
- **Done when:** M07 O "recurring" criterion passes.

### P3-06 Service catalog (M07)

- **Needs:** P3-04.
- **Steps:** `ServiceCatalogItem` content type with `ServiceCatalogItemPart` (category taxonomy, audience by role/account, ticket type, template, approval policy, fulfilment SLA policy, displayed price), form fields via Conditional Fields. `IServiceCatalogService` returns items visible to a requester and submits a request (ticket from template + answers + Ticket Approvals when a policy applies). Audience enforced on the server at submit.
- **Tests first:** `VisibleItems_RespectAudience`; `Submit_ItemNotInAudience_IsRefused`; `Submit_WithPolicy_StartsApproval`.
- **Done when:** items can be published and requested (portal UI comes in P3-08).

### P3-07 Knowledge Base lifecycle (M08)

- **Needs:** P1-04.
- **Steps:** owner, review date, states Draft → In review → Published → Retired with an approval type `KnowledgeArticle` for governed categories; review reminder background task (once per article per review date); "Create article from ticket" action linking both (record link kind `DocumentedBy`); known-error flag linking to a problem ticket.
- **Tests first:** `Publish_GovernedCategory_RequiresApproval`; `ReviewReminder_FiresOnce`; `InternalArticle_NotReturnedBySuggestApiToAnonymous`.
- **Done when:** M08 O criteria pass; add the KB to the isolation harness.

### P3-08 Client portal v1 (M22)

- **Needs:** P2-01, P3-06.
- **Read first:** Users "My Account" feature, Tickets Portal (`TicketPortalController`, `TicketPortalAccess`), Resources `PageSections`, `UserParties`, `IPartyMembershipService`.
- **Steps:**
  1. Create `CrestApps.OrchardCore.Psa.Portal` (module only).
  2. A portal shell page with a menu built from registered sections; sections for: My tickets (existing portal pages), Request a service (catalog), Help center (KB).
  3. Portal administrator role per account: can see all the account's tickets, manage the account's portal users, approve catalog requests.
  4. Rate limits on portal endpoints.
- **Tests first:** add portal probes to the isolation harness (P1-05): portal user of A cannot see B's tickets, catalog requests or articles; `PortalAdmin_SeesAllAccountTickets`; `PortalUser_SeesOwnTicketsOnly_WhenSharingOff`.
- **Done when:** isolation harness green with portal probes; User Manual page for portal users.

---

## 8. Phase 4: Commercial management

Goal: full contract model and quotes. Plan row P4; specifications M09, M10. Can run in parallel with Phase 3.

### P4-01 Agreements: entitlements and ledger (M09)

- **Needs:** P2-05.
- **Read first:** plan M09 all; `Agreement`, `AgreementHours`, `IAgreementBalanceService`, `AgreementBalanceService`, `AgreementPeriods`, `IQuantityLedger<T>` and how Workforce time off registers a ledger (`AddQuantityLedger`), R7, R11.
- **Steps:**
  1. Add `Entitlement` (Id, Kind Hours/Money/Incidents/Units, Amount, Period, Rollover, ExpiresAfter, Thresholds, AutoRepurchase) and `Agreement.Entitlements` list.
  2. Register an `AgreementEntitlements` quantity ledger.
  3. `IEntitlementService`: `DrawAsync(agreementId, entitlementId, amount, sourceKey)`, `ReverseAsync(sourceKey)`, `GetBalanceAsync(agreementId, entitlementId, date)` (R11: lock `agreement:<id>`, key = sourceKey).
  4. Resumable step: convert each agreement's `AgreementHours` into one Hours entitlement and post existing usage as draws so balances match.
  5. Make `IAgreementBalanceService` read the ledger; keep its public contract.
  6. Keep `AgreementHours` readable until a later cleanup task (mark obsolete in docs, not deleted).
- **Tests first:** characterization tests of current balances first; `Draw_SameSourceKeyTwice_ChangesBalanceOnce`; `ParallelDraws_NeverExceedAmount_WhenNoOverage`; `Rollover_MonthlyAllowance_CarriesUnused`; `Expired_EntitlementRefusesDraw`; `Migration_BalancesEqualBefore`.
- **Done when:** all existing Agreements, Tickets and Work Orders tests pass; new tests pass.

### P4-02 Agreements: kinds and types (M09)

- **Needs:** P4-01.
- **Steps:** `IAgreementKind` with `AddAgreementKind<T>` (R4) and the seven kinds of plan M09 C registered by Agreements; `AgreementType` catalog record (template: kind, entitlements, coverage rules, additions, SLA policy, renewal policy) with admin pages; "New agreement from type".
- **Tests first:** `NewFromType_CopiesTemplate`; `UnknownKind_IsRefused`.
- **Done when:** the six competitor contract kinds can be created from types.

### P4-03 Agreements: coverage service (M09, A2)

- **Needs:** P4-02.
- **Read first:** `ITicketAgreementResolver` and its implementation in Tickets, `TicketAgreementTerms`.
- **Steps:**
  1. `CoverageRule` on agreements (includes/excludes: work role IDs, work type IDs, desks/ticket types via an open "subject filter" kind registered by consumers, time window, sites; rate override).
  2. `IAgreementCoverageService.ResolveAsync(CoverageRequest)` returning `CoverageDecision`, following plan M09 D rules 1, 2 and 5 (most specific wins, umbrella last, earliest start, default agreement per account).
  3. `AgreementCoverageIndex` for fast lookup.
  4. Resumable step: convert `TicketAgreementTerms` into coverage rules.
  5. In Tickets: reimplement `ITicketAgreementResolver` as an adapter over the coverage service (keep the interface).
  6. Register `Agreement` as a Rates scope (`AddRateScope`).
- **Tests first:** a decision-table `[Theory]`: overlapping agreements, exclusions falling through, umbrella, default agreement, after-hours window, site rule; `TicketTermsMigration_SameAgreementChosenAsBefore`.
- **Done when:** existing Tickets agreement tests pass through the adapter; coverage p95 under the P0-06 target in the benchmark.

### P4-04 Agreements: additions and quantity sources (M09)

- **Needs:** P4-02.
- **Steps:** `Addition` list on agreements (product reference via Products, quantity source, quantity, unit price, unit cost, frequency, timing advance/arrears, pro-rata policy, start, end); `IAdditionQuantitySource` with `AddAdditionQuantitySource<T>` (R4); a built-in "account contacts count" source in Accounts (R5). (Assets' device count comes in P6-01.)
- **Tests first:** `Quantity_FromSource_ReadAtRunDate`; `ProRata_MidPeriodStart`.
- **Done when:** additions are editable on the agreement page.

### P4-05 Agreements.Billing bridge (M09)

- **Needs:** P4-04.
- **Read first:** `.agents/skills/crestapps-invoicing/SKILL.md`, `IRecurringPlanService`, recurring plan docs.
- **Steps:** new feature `CrestApps.OrchardCore.Agreements.Billing` (depends on Agreements and Invoicing Recurring). One recurring plan per active agreement with additions; sync on agreement change; freeze quantities on the invoice line at each run; milestones and overage posted as `BillableItem`s (review queue comes in P7-01); register the invoice grouping policy hook later used by M17.
- **Tests first:** `Activate_CreatesOneRecurringPlan`; `Run_FreezesQuantitiesOnLines`; `Run_Twice_InvoicesOnce` (rely on plan occurrence idempotency); `Overage_CreatesBillableItemOnce`.
- **Done when:** workflow W8 passes in an integration test.

### P4-06 Agreements: lifecycle, renewals, amendments, notices (M09)

- **Needs:** P4-01.
- **Steps:** states per plan M09 G with `TransitionTable`; versions (`Version`, `ParentAgreementId`); renewal (manual/auto, uplift %); amendments recorded as change records; threshold notices and auto-repurchase in the existing Agreements background task (extend, do not add another loop); failed repurchase → payment issue record via Checkout's `IPaymentIssueRecorder` if available, else a notification.
- **Tests first:** state machine tests for each allowed and refused move; `AutoRenew_CreatesNewVersion_WithUplift`; `Threshold_NotifiesOncePerPeriod`; `Amendment_DoesNotChangePostedDraws`.
- **Done when:** M09 O criteria 1–3 pass.

### P4-07 Estimates as quotes (M10)

- **Needs:** P2-05, P4-02.
- **Read first:** plan M10, `IEstimateCommandService`, estimate models and public page.
- **Steps:**
  1. Sections with optional lines; line kinds `Labor` (priced by `IRateResolver`) and `Recurring` (frequency, term).
  2. Totals shown as one-time and recurring.
  3. Margin service (`IEstimateMarginService`); approval type `Estimate` when margin or discount crosses settings thresholds.
  4. Signature capture on the public accept page (typed name, time, IP hash, document SHA-256).
  5. `IEstimateConversionTarget` (R4); conversions recorded and run once per (estimate version, section).
  6. Agreements.Billing registers an agreement conversion target (recurring section → agreement with additions).
  7. Opportunity link field.
- **Tests first:** `Accept_WithOptionalLinesSelected_TotalsCorrect`; `SignatureHash_MatchesDocument`; `Convert_Twice_CreatesOnce`; `LowMargin_RequiresApproval`; `PublicPage_ShowsNoCost`.
- **Done when:** M10 O passes for the agreement target (the project target is added in P5-06).

### P4-08 CRM opportunities link to quotes (M04)

- **Needs:** P4-07.
- **Steps:** opportunity gains estimate links, forecast category, recurring and one-time amounts; opportunity page lists quotes; "Create quote" action.
- **Tests first:** `CreateQuote_FromOpportunity_LinksBoth`.
- **Done when:** workflow W1 up to agreement creation passes.

---

## 9. Phase 5: Projects and time

Goal: billable time ledger, projects, resource planning. Plan row P5; specifications M11, M12, M13.

### P5-01 Time entries: billable attribution (M13, A5)

- **Needs:** P2-05, P4-03.
- **Read first:** plan M13, `ITimeTrackerService`, Tickets' `TicketTime.LogAsync`, Time Tracker README.
- **Steps:** add `Subject` (`RecordRef`), `WorkRoleId`, `WorkTypeId`, `Billable`, `CustomerVisibleNote`, `InternalNote`, `AgreementId`, `CoverageDecision`, `RateSnapshot`, `TimesheetId`, `ApprovalState` to time entries; update the index (columns from P0-04); `ITimeEntryDefaultsProvider` (R4) with a Tickets implementation (role from staff default, type from desk default); ticket time panel captures role and type; migration sets `Subject` where the existing link is known.
- **Tests first:** `Entry_WithoutSubjectOrInternalType_IsRefused`; `TicketDefaults_Applied`; `Migration_SetsSubjectForTicketEntries`.
- **Done when:** existing Time Tracker and Tickets time tests pass.

### P5-02 Timesheets and approval (M13)

- **Needs:** P5-01, P1-04.
- **Steps:** feature `TimeTracker.Timesheets`; `Timesheet` record per staff and period (period from Payroll Pay Setup schedules if configured, else weekly); submit, recall, approve, reject through an approval type `Timesheet` with approver rules manager and "project manager of any entry's project" (registered later by Projects); lock entries on submit; reversal entries after approval; weekly grid UI.
- **Tests first:** `Submit_LocksEntries`; `Recall_BeforeDecision_Unlocks`; `EditApproved_CreatesReversal`; `OverlappingClockIntervals_BlockSubmit`.
- **Done when:** timesheets can be submitted and approved end to end.

### P5-03 Time posting on approval (M13)

- **Needs:** P5-02, P4-05.
- **Steps:** `ITimeEntryPostingService` on timesheet approval: coverage → entitlement draw (key = entry ID) → rate snapshot → `BillableItem` for uncovered billable minutes; implement `IAgreementUsageProvider`, `IBillableSource`; optional `IPayInputSource` for hourly staff (setting).
- **Tests first:** `Approve_Mixed_ProducesExactDrawsAndItems`; `ReapproveAfterRecall_NoDuplicates`; `PartialFailure_RetriedOnce` (fault injection).
- **Done when:** workflow W7 passes; M13 O passes.

### P5-04 Projects core (M11)

- **Needs:** P1-01, P1-03, P2-05.
- **Read first:** plan M11 all, R1–R3, R12.
- **Steps:** create the Projects block; `Project`, `ProjectPhase` (decide in-document vs separate using P0-04 results; default separate records), `ProjectTask` (separate documents), `TaskDependency` value object, project types (open kind); states with `TransitionTable`; numbering `PRJ-`, `T-`; `IProjectAccessService` + `ProjectScopeFilter`; project list and project page with Overview, Tasks (list) sections; register `Project` and `ProjectTask` record sources, timeline and attachment access providers; add to isolation harness.
- **Tests first:** state machine tests; `Task_InClosedProject_IsRefused`; isolation probes (staff outside team, portal user of other account).
- **Done when:** projects and tasks can be managed; isolation harness green.

### P5-05 Project scheduler (M11)

- **Needs:** P5-04.
- **Steps:** `IProjectScheduler` as a pure service: input tasks, dependencies (FS, SS, FF, SF with lag), calendar working time (Business Hours schedule of the project), pinned tasks; output start/finish, slack, critical flag; summary rollups; recalculation command under `project:<id>` lock writing only changed tasks; cycle detection on dependency add; Gantt section (read-only first, drag later).
- **Tests first:** property tests on random DAGs (no task before predecessors + lag; critical path tasks have zero slack); fixed reference networks (20 cases) with expected dates; `AddDependency_CreatingCycle_IsRefused`; `PinnedTask_Conflict_KeepsPinAndWarns`; performance test 2,000 tasks under target.
- **Done when:** M11 O criteria 1–2 pass.

### P5-06 Project templates, budgets, change orders, baselines (M11)

- **Needs:** P5-05, P4-07.
- **Steps:** `ProjectTemplate` (roles not people) and "create from template"; budget lines and actuals via `IProjectCostSource` (R4) implemented by Time Tracker (approved time at cost) — Expenses and Inventory register later; budget threshold event; baselines (max 10); change orders with approval type `ChangeOrder`; Projects registers an Estimates conversion target (labor section → project from template); Boards source for tasks (Kanban section).
- **Tests first:** `Template_Copy_Fidelity`; `Actuals_EqualApprovedTimeAtCost`; `ChangeOrderApproved_UpdatesBudget`; `QuoteConvert_CreatesOneProject`.
- **Done when:** M11 O criteria 3 and 5 pass.

### P5-07 Projects.Billing bridge (M11)

- **Needs:** P5-06, P4-05.
- **Steps:** feature `Projects.Billing`: milestone achieved → agreement milestone billable (fixed price) or nothing (T&M uses time posting); project billing method on project; close blockers (open tasks, unapproved time, unbilled items) with override reason.
- **Tests first:** `MilestoneAchieved_FixedPrice_PostsOnce`; `Close_WithUnapprovedTime_IsBlocked`.
- **Done when:** workflow W5 passes.

### P5-08 Resource Planning (M12)

- **Needs:** P5-04.
- **Read first:** plan M12; Workforce `IWorkedTimeSource`; Calendars `ICalendarBusyTimeSource`; Teams `ITeamMemberAvailabilitySource`; Skills `SkillRequirement`.
- **Steps:** create the block; `IWorkItemSource` with `AddWorkItemSource<T>` (R4); `Allocation` record; `ICapacityService`; `IAllocationService` (book soft/hard, move, release; over-capacity hard booking needs `OverrideOverallocation` and a reason); `IResourceFinder`; work item sources registered by Projects (tasks and role demands), Tickets (scheduled tickets with planned dates), Dispatch (visits); planning board page with masking of titles the viewer cannot see; time-off approval re-evaluates allocations (observer).
- **Tests first:** `Capacity_ShiftHolidayHalfDayAbsence_EqualsFixture`; `HardBookingOverCapacity_WithoutPermission_IsRefused`; `CancelledWorkItem_ReleasesAllocation`; `Board_MasksTitlesWithoutAccess`; performance test.
- **Done when:** M12 O passes.

---

## 10. Phase 6: Field, assets and procurement

Goal: CMDB, ticket–asset links, change management, procurement. Plan row P6; specifications M15, M16, M07 (Changes, Assets). Can run in parallel with Phase 5 after Phase 4.

### P6-01 Assets as CMDB (M15)

- **Needs:** P1-01, P4-04.
- **Steps:** relationship link kinds (DependsOn, RunsOn, ConnectedTo, InstalledOn, Contains, BackedUpBy) via record links; impact query (depth limit 10); `ExternalKeys`, `LifecycleState`, `ManualFields`; `SoftwareLicense` record; warranty and license expiry notices (once); `IAssetDiscoverySource` upsert by external key respecting manual fields; implement `IAdditionQuantitySource` (active assets of selected types per account).
- **Tests first:** `Upsert_SameDeviceTwice_NoChange`; `Upsert_DoesNotOverwriteManualField`; `Impact_TraversalStopsAtDepth10`; `QuantitySource_CountsActiveOfTypes`.
- **Done when:** M15 O passes.

### P6-02 Tickets.Assets (M07)

- **Needs:** P6-01.
- **Steps:** link kind `AffectsAsset`; ticket page panel (asset coverage, warranty, recent tickets); asset page lists tickets.
- **Tests first:** `Panel_HidesAssetOfOtherAccount`.
- **Done when:** links work both ways with access checks.

### P6-03 Tickets.Changes (M07)

- **Needs:** P6-02, P1-04.
- **Steps:** `ChangePart` (fields per plan M07); change type flag on ticket types; states per plan M07 G mapped onto ticket status categories; risk questionnaire scored with `ConditionRule`; approval type `Change` with policies by change type and risk (CAB = team rule); standard changes from templates skip approval; emergency changes allow post-implementation approval; change calendar as a Calendars type (follow `.agents/skills/crestapps-calendar-types/SKILL.md`) with overlap warnings on the same CI.
- **Tests first:** full state machine; `Normal_RequiresAllCabStages`; `Standard_SkipsApproval`; `Emergency_ApprovedAfterImplementation`; `OverlappingChangesSameCi_Warns`.
- **Done when:** M07 O change criteria pass.

### P6-04 Inventory and procurement (M16)

- **Needs:** P0-03, P4-07.
- **Steps:** make the changes listed by the P0-03 report; supplier part on Accounts; `PurchaseRequisition` with demand `RecordRef`; aggregate requisitions into purchase orders by supplier; approval type `PurchaseOrder` by amount; serial tracking on receipt and issue; create assets from delivered serialized items; reservations for demand; Inventory registers an Estimates conversion target (hardware section → requisitions).
- **Tests first:** `QuoteHardware_ToRequisitions_ToPo_ToReceipt_ToAssets` (integration); `Reservation_ReducesAvailable`; `SerialIssuedTwice_IsRefused`.
- **Done when:** M16 O passes; workflow W6 parts path passes.

---

## 11. Phase 7: Financial operations

Goal: approve-and-post, expenses, accounting sync, portal v2. Plan row P7; specifications M17, M14, M18, M19, M22.

### P7-01 Approve-and-post review (M17)

- **Needs:** P5-03.
- **Steps:** `BillableItem` gains review state, adjustments, subject, agreement ID; billing review page (AdminList with filters account, agreement, project, period, source); line actions approve, adjust (reason), non-billable, defer; posting groups items into drafts by bill-to party, currency and `IInvoiceGroupingPolicy` (R4; Agreements.Billing and Projects.Billing register policies); cut-off date; reverse before issue; automatic posting rules per source.
- **Tests first:** `Post_SameItemTwice_PostsOnce`; `Grouping_PerAgreementPolicy`; `Adjust_RecordsBeforeAfter`; month-end fixture (3 accounts, 5 agreements, 400 entries).
- **Done when:** M17 O passes.

### P7-02 Expenses (M14)

- **Needs:** P4-03, P5-06.
- **Steps:** create the block per plan M14 (R1–R3); categories, policies, mileage rates, reports and items, receipts via Attachments, approval type `ExpenseReport` with a policy-exception stage, billing via `IBillableSource`, project cost via `IProjectCostSource`, reimbursement via `IPayInputSource` and an export provider; personal-data contributor; duplicate warning.
- **Tests first:** `PolicyViolation_RequiresJustificationAndExtraStage`; `ForeignCurrency_ConvertedOnExpenseDate`; `Approve_Mixed_ProducesItemsCostsReimbursementsOnce`; `Receipt_AccessDeniedToOtherStaff`.
- **Done when:** workflow W9 passes; M14 O passes.

### P7-03 Accounting sync (M19)

- **Needs:** P7-01.
- **Steps:** `CrestApps.OrchardCore.AccountingSync` block with `IAccountingProvider` (customers, items, invoices, credit notes out; payments in); mapping tables; sync log; work on the leased queue with retries; providers QuickBooks Online and Xero as separate features using Data Connectors and egress guard; never edit issued invoices from the accounting side.
- **Tests first:** fake provider: `IssuedInvoice_ExportsOnce`; `Payment_ImportsOnce`; `ProviderDown_RetriesWithBackoff`.
- **Done when:** M19 accounting criteria pass.

### P7-04 Portal v2 (M22)

- **Needs:** P3-08, P4-06, P5-04.
- **Steps:** add sections: My invoices (existing page), Agreements and balances (read-only, no costs), Projects (customer-visible fields, milestones, change order approval), Quotes (accept page links).
- **Tests first:** portal probes for invoices, agreements, projects in the isolation harness.
- **Done when:** M22 O passes.

### P7-05 Revenue recognition (M18), only if ADR-09 requires it

- **Needs:** P7-01, ADR-09 decision.
- **Steps:** follow plan M18; if ADR-09 says "no", close this task with a note.

---

## 12. Phase 8: Intelligence and automation

Plan row P8; specifications M20, M23, section 20, M19 RMM.

### P8-01 Data sets for new blocks (M20)

- **Needs:** P7-01.
- **Steps:** each block adds its data sets (R8): Rates register, agreements and entitlement balances, projects portfolio and budgets, allocations, timesheets compliance, expenses, billing review, changes. Each applies its scope filter.
- **Tests first:** one isolation probe per data set.
- **Done when:** harness green.

### P8-02 Dashboard pages and role dashboards (M20, M21)

- **Needs:** P8-01.
- **Steps:** a dashboard page type composed of report widgets; KPI tile and trend widgets; drill-through to admin lists; default dashboards per PSA role created by the `Psa` recipe.
- **Tests first:** `Dashboard_HidesWidgetsUserCannotRun`.
- **Done when:** each PSA role sees its default dashboard.

### P8-03 Psa.Analytics KPIs (M23)

- **Needs:** P8-01.
- **Steps:** implement the KPIs of plan section 19 as data sets that join other blocks' data sets by ID; nightly snapshot background task for trend KPIs (R14); permission `ViewPsaFinancials`.
- **Tests first:** one fixture test per KPI with hand-computed expected values.
- **Done when:** M23 O passes.

### P8-04 AI use cases (section 20)

- **Needs:** P8-01.
- **Read first:** AI module docs, how Tickets and Invoicing register AI tools (`AddCoreAITool`), prompt templates (`Templates/Prompts/*.md`), `IAIDataSourceResultFilter`.
- **Steps (one pull request each):** ticket triage tool with per-desk confidence threshold; ticket summary; suggested resolution and articles (retrieval filtered by scope); draft reply; time entry suggestions; receipt extraction; article from ticket; project plan draft. Every tool acts as the user; writes are drafts unless confirmed; auto-applied triage is audited with model and confidence.
- **Tests first:** per tool, a permission test (no out-of-scope data in context or output) and an evaluation set with a recorded baseline.
- **Done when:** baselines recorded; isolation probes for AI tools green.

### P8-05 RMM connectors (M19)

- **Needs:** P6-01.
- **Steps:** `Integrations.Rmm` abstractions (device sync via `IAssetDiscoverySource`, alerts via `IProviderWebhookInbox` with alert-key de-duplication, auto-resolve option, account mapping); first provider as a separate feature.
- **Tests first:** `SameAlertTwice_OneTicket`; `AlertCleared_ResolvesWhenEnabled`; `UnmappedAccount_GoesToTriageDesk`.
- **Done when:** M19 RMM criteria pass.

---

## 13. Phase 9: Enterprise readiness

Plan row P9 and sections 16, 23, 24.

| Task | Steps | Done when |
| --- | --- | --- |
| P9-01 Threat model | One threat model per bounded context (STRIDE), stored in `docs/engineering/psa/security/` | Reviewed by a maintainer |
| P9-02 ASVS 5.0 L2 checklist | For each module, a checklist of applicable requirements with the test that proves each | No unproven L2 requirement |
| P9-03 Penetration test | External test of a staging tenant | No high findings open |
| P9-04 Load and soak | Reference data set (plan section 23), budgets measured | All budgets met at p95 |
| P9-05 Disaster recovery drill | Restore one tenant to a point in time | RPO/RTO from P0-06 met |
| P9-06 Compliance evidence exports | Access review export, audit export, configuration history, backup logs | Exports produce files a reviewer can use |
| P9-07 Final end-to-end | Fresh tenant from the `Psa` recipe runs W1–W9 | All green |

---

## 14. Appendix A: Done checklist for every pull request

Copy this list into every pull request and tick each line.

- [ ] Every behaviour in the task's **Tests first** list has a test that failed first and passes now.
- [ ] Whole test project passes, including Architecture tests (`LayeringRatchetTests`, `DuplicatePatternRatchetTests`, `FeatureIdLiteralRatchetTests`, `EmptyCatchRatchetTests`, PSA guards).
- [ ] Build passes with warnings as errors.
- [ ] Public API baselines updated and the diff reviewed (if public types changed).
- [ ] New projects added to `CrestApps.OrchardCore.slnx`; new modules added to the Targets project.
- [ ] Feature IDs are constants in `.Abstractions`.
- [ ] Permissions defined and checked on every action and endpoint.
- [ ] Scope filter applied to every list, API, data set, search, attachment, timeline and AI tool; isolation probes added.
- [ ] Money, entitlement, stock or time changes use a lock and an idempotency key.
- [ ] Recipe and deployment steps for every new record type; round-trip test.
- [ ] Personal-data contributor for records with personal data.
- [ ] Telemetry on background tasks; no IDs or names in metric tags.
- [ ] Dates shown with `ILocalClock`; `IClock` used for "now".
- [ ] Admin lists use `<admin-list>`; forms use `action-bar-bottom`.
- [ ] Module `README.md` updated.
- [ ] Technical Manual and User Manual pages updated, `sidebars.js` updated, `npm run check:manuals` and `npm run build` pass in `src/CrestApps.Docs`.
- [ ] `docs/engineering/building-blocks.md` updated if a building block or extension point was added or changed.
- [ ] No unused `using` statements or injected services; no commented-out code.
- [ ] Commit author is the configured human author; no AI co-author trailer.

---

## 15. Appendix B: Glossary of repository terms

| Term | Meaning in this repository |
| --- | --- |
| Building block | A module that owns one concept and is extended by others (catalog in `docs/engineering/building-blocks.md`) |
| `.Abstractions` project | Contracts: interfaces, records, constants, permissions, feature IDs |
| `.Core` project | Implementations, stores, indexes, handlers shared by modules |
| Catalog record | A YesSql document managed by a catalog manager (`CatalogItem` subclass with a `DocumentCatalog` store) |
| Index | A YesSql map index table used for listing, filtering and lookups |
| `RecordRef` | A reference to any record as `Kind:Id` |
| `PartyRef` | A reference to who a record belongs to (user, customer, account, contact) |
| Scope filter | SQL restriction that limits records to what the viewer may see |
| Observer | An interface a block calls after a change is saved so others can react |
| Resumable step | A data upgrade that runs in batches in the background with a cursor |
| `[RequireFeatures]` startup | A startup that runs only when another feature is enabled; used for optional integrations |
| Ratchet test | An architecture test that only allows a list of known exceptions to shrink |
| Kit | The small test fixture style used in `AgreementsTests` |

---

## 16. Appendix C: Task index

| Phase | Tasks | Plan modules |
| --- | --- | --- |
| P0 | P0-01 to P0-06 | ADRs, assumptions |
| P1 | P1-01 to P1-06 | M01, M02, M03, isolation harness |
| P2 | P2-01 to P2-06 | M04, M05, M21 |
| P3 | P3-01 to P3-08 | M06, M07, M08, M22 |
| P4 | P4-01 to P4-08 | M09, M10, M04 |
| P5 | P5-01 to P5-08 | M13, M11, M12 |
| P6 | P6-01 to P6-04 | M15, M07, M16 |
| P7 | P7-01 to P7-05 | M17, M14, M19, M22, M18 |
| P8 | P8-01 to P8-05 | M20, M21, M23, AI, M19 |
| P9 | P9-01 to P9-07 | Enterprise readiness |

Parallel lanes: P3 and P4 after P2; P5 and P6 after P4; within P1, tasks 01, 03, 04, 05 and 06 are independent (02 needs 01).
