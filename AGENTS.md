# CrestApps.OrchardCore Development Instructions

> This is the shared instruction file for all AI coding agents working in this repository (it follows the cross-tool `AGENTS.md` convention). Keep tool-agnostic guidance here so every assistant reads the same conventions.

**ALWAYS reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.**

## Project Overview

CrestApps.OrchardCore is a collection of open-source modules for **Orchard Core CMS**, a modular application framework built on **ASP.NET Core/.NET 10**. The repository contains AI modules, omnichannel communication, user management enhancements, content access control, and other CMS extensions.

**License**: MIT  
**Target Framework**: .NET 10.0 (net10.0)  
**Architecture**: Modular, multi-tenant application framework

## Building Blocks and Reuse (read before you build anything new)

The solution is a set of building blocks. Each module owns one concept (an account, a team, an address, a phone number, a calendar, a skill, money, a report) and other modules compose those concepts into products, extending them through shapes, display drivers, parts, handlers and provider interfaces. The same concept is never built twice.

Before you add a module, a record type, a settings page, a provider or a shared helper, read [`docs/engineering/building-blocks.md`](docs/engineering/building-blocks.md), the catalog of which building block owns which concept and how to extend it. [`docs/engineering/reusability-audit-2026-11.md`](docs/engineering/reusability-audit-2026-11.md) lists the duplicates that still exist and the plan to remove them: never copy a pattern that the audit lists as a duplicate, even though the code is in the repository.

### The reuse ladder

Take the first step that works, and say in the pull request which step you took:

1. **Use** an existing building block: reference its `.Abstractions` project, depend on its feature (or gate the integration with `[RequireFeatures]`), and call its contracts.
2. **Extend** it through its extension points: a display driver or shape on its record, a content part, a handler or observer, a provider interface, an `AddXxx` registration (`AddCalendarType`, `AddReport`, …) or its `Properties` bag.
3. **Add the missing extension point to the owner** (an interface, a handler hook, a driver-based editor, an open "kind" value), then use it. Do not work around the owner.
4. **Extract a new building block** when a second module needs a concept that only one module has. Move it into its own `X.Abstractions`/`X.Core`/module (or into the owner's Abstractions) and point both modules at it in the same plan. The second copy is the trigger; never wait for a third.
5. **Keep it private** only when the concept is truly specific to one module, with a name that does not collide with a building block.

### Reuse rules

* Search before you build, by name and by meaning (for business hours, search "hours", "opening", "schedule", "availability", "holiday"). Check the catalog and `src/Targets/CrestApps.OrchardCore.Cms.Core.Targets`.
* One concept, one owner. People are Users (`IDisplayNameProvider` for names), organizations are Accounts, groups are Teams, skills are Skills, addresses are Addresses, phones are PhoneNumbers, places and coordinates are Addresses + Geo, events and reminders are Calendars, business hours and holidays are Business Hours (`IOpenHoursService`, `HoursSchedule`), reports are Reports, bulk import is ContentTransfer, external systems are DataConnectors, flows are Wizard, child tenants are TenantHierarchy, selling anything is Carts → Checkout → Orders.
* Use the shared value types instead of re-declaring their fields: `Address` (never `AddressLine1`/`City` properties or a one-string address), `PhoneNumber` normalized through `IPhoneNumberService` (never a raw phone string), `GeoPoint` (never latitude/longitude pairs), `Money` with `CurrencyScale` (never a hard-coded 2-decimal rounding or another currency table), `CalendarTime` for every wall-clock conversion (never `TimeZoneInfo.ConvertTimeToUtc`), `RecurrenceRule` for repeats, a `UserId` resolved through `IDisplayNameProvider` (never a copied user name that goes stale).
* Use the shared platform helpers instead of copying them: CrestApps.Core catalogs with `CatalogEntryHandlerBase<T>` and `AuditedCatalogEntryHandlerBase` for created/owner/author stamping; `NamedCatalogRecipeStep`, `NamedCatalogDeploymentSource` and `CatalogDeploymentSerializer`; `ProtectedSettingExtensions` for secrets; `SignedTokenService` for signed links; `SiteUrlGenerator`; `SanitizeLogValue()`; `BackgroundWorkPacer` and `DistributedLockExtensions`; `IFinancialDocumentNumberGenerator` for gap-free numbers; Orchard's `ITimeZoneSelectListProvider` for time-zone pickers; Reports (`IReport`, `IndexedReportDataSet`, `IReportExportFormat`) for analytics pages and CSV/Excel exports; the provider webhook inbox (`IProviderWebhookInbox`) for inbound provider webhooks; `IContactOptOutResolver` for every do-not-contact decision.
* Make a building block extensible from day one: editors rendered through `IDisplayManager<TModel>` and display drivers (so other modules add fields with their own drivers), life-cycle handlers or observers, open string "kinds" instead of enums that list consumers, `AddXxx` registration helpers, and `Properties` for extension data.
* A building block never names its consumers: no hard-coded tabs, enum values, feature IDs or types of the modules that build on it. Consumers register themselves.
* When you extract or change a building block, migrate the existing copies in the same plan and update the catalog, so the solution never keeps two versions of a concept.

### Layering and project references

* `CrestApps.OrchardCore.X.Abstractions` holds contracts, shared records and value types, constants and feature IDs. It references only other `.Abstractions` projects and framework abstractions.
* `CrestApps.OrchardCore.X.Core` holds implementations, stores and indexes, and the reusable pieces that more than one module needs, including UI building blocks: base display drivers, shared view models, tag helpers and view components. It holds no module-specific views, controllers, routes or endpoints, and it never references a module project.
* `CrestApps.OrchardCore.X` (the module) holds the manifest, startups, drivers, controllers, views, migrations, recipe and deployment steps and endpoints.
* A module may keep a `<ProjectReference>` to another module, but only to carry the feature dependency: the module declares the dependency in its manifest and references the project so a site that installs its NuGet package gets the dependency's package too. Module **code**, including compiled views, never uses a class, model, service, view model, tag helper or view component from another module assembly. Anything two modules share lives in the owner's `.Abstractions` (contracts, models, constants) or `.Core` (implementations, base classes, shared tag helpers and view components); move it there first. This applies across the board, with no exception for sub-modules, providers or `Resources`. `ModuleLayeringTests` enforces it on the compiled assemblies, with a shrink-only allowlist of today's violations. Remove project references that nothing needs.
* Put optional integrations (AI tools, reports, workflows, another domain) in a `[RequireFeatures]` startup in the consumer or in a bridge module (`Workforce.ContactCenter` is the pattern), so the building block works without them. Do not add a hard project reference to `AI.Core` only to register AI tools.
* Domain types never go into the solution-wide `CrestApps.OrchardCore.Abstractions`; they go into their own domain's `.Abstractions`.
* Feature IDs are constants in the owning `.Abstractions` project. Never repeat a feature ID as a string literal in `[RequireFeatures]`, manifests or recipes.
* Channel and carrier modules (Messaging, SMS, Telnyx, Asterisk, future providers) reference `ContactCenter.Abstractions` only, never `ContactCenter.Core`, and carrier adapters hold no orchestration (queues, IVR, routing). See the layer boundary in the next section.

### Reliability rules the audit added

* Inbound provider webhooks are accepted through the durable, de-duplicated provider inbox. Never process them in a fire-and-forget task or de-duplicate them in memory.
* Idempotency keys come from stable business IDs (vendor + order + earning), never from an ID generated on each attempt.
* Document numbers (orders, returns, purchase orders, receipts) come from a locked sequence, never from `Random` or a max+1 query.
* CSV output escapes quotes, commas, `\r` and `\n` and guards against formula injection.
* Every endpoint checks a permission, not only that the caller is signed in.
* Requests never authorize against a capped list (`Take(1000)`); query the record by ID.
* Never write another attempt counter or 429 response; register an attempt policy (`IAttemptLimiter`) or a group limit (`AddClientGroupRateLimit`). A route a guest or another site can call, or one that moves money or checks a secret, carries a rate limit group and a route name; `ClientNetworkKey` is the only place an address becomes a key. See `.agents/skills/crestapps-rate-limits`.

## Contact Center module (active, multi-phase project)

The **Contact Center** module set is a large, multi-phase orchestration layer built between the CRM (Omnichannel) and Telephony modules. The planning scaffolding (the multi-phase plan documents, their release-tracking ledgers, and the machine-readable governance ledgers that used to live under `.github/contact-center/`) has been retired now that the work has landed. Treat the shipped code and its tests as the source of truth for scope and boundaries before doing Contact Center work (anything under `src/**/CrestApps.OrchardCore.ContactCenter*`).

Key rules for this module set:

- Respect the layer boundary: **CRM (Omnichannel) owns business work data, Contact Center owns orchestration, Telephony owns media execution.** `OmnichannelActivity` remains the universal work item; `Interaction` is communication history for one attempt and never owns workflow or disposition.
- **Never** write competitor product names in code, comments, identifiers, or public docs. Adopt only generic, industry-standard terminology.
- Group related capabilities into separate, feature-gated Orchard modules/features, the way commercial platforms separate licensed capabilities.
- The shipped deployment topologies live in code as `ContactCenterTopologyProfiles`; the tenant/provider profiles and feature-set expectations live in the Contact Center tests. Keep these in sync with the modules they describe.

## Working Effectively

### Prerequisites and Environment Setup

Install .NET 10.0 SDK first:
```bash
# Add Microsoft package repository (Ubuntu/Debian)
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install .NET 10.0 SDK - TAKES 1-2 MINUTES
sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0

# Verify installation
dotnet --version  # Should show 10.0.x
```

### Build Process

**CRITICAL BUILD LIMITATION**: The .NET build requires access to the Orchard Core 4.0 preview packages (`OrchardCoreVersion` in `Directory.Packages.props`) from `https://nuget.cloudsmith.io/orchardcore/preview/v3/index.json` and to the CrestApps.Core preview packages from `https://nuget.cloudsmith.io/crestapps/crestapps-core/v3/index.json`. In environments with restricted network access (like CI runners or sandboxed environments), the build will fail with network connectivity errors.

#### Asset Build (Always Works)
```bash
# Install npm dependencies - TAKES 2-3 MINUTES, NEVER CANCEL
npm install

# Build frontend assets - TAKES ABOUT 6 SECONDS
npm run rebuild
```

#### .NET Solution Build (Network Dependent)
```bash
# Full solution build - ONLY WORKS WITH NETWORK ACCESS TO CLOUDSMITH
# TAKES 5-10 MINUTES when successful, NEVER CANCEL
dotnet build -c Release -warnaserror /p:TreatWarningsAsErrors=true /p:RunAnalyzers=true /p:NuGetAudit=false
```

**Important for Copilot skills**: In a fresh environment, build the project early and then check for newly generated skills under `.agents\skills`. One of the packages can create `.agents\skills` on the first build, and those generated skills may contain important, up-to-date capabilities that are not visible before the build runs.

**If build fails with NU1301 errors** about `nuget.cloudsmith.io`, this is expected in restricted environments. Document this limitation rather than attempting workarounds.

#### Unit Tests (Requires Successful Build)
```bash
# Run unit tests - TAKES 2-5 MINUTES, NEVER CANCEL
dotnet test -c Release --no-build ./tests/CrestApps.OrchardCore.Tests/CrestApps.OrchardCore.Tests.csproj
```

### Running the Application

**Web Application**: The main application is in `src/Startup/CrestApps.OrchardCore.Cms.Web/`

```bash
# Run the CMS web application (requires successful build)
cd src/Startup/CrestApps.OrchardCore.Cms.Web
dotnet run

# Application runs on: http://localhost:5000
# Admin setup occurs on first run
```

**Aspire Orchestration**: For full-stack local development with Ollama, Elasticsearch, and Redis, use the Aspire AppHost:

```bash
cd src/Startup/CrestApps.Aspire.AppHost
dotnet run
```

### Code Guidelines

* Follow `.editorconfig` at all times.
* Prefer constructor injection.
* Do not add `ArgumentNullException.ThrowIf...` guards in constructors.

#### Null Handling

* Add null guards in public implementation methods when a non-nullable input is required and the method does not intentionally support `null`.
* Skip null guards for nullable or intentionally null-tolerant parameters.
* After the final null-check or argument-validation line in a method, add a blank line before the next statement (even if it is the only guard).

#### Formatting & Layout

* Never use more than one consecutive blank line.
* Add a blank line before a `return` statement unless it is the first statement in a `{ ... }` block.
* Add a blank line before and after `if`, `switch`, and loop blocks unless the block is immediately preceded by `{`.
* Do not add a blank line between a control statement (`if`/`else`/`switch`/loop) and its opening `{`.
* Do not leave extra blank lines between consecutive closing braces.
* Add a blank line after `};` and after multi-line object initializers unless returned inline.
* When an object initializer spans multiple lines, place each property assignment on its own line.
* Format conditional (`?:`) operators across multiple lines with the condition, `?`, and `:` each on their own properly indented lines.
* Always keep exactly one trailing newline at the end of each file.
* In `.cshtml` files, never introduce stray carriage returns (`^M`) or CRCRLF line endings; keep view files normalized so they do not render with artificial blank lines.

#### `#pragma` Rules

* Do not add a blank line immediately after `#pragma warning disable`.
* Do not add a blank line immediately before `#pragma warning restore`.
* Do not add a blank line after `#pragma warning restore` if followed by `{`.
* Add a blank line before a `#pragma warning disable` block when it starts a new member after a `}`.
* Add a blank line between `#pragma warning restore` and the next `#pragma warning disable` when guarding separate members.

#### Usings & Types

* Do not use `global using`; declare explicit `using` statements at the top of each file.
* Prefer top-of-file `using` directives over fully qualified type names.
* Prefer `using` statements over fully qualified names when there is no conflict.
* Use `var` consistently with repository style.

#### Members & Structure

* Only use expression-bodied members when the entire member fits on a single short line.
* Use full block bodies for anything longer or multi-line.
* When a constructor has more than one parameter, place each parameter on its own line.
* Place constructor initializer clauses (`: base(...)`) on their own indented line.
* Seal publicly accessible classes by default unless inheritance is intentionally required (Except for View Models). In OrchardCore, we can't seal view models.

#### Time Abstraction

* Avoid `DateTime.UtcNow`; always use an injected `IClock`.

#### Showing Dates and Times (never UTC)

Times are stored in UTC, but a user never sees a UTC time unless they asked for one. Every date and time shown anywhere (views, admin lists, reports and their CSV/Excel/PDF exports, emails, SMS, notifications, notices, timeline and audit text, AI prompts that mention the current time) is converted to the local time zone with Orchard Core's `ILocalClock`, which is the user's own zone when the User Time Zone feature is on and the site's otherwise.

* In Razor, use the `DateTime` shape (`@await DisplayAsync(await New.DateTime(Utc: value, Format: "g"))`) or `ILocalClock.ConvertToLocalAsync`. In C#, inject `ILocalClock` and call `ConvertToLocalAsync`. Contact Center reports use `ReportTimeZone`, which reads its zone from `ILocalClock`.
* Mark the value as UTC first: `DateTime.SpecifyKind(utc, DateTimeKind.Utc)`. A value read back from YesSql or JSON often has an unspecified kind, which `ConvertToLocalAsync` would treat as server-local.
* Never call `DateTime.ToLocalTime()` (that is the server's zone, UTC in production), never format a stored UTC value directly (`utc.ToString("g")`, `T["... {0:g}", utc]`), and never label a value "(UTC)".
* Group report rows by the local hour, weekday or day, not the UTC one.
* An editor shows its date-times in local time and converts the entered value back with `ILocalClock.ToUtcLenientAsync` (`CrestApps.OrchardCore.Calendars`), which accepts a time a daylight-saving change skips or repeats. Never ask the user to type a time in UTC.
* JSON sent to the browser carries the UTC instant with a `Z` (mark it with `SpecifyKind` when it was read back from storage); the page formats it for display.
* Exceptions: a pure date with no time (a due date picked as a date, a tax effective date) is not converted; a value deliberately shown in a contact's or lead's own time zone stays in that zone; machine formats (APIs, recipe and JSON schemas, content import/export columns, files scripts read) stay UTC.

#### Documentation

* Keep public docs and comments accurate and aligned with the code.
* In Markdown docs, do not manually hard-wrap prose at a fixed column. Keep each paragraph or list item on a single line and let the editor/viewer wrap it visually.
* Always document:

  * Every public method (including constructors) with XML `<summary>` and `<param>` tags.
  * Every public property with an XML `<summary>`.
  * All interfaces and their members.
* Only include `<param>` tags for actual parameters, in the exact order of the signature.
* Improve existing XML docs in place—do not duplicate `<summary>` blocks.
* Always insert a blank line before XML `<summary>` blocks unless immediately preceded by `{`.
* Never insert a blank line between an XML doc block and the member it documents.
* Always leave a blank line between a property and a following XML documentation block.
* Remove decorative or low-value comments; prefer meaningful XML documentation.

#### Testing

* In unit tests:

  * Separate setup, act, and assert sections.
  * Use `// Arrange`, `// Act`, and `// Assert` when helpful.
  * Wrap long fluent chains across multiple lines with consistent indentation.
  * Add a blank line after multi-line setup statements.

#### JavaScript in `.cshtml`

* Ensure proper indentation for all JavaScript inside `.cshtml` files.
* When editing `.cshtml` files, preserve normalized line endings and remove any stray `^M`/CRCRLF formatting damage instead of leaving it in place.

#### Architecture & Design

* Prefer SOLID, DRY, KISS, and YAGNI principles.
* Consolidate duplicated provider, transport, or store logic into shared abstractions before introducing new one-off implementations.
* Favor additive shared infrastructure first, then migrate consumers in safe, incremental steps.
* Optimize framework code for consistency, extensibility, and long-term maintainability across providers and hosts.
* For optional provider integrations in sample hosts, do not eagerly read validated options in UI setup paths—unconfigured providers should appear unavailable rather than cause failures.

#### Admin Index (CRUD List) Pages

Every admin page that lists records follows one pattern, so all lists look alike and honor the List/Grid layout and the Buttons/Menu row actions picked in **Configuration → Settings → Admin**. Follow `.agents/skills/crestapps-admin-index-views/SKILL.md` for every new list and whenever you touch an existing one; AI Profiles is the reference page.

* Render the list with Orchard Core's `<admin-list name="..." context="Model.List">` tag helper. Never hand-write a `<ul class="list-group">` or `<table>` listing, a toolbar row, a select-all checkbox, an item count, an empty-state row or a pager.
* The controller passes the records in an `AdminListContext<TModel>` (`Source`, `Pager`, optional `ToolbarActions`/`Header`). It never builds `SummaryAdmin` row shapes.
* Rows come from a display driver for the domain model. Give every shape a `SummaryAdmin` location and a `SummaryAdminGrid` location in a column zone (`Actions`/`ActionsMenu` serve both), and add a checkbox shape only when the list has bulk actions.
* Row templates use the shared row layout: the title is a plain link, facts are `badge ta-badge fw-normal` badges with an icon and a tooltip, the description sits under the title, and actions render through `AdminListActions`. `ActionsMenu` items are bare `dropdown-item` elements, never wrapped in `<li>`.
* The action bar above the list holds only the search (an input group ending with the magnifying-glass button) and the page buttons such as **Add**. Filters (status, type, "Show" selects) go in the list's toolbar through `AdminListContext.ToolbarActions`, never beside the **Add** button.
* Declare the grid columns in the view (`<admin-list-grid>` with `<admin-list-column>`), give the list a unique PascalCase name, and check the page in both layouts and both action modes before you hand it off.
* Toolbar filters are bootstrap-select pickers, as on Orchard Core's **Manage Content** list: `<div class="btn-group filter">` holding `<select class="selectpicker show-tick" data-style="btn-sm" data-width="fit" data-header="..." data-dropdown-align-right="auto" aria-label="...">`, with `<style asp-name="bootstrap-select">` and `<script asp-name="bootstrap-select" depends-on="bootstrap" at="Foot">` on the page. A filter submits the list's form when it changes. Never a plain `form-select` in the toolbar.

#### Admin Forms and Menus

Build admin UI the way Orchard Core builds it, and add a new concept only when Orchard Core has none.

* Every field uses the `ocat-*` layout (`ocat-wrapper`, `ocat-label`, `ocat-end`, and `ocat-end-offset` for a checkbox): one label and field per line, stacked. No multi-column field grids.
* Group related fields in Bootstrap cards (`card` with a `card-header` naming the group and a `card-body`), one card per group, especially on forms with many fields.
* Every top-level admin menu item has an icon (`icon-class-fa-solid`, `icon-class-fa-...`). One-time setup pages, such as a "Get started" page, are not in the menu; link to them from the module's pages.
* Reports belong under the **Reports** menu, not under the module's own menu.

#### Data Portability

* Every kind of record a module stores (CRUD records and their settings) has an Orchard Core recipe step (`AddRecipeExecutionStep`) and deployment step (`AddDeployment`), so it can be exported and imported.
* Each recipe step has an `IRecipeStep` schema, and each deployment step a deployment step schema, registered in a startup with `[RequireFeatures("CrestApps.OrchardCore.Recipes")]`, so CrestApps Recipes exports their definitions.
* Every options class bound from configuration (appsettings) is described in the module's `ConfigurationSchema.json`.

#### Admin Edit (Form) Page Buttons

Every admin create, edit, settings, import or other form page keeps its buttons in Orchard Core's sticky action bar at the bottom of the screen (OrchardCore PR #19302), so **Save** and **Cancel** stay visible however long the form is. Follow `.agents/skills/crestapps-admin-edit-views/SKILL.md` for every new form page and whenever you touch an existing one.

* Put the form's buttons straight in `<div class="action-bar-bottom">` inside the `<form>`, main action first and **Cancel**/**Back** last. Never end a form with an `ocat-wrapper`/`ocat-end-offset`, `d-flex gap-2` or `mb-3` row of buttons, and do not wrap the buttons inside the bar or give them spacing classes.
* Mark destructive and rarely used buttons (**Delete**, **Preview**, a second "receive everything" action) with `action-bar-secondary`, so they move into the bar's ellipsis menu in the **Menu** actions layout (**Configuration → Settings → Admin → Actions layout**) and on small screens.
* One bar per page. Hints or warnings that sat under the buttons go above the bar in their own `ocat-end-offset` row.
* Modals, sub-forms inside a card or an accordion, list filters and row actions, and storefront/customer pages keep their buttons inline. A partial shared by a page and a modal renders the bar only on the page.
* `tests/js/lint/admin-forms-use-action-bar.test.js` guards the rule. Check the page in both actions layouts and at phone width before you hand it off.

#### Quality Enforcement

* Treat all warnings as errors and resolve every warning.
* Continuously evolve and update this `AGENTS.md` file (and `docs/engineering/building-blocks.md` for building blocks) with newly established preferences and patterns.
* Use ASD-STE100 Simplified Technical English for all responses.

### Validation Scenarios

**When the build succeeds**, always validate changes by:

1. **Build Validation**: Run both asset and .NET builds
2. **Test Validation**: Execute the full test suite  
3. **Web Application Testing**: 
   - Start the web application
   - Complete the Orchard Core setup wizard
   - Enable relevant CrestApps modules in the admin dashboard
   - Test module-specific functionality (AI chat, user management, etc.)
4. **Asset Validation**: Run `npm run rebuild` and verify no asset changes are uncommitted

**When build fails due to network issues**:
- Focus on asset-only changes using `npm run rebuild`
- Document any .NET code changes for testing in environments with network access
- Use static analysis and code review for .NET validation

## Repository Structure and Navigation

### Key Directories
```
src/
├── Abstractions/   # CrestApps.OrchardCore.{X}.Abstractions: contracts, shared records, constants, feature IDs
├── Core/           # CrestApps.OrchardCore.{X}.Core: implementations, stores, indexes (no UI); shared platform
│                   #   libraries CrestApps.OrchardCore.Core and CrestApps.OrchardCore.YesSql.Core
├── Modules/        # CrestApps.OrchardCore.{X}: about 120 Orchard Core modules (manifest, startups, drivers, views)
├── Themes/         # CrestApps.OrchardCore.StorefrontTheme
├── CrestApps.Docs/ # Docusaurus documentation site (User Manual and Technical Manual)
├── Startup/        # Runnable hosts: Cms.Web (main app), Aspire.AppHost, Asterisk.Web (dev diagnostics),
│                   #   Samples.McpClient, Samples.A2AClient
└── Targets/        # CrestApps.OrchardCore.Cms.Core.Targets: references every module so Orchard Core discovers it

tests/
├── CrestApps.OrchardCore.Tests/                          # Unit tests (most modules)
├── CrestApps.OrchardCore.Commerce.IntegrationTests/
├── CrestApps.OrchardCore.ContactCenter.DistributedTests/ # Need Redis/PostgreSQL environment variables
├── CrestApps.OrchardCore.ContactCenter.FeatureActivationTests/
├── CrestApps.OrchardCore.TenantHierarchy.IntegrationTests/
├── CrestApps.OrchardCore.Telephony.PlaywrightTests/
├── CrestApps.OrchardCore.Benchmarks/
└── js/                                                   # Vitest asset and view lint tests

docs/engineering/   # Engineering plans and reviews, including building-blocks.md (the building-block catalog)
.agents/skills/     # Repository development skills
.github/workflows/  # CI/CD pipelines
```

### Module Map

Modules by product area (each `X` is `src/Modules/CrestApps.OrchardCore.X`; most have matching `.Abstractions` and `.Core` projects). The building blocks other modules reuse are in **bold**; see [`docs/engineering/building-blocks.md`](docs/engineering/building-blocks.md) for what each owns and how to extend it.

- **Platform**: **Recipes**, **Resources**, **ContentFields**, ContentAccessControl, **ContentTransfer** (+ Azure, OpenXml), **Wizard**, **Users**, Roles, **TimeZones**, **TenantHierarchy**, **WebSockets**, SignalR (deprecated), **Seo**, NLWeb, **DataConnectors** (+ SqlServer), **Reports** (+ OpenXml, Pdf).
- **People and places**: **Accounts**, Accounts.B2B, **Customers**, **Teams**, **Skills**, **Addresses**, **PhoneNumbers**, PhoneNumbers.Verifications, **Geo** (+ AzureMaps, GoogleMaps, Osrm).
- **Time and work**: **Calendars**, **Scheduling**, **TimeTracker**, Workforce, Workforce.ContactCenter, WorkOrders, Dispatch, **Assets**, OrTools.
- **CRM and communication**: **Omnichannel**, Omnichannel.Managements (CRM), Omnichannel.EventGrid, **Omnichannel.Messaging** (+ Azure, Sms), Omnichannel.Sms, Omnichannel.Voice, **DncRegistry** (+ Azure), ContactCenter, **Telephony** (+ Azure), Telnyx, Asterisk.
- **AI**: **AI**, AI.A2A, AI.Agent, AI.Chat, AI.Chat.Claude, AI.Chat.Copilot, AI.Chat.Interactions, AI.DataSources (+ AzureAI, Elasticsearch, PostgreSQL), AI.Documents (+ Azure, AzureAI, Elasticsearch, OpenXml, Pdf), AI.FileSources (+ Ftp, Sftp), AI.Mcp (+ Resources.Ftp, Resources.Sftp), AI.Memory (+ AzureAI, Elasticsearch), AI.Prompting, AI.WebCrawlers, AI.WebMcp; providers OpenAI, OpenAI.Azure, AzureAIInference, Ollama, Cartesia, ElevenLabs.
- **Commerce**: Commerce, Commerce.Webhooks, **Products** (+ AzureAI, Elasticsearch, Lucene), **Carts**, **Checkout**, **Orders**, **Transactions**, **Receipts**, Stripe, PayLater, **Subscriptions**, Memberships, Inventory, Shipping, **Taxation**, Markets, Marketing, Marketplace, Classifieds, PointOfSale, Fundraising, Downloads, Reviews, Bookings.

### Important Files
- `CrestApps.OrchardCore.slnx` - Main solution file (Visual Studio XML format)
- `global.json` - .NET SDK version (10.0.100)
- `Directory.Build.props` - Common MSBuild properties (TFM, versioning, analysis rules)
- `Directory.Packages.props` - Centralized NuGet package versions
- `NuGet.config` - Package source configuration (includes CloudSmith feed for Orchard Core previews)
- `package.json` - npm dependencies and scripts
- `.scripts/assets/` - Asset build CLI (Vite); see `docs/engineering/assets-pipeline.md`
- `.node-version` - The Node.js major the asset build and CI use
- `.editorconfig` - Code style and formatting rules

## Common Development Tasks

### Adding a New Module
1. **Reuse first** (see *Building Blocks and Reuse*): list every concept the module needs (people, organizations, groups, addresses, phones, places, hours, events, skills, money, numbers, reports, imports, webhooks, files, secrets) and map each one to its owner in [`docs/engineering/building-blocks.md`](docs/engineering/building-blocks.md). Build only what has no owner, extract a concept into a building block when another module already has it, and record the mapping in the plan and the pull request.
2. Create the projects the layering rules need: `src/Abstractions/CrestApps.OrchardCore.{ModuleName}.Abstractions/` for contracts others consume, `src/Core/CrestApps.OrchardCore.{ModuleName}.Core/` for implementations, and `src/Modules/CrestApps.OrchardCore.{ModuleName}/` for the module. Reach other building blocks' services and models through their `.Abstractions` and `.Core` projects; a module reference only carries a feature dependency, and module code never uses another module's types (see *Layering and project references*).
3. Add the module project file following existing patterns, and add every new project to `CrestApps.OrchardCore.slnx`.
4. Include `Manifest.cs` with the module definition, with feature IDs as constants in the `.Abstractions` project and optional integrations as separate features or `[RequireFeatures]` startups.
5. **Update Targets**: Add a reference to the new module in the targets project `src/Targets/CrestApps.OrchardCore.Cms.Core.Targets/CrestApps.OrchardCore.Cms.Core.Targets.targets` so it is discoverable by Orchard Core.
6. **Make it extensible and portable**: driver-based editors, life-cycle handlers, a recipe step and a deployment step for every record type, and `ConfigurationSchema.json` for every options class.
7. **Work test-first**: add the failing test before the implementation and drive the module with TDD (see *Test-Driven Development*).
8. **Document it**: add/refresh the module `README.md` and the relevant page under `src/CrestApps.Docs/docs` in the same change (see *Documentation expectations*). If the module adds or changes a building block, update `docs/engineering/building-blocks.md` too.
9. **Consider a development AI skill**: if the module introduces extension points others (or an AI agent) will build against, add a skill under `.agents/skills/` (see *AI Development Skills*).

### Working with AI Modules
- **Base AI Module**: `CrestApps.OrchardCore.AI` - start here for AI-related changes; manages deployments and provider connections
- **Chat Interface**: `CrestApps.OrchardCore.AI.Chat` - AI chat profiles and UI
- **Real-time Chat**: `CrestApps.OrchardCore.AI.Chat.Interactions` - SignalR-based interactive chat hub
- **Copilot Chat**: `CrestApps.OrchardCore.AI.Chat.Copilot` - GitHub Copilot-style embedded chat experience
- **Document Indexing**: `CrestApps.OrchardCore.AI.Documents` - indexes documents for AI retrieval
- **Data Sources**: `CrestApps.OrchardCore.AI.DataSources` - configures external AI data sources
- **MCP Server**: `CrestApps.OrchardCore.AI.Mcp` - exposes Orchard Core content as MCP resources
- **AI Agents**: `CrestApps.OrchardCore.AI.Agent` - defines reusable AI agents/tools
- **Provider modules**: `CrestApps.OrchardCore.OpenAI`, `CrestApps.OrchardCore.OpenAI.Azure`, `CrestApps.OrchardCore.Ollama`, `CrestApps.OrchardCore.AzureAIInference`
- **AI Prompt Templates**: Never hardcode AI system prompts or prompt-style recovery instructions in C# code. Store them in `Templates/Prompts/*.md`, add a constant in `AITemplateIds`, and render them through `ITemplateService`.
- **AI function catalog docs**: Whenever you add, remove, rename, re-categorize, or change the description or feature requirements of an AI function/tool, update `src\CrestApps.Docs\docs\ai\tools.md` and any related feature doc (for example `ai\memory.md`) in the same change so the documented function lists stay synchronized with the codebase.

### Working with Omnichannel Modules
- **Base Module**: `CrestApps.OrchardCore.Omnichannel` - unified communication layer
- **SMS Automation**: `CrestApps.OrchardCore.Omnichannel.Sms` - AI-driven automated SMS conversations
- **Messaging Workspace**: `CrestApps.OrchardCore.Omnichannel.Messaging` - the channel-agnostic human inbox (customer list + a tab per channel). Channels plug in as features by implementing `IMessagingChannel`; never add channel-specific code to the workspace
- **SMS Messaging Channel**: `CrestApps.OrchardCore.Omnichannel.Messaging.Sms` - SMS as a channel of the messaging workspace (the reference channel)
- **Event Grid**: `CrestApps.OrchardCore.Omnichannel.EventGrid` - Azure Event Grid integration
- **Management UI**: `CrestApps.OrchardCore.Omnichannel.Managements` - admin management interface

### AI Development Skills (`.agents/skills/`)

Repo-level development skills live under `.agents/skills/<skill-name>/SKILL.md` (with optional
`references/*.md`). These teach an AI agent how to correctly extend this codebase — they are distinct from the
runtime MCP skills shipped by the `CrestApps.OrchardCore.AI.Mcp` module (which come from the
`crestapps.agentskills.mcp` NuGet package and appear only under `bin/.../.agents`).

- **Add a skill whenever a module introduces reusable extension points** — a provider abstraction, a set of
  capability interfaces, a display-driver/handler pattern, an event-ingress seam, or any "implement this
  contract to plug in" surface that others (or an AI agent) will build against. The goal is that a future
  agent can extend the infrastructure accurately without missing a capability or a required registration.
- **Model the skill on the most complete existing implementation** and enumerate *every* contract/capability
  the extension point exposes, marking which are required vs. optional, so nothing is silently omitted.
- **Format**: `SKILL.md` starts with YAML frontmatter (`name`, `description`, `license: Apache-2.0`,
  `metadata: { author: CrestApps Team, version }`). The `description` must be specific enough to trigger on
  the right requests. Put deep detail in `references/` and link to the authoritative source files and the
  matching docs page under `src/CrestApps.Docs/docs`.
- **Keep skills honest and current**, exactly like the docs: when you change an extension point, update its
  skill in the same change; when a skill no longer matches the code, fix or remove it rather than leaving it
  stale. A skill that misdescribes a contract is worse than no skill.
- **Example**: `.agents/skills/crestapps-phone-provider` — how to add a new telephony/phone provider (Twilio,
  Vonage, …) modeled on the Telnyx implementation, covering every telephony/Contact Center/SMS/media/webhook
  capability contract.
- **Example**: `.agents/skills/crestapps-messaging-channel` — how to add a new messaging channel (Email, WhatsApp,
  Messenger, …) to the Omnichannel Messaging workspace, modeled on the SMS channel: the `IMessagingChannel`
  contract, inbound through the durable provider inbox, channel-only inbound rules, delivery receipts, endpoints,
  wiring, tests and docs.
- **Example**: `.agents/skills/crestapps-report-data-source` — how to add a data source (connector) to the Report
  Designer (a database, a search index, an API, or a module's own records) by implementing `IReportDataSource`, and
  how to extend the content items source for custom content fields and parts: the security boundary, value types, safe filter
  push-down, limits, wiring, tests and docs.
- **Example**: `.agents/skills/crestapps-admin-index-views` — how every admin list (index) page is built on
  Orchard Core's `AdminList` shape and `<admin-list>` tag helpers, in the List or Grid layout the site and the user
  pick: the row display driver, the row template, the controller's `AdminListContext`, the view and its columns.
  Never hand-write a `<ul class="list-group">` listing.
- **Example**: `.agents/skills/crestapps-admin-edit-views` — how every admin form page puts its buttons in Orchard
  Core's sticky bottom action bar (`action-bar-bottom`, `action-bar-secondary`), and which forms (modals, card
  sub-forms, filters, storefront pages) keep their buttons inline.
- **Example**: `.agents/skills/crestapps-calendar-types` — how another module builds on the Calendars module:
  registering a calendar type and its event types with `AddCalendarType` (owner kind, capacity, `ManagedByCode`),
  creating and keeping events in step with the module's own records, busy-time and event sources, handlers and
  after-commit observers, and the naming and wall-clock time traps.
- **Example**: `.agents/skills/crestapps-calendar-provider` — how to add an external calendar provider (Google Calendar,
  Microsoft 365, CalDAV, …) to External Calendars: full and incremental reads, two-way writes with versions, conflict
  policies, refused access, account connections, event mapping, registration, tests and docs.
- **Example**: `.agents/skills/crestapps-webmcp-tools` — how to extend the WebMCP module, a thin layer over CrestApps.Core's
  WebMCP: tools registered with the standard `document.modelContext.registerTool`, scripts and settings added through
  `IWebMcpClientConfigurationHandler`, the server-side checks every WebMCP list and call must keep, the chat bridge's
  limits and options, and the tests to add.
- **Example**: `.agents/skills/crestapps-rate-limits` — how to limit a route with a request group of Orchard Core's Rate
  Limits and its built-in limit, count payments, declines, codes or lookups with an `IAttemptLimiter` policy, key them
  with `ClientNetworkKey`, and keep the architecture test that guards guest-reachable commerce endpoints green.

### Frontend Development
- CSS/SCSS files are in individual module `Assets/` directories
- JavaScript and SCSS files are built with Vite by the asset CLI in `.scripts/assets/`
- Run `npm run rebuild` after any frontend changes
- Use `npm run watch` for development with auto-rebuild

### Testing Workflow
1. Make code changes
2. Run `npm run rebuild` for frontend changes
3. Run `dotnet build` for backend changes (if network allows)
4. Run `dotnet test` for unit tests (if build succeeds)
5. Start web application and test manually

### Documentation Workflow

Whenever code is modified, you MUST update the documentation project located at `src/CrestApps.Docs`. **Read [`src/CrestApps.Docs/README.md`](src/CrestApps.Docs/README.md) before you touch any page**: it is the contributor guide for the docs site and the rules there are enforced in CI.

The site holds two manuals:

- The **User Manual** (`src/CrestApps.Docs/docs/user-manual/**`) is for people who use the app in the browser: agents, supervisors, managers and the administrators who work in the admin screens. It explains what each screen is for and how to use it, step by step, with no code, configuration files or feature IDs. Most readers are not administrators, so every feature page lists the menu, permission and feature it needs, followed by `<AskYourAdmin />`.
- The **Technical Manual** (every other page under `src/CrestApps.Docs/docs/`) is for developers and IT: feature IDs, `appsettings.json` and environment variables, recipes, architecture, extension points and operations.

The dividing rule: if it can be done in the browser, it belongs in the User Manual; if it needs code, a configuration file, an environment variable, recipe JSON, a server or a deployment, it belongs in the Technical Manual. Most features need a page in each, linked through `technical_manual` / `user_manual` front matter.

1. **Update feature documentation first** – find the relevant pages and keep them accurate with the latest behavior:
   - A change to a screen, label, menu, button, setting or permission name → the User Manual page for that screen. Labels in the docs must match the screen exactly.
   - A change to configuration → the feature's Technical Manual page **and** `docs/configuration.md`, showing both the `appsettings.json` and the environment-variable form.
   - A change to feature IDs, recipes, events, APIs or extension points → the Technical Manual page (and `docs/feature-reference.md` for feature IDs).
   - A new feature → a page in each manual, both sidebars in `sidebars.js`, and the training paths or use cases it belongs in.
2. **Documentation changes are NOT optional** – code changes without documentation updates are considered incomplete.
3. **Validate the docs** – run `npm run check:manuals` and `npm run build` in `src/CrestApps.Docs`. The build fails on any broken link or anchor, and CI runs both checks; failing to validate locally will cause workflow failures.

### Documentation Videos

- **Only produce videos when the user explicitly asks for them.** Never record or regenerate demo videos as part of a routine documentation update.
- **Never commit a video file** (`.mp4`, `.webm`, an animated `.gif` screencast, or a video's captions and poster) to this repository. Videos are published on the CrestApps YouTube channel, and their sources (storyboards, recorder scenes, YouTube metadata) live in the `CrestApps/CrestApps.Videos` repository, together with the `crestapps-video-producer` skill that makes them.
- Embed a video by name with the `<YouTube video="<slug>" title="..." />` component (`src/CrestApps.Docs/src/components/YouTube`), available on every page without an import. The slug is the video's folder name in `CrestApps.Videos`; its YouTube id comes from `src/CrestApps.Docs/src/data/videos.json`, which the `CrestApps.Videos` publish workflow updates by pull request. Until the slug is in that map the embed renders nothing, so a page can include it before the video is published. To show one chapter of a longer video, add `start` and `end` in seconds: `<YouTube video="<slug>" title="..." start={99} end={146} />`.

## Documentation expectations

**Keep the site docs honest.** Documentation must describe what the code actually does today — not what is planned, aspirational, or hoped. Never present a feature, option, setting, or behavior as available or working in the docs unless it is implemented and verified in the current code. When you change behavior, update the docs in the same change; when you find docs that no longer match the code, correct them rather than leaving them stale, and prefer removing an inaccurate claim over letting it stand. The same honesty applies to status you report back to the user: state plainly what was done and verified versus what is still pending, and never describe something as working that you have not confirmed.

When a change affects public behavior, configuration, setup, or project guidance:

1. Update the relevant page under `src\CrestApps.Docs\docs`
2. Update the changelog or release-notes file under `src\CrestApps.Docs\docs\changelog` that matches `VersionPrefix` in `Directory.Build.props` (for example, `VersionPrefix` `2.0.0` maps to `v2.0.0.md`)
3. Do not create a new changelog version file unless `VersionPrefix` changed or the user explicitly instructs you to create a different file
4. Build the docs site

## Troubleshooting

### Build Issues
- **NU1301 errors**: Network connectivity to CloudSmith required, expected in restricted environments
- **SDK version errors**: Ensure .NET 10.0.100+ is installed via `dotnet --version`
- **npm install failures**: Node.js 24 required, as pinned in `.node-version` (check with `node --version`)

### Runtime Issues  
- **Database**: Uses SQLite by default, no external DB required for development
- **Modules not appearing**: Enable modules in Orchard Core admin dashboard
- **Permission errors**: Check content access control module configuration
- **Application logs**: When troubleshooting runtime issues, always read the log at `src/Startup/CrestApps.OrchardCore.Cms.Web/App_Data/logs/orchard-log-{YYYY-MM-DD}.log` (for example `orchard-log-2026-03-11.log`). It contains errors, warnings, and debug output from all modules; trace the root cause there before changing code.

### Network Dependencies
This project requires network access to:
- `https://api.nuget.org/v3/index.json` (public NuGet)
- `https://nuget.cloudsmith.io/orchardcore/preview/v3/index.json` (Orchard Core previews)
- `https://nuget.cloudsmith.io/crestapps/crestapps-core/v3/index.json` (CrestApps.Core previews)

If CloudSmith is inaccessible, only asset builds and code analysis are possible.

## Coding Standards and Conventions

### C# Code Style (enforced via .editorconfig)

#### Naming Conventions
- **Interfaces**: Prefix with `I` (e.g., `IAICompletionService`, `IUserCacheService`)
- **Services**: Suffix with `Service` for service implementations (e.g., `DefaultAIToolsService`, `DefaultUserCacheService`)
- **Drivers**: Suffix with `Driver` for display drivers (e.g., `AIProfileDisplayDriver`, `AIToolInstanceDisplayDriver`)
- **Handlers**: Suffix with `Handler` for handlers (e.g., `AIProviderConnectionHandler`, `FunctionInvocationAICompletionServiceHandler`)
- **Providers**: Suffix with `Provider` for providers (e.g., `AIConnectionsAdminMenu`, `AIPermissionsProvider`)
- **Tests**: Suffix test classes with `Tests` (e.g., `OrchardCoreHelpersTests`)

#### Code Formatting
- **Indentation**: 4 spaces for C#, 2 spaces for JSON/YAML/XML
- **Line endings**: CRLF
- **Charset**: UTF-8
- **Braces**: Always use braces for code blocks, opening brace on new line (Allman style)
- **var usage**: Prefer `var` everywhere (built-in types, apparent types, and elsewhere)
- **Strings**: Prefer standard string interpolation for short single-line strings. Use raw triple-quoted strings (`"""`) or raw interpolated strings (`$"""` / `$$"""`) for multi-line or more complex text when they improve readability without changing whitespace or line-ending behavior. Keep concatenation only when interpolation or raw strings would hurt readability or change semantics.
- **this.**: Avoid using `this.` qualifier unless necessary
- **Language keywords**: Use language keywords (e.g., `int`, `string`) over framework types (e.g., `Int32`, `String`)
- **Namespaces**: Use file-scoped namespace declarations (C# 10+)
- **Using statements**: 
  - Sort System directives first
  - Prefer simple using statements over braces when possible
  - Remove unused usings from the code you touch
  
#### Code Preferences  
- **Range/Index operators**: Avoid using range/index operators (enforced as warning)
- **Code Analysis**: `AnalysisLevel` is set to `latest-Recommended`
- **Implicit usings**: Enabled globally
- **Database IDs**: Use `IdGenerator.GenerateId()` when creating database IDs manually. Generated IDs are always 26 characters long.
- **Date/time**: Never use `DateTime.UtcNow`. Always inject `IClock` in the constructor (e.g., `IClock clock`) and store it as `private readonly IClock _clock = clock;`, then call `_clock.UtcNow` in methods.
- **Interface and domain-model docs**: Every method declared on an interface or domain model should have an honest XML `<summary>` comment.
- **Parameter docs**: Interface methods with parameters should document each parameter with an XML `<param>` tag.
- **Namespace usage**: Prefer file-level `using` directives over fully qualified namespace references when no ambiguity exists.
- **Constructor layout**: Constructors with more than one parameter should place each parameter on its own line.
- **Initializer layout**: Multi-line object initializers should put each property on its own line and should usually be followed by a blank line after the terminating `};`, unless the initializer is returned on the same line.
- **Property/doc spacing**: Leave a blank line between a property and any following XML documentation block.
- **Brace spacing**: Do not insert an extra blank line between consecutive closing braces.
- **Unit test readability**: Keep setup, act, and assertion sections visually separated, use standard `// Arrange`, `// Act`, and `// Assert` comments where helpful, and wrap crowded fluent chains onto multiple lines with aligned indentation.
- **Dependency injection**: Prefer constructor injection over lazy service resolution. Only fall back to lazy resolution when it is absolutely necessary to break a real framework or container circular dependency.
- **Authorization handlers**: Do not constructor-inject `IAuthorizationService` into an `AuthorizationHandler`. Resolve and cache it lazily inside the handler because the authorization pipeline can otherwise create circular dependencies.
- **Collection handling**: Do not call `.ToList()` or `.ToArray()` unless a concrete snapshot is truly required for correctness or lifetime safety. Prefer consuming `IEnumerable<T>` directly when you only need to iterate.
- **Localization extraction**: When using `ILocalizer`, the property/variable must be named `S`, and localized strings must use the literal pattern `S["This is a localized string"]`. Do not use variables inside the brackets because extraction tooling looks specifically for `S["..."]`.
- **Select lists from enums**: Do not build Orchard `SelectListItem` collections by iterating enums with `Enum.GetValues(...)` and piping `ToString()` into the text. Define each item explicitly so the title uses `S["..."]` for extraction and multi-word labels can use the correct spacing.
- **Settings UI casing**: Use sentence case for settings labels, hints, and warning headings. Keep placement tab/card/column names and admin menu labels in title case.
- **Catalog entry handlers**: When a feature must react to create, update, or delete operations for catalog-backed models, prefer `CatalogEntryHandlerBase<T>` registered as `ICatalogEntryHandler<T>` and route write operations through the matching catalog manager so the handler events actually run. Do not rely on raw store writes alone when handler lifecycle behavior is required.
- **Deferred third-party work**: When a catalog handler must call indexing systems, external APIs, or other third-party/background-style services, follow the deferred task pattern used by `ChatInteractionHandler` and `AIMemoryEntryHandler`: capture the affected models/IDs in the scoped handler and schedule the work with `ShellScope.AddDeferredTask(...)` so the external work runs later instead of inline during the catalog event.
- **One type per file**: Every public type must live in its own file. The file name must always match the type name (e.g., `MyService.cs` for `class MyService`)
- **Global usings**: Do not add `GlobalUsings.cs` files or new global using directives; prefer explicit file-level usings
- **sealed classes**: Seal all classes by default (`sealed class`), **except** ViewModel classes that are consumed by any Orchard Core display driver — those must remain unsealed because the framework creates runtime proxies for them and proxies cannot be created from sealed types
- **AI site settings navigation**: Every `SiteDisplayDriver<>` that sets `SettingsGroupId => AIConstants.AISettingsGroupId` must also register `.AddNavigationProvider<AISiteSettingsAdminMenu>()` in its feature's `Startup` class. This ensures the **Settings → Artificial Intelligence** admin menu entry exists whenever the feature is enabled, so users can navigate to the settings page.
- **Settings group permissions**: Every `SiteDisplayDriver<>` that checks a module permission (anything other than `ManageSettings`) must register it with `services.AddSiteSettingsPermission(groupId, permission)` in the same startup class that registers the driver, and its project must reference `OrchardCore.Settings.Core` (or `OrchardCore.Settings`). Orchard Core's settings screen authorizes `ManageGroupSettings` for the group, so without the registration a role holding only the module permission sees the menu item and gets access denied. `SiteSettingsGroupPermissionRegistrationTests` enforces this.

### Module Structure Conventions

Every Orchard Core module in this repository follows a standard structure:

```
CrestApps.OrchardCore.{ModuleName}/
├── Assets/              # Frontend assets (JS, CSS, SCSS)
├── Controllers/         # MVC Controllers
├── Drivers/            # Display drivers
├── Handlers/           # Event handlers
├── Indexes/            # YesSql indexes
├── Migrations/         # Data migrations
├── Models/             # Domain models
├── Recipes/            # Recipe steps
├── Services/           # Business logic services
├── ViewModels/         # View models
├── Views/              # Razor views
├── Workflows/          # Workflow activities (if applicable)
├── Manifest.cs         # Module manifest (required)
├── Startup.cs          # Service registration (required)
├── README.md           # Module documentation
├── package.json        # npm dependencies (if has assets)
└── wwwroot/            # Compiled static files
```

### Startup Class Patterns

Modules use `StartupBase` classes for service registration:
- Main `Startup` class for core services
- Feature-specific startup classes with `[Feature("FeatureName")]` attribute
- `[RequireFeatures()]` attribute for conditional features
- Separate startup classes for Recipes, Deployment, Workflows integrations

Example pattern:
```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Register services
    }
}

[Feature(Constants.Feature.FeatureName)]
public sealed class FeatureStartup : StartupBase
{
    // Feature-specific registration
}
```

### Namespace Conventions

- Namespace matches folder structure
- Pattern: `CrestApps.OrchardCore.{ModuleName}.{FolderName}`
- Examples:
  - `CrestApps.OrchardCore.AI.Services`
  - `CrestApps.OrchardCore.AI.Recipes`
  - `CrestApps.OrchardCore.Users.Core`

## Testing Practices

### Test Structure
- Tests are located in `tests/CrestApps.OrchardCore.Tests/`
- Use xUnit for all tests
- Test class naming: `{ClassUnderTest}Tests`
- Use `[Theory]` and `[InlineData]` for parameterized tests
- Use `sealed` modifier for test classes

### Test Naming
- Test method pattern: `{MethodName}_{Scenario}_{ExpectedBehavior}`
- Example: `IsVersionGreaterOrEqual_WhenVersionIsGreater_ShouldReturnTrue`
- Be descriptive and explicit about what is being tested

### Test Organization
- Group related tests in nested folders matching source structure
- Mock interfaces with test implementations (e.g., `TestCatalogEntryHandler<T>`)
- Use Func<> delegates for flexible test behavior

### Test Coverage
- Add tests for new features and bug fixes
- Focus on business logic and service implementations
- Test edge cases and error conditions

### Test-Driven Development

Work test-first so refactoring is done confidently and behavior that was already fixed does not silently break again:

- **Write the test before the change.** For new behavior, add the failing test first. For a bug fix, first add a test that reproduces the bug and fails on the current code, then make it pass — the failing-first test is what proves the fix and locks the behavior against regression.
- **Red → Green → Refactor.** Confirm the test fails for the right reason, make it pass with the smallest change, then refactor with the test holding the behavior in place.
- **Do not refactor untested logic blind.** If you are about to change logic that has no coverage, first add characterization tests that pin the current behavior, then refactor against them.
- **Every bug fix ships with a regression test** named for the scenario it prevents, so the same defect cannot quietly return in a later change.
- **Run the affected tests after every change and before reporting work done** — never rely on "it builds". Run the specific test class or namespace for fast feedback while iterating, and the broader suite before finishing a change set. See the build/test commands under *Working Effectively → Unit Tests*.
- **When a change alters a public API surface** tracked by the PublicApi approval tests, update the approved baseline in the same change and review the diff as a deliberate surface change, not an afterthought.

## Documentation Standards

### Module README Files
Every module MUST have a README.md file with:
- Module purpose and features
- Installation instructions
- Configuration details
- Usage examples
- Dependencies on other modules

### Documentation Project
The Docusaurus documentation site is located at `src/CrestApps.Docs`; its [`README.md`](src/CrestApps.Docs/README.md) explains how to contribute. It contains two manuals:
- The **User Manual** under `docs/user-manual/`: getting started, training paths, use cases, feature pages by product area, a glossary and troubleshooting.
- The **Technical Manual** under the rest of `docs/`: getting started for developers, the configuration reference (`docs/configuration.md`), the feature ID reference, module guides under `docs/ai/`, `docs/omnichannel/`, `docs/telephony/`, `docs/contact-center/` and `docs/modules/`, samples, and the changelog under `docs/changelog/`.

### Code Documentation
- XML documentation comments for public APIs
- Inline comments for complex logic only
- Keep comments up-to-date with code changes
- Avoid obvious comments that duplicate code

## CI/CD Integration

Before committing:
1. Run `npm run rebuild` - asset build must complete cleanly
2. Run `dotnet build -warnaserror` (if network allows) - the build must produce **zero warnings**; fix every warning across the entire project, not only in files you changed, before running with `-warnaserror`
3. Run `dotnet test` (if build succeeds) - all tests must pass
4. Verify no uncommitted asset changes with `git status`

The CI pipeline validates builds on both Ubuntu and Windows, so test locally on similar environments when possible.

---

**Remember: Always build and validate your changes thoroughly. The modular architecture means changes can affect multiple modules, so comprehensive testing is essential.**

## Frontend Development Guidelines

### Asset Management
- Frontend assets are built by a small CLI in `.scripts/assets/` that runs **Vite**, the only bundler
- Asset groups are defined in `Assets.json` files within each module (`action`, `name`, `source`, `dest`, optional `tags` and `global`)
- Built assets are output to the module's `wwwroot/` directory and committed
- The repository is one npm workspace: a module that ships a third-party library declares it in its own `package.json`, and `npm install` at the root installs every module
- Full reference: `docs/engineering/assets-pipeline.md`

### Build Commands
```bash
# Install dependencies (run once or when package.json changes)
npm install

# Build asset groups whose sources changed (content-hashed)
npm run build

# Build only some groups, by name or by tag (every group is tagged with its module, e.g. Telephony)
npm run build -- -n soft-phone.js
npm run build -- -t Telephony

# Full rebuild (all assets)
npm run rebuild

# Watch mode for development (auto-rebuild on changes; copies do not watch)
npm run watch

# List the selected groups and the files each one writes, without building
npm run dry-run -- -t vendor

# Lint every module's scripts (ESLint), and type-check the ones marked // @ts-check (TypeScript, no output)
npm run lint
npm run check
```

### Supported Asset Types
- **JavaScript**: Bundled by Vite (Rolldown) into a classic IIFE script, minified with Oxc; targets modern browsers
- **SCSS/Sass and CSS**: Compiled by Vite with Dart Sass, minified with Lightning CSS
- **Vendored libraries**: `copy` groups copy a package's own `dist` files; a `vite` group with `global` bundles an ES module or CommonJS package into a script that sets `window[global]`

### Asset Build Process
1. Source files are in module `Assets/` directory
2. Each `vite` group's `source` is its main script; helpers are ES modules the script `import`s, and the build follows the imports. Never attach helpers to a `window` namespace; a bundle offers other bundles only a deliberate page API such as `window.telephonyClient`
3. Each group writes its readable `dest` file and a `.min` sibling, the pair the resource manifest's `SetUrl` points at
4. Every built file is written with LF line endings, and only when its contents changed

### Frontend Best Practices
- Always run `npm run rebuild` after modifying frontend code
- Commit compiled assets (wwwroot) along with source files
- Use `npm run watch` during active development
- Run `npm run lint` and `npm run check` before committing; CI fails on any error
- Follow existing patterns for module-specific assets

## Pull Request Guidelines

### Before Submitting
1. **Build Validation**: Ensure both .NET and asset builds succeed
2. **Test Coverage**: Add tests for new features and bug fixes
3. **Code Quality**: Follow coding standards and conventions
4. **Documentation**: Update README files, code comments, and the Docusaurus docs in `src/CrestApps.Docs` (both manuals where the change affects them; see `src/CrestApps.Docs/README.md`)
5. **Commit Messages**: Write clear, descriptive commit messages
6. **Branch Naming**: Use descriptive branch names (e.g., `feature/ai-chat-improvements`, `fix/user-avatar-bug`)

### PR Description Template
- Link to related issue using `Fix #issue_number` or `Closes #issue_number`
- Describe what changed and why
- Include screenshots for UI changes
- List any breaking changes
- Note any migration or deployment considerations

### Review Process
- Address feedback promptly
- Don't manually resolve conversations - let reviewers do that
- Use "Re-request review" when changes are ready
- Keep discussions within the PR thread
- Allow maintainers to edit your PR branch

## Security Considerations

### Secure Coding Practices
- Validate all user inputs
- Use parameterized queries (YesSql handles this)
- Implement proper authentication and authorization checks
- Never commit secrets or sensitive data
- Follow OWASP guidelines for web security

### Permission Checks
- Always check permissions before accessing restricted resources
- Use `IAuthorizationService` for authorization
- Define permissions in `PermissionProvider` classes
- Test permission boundaries

### Sensitive Data
- Never log sensitive information
- Use secure storage for secrets (e.g., Azure Key Vault, environment variables)
- Encrypt sensitive data at rest and in transit

## Common Patterns

### Service Registration
```csharp
services.AddScoped<IMyService, MyService>();        // Scoped per request
services.AddTransient<IMyService, MyService>();     // New instance each time
services.AddSingleton<IMyService, MyService>();     // Single instance
```

### Display Drivers
```csharp
services.AddDisplayDriver<TModel, TDriver>();
```

### Handlers
```csharp
services.AddScoped<IEventHandler, MyEventHandler>();
```

### Migrations
```csharp
services.AddDataMigration<MyMigrations>();
```

### Navigation
```csharp
services.AddNavigationProvider<MyAdminMenu>();
```

## Anti-Patterns to Avoid

- Don't re-model a concept that a building block owns: no address-line fields, raw phone strings, latitude/longitude pairs, second organization or "company" records, roles used as teams, extra business-hours, holiday, skill or currency tables, or copied user names (see `docs/engineering/building-blocks.md`)
- Don't copy a helper, base class, serializer or view from another module; move it to the owning building block or the shared platform library and reuse it from both places
- Don't use any type from another module project in module code (a module reference only carries a feature dependency), and never let a `.Core` or `.Abstractions` library reference a module project
- Don't make a building block name its consumers (hard-coded tabs, enum values or feature IDs of the modules built on it)
- Don't use static mutable state
- Don't create tight coupling between modules
- Don't bypass Orchard Core's dependency injection
- Don't hardcode connection strings or secrets
- Don't use synchronous I/O operations (use async/await)
- Don't ignore compiler warnings (TreatWarningsAsErrors is enabled) — fix all warnings in the entire project, not just changed files
- Don't skip writing tests for new features
- Don't commit commented-out code
- Don't use `System.Range` or `System.Index` operators (enforced as warning)
- Don't leave unused services injected through dependency injection
- Don't leave unused `using` statements in source files
- Don't use `DateTime.UtcNow` — inject `IClock` and use `_clock.UtcNow` instead
- Don't leave interface or domain-model methods undocumented.
- Don't omit `<param>` documentation on interface methods that take parameters.
- Don't use fully qualified namespaces when a normal `using` directive would be clear and conflict-free.
- Don't keep multi-parameter constructors on a single line.
- Don't leave decorative region blocks or useless AI-style comments in source.
- Don't place an XML documentation block immediately after a property without a blank line between them.
- Don't crowd unit tests by running setup, act, and assertion code together without visual separation.
- Don't seal ViewModel classes that are used by any Orchard Core display driver — the framework requires unsealed types to generate runtime proxies
- Don't put multiple public types in a single file — each public type must be in its own file whose name matches the type name

## Code Cleanup (Required After Completing Work)

After completing any code change, always clean up:

- Remove any **unused services** that were injected through dependency injection but are no longer referenced in the class body.
- Remove any **unused `using` statements** from all modified source files.
- Prefer clarity over cleverness; do not introduce undocumented behavior.

## Useful Resources

- [Orchard Core Documentation](https://docs.orchardcore.net/)
- [Orchard Core GitHub](https://github.com/OrchardCMS/OrchardCore)
- [Project Repository](https://github.com/CrestApps/CrestApps.OrchardCore)
- [Contributing Guidelines](.github/CONTRIBUTING.md)
- [MIT License](https://opensource.org/licenses/MIT)

- @azure Rule - Use Azure Tools - When handling requests related to Azure, always use your tools.
- @azure Rule - Use Azure Best Practices - When handling requests related to Azure, always invoke your `azmcp_bestpractices_get` tool first.
- @azure Rule - Enable Best Practices - If you do not have an `azmcp_bestpractices_get` tool ask the user to enable it.
