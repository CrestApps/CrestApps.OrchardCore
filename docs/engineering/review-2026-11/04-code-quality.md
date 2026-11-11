# Workstream CQ: C# structure, reliability and build hygiene

Part of the [November 2026 review plan](README.md). Paths are relative to `src/`. `M/X` is `Modules/CrestApps.OrchardCore.X`, `C/X` is `Core/CrestApps.OrchardCore.X`, `A/X` is `Abstractions/CrestApps.OrchardCore.X`, and `T/` is `tests/CrestApps.OrchardCore.Tests/`.

Measured on `main` at `a12102c7a` over 10,156 C# files and 2,952 views in `src/` (the 2026-11-03 audit measured 9,158 and 2,905), with `bin`, `obj`, `node_modules` and `wwwroot` excluded. "Read" means the code was opened and the problem confirmed; other examples are scan hits the implementer confirms before changing them. Nothing was built for this review, so analyzer output and package vulnerabilities are not verified.

Items are ordered by value: correctness and safety first, style last. Each item is one pull request unless it says otherwise, and every behavior change starts with a failing test.

## Summary of measurements

| Metric | 2026-11-03 | Now | Note |
| --- | --- | --- | --- |
| `GetAwaiter().GetResult()` | 5 | 2 | One on a single line, and one split over three lines that the line-based ratchet cannot see (`M/ContactCenter/Services/ContactCenterShutdownDrain.cs:127-129`, deliberate) |
| Untracked `Task.Run` | 1 | 14 in 11 files, 9 discarded | CQ-2 |
| `catch (Exception` / bare `catch` | 646 / 53 | 726 / 54 | 97 neither log nor rethrow; CQ-6 |
| `.ListAsync(` | 504 | 814 to 985 (by two counts) | 244 on a query with no `Take`, `Skip` or id key; CQ-4 |
| Files over 1,000 lines | 18 | 21 (35 over 800) | CQ-10 |
| Files with more than one public type | 728 | 838 | CQ-10 |
| Interface async methods without `CancellationToken` | not measured | 197 of 2,122 | CQ-3 |
| Classes taking `IServiceProvider` | not measured | 144 (126 outside the existing test's scope) | CQ-7 |
| Constructors with more than 10 parameters | not measured | 144 (40 over 15, 10 over 20) | CQ-10 |
| Background tasks | 96 (45 a minute) | 101 (45 a minute, 23 every five) | CQ-5 |
| Modules or Core projects with no tests | 21 | 11 | CQ-12 |
| `ConfigurationSchema.json` | not measured | 1 module of 22 that bind configuration | CQ-9 |
| Public API baselines | not measured | 17 of 222 projects, none in commerce | CQ-8 |

Clean and staying clean (no work): `async void` 0, `.Wait()` 0, `throw ex;` 0, interpolated log calls 0, `#nullable disable` 0, `new HttpClient(` 1 (a sample host), singletons that capture a scoped service 0 of 160, `lock` around an `await` 0.

## CQ-1. Live-call state that lives in one node's memory (High, M)

Security-relevant: see also SEC finding "Secure keypad digits can reach the model and the transcript on a multi-node site" in `01-security-findings.md`.

**Evidence (read).** `C/Omnichannel.Voice.Core/Services/SecureKeypadCaptures.cs:23` keeps secure keypad captures (card, bank and national-id digits) in a `static readonly ConcurrentDictionary` keyed by activity id. `C/Omnichannel.Voice.Core/Tools/CollectSecureDigitsTool.cs:97` starts the capture on the node that runs the AI session. `VoiceAgentConversationLoop.Supervision.cs:169` calls `Offer` on the node that receives the provider's DTMF webhook. On a second node `Offer` answers `NotCapturing`, so lines 178-190 relay the digits as ordinary keys: they reach the model as a caller turn (`RealtimeVoiceConversationRunner.Supervision.cs:177`) and the transcript (`VoiceAgentConversationLoop.Supervision.cs:267`). `SecureKeypadCaptures.Forget` has no caller, and nothing evicts an abandoned capture, so partial digits stay in memory until the process restarts.

Other per-node call state (20 `static readonly Concurrent*` collections, 8 with live-call state; 25 tenant singletons with instance dictionaries): `VoiceAgentConversationLoop.Voicemail.cs:56,267` (two nodes can each leave a voicemail), `TurnBasedToolFillers.cs:17`, `TurnBasedCallerKeys.cs:53`, `AnsweringMachineSignals.cs`, `TurnBasedSilenceWatchdog.cs`, `M/Omnichannel.Managements/Services/ActivityBatchLoadRegistry.cs`, `C/Omnichannel.Core/Services/InMemoryAutomatedConversationGate.cs`. Leave these as they are: `M/Asterisk/Services/AsteriskPendingCallerTerminationRegistry.cs:22-23` (tenant-qualified keys) and `C/Telnyx.Core/Services/TelnyxRecordingIngestService.cs:44` (a durable job stands behind it).

**Change.**

1. Fail closed first (S, its own commit): while a secure capture window is open for an activity, never forward keys as a caller turn or into the transcript, on any node. Put the window (`SecureCaptureWindow { Field, UntilUtc }`) on the activity record, which every node reads, and check it in both key paths before `Offer`: the turn-based loop (`VoiceAgentConversationLoop.Supervision.cs:169`) and the realtime runner's `KeysPressed` path (`RealtimeVoiceConversationRunner.Supervision.cs:73-78,177`). The window alone stops the leak but leaves the capture unable to finish when the keys land on another node, so in the same commit relay the keys to the node that owns the call over the existing voice call message bus (a "secure keys" command, the way other call commands are relayed). Call `SecureKeypadCaptures.Forget` when the call ends (it has no caller today).
2. Move the captured digits to `ISecureCaptureStore` in `C/Omnichannel.Voice.Core`: `IDistributedCache`, values protected with data protection (`ProtectedSettingExtensions`' protector purpose pattern), a per-activity distributed lock, an expiry equal to `SecureKeypadCaptures.Expiry`, and removal when the capture completes, the call ends or the window expires. With the store in place the relay in step 1 is no longer needed.
3. Move the voicemail "leave one" claim and the turn-based state to the same kind of store, or to the activity record, so a webhook that lands on another node finds it. `InMemoryAutomatedConversationGate` is already superseded by the durable provider inbox for Twilio and Event Grid (audit defect 7): delete it if nothing else reads it, otherwise move it.

```csharp
// before: node B has no capture, so the digits become a caller turn
var entry = SecureKeypadCaptures.Offer(voiceEvent.ActivityId, digits, _clock.UtcNow);

// after: the capture window is on the activity, visible to every node
if (activity.TryGet<SecureCaptureWindow>(out var window) && window.UntilUtc > _clock.UtcNow)
{
    var entry = await _captureStore.OfferAsync(activity.ItemId, digits, cancellationToken);

    if (!entry.Captured)
    {
        return; // never forward keys while a window is open
    }
}
```

**Tests.** Unit: a DTMF event during an open window is never added to the model input or the transcript, including when the store has no entry. `tests/CrestApps.OrchardCore.ContactCenter.DistributedTests`: start the capture on node A, deliver digits on node B, assert the capture completes and neither node's transcript holds the digits. An expired window releases the digits from the store.

**Guard.** New `T/Architecture/ProcessWideStateRatchetTests.cs`: no new `static readonly Concurrent*` field outside `Telemetry/` and `Diagnostics/`, with today's 20 as an allowlist that only shrinks.

## CQ-2. Background work nobody tracks (Low to Medium, S to M)

**Evidence.** 14 `Task.Run` in 11 files, 9 discarded; 2 discarded async calls; 7 `_ = task.ContinueWith(...)`.

- `M/Omnichannel.Voice/Services/RedisAIVoiceCallMessageBus.cs:43` (read): `(_, value) => Task.Run(() => handler(...))` under an `Action` delegate. The bus itself observes nothing; today's only subscriber catches and logs its own faults (`AIVoiceCallCommandRelay.cs:164-179`), so nothing is lost now, but the next subscriber gets no such protection.
- `C/Omnichannel.Voice.Core/Services/AIVoiceSessionTracker.cs:141` and `M/Telnyx/Services/TelnyxAiVoiceRecordingHandler.cs:69` (read): they catch and log but are not registered with `AIVoiceCallShutdown.TrackHandOn`, so a deployment drops the call summary or the recording start.
- Same pattern, to confirm one by one: `RealtimeVoiceConversationRunner.Supervision.cs:60,68,78,86`, `.Guardrails.cs:125`, `.Handover.cs:60`, `TurnBasedCallerKeys.cs:58`, `C/Telnyx.Core/Services/TelnyxContactCenterVoiceMediaSession.cs:95`.

**Change.** Lift the tracker that already exists (`RealtimeCallCompletionRunner.cs:76`, `AIVoiceCallShutdown.TrackHandOn`) to a tenant-level `IBackgroundWorkTracker` in `A/Abstractions/BackgroundWork` (`Track(Task work, string name)`; drains on tenant shutdown; logs a fault once; reports through `CrestAppsOrchardCoreTelemetry`). Route every site above through it. Where the work must survive a restart (call summary, recording start), enqueue it on `ILeasedWorkQueue<T>` instead of running it in memory.

```csharp
_ = Task.Run(() => WriteInOwnScopeAsync(scope, draft));                        // before
_backgroundWork.Track(Task.Run(() => WriteInOwnScopeAsync(scope, draft)), "call-summary"); // after
```

**Guard.** Add `_ = Task.Run(` and `=> Task.Run(` to `DuplicatePatternRatchetTests`, and change that test to match on whole-file text so a call split over several lines is seen (it misses `ContactCenterShutdownDrain.cs:127-129` today).

## CQ-3. Cancellation stops at contracts that take no token (Medium, M, public API)

**Evidence.** Endpoints pass `RequestAborted` (1,521 uses in 263 files), but 197 of 2,122 interface async methods take no token; 206 YesSql terminal calls and 39 `SaveChangesAsync()` pass none. Worst contracts (first two read): `A/Checkout.Abstractions/Handlers/ICheckoutHandler.cs:13-74` (11 methods, 21 implementations), `C/Stripe.Core/IStripePaymentIntentService.cs:15-67` (8), `IStripeSubscriptionService` (5), `IStripeCustomerService` (4), `A/TenantHierarchy.Abstractions/Services/ITenantHierarchyBroker.cs` (17), `A/Wizard.Abstractions/Handlers/IWizardHandler.cs` (9), `A/ContentTransfer.Abstractions/IContentImportManager.cs` (4). Token-less query clusters: `M/Omnichannel.Managements/Drivers/AccountCrmTabsDisplayDriver.cs` (13), `ActivitiesController.cs` (8).

**Change.** Add `CancellationToken cancellationToken = default` as the last parameter, contract by contract, in this order: Stripe services, `ICheckoutHandler`, `IWizardHandler`, `ITenantHierarchyBroker`, `IContentImportManager`, then the rest. Pass it to stores and HTTP calls. Keep `CancellationToken.None` on a commit that follows money moving; `M/Stripe/Endpoints/CreateWebhookEndpoint.cs:179-189` shows the pattern. Update the public API baselines in the same commit.

Adding a parameter to an interface method breaks every implementer outside this repository, default value or not. Do the internal services (the Stripe services, stores, token-less query clusters) at any time. Change the extension contracts that other packages implement (`ICheckoutHandler`, `IWizardHandler`, `IContentImportManager`) together in one release that is announced as breaking, before 4.0 leaves preview, and list them in the changelog.

**Guard.** New ratchet in `T/Architecture`: "interface method named `*Async` without a `CancellationToken`", starting at 197 and only shrinking. Raise CA2016 to error once CQ-8 lands.

## CQ-4. Queries that load a table to count, sum or filter it (Medium, M)

**Evidence.** 244 YesSql `ListAsync` calls have no `Take`, `Skip` or id key; 85 of 639 display drivers run queries.

| Where | Problem |
| --- | --- |
| `M/Subscriptions/Drivers/SubscriptionSummaryPartDisplayDriver.cs:74-87` (read) | Four counts, then every succeeded `PaymentAttemptIndex` row loaded to sum in memory, on each render when the cache is 0 minutes |
| `M/Geo/Drivers/GeoSettingsDisplayDriver.cs:76-80` (read) | Every `GeocodeRequestIndex` row loaded to count three statuses |
| `M/Assets/Drivers/AssetDisplayDriver.cs:78` | Every asset loaded into a parent dropdown |
| `M/Workforce/Api/WorkforceApiEndpoints.cs:73,95` | `ITeamManager.GetAllAsync()` on each request |
| `M/Dispatch/Controllers/VisitsController.cs:143`, `M/WorkOrders/Controllers/WorkOrdersController.cs:154` | All index rows for a worker or asset, then an in-memory filter |
| `M/Workforce/Controllers/MyTeamController.cs:117-177`, `M/ContactCenter/Endpoints/SupervisorDashboardEndpoints.cs:469`, `M/Omnichannel.Managements/Drivers/OmnichannelActivityBatchDisplayDriver.cs:259` | A lookup per row while rendering |
| `LeadImportBackfill.cs:171`, `ReportGroupMigrations.cs:52,60`, `WorkforceTeamsMove.cs:68` | Migrations that load a whole table |

**Change.**

```csharp
var all = await _session.QueryIndex<GeocodeRequestIndex>(collection: c).ListAsync();        // before
model.Waiting = all.Count(i => i.Status == pending);

model.Waiting = await _session.QueryIndex<GeocodeRequestIndex>(i => i.Status == pending, c) // after
    .CountAsync(cancellationToken);
```

1. Counts become `CountAsync` per status.
2. Sums: add `SumAsync` and grouped-count helpers to the `DocumentCatalog` base in `C/YesSql.Core` (SQL through `IDbConnectionAccessor`, dialect-safe, with the table prefix and collection applied), and use them from the stores. Do not hand-write SQL in a driver.
3. Dropdowns of unbounded records become the picker for that record (asset picker, user picker).
4. Per-row lookups: add `GetManyAsync(ids)` to `IDisplayNameProvider` and the catalog managers and call it once before the loop.
5. Migrations page through the table in batches with `BackgroundWorkPacer`.

**Guard.** New `T/Architecture/UnboundedQueryRatchetTests.cs`: no `ListAsync(`/`GetAllAsync(` without `Take` in `*Driver.cs`, `*Controller.cs` and `*Endpoints.cs`, with today's hits as a shrink-only allowlist. Extend `UserListLoadArchitectureTests` to the asset and team lists.

## CQ-5. Background task load (Medium, S)

**Evidence.** 101 `[BackgroundTask]`: 45 every minute, 23 every five minutes. With every feature on, that is 45 shell scopes per tenant per minute.

- Five index backfills poll every minute for ever after they finish; each opens a scope and reads a state document (`M/Omnichannel.Managements/BackgroundTasks/ContactPhoneNumberIndexBackfillBackgroundTask.cs:58-66`, read; also `DialerDecisionIndexBackfill`, `QueueItemDialerProfileIndexBackfill`, `ActivityDialerProfileIndexBackfill`, `LeadImportBackfill`).
- Three tasks set no `LockTimeout`/`LockExpiration`: two in `M/Dispatch/Integrations/DispatchIntegrations.cs`, one in `M/Dispatch/Mobile/FieldIntegrations.cs`.
- 17 Contact Center sweeps run each minute.
- `M/ContentTransfer/BackgroundTasks/ImportFilesBackgroundTask.cs` (1,638 lines) has a 30-second lock expiry for work that runs longer.

**Change.** One `IndexBackfillRunner` base in `C/Core` that disables its task through `BackgroundTaskSettings` when the state document says done (and re-enables on a new migration). Add both lock values to the three Dispatch tasks. Merge Contact Center sweeps that share a lock family into one task that runs them in sequence. Size the import task's lock to its batch and renew it while it works.

**Guard.** New `T/Architecture/BackgroundTaskConventionTests.cs`: every `[BackgroundTask]` sets `LockTimeout` and `LockExpiration`; the count of one-minute schedules only shrinks.

## CQ-6. Error handling on money and consistency paths (Medium, M)

**Evidence.** 780 broad catches: 102 rethrow, 581 log or report and continue, 97 do neither. Only 210 have a `when` filter, so the rest also swallow `OperationCanceledException`. The roughly 20 catches read in `DefaultCheckoutEngine` and `StripeCheckoutPaymentProvider` are deliberate and turn into result objects. The real gaps:

| Where | Problem |
| --- | --- |
| `C/Checkout.Core/Services/DefaultCheckoutEngine.cs:1485-1488` (read) | A provider attempt that cannot be cancelled is only logged. No `PaymentIssue` is recorded, so the buyer can still pay a cancelled checkout and nobody is told |
| `DefaultCheckoutEngine.cs:90,1424` (read) | `IPaymentIssueRecorder paymentIssues = null`: a host without the Transactions registration silently goes back to log-only, which audit defect 22 fixed |
| `M/Stripe/Services/StripeCheckoutPaymentProvider.cs:210,297,461,581,746`, `StripeRecurringPaymentProvider.cs` (4 sites); 45 sites in 31 files in all | Raw exception text returned to the caller |
| `M/AI/Controllers/AIVoiceLibraryController.cs:91,142,174` and 4 more | A localized message that embeds `ex.Message` |
| `DefaultCheckoutEngine.cs:873` | An aborted request is logged as a provider error |
| `M/Asterisk/Services/AsteriskPjsipCredentialIssuer.cs` (6), `TelnyxContactCenterVoiceMediaSession.cs` (4), `M/AI.FileSources/Services/TenantFileSourceRoot.cs` (4), `M/AI/Migrations/AIChatSessionV1PromptDataMigrations.cs` (3) | Catches that neither log nor rethrow (to confirm) |

**Change.**

```csharp
public DefaultCheckoutEngine(..., IPaymentIssueRecorder paymentIssues = null)   // before
if (_paymentIssues is not null) { await _paymentIssues.RecordAsync(issue); }

services.TryAddScoped<IPaymentIssueRecorder, LoggingPaymentIssueRecorder>();     // after, registered by Checkout
await _paymentIssues.RecordAsync(issue);
```

1. Make the recorder a required dependency with a logging null object (`LoggingPaymentIssueRecorder` in `C/Checkout.Core`, registered with `TryAdd` so Transactions replaces it).
2. Record a `PaymentIssue` ("Payment could not be cancelled at the provider") at `DefaultCheckoutEngine.cs:1485`.
3. Add `when (ex is not OperationCanceledException)` to broad catches on request paths in Checkout, Orders, Transactions, Subscriptions and Stripe.
4. Map gateway exceptions to a safe, localized buyer message plus a logged reference id (one helper in `C/Checkout.Core`: `PaymentFailureMessages.For(exception)`); never return `ex.Message` to a browser or an API client.

**Tests.** Cancel fails at the provider: an issue is recorded; a host without Transactions still resolves the engine; an aborted request logs nothing at error level; a Stripe exception's text does not appear in the HTTP response.

**Guard.** Extend `EmptyCatchRatchetTests` to "broad catch without a log, a rethrow or a returned failure" with an allowlist; ratchet `Failure(ex.Message)` and `ex.Message]` in `S[...]`/`T[...]` arguments.

## CQ-7. Service locator and optional dependencies (Medium, M to L, incremental)

**Evidence.** 144 classes take `IServiceProvider` in the constructor (background tasks, startups, hubs and migrations excluded); `DependencyInjectionArchitectureTests` scans only Contact Center, Telephony and Omnichannel paths, so 126 are outside it. 1,910 `GetService`/`GetRequiredService` calls in 600 files; 66 optional `= null` service parameters in 51 constructors; 64 `_x = injected.FirstOrDefault()` provider picks (the test already calls this an anti-pattern for 5 contracts).

Worst: `M/Workforce/Api/WorkforceApiEndpoints.cs` (36 lookups; 21 handlers take `IServiceProvider services`, lines 73-273 read), `M/Dispatch/Api/FieldServiceApiEndpoints.cs` (22 lookups), `M/Omnichannel.Sms/Handlers/SmsOmnichannelEventHandler.cs` (16 lookups on top of 28 constructor dependencies, lines 742-910 read), `C/Omnichannel.Voice.Core/Services/VoiceAgentConversationLoop.Conclusion.cs` (26), `C/TenantHierarchy.Core/Services/TenantHierarchyBroker.cs` (21), money services (`C/Checkout.Core/Services/CouponRedemptionService.cs:55,501`, `DefaultCheckoutRefundService`, `C/Transactions.Core/Services/TransactionManager.cs`, `TransactionSettlementService.cs`, 13 classes in Orders), `C/Telnyx.Core/Services/TelnyxOutboundBridgeOrchestrator.cs` (8 optional parameters), `M/Workforce/Drivers/WorkerProfileDisplayDriver.cs:129-132` (`RequestServices.GetService` in a driver).

Why it matters here: a feature that is off fails at run time in one tenant, not when the feature is enabled, and an optional dependency that is missing turns a behavior off without telling anyone (the audit's `CampaignService` e-mail case).

**Change.** Minimal-API handlers take their services as parameters. Optional contracts get a null-object default registered with `TryAdd` by the owner. A provider chosen with `FirstOrDefault()` is chosen by name through `ProviderLookup` (DRY-4 in `03-reuse-and-building-blocks.md`). Authorization handlers keep lazy resolution (the `AGENTS.md` rule). Do the money services first, then the two API endpoint files, then the rest as files are touched.

**Guard.** Widen `DependencyInjectionArchitectureTests.IsInScope` to all of `src/` with today's 126 classes as a shrink-only list; add ratchets for `= null` service parameters and `.FirstOrDefault()` assignments of injected collections.

## CQ-8. Build and analysis settings (High leverage, S)

**Evidence (read `Directory.Build.props`, `.editorconfig`, `Directory.Packages.props`, `global.json`).**

- `TreatWarningsAsErrors` is in no props file; CI passes `-warnaserror /p:TreatWarningsAsErrors=true /p:RunAnalyzers=true` (`.github/workflows/build_and_test.yml:56`). `AGENTS.md` says it is enabled, but a local build does not fail on a warning. Because CI already builds this way, turning it on locally adds no new failures.
- The SDK analyzers are on (`AnalysisLevel` is `latest-Recommended`, `Directory.Build.props:91`), but there is no extra analyzer package among 191 `PackageVersion` entries and no `EnforceCodeStyleInBuild`; `.editorconfig` has three IDE rules at warning (IDE0056, IDE0057, IDE0060), which a build does not enforce today.
- 16 CA rules are suppressed, among them CA1001 (12 `CancellationTokenSource` fields never disposed), CA1304/1305/1310/1311 (24 culture-less `Parse` calls, for example `C/ContentTransfer.Core/Handlers/Fields/NumberFieldImportHandler.cs:22` and `M/Calendars/Api/CalendarApiEndpoints.cs:72`), CA1848 and CA2201.
- `<Nullable>` is on in one project; `global.json` has `rollForward: latestMajor`.
- Pre-release pins: OrchardCore `4.0.0-preview-19219`, CrestApps.Core `2.0.0-preview.367`, Fluid.Core `3.0.0-beta.9`, Lucene `4.8.0-beta00018`, A2A `1.0.0-preview4`.
- Good and staying: NuGet audit runs on all packages at `low` and fails the build.
- Public API baselines cover 17 of 222 projects (Contact Center, Omnichannel, Telephony, Accounts); no commerce contract has one.
- `FileSizeRatchetTests` scans 11 projects; the text ratchets are line-based.

**Change.**

```xml
<!-- Directory.Build.props -->
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>

<!-- src projects only: the tests use DateTime.UtcNow about 170 times, deliberately -->
<ItemGroup Condition="'$(IsTestProject)' != 'true'">
  <PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" PrivateAssets="all" />
  <AdditionalFiles Include="$(MSBuildThisFileDirectory)BannedSymbols.txt" />
</ItemGroup>
```

```xml
<!-- Directory.Packages.props (central package management needs the version here) -->
<PackageVersion Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" Version="..." />
```

```text
# BannedSymbols.txt
P:System.DateTime.UtcNow;Inject IClock
P:System.DateTime.Now;Inject IClock
P:System.DateTime.Today;Inject IClock and ILocalClock
M:System.DateTime.ToLocalTime;Use ILocalClock
M:System.TimeZoneInfo.ConvertTimeToUtc(System.DateTime,System.TimeZoneInfo);Use CalendarTime
M:System.Net.Http.HttpClient.#ctor;Use IHttpClientFactory
```

`System.Random` is not banned: its eight uses in `src` are retry jitter and seeded solvers.

1. Add `TreatWarningsAsErrors` and the banned-API analyzer for `src` projects. RS0030 replaces three text patterns in `DuplicatePatternRatchetTests` and sees multi-line code. Use `#pragma warning disable RS0030` with a reason at the six allowlisted `ConvertTimeToUtc` sites. `DateTime.UtcNow`, `Now` and `Today` occur zero times in `src` today, so this step should build clean.
2. `EnforceCodeStyleInBuild` is the step with first-build risk: it starts enforcing IDE0056, IDE0057 and IDE0060 at warning across about 10,000 files. Measure once with a local build before committing. If the count is small, fix the hits and turn it on; if it is large, set those three rules to suggestion in `.editorconfig`, turn the property on, and raise them one at a time.
3. Raise CA2016, CA1001 and CA1305 to warning for `src/Core` and `src/Abstractions` (`.editorconfig` section per folder), fix the hits (12 fields, 24 parses), then widen to `src/Modules`.
4. Set `rollForward` to `latestFeature`.
5. Add public API baselines for `Checkout.Abstractions`, `Payments.Abstractions`, `Transactions.Abstractions`, `Orders.Core` and `Inventory.Core` before workstreams RL and CQ-3 change them.
6. Correct `AGENTS.md` where it says warnings are errors locally (true after step 1).

Do this early: later guards build on it. It does not block the small security fixes in phase 1a, which ship first.

## CQ-9. Options without a configuration schema (Low, S each)

**Evidence.** `ConfigurationSchema.json` exists in one module (`M/Dispatch`). 21 modules bind configuration sections without one: Contact Center, Telephony, Asterisk, Stripe, Reports, DataConnectors, ContentTransfer, Omnichannel.Messaging, AI.Mcp, AI.A2A and others. `building-blocks.md` section 4 requires an entry for every options class.

**Change.** Add the file per module, generated from the options classes' properties and XML documentation. **Guard:** new `T/Architecture/ConfigurationSchemaCoverageTests.cs`: every `Configure<T>(configuration.GetSection(...))` or `BindConfiguration` in a module has a matching schema entry.

## CQ-10. Type design (Low to Medium, L, incremental)

Do these only when a workstream already touches the file, except the guards, which go in now.

**God files (top 25, with the split to make):**

| # | File | Lines (2026-11-03, then now) | Split |
| --- | --- | --- | --- |
| 1 | `M/Omnichannel.Managements/Controllers/ActivitiesController.cs` | 2,093, now 2,223 | List and query, complete and disposition, bulk, and export controllers, with an `ActivityWorkflowService` |
| 2 | `M/Recipes/Startup.cs` (138 types) | 2,045, now 1,895 | One schema startup file per owning module |
| 3 | `M/ContentTransfer/BackgroundTasks/ImportFilesBackgroundTask.cs` | 1,311, now 1,638 | A thin task with `ImportRunner`, `ImportBatchProcessor`, `ImportProgressWriter` |
| 4 | `C/AgenticCommerce.Core/Services/AgentCheckoutService.cs` | new, 1,595 | Session life cycle, pricing and tenders, completion |
| 5 | `M/ContentTransfer/Controllers/AdminController.cs` | 1,402, now 1,579 | Import, export and download controllers (`DownloadExport` alone is 297 lines) |
| 6 | `C/Subscriptions.Core/Services/DefaultInstallmentPlanService.cs` | 1,540, now 1,572 | Plan factory, payment application, dunning |
| 7 | `C/Checkout.Core/Services/DefaultCheckoutEngine.cs` | 1,280, now 1,532 | Begin-payment, completion and compensation collaborators behind the same facade |
| 8 | `M/Products/Services/ProductListingService.cs` | 1,510 | Query builder, facets, sorting |
| 9 | `M/Omnichannel.Sms/Handlers/SmsOmnichannelEventHandler.cs` | 1,415, now 1,479 | `HandleAsync` is 856 lines: inbound router, AI reply turn, conclusion service |
| 10 | `M/Omnichannel.Managements/Services/DefaultContactActivityBatchLoader.cs` | 1,296, now 1,368 | `LoadAsync` is 814 lines: candidate query, screening, writer |
| 11 | `C/AI.Core/Services/DataSourceIndexingService.cs` | 1,188, now 1,359 | Sync planner, document writer, lock and lease |
| 12 | `M/Asterisk/Services/AsteriskTelephonyProviderBase.cs` | 1,349 | Call control, transfer, recording |
| 13 | `M/DncRegistry/Services/DefaultLocalDncListManager.cs` | 1,116, now 1,260 | List CRUD, import, lookup |
| 14 | `M/Telephony/Hubs/TelephonyHub.cs` (2,102 across 10 partial files) | 1,238 | Hub methods delegate to command services |
| 15 | `M/ContactCenter/Reports/Providers/EnterpriseInteractionReportProvider.cs` | 1,220, now 1,149 | One provider per report section |
| 16 | `C/TenantHierarchy.Core/Services/TenantHierarchyBroker.cs` | 1,102, now 1,127 | Access grants, delegated sessions, provisioning |
| 17 | `M/Workforce/Controllers/MyWorkController.cs` | 1,119 | Portal pages, punch API, push, the service-worker script as a resource |
| 18 | `C/Reports.Core/Designer/ReportQueryEngine.cs` (2,013 across 3) | 1,051 | Planner, executor, materializer |
| 19 | `M/Stripe/Services/StripeCheckoutPaymentProvider.cs` | 1,039 | One-off, saved-card, delegated-credential and subscription strategies |
| 20 | `C/Reports.Core/Designer/ReportDesignDocumentBuilder.cs` | 1,037 | Per-section builders |
| 21 | `C/Calendars.Core/Services/DefaultCalendarEventManager.cs` | 1,036 | Recurrence, conflicts, attendees |
| 22 | `M/Workforce/Controllers/SchedulesController.cs` | 978 | Plan, publish and copy controllers |
| 23 | `M/Reports/Designer/Services/ReportDesignRunner.cs` | 965 | Run pipeline stages |
| 24 | `M/Asterisk/Services/AsteriskAriClient.cs` | 931 | Channels, bridges and recordings clients |
| 25 | `M/Omnichannel.Managements/Services/LeadConversionService.cs` | 907 | Matcher, copier, audit |

Partial classes hide more: `RealtimeVoiceConversationRunner` is 4,886 lines in 16 files and `VoiceAgentConversationLoop` 4,862 in 18. Splitting these needs live-call testing; plan them with the Telnyx split in the reusability audit (phase 5, item 5), not here.

**Constructors with the most dependencies:** `VoiceAgentConversationLoop` 34; `SmsOmnichannelEventHandler` 28; `InboundVoiceCallProcessor` 27; ContentTransfer `AdminController` 26; `PredictiveAgentConnector` 22; `DefaultSchedulePlanningService`, `PredictiveOverDialPacer`, `ContactCenterSupervisorInterventionService` 21; `MessagingWorkspaceBuilder`, `DialerAttemptService`, `AgentCheckoutService` 20; `ProductListingService`, `ActivitiesController`, `AgentAvailabilityReportProvider`, `DefaultTimeOffRequestService`, `DefaultInstallmentPlanService`, `ActivityReservationService` 19; `MyWorkController`, `CallRecordingsController`, `DefaultTimesheetService` 18.

**Other measurements:** 97 methods over 150 lines (20 over 250, 4 over 400); 838 files with more than one public type (242 are `ViewModels` and `Startup` files); 28 unsealed public non-view-model classes, 13 never a base; positional record DTOs with 34 (`ApiCoupon`, `M/Checkout/Api/CouponAdminApiEndpoints.cs:331`) and 32 parameters (`ApiOrder`, `M/Orders/Api/OrderApiModels.cs:40`): change these two to init properties.

**Guards (S, do now):**

- Run `FileSizeRatchetTests` over all of `src/` with the 35 files over 800 lines recorded. `TelnyxOutboundBridgeOrchestrator.cs` and `ContactCenterReportingService.cs` sit exactly at the limit today.
- New `ConstructorDependencyRatchetTests` (over 12 parameters, shrink-only) and `MethodLengthRatchetTests` (over 150 lines, shrink-only).
- New `OneTypePerFileRatchetTests` at 838, shrink-only.

## CQ-11. Conventions in endpoints, routes and dates (Low to Medium, S to M)

| Item | Evidence | Change | Size |
| --- | --- | --- | --- |
| One API error contract | Problem-details results in about 20 files (41 to 76 uses, by two counts); `Results.BadRequest` in about 31 files, 19 with an unlocalized literal (`M/AI/Endpoints/GetDeploymentsEndpoint.cs`, `M/AI.WebMcp/Endpoints/*`); `Results.Json(new { error })` 3. `C/Commerce.Api.Core/ApiProblems.cs` is the only shared helper | DRY-8: move `ApiProblems` to `C/Core/Api` (namespace `CrestApps.OrchardCore.Api`), keep a type-forward for commerce, and use it in every minimal API; localize the literals | M |
| `Task.WhenAll` over scoped services | `M/Omnichannel.Managements/Endpoints/SubjectActionEndpoints.cs:62-119` (read) runs lambdas that share `IContentDefinitionManager` and `ILocalClock` | A plain loop | S |
| UTC shown to shoppers | `M/Carts/Drivers/AddToCartDisplayDriver.cs:296`, `GroupedProductsDisplayDriver.cs:166` format `ReleaseDateUtc` directly; `M/TenantHierarchy/Services/ChildTenantManager.cs:424` writes a `"u"` string into audit text | `ILocalClock` (see workstream UX) | S |
| Unlocalized strings | `M/Checkout/Api/CouponAdminApiEndpoints.cs:255,260`, `M/ContactCenter/Hubs/ContactCenterHub.Supervision.cs:101` | `IStringLocalizer` | S |
| Thick handlers | 25 controllers over 500 lines; 19 endpoint handlers over 60 lines, 4 over 120 (`M/Telnyx/Endpoints/TelnyxWebhookEndpoint.cs:69` 244, `M/ContactCenter/Endpoints/SupervisorDashboardEndpoints.cs:57` 215, `M/AI/Endpoints/Api/ApiAICompletionEndpoint.cs:74` 212, `M/Stripe/Endpoints/CreateWebhookEndpoint.cs:343` 364) | Move the logic to a service the endpoint calls; start with the Stripe and Telnyx webhooks, which workstream SEC also touches | M |
| Route shapes | The AI module uses `api/ai/...`, `ai/connections`, `ai/deployments` and `ai/api/voices`; Contact Center mixes `Admin/api/...` and `api/contact-center` | Record the rule in `AGENTS.md` for new routes (`api/crestapps/{area}/...`, admin APIs under the admin prefix). Do not rename existing routes: it breaks clients for no defect fixed | S |

## CQ-12. Tests (Medium, M)

**Evidence.** 1,968 test files, 11,760 test methods. 11 projects have no type referenced from tests: Wizard (2,563 lines), Teams, AI.Chat.Claude, Commerce.StoreProfile, AzureAIInference, AI.Documents.Elasticsearch, AI.Memory.Elasticsearch, AI.FileSources.Ftp and .Sftp, the Outreach module and the Recipes module. Thin: Skills (2 of 38 types), AI.Memory, TimeTracker, the Workforce module (27 of 255 types, 22.8k lines).

Money paths rest on mocks: 6,257 mock creations in 842 files against 4 commerce integration test classes. Three commerce test files exercise concurrency (`T/Checkout/DefaultCheckoutEngineTests.cs`, `T/Inventory/InventoryStockTests.cs`, `AgenticCheckoutTests.cs`). None cover coupon redemption caps, gift-card, store-credit or loyalty balances, refunds, installment payments, or a duplicate Stripe delivery when the inbox is off. The code itself looks right (about 40 stores set `CheckConcurrency => true`, with distributed locks on top); the tests that would catch a regression are missing.

Flaky patterns: `Task.Delay` 137 times in 52 files (`T/Modules/Omnichannel/Voice/RealtimeVoiceConversationRunnerTests.cs` has 39), `Thread.Sleep` 3, about 170 `DateTime.UtcNow`, 10 `Skip=`. One test project references 177 projects, so any test run builds the solution.

**Change.**

1. Two-session concurrency tests on SQLite, one per balance and cap, modeled on `T/Modules/ContactCenter/ActivityReservationSharedDatabaseTests.cs`: two redemptions of a coupon with one use left; two spends of one gift card, store-credit account and loyalty balance; two refunds of one payment; two installment payments of one instalment; the same Stripe event delivered twice with the inbox off. Each asserts exactly one winner and a clean failure for the other.
2. Tests for Wizard and Teams (the two building blocks other modules build on) before anything else in the untested list.
3. `FakeTimeProvider` in the voice runner tests in place of `Task.Delay`.
4. Split `tests/CrestApps.OrchardCore.Commerce.Tests` out of the single unit-test project so a commerce change builds and runs only commerce tests; then Contact Center.

**Guard.** New `T/Architecture/TestConventionTests.cs`: test classes sealed; `Task.Delay` count shrink-only.

## Not worth doing

Checked and left alone, so the implementer does not spend time on them:

- Stock, balance and coupon concurrency in the code (only the tests are missing, CQ-12).
- Singleton and scoped lifetimes, `IOptions` use and cache tenant scoping: no captive dependency; `IMemoryCache` and `IDistributedCache` are per tenant in Orchard Core.
- `ConfigureAwait` (none, consistently), `lock` (73, none around an `await`), `SemaphoreSlim` (sampled, all in `try/finally`).
- Index providers doing I/O: none.
- `TryUpdateModelAsync` onto an entity: one site (`M/Subscriptions/Drivers/SubscriptionPartSettingsDisplayDriver.cs:40`); fix it when the file is touched.
- Converting 2,186 log calls to `[LoggerMessage]`; `SanitizeLogValue` and `Redactor` are already used in 325 files.
- Turning nullable on solution-wide, making 5,007 public module types internal, replacing 244 `decimal` amounts with `Money`, removing 305 boolean parameters: high churn for few defects. Apply to new code only.
- Renaming existing routes.
