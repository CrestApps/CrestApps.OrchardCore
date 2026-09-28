# 00 - Phase 0: preparation inside the Orchard code base (seams, defaults, tests)

Phase 0 lands every missing abstraction, every host seam, and every behaviour-preserving refactor **inside the existing Orchard projects**, with the Orchard implementations bound and all tests green, before a single file moves. When Phase 0 is done, Phase 1 is a relocation (`git mv`, namespace rewrite, project references) with no logic change, and the tests written or verified here guard the moves.

Principles:

1. **No new top-level projects in Phase 0.** New contracts go into the existing `CrestApps.OrchardCore.*.Abstractions` projects (a new `CrestApps.OrchardCore.Omnichannel.Abstractions` is created because Omnichannel has none today); new defaults go into the existing `*.Core` projects; Orchard bindings go into the modules. Namespaces stay `CrestApps.OrchardCore.*`; Phase 1 renames them mechanically.
2. **Behaviour identical.** Feature ids, dependencies, permissions, UI, stored data, background schedules, hub paths, endpoint routes, recipes, deployment steps: unchanged. FeatureActivationTests must pass without assertion changes.
3. **Every refactor is test-first.** Before a class is touched, its behaviour is pinned by a test (existing or newly written characterization test). The Phase 0 coverage audit (P0.1) is the entry ticket for every other workstream.
4. **Framework shape from day one.** The contracts are written exactly as they will exist in `CrestApps.Core` ([03](03-host-seams.md), [04](04-registration-api.md)): same names, same members, XML docs, sealed classes, `TimeProvider`, `TryAdd` defaults, `AddCore*`-style registration methods (temporarily named `AddContactCenter*`-style inside Orchard is acceptable only if renamed in Phase 1; prefer the final `AddCore*` names now).
5. **One workstream per pull request**, each ending with the gate in section 3.

## 1. Workstreams

### P0.1 - Test baseline and coverage audit (its own milestone; nothing else in Phase 0 starts until it is signed off)

P0.1 is delivered and reviewed on its own before P0.2 begins. Its acceptance is quantitative: every moving class has a recorded covering test in the audit table; line coverage of each moving assembly (`ContactCenter.Core`, `ContactCenter` framework folders, `Telephony.Core`/`Telephony`, `Telnyx.Core`, `Asterisk`, `Dialpad`, `Omnichannel.Core`, `Omnichannel.Sms.Portal.Core`, `Omnichannel.Voice.Core`, `Omnichannel.Sms`, `PhoneNumbers.Core`, `WebSockets`) is at or above 80 percent with no public service class below 60 percent, and every public member that a framework consumer can call has at least one assertion. Classes that cannot reach the bar are listed with a reason and a mitigation (integration harness, contract test, or explicit acceptance) signed off by Mike. The numbers are recorded in `phase-0-baseline.md` and become the floor that the gate in section 3 enforces for the rest of Phases 0 and 1.

1. Make every test project green on `main` before anything else: `tests/CrestApps.OrchardCore.Tests` (including the `PublicApi` approval tests, whose baselines are known to need regeneration), `ContactCenter.FeatureActivationTests`, `ContactCenter.DistributedTests` (Redis + PostgreSQL provisioned), `Telephony.PlaywrightTests`. Record the exact commands and the pass counts in `docs/contact-center-framework-extraction/phase-0-baseline.md`.
2. Run coverage (`coverlet.collector` + `dotnet test --collect:"XPlat Code Coverage"`, or `dotnet-coverage`) for the moving assemblies and produce a per-class table: class, covering tests, line coverage. Start from [appendix D](appendix-d-test-coverage-gaps.md) (356 of 570 moving classes have no same-named test file) and resolve each row into one of: covered by `<test>`, needs a characterization test, or trivially non-testable (DTO-like, marker).
3. Write the missing characterization tests **against current behaviour** (no refactor yet): stores over SQLite (existing `*SharedDatabaseTests` and `*PersistenceTests` are the model), services with fakes (`Telephony/Doubles`), background task passes, hub methods through the hub class with a fake `IHubCallerClients`, endpoints through `WebApplicationFactory` or direct handler invocation as the existing `*EndpointTests` do. Priority order by risk: routing/reservation/work-state, call sessions and provider commands, dialer, recording governance, SMS portal routing/dispatch, Telnyx webhook/credential paths, Asterisk ARI/PJSIP, Omnichannel activity store SQL, automation helpers.
4. Add an architecture test that fails when a public class in the moving set has no covering test recorded in the audit table (the table is a checked-in JSON/CSV; the test cross-references it), so coverage cannot regress silently during Phases 0-1.
5. Add the "upgrade from `main`" test scaffold now: a SQLite database snapshot built by `main` (queues, agents, entry points, dialer profiles, interactions, SMS conversations, Telnyx credentials, contacts/subjects as content items) checked into `tests/.../Migrations/Snapshots`, loaded by a test that runs the current migrations and asserts documents load. Phase 1 adds the type-name rewrite assertions to the same test.

Definition of done: all suites green, coverage table checked in, architecture test in place, snapshot test in place, `AGENTS.md` test-running section updated.

### P0.2 - Clock (S1)

Replace `IClock` with `TimeProvider` in every moving class (Contact Center core and module services, SMS portal core, Telnyx core, Telephony core, Omnichannel voice, background tasks). `TimeProvider` registration moves to `CrestApps.OrchardCore.Core` (`TryAddSingleton<TimeProvider, ClockTimeProviderAdapter>()`), called from each pillar's base startup. Tests switch from `StubClock` to `FakeTimeProvider`. Mechanical, but do it first: it touches ~100 files and every later diff gets smaller.

### P0.3 - Host seams (S2, S3, S4, S9, S21, S23)

Add to `CrestApps.OrchardCore.Abstractions` (namespace `CrestApps.OrchardCore.Hosting` for now):

- `IDistributedLockProvider` + `ILocker`; implementation `OrchardCoreDistributedLockProvider` in `CrestApps.OrchardCore.Core`; every moving class that injects `IDistributedLock` switches to the provider. Test double `FakeDistributedLockProvider` replaces `FakeDistributedLock`.
- `ITenantAccessor`; implementation `ShellSettingsTenantAccessor`; hubs, Telnyx client state, work manager, process-health switch to it.
- `IScopedWorkExecutor` + `IAfterCommitTaskQueue`; `IContactCenterScopeExecutor` keeps its interface, `ContactCenterScopeExecutor` becomes the Orchard implementation of both; `DefaultContactCenterScopeExecutor` (service-provider scopes + after-commit queue) is written now and unit-tested but not registered in Orchard.
- `IUserDirectory` + `UserSummary`, derived from the inventory of `UserManager<IUser>`/`IUser` usages (31 + 10); `OrchardCoreUserDirectory`; `ITelephonyUserAccessor`/`DefaultTelephonyUserAccessor` retired in favour of `IUserAccessor` + `IUserDirectory`.
- `IAgentSignOutHandler` in Contact Center core; `ContactCenterAgentSignOutCookieConfiguration` calls it.
- Client configuration models and providers (`SoftPhoneClientConfiguration`, `ContactCenterClientConfiguration`, `SmsPortalClientConfiguration`); resource configurations and views serialize them; scripts read the JSON (Orchard-compatible shim kept until W10).
- `TenantSignalRGroupName`/`HubConnectionWork` stay where they are (moved in Phase 1).

### P0.4 - Contacts, subjects, subject flows (S11, D-4, D-11)

The former "W4.0 spike" becomes real work here:

1. Create `src/Abstractions/CrestApps.OrchardCore.Omnichannel.Abstractions` with the CRM model types and contracts from S11 (`ContactDefinition`, `OmnichannelContact`, `ContactPhoneNumber`, `ContactEmail`, `SubjectDefinition`, `SubjectFieldDefinition`, `OmnichannelSubject`, `IContactDefinitionProvider`, `IOmnichannelContactResolver`, `IOmnichannelContactSearch`, `IOmnichannelContactWriter`, `ISubjectDefinitionProvider`, `IOmnichannelSubjectAccessor`, `IContactTimeZoneResolver`), and move the already content-free Omnichannel contracts and models there from `Omnichannel.Core` (this is the one intra-Orchard move allowed in Phase 0, because Omnichannel had no abstractions project).
2. Re-base `ISubjectFlowSettingsService` on `SubjectDefinition`; `SubjectFlowSettingsService` becomes `ContentTypeSubjectFlowSettingsService` (Orchard) with the same behaviour and tests.
3. Implement the Orchard bindings in `CrestApps.OrchardCore.Omnichannel.Core`: `ContentTypeContactDefinitionProvider`, `ContentItemOmnichannelContactResolver`, `ContentItemOmnichannelContactSearch`, `ContentItemOmnichannelContactWriter`, `ContentTypeSubjectDefinitionProvider`, `ContentItemOmnichannelSubjectAccessor`, `TimeZoneMapContactTimeZoneResolver`; register through `AddOrchardCoreOmnichannelContacts()`/`...Subjects()` from `OmnichannelActivitiesStartup`.
4. Refactor consumers onto the contracts: `OmnichannelSubjectWriter`, `OmnichannelAutomationHelper`, `SmsConversationService`, `SmsInboundProcessor`, `AutoReplyRouter`, `VoiceAgentConversationLoop(.Conclusion)`, `InboundContactLookup`, `DefaultSubjectActionExecutor`, `DefaultActivityDispositionService`, `SubjectActionExecutionContext`, `OmnichannelActivityContainer` (Orchard drivers get `OmnichannelActivityContentContainer` with the `ContentItem`s), `DefaultContactActivityBatchLoader` (keeps its content query behind `IOmnichannelContactSearch`).
5. Write the framework default model **now** in `Omnichannel.Core` (stores, managers, handlers, catalog-backed providers) so it is unit-tested against SQLite in Phase 0, but do not register it in any Orchard startup.
6. Tests: every consumer's existing tests pass against the in-memory fake with unchanged assertions; new tests for the Orchard bindings (content item round-trips) and for the default model.

Definition of done: no moving class references `ContentItem`, `ContentPart`, `ContentTypeDefinition`, `IContentManager`, or `IContentDefinitionManager` except the Orchard bindings and the content parts themselves; `grep` gate recorded in the PR.

### P0.5 - SMS provider abstraction (S12, D-6, D-16)

- Add `ISmsProvider`, `ISmsProviderResolver`, `SmsMessage`, `SmsResult`, `ISmsInboundMessageParser` to `CrestApps.OrchardCore.Abstractions` (namespace `CrestApps.OrchardCore.Sms`, moved in Phase 1 to `CrestApps.Core.Sms`).
- `OrchardCoreSmsProviderResolver` adapter in `CrestApps.OrchardCore.Core`; `ISmsDispatcher`/`ISmsDispatchProvider`/`SmsDispatcher`/`SmsOmnichannelProcessor` re-based on the framework types; `TelnyxSmsProvider` implements the framework interface and gets an `OrchardCore.Sms.ISmsProvider` wrapper registered where it is today.
- Twilio: `TwilioRequestValidator`, `TwilioInboundMessageParser`, and a `TwilioSmsProvider` (REST sender) written in `Omnichannel.Sms` now (Phase 1 moves them to `CrestApps.Core.Sms.Twilio`); the Orchard endpoint uses the parser; sending still goes through `OrchardCore.Sms.Twilio` in Orchard.

### P0.6 - Settings to options (S5, S13)

For every `ISiteService` read in a moving class, introduce the options POCO (or reuse the existing settings class as the options type), add the `IConfigureOptions<T>` + change-token bridge in the module, and switch the service to `IOptionsMonitor<T>`. `IShellConfiguration` reads move to `IConfiguration` sections passed by the startup. Tests use `TestOptionsMonitor<T>`.

### P0.7 - Authorization operations (S8)

Add `ContactCenterOperations`, `TelephonyOperations`, `SmsPortalOperations`; refactor `SupervisorQueueAuthorizationService`, `TransferDestinationResolver`, `CallControlAuthorizationService`, `AgentRecordingControlService`, `RecordingAccessGovernanceService`, `SmsConversationAuthorizationService` and the hubs to `AuthorizeAsync(principal, resource, operation)`; add the Orchard `AuthorizationHandler`s mapping each operation to the existing permission. Permission ids, names, descriptions, and providers unchanged. Tests: operation-to-permission mapping tests plus the existing authorization tests unchanged.

### P0.8 - Background tasks to cycles (S10)

Split every `IBackgroundTask` in the mapping table of S10 into `I<Name>Cycle`/`<Name>Cycle` (in the `*.Core` projects) and the wrapper task (unchanged schedule). `CycleRunner<TCycle>` and `AddCoreContactCenterBackgroundWorkers()`-style methods are written and unit-tested but not registered in Orchard. Existing `*BackgroundTaskTests` become `*CycleTests`.

### P0.9 - Hubs (S3)

Extract `TelephonyHubBase<TClient>`, `ContactCenterHubBase<TClient>`, `SmsPortalHubBase<TClient>` inside the modules; the concrete hubs become sealed `[Authorize]` subclasses with the same class names and paths (`SignalRHubRoutes.GetHubPath<T>()` output unchanged, verified by `SignalRHubRoutesTests`). Notifiers become generic over `THub` (`ContactCenterRealTimeNotifier<THub>`, `SmsRealTimeNotifier<THub>`).

### P0.10 - Entities, capabilities, lifecycle (S7, S18)

- `TelephonyInteraction`, `OmnichannelMessage`, `Interaction` drop the Orchard entity base/interface for a plain `JsonObject` property with the same JSON name; serialization tests assert byte-identical JSON before/after.
- `ContactCenterCapabilities` introduced; `IContactCenterFeatureWorkManager`, lifecycle participants (Contact Center, Telnyx, Asterisk, Dialpad), and hub work leases key on capabilities; `ContactCenterFeatureLifecycleHandler` maps feature ids to capabilities. `ContactCenterFeatureDependencyAuditTests` and lifecycle tests unchanged.

### P0.11 - Schema migration steps (D-12)

Move the body of every Orchard `*Migrations` class (Contact Center 28, Telephony 4, Omnichannel 6, SMS Portal 1, Telnyx 2, Asterisk 3) into a `*SchemaMigration` step class in the corresponding `*.Core` project (`Create`, `UpdateFromN`, same version numbers, schema builder passed in); the Orchard `DataMigration` delegates. `ContactCenterMigrationSql`/`SmsPortalMigrationSql` move to core. `SchemaMigrationRunner` + `SchemaVersion` document written and unit-tested against SQLite but not used by Orchard. `MigrationAdditiveOnlyGuardTests`, `ContactCenterMigrationSqlTests`, and the snapshot upgrade test stay green.

### P0.12 - Endpoints as `Map*` methods

Convert each `Endpoints/*` file to a `Map*` extension method on `IEndpointRouteBuilder` with route defaults equal to today's routes and an options callback for filters; Orchard `Configure` calls them. Add `MapContactCenterVoiceMediaEndpoints` from the media/voicemail controller actions (G7) while keeping the controllers for the admin UI. Existing endpoint tests unchanged.

### P0.13 - Registration methods (`AddCore*`)

Introduce the `AddCore<Pillar><Feature>()` and `AddCore<Pillar><Feature>StoresYesSql()` methods in the `*.Core` projects and make every Orchard startup call them, leaving only `[stay]` lines from [appendix C](appendix-c-startup-registration-inventory.md) inline. A DI snapshot test (resolve a tenant container per feature profile in FeatureActivationTests, dump `ServiceDescriptor`s) is captured **before** this workstream and compared after: same service types, same lifetimes, same implementation types, same order for `IEnumerable<T>` chains. Also introduce the framework default DNC registry (`ContactPreferenceDoNotCallRegistry`, not registered in Orchard) and `ContactCenterComplianceOptions.FailClosedWithoutNationalRegistry`.

### P0.14 - Store base class

Rename the Orchard `DocumentCatalog<T,TIndex>` subclass usage to a `ConcurrentDocumentCatalog<T,TIndex>` type in `CrestApps.OrchardCore.YesSql.Core` (the original stays for other consumers) so Phase 1 can move the new type without touching unrelated modules. Store tests unchanged.

## 2. What Phase 0 changes about Phase 1

After Phase 0, Phase 1 workstreams W1-W8 contain no "**A** (adapt)" items: every file is a move plus namespace rewrite. The remaining Phase 1 logic is limited to: creating the `Transitions` projects, moving the seam interfaces into `CrestApps.Core.Hosting.Abstractions`/`CrestApps.Core.Sms.Abstractions`, deleting the Orchard duplicates, wiring `AddYesSqlStores()` builder methods, the type-name rewrite migrations, and the test project split. [05-phase-1-transition.md](05-phase-1-transition.md) keeps its detailed lists as the move checklist; where it says "A (S-n)", read "already adapted in Phase 0, verify".

## 3. Gate after every Phase 0 workstream

```bash
dotnet build CrestApps.OrchardCore.slnx -c Release -warnaserror /p:TreatWarningsAsErrors=true /p:RunAnalyzers=true /p:NuGetAudit=false
dotnet test tests/CrestApps.OrchardCore.Tests -c Release /p:NuGetAudit=false
dotnet test tests/CrestApps.OrchardCore.ContactCenter.FeatureActivationTests -c Release /p:NuGetAudit=false
dotnet test tests/CrestApps.OrchardCore.ContactCenter.DistributedTests -c Release /p:NuGetAudit=false
git diff main -- "src/Modules/**/Manifest.cs"            # must be empty
# coverage must not drop below the P0.1 baseline for any moving assembly
# DI snapshot diff (P0.13) must be empty or explained line by line
```

Phase 0 exit criteria (manual review):

- [ ] Coverage table complete; every moving class has a recorded covering test; architecture test enforces it.
- [ ] No moving class references `IClock`, `IDistributedLock`, `ShellSettings`, `ShellScope`, `ISiteService`, `ISignal`, `OrchardCore.Entities`, `OrchardCore.Security.Permissions`, `UserManager<IUser>`, `IBackgroundTask`, `OrchardCore.Sms`, `ContentItem`/`ContentPart`/`IContentManager`/`IContentDefinitionManager` (grep list in appendix B), except the Orchard binding classes listed in [10](10-backward-review-framework-to-orchard.md).
- [ ] Every Orchard startup contains only framework calls plus `[stay]` registrations; DI snapshot identical.
- [ ] Snapshot upgrade test green; FeatureActivationTests green without assertion edits; Playwright soft phone green; manual smoke of soft phone, agent workspace, supervisor dashboard, SMS inbox, CRM admin, provider settings.
- [ ] Production-readiness changelog entry for Phase 0.
