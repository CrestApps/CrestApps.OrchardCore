# 05 - Phase 1: build the framework inside this repository (`Transitions` folders)

Phase 1 creates every `CrestApps.Core.*` project of the Contact Center Suite inside this repository, moves framework code into them with **final namespaces and assembly names**, rewires the Orchard modules to consume them, and moves framework tests into an Orchard-free test project. Nothing user-visible changes.

Phase 1 starts only after [Phase 0](00-phase-0-preparation.md) is complete. Because Phase 0 already landed every seam, contract, default, cycle, hub base, schema step, endpoint method, and `AddCore*` registration inside the Orchard projects, the workstreams below are relocations: `git mv`, namespace rewrite, project references, `AddYesSqlStores()` builder sugar, stored type-name rewrite migrations, and the test project split. Wherever a step below says "adapt" or cites a seam (S1-S23), read it as "already done in Phase 0; verify it moved intact".

Work in the order below. Each workstream is one pull request (or one reviewable commit series) and ends with the full gate in section 3. Do not start a workstream while the previous gate is red.

## 0. Ground rules for every workstream

1. **Move with history:** `git mv` the file first, then edit. One commit for moves, one for edits, per project, so reviewers can diff logic changes separately from relocations.
2. **Namespaces are final** from the first commit (see [appendix B](appendix-b-namespace-map-commands-checklists.md) map). Never introduce a temporary namespace.
3. **Assembly names are final:** the `csproj` file name and `AssemblyName` are the future package ids.
4. **Framework projects never reference Orchard** (`OrchardCore.*` packages, `CrestApps.OrchardCore.*` projects). Grep gate after every commit:
   ```bash
   grep -rlE "OrchardCore" src/Abstractions/Transitions src/Core/Transitions src/Modules/Transitions tests/Transitions --include=*.cs --include=*.csproj --include=*.props
   ```
   The only allowed hits are comments that mention Orchard as a downstream product.
5. **Every moved public type gets XML docs** if it lacks them, and gets `sealed` unless inheritance is used. Run the build with `-warnaserror`.
6. **Keep the Orchard behaviour identical.** When a framework default differs from what Orchard needs, Orchard replaces the service; the framework default is the sample-host behaviour.
7. **Stored data:** any moved class that is persisted as a YesSql document (anything saved through `ISession.SaveAsync` or a `DocumentCatalog`) is added to the type-name rewrite table in appendix B in the same pull request. Any moved class that is embedded inside a stored document keeps its property names and JSON attributes.
8. **Tests move with code.** A test moves when its subject moved; it stays when it tests Orchard glue.
9. **No competitor names** in new identifiers, comments, or docs.
10. Update `src/CrestApps.Docs/docs/contact-center/production-readiness-changelog.md` at the end of each workstream with a one-paragraph entry.

## 1. Workstreams

### W0 - Scaffolding (no behaviour change)

Tasks:

1. Create the folders from [02-inventory-and-target-layout.md](02-inventory-and-target-layout.md) section 3 and add solution folders to `CrestApps.OrchardCore.slnx`.
2. Add `src/Abstractions/Transitions/Directory.Build.props`, `src/Core/Transitions/Directory.Build.props`, `src/Modules/Transitions/Directory.Build.props`, `tests/Transitions/Directory.Build.props`:
   - import the parent props (`<Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />`),
   - set `IsPackable=false` (this repository must not publish them), `RepositoryUrl=https://github.com/CrestApps/CrestApps.Core`, `PackageTags=CrestApps-Core`, `CrestAppsDescription` copied from the Core repository's `Directory.Build.props`,
   - `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`, `Nullable` as in the Core repository (`enable` only where the Core project has it; default disabled),
   - `<InternalsVisibleTo Include="CrestApps.Core.ContactCenter.Tests" />` for the source projects.
3. Copy `.editorconfig` rules from the Core repository if they differ (diff the two files; the Core rules win inside `Transitions`).
4. Create every project listed in the target map as an empty project with the final `csproj` metadata (`RootNamespace`, `Title`, `Description`, `PackageTags`, references). Reference existing Core packages (`CrestApps.Core.Abstractions`, `CrestApps.Core`, `CrestApps.Core.Data.YesSql`, `CrestApps.Core.SignalR`, `CrestApps.Core.Support`, `CrestApps.Core.AI`, `CrestApps.Core.AI.Chat`, `CrestApps.Core.AI.Resilience`, `CrestApps.Core.Templates`) as `PackageReference` with the central `CrestAppsCoreVersion`. Add `CrestApps.Core.SignalR`, `CrestApps.Core.AI.Resilience`, and `CrestApps.Core.AI.Templates` to `Directory.Packages.props` if missing (`CrestApps.Core.Templates` and `CrestApps.Core.Support` already exist).
5. Create `tests/Transitions/CrestApps.Core.ContactCenter.Tests` mirroring `tests/CrestApps.Core.Tests/CrestApps.Core.Tests.csproj` (xunit v3, Moq, `Microsoft.Extensions.TimeProvider.Testing`, `YesSql.Provider.Sqlite`, `Microsoft.Data.Sqlite`, `PublicApiGenerator`), referencing only `Transitions` projects. Add a `PublicApi/` approval test copied from the Orchard one and a `Support/` folder for fakes.
6. Add `Directory.Packages.props` entries: `Microsoft.Extensions.TimeProvider.Testing`, `Azure.Storage.Blobs` (if W9 optional work happens), `StackExchange.Redis` (not needed in Phase 1).
7. Scaffold the versioned schema migration runner in `CrestApps.Core.Data.YesSql.ContactCenter` (D-12): `ISchemaMigration`, `SchemaMigrationRunner`, `SchemaVersion` document, `RunContactCenterSuiteSchemaMigrationsAsync`, with a unit test that runs a two-step migration against SQLite twice and proves idempotence. From W3 on, every Orchard `*Migrations` class body moves into a framework `*SchemaMigration` step with the same version numbers, and the Orchard class delegates to it (Orchard keeps tracking versions in its own migration table; the runner tracks them in the `SchemaVersion` document for standalone hosts).
7. Add a CI step to `.github/workflows` that runs the grep gate from section 0 and the new test project.

Definition of done: solution builds with the empty projects; grep gate is green; the new test project runs zero tests successfully.

### W1 - Host seams (`CrestApps.Core.Hosting.Abstractions`, `CrestApps.Core.Hosting`, `CrestApps.Core.Sms.Abstractions`)

Implement [03-host-seams.md](03-host-seams.md) S2, S3, S4, S9, S12, S17, S18 and the `CycleRunner<TCycle>` helper for S10:

1. `CrestApps.Core.Hosting.Abstractions`: `Locking/IDistributedLockProvider`, `Locking/ILocker`, `Hosting/ITenantAccessor`, `Hosting/IScopedWorkExecutor`, `Hosting/IAfterCommitTaskQueue`, `Hosting/IStartupCheck` (`Task ValidateAsync(CancellationToken)`), `Security/IUserDirectory`, `Security/UserSummary`, `Diagnostics/LogDataClassifications`, `Builders/CrestAppsContactCenterSuiteBuilder`, `BackgroundWork/ICycle`, `BackgroundWork/CycleOptions`.
2. `CrestApps.Core.Hosting`: `LocalDistributedLockProvider`, `SingleTenantAccessor`, `ServiceProviderScopedWorkExecutor`, `AfterCommitTaskQueue`, `AfterCommitStoreCommitter` (decorator; registration helper wraps whatever `IStoreCommitter` is present, or `NoOpStoreCommitter`), `ClaimsUserDirectory` (returns the current principal only) and `NoUserDirectory`, `CycleRunner<TCycle> : BackgroundService`, `StartupCheckHostedService`, `TenantSignalRGroupName` + `HubConnectionWork` (moved from `CrestApps.OrchardCore.SignalR.Core`, namespace `CrestApps.Core.SignalR`), `ServiceCollectionExtensions.AddCoreHosting()` and `CrestAppsCoreBuilderExtensions.AddContactCenterSuite(...)`.
3. `CrestApps.Core.Sms.Abstractions`: the S12 contracts.
4. Orchard adapters in `CrestApps.OrchardCore.Core` (`Services/Hosting/`): `OrchardCoreDistributedLockProvider`, `ShellSettingsTenantAccessor`, `ShellScopeWorkExecutor`, `OrchardCoreUserDirectory`, `OrchardCoreSmsProviderResolver`, `OrchardCoreStartupCheckTenantEvents`, and one `AddCrestAppsOrchardCoreHosting()` extension that calls `AddCoreHosting()` then `Replace`s each default. Move the `TimeProvider` registration here (`TryAddSingleton<TimeProvider, ClockTimeProviderAdapter>()`), leave the AI module's registration in place (it becomes a no-op through `TryAdd`).
5. `CrestApps.OrchardCore.SignalR.Core` keeps `SignalRConstants`; its `TenantSignalRGroupName` and `HubConnectionWork` become `[Obsolete]` forwarders (or are deleted and usages updated; deleting is preferred since all usages are in scope).
6. Tests: `LocalDistributedLockProviderTests`, `AfterCommitTaskQueueTests`, `CycleRunnerTests`, `TenantSignalRGroupNameTests` (moved from `tests/.../SignalR`), fakes `FakeDistributedLockProvider`, `FakeTenantAccessor`, `FakeUserDirectory`, `FakeSmsProvider` in `tests/Transitions/.../Support`.

Definition of done: seams have unit tests; Orchard adapters registered; nothing else consumes them yet.

### W2 - Phone numbers and WebSockets

1. `git mv` the 10 files of `CrestApps.OrchardCore.PhoneNumbers.Abstractions` into `CrestApps.Core.PhoneNumbers.Abstractions` (namespace `CrestApps.Core.PhoneNumbers`). Delete the Orchard abstractions project; repoint all `ProjectReference`s (Omnichannel, ContactCenter, Telephony, DncRegistry, Managements, Verifications, tests) to the new project.
2. Move `DefaultPhoneNumberService` and `PhoneNumberServiceExtensions` into `CrestApps.Core.PhoneNumbers` with `AddCorePhoneNumbers()`; `CrestApps.OrchardCore.PhoneNumbers/Startup.cs` calls it. Verification manager/part/settings/handlers stay in `CrestApps.OrchardCore.PhoneNumbers.Core` (they are content parts).
3. Move `IWebSocketConnectionRegistry`, `WebSocketRendezvous`, `InMemoryWebSocketConnectionRegistry`, `DistributedWebSocketConnectionRegistry`, `IRendezvousOwnerStore`, `WebSocketsNode` into `CrestApps.Core.WebSockets` (namespace `CrestApps.Core.WebSockets`) with `AddCoreWebSockets()`; `RedisRendezvousOwnerStore` stays in the Orchard module and replaces the in-memory owner store when Redis is enabled (same `[RequireFeatures]` as today). Delete `CrestApps.OrchardCore.WebSockets.Abstractions`; repoint Telnyx.
4. Tests: `PhoneNumberTests`, `PhoneNumberComparisonKeyTests` (currently under `Modules/ContactCenter`), `WebSockets/*` move; `Modules/PhoneNumbers/Verifications/*` stay.
5. Public API: delete no baselines yet (none governed); add baselines for the two new abstractions assemblies in the transition test project.

### W3 - Telephony abstractions and primitive

1. `git mv` `CrestApps.OrchardCore.Telephony.Abstractions/**` to `CrestApps.Core.Telephony.Abstractions` except `TelephonyPermissions.cs` (stays in a slimmed Orchard `CrestApps.OrchardCore.Telephony.Abstractions` together with `TelephonyConstants.Feature`). Apply S7 to `TelephonyInteraction`. Split `TelephonyConstants`: feature ids stay Orchard, everything else moves. Add `Builders/CrestAppsTelephonyBuilder`.
2. Move `CrestApps.OrchardCore.Telephony.Core/**` into `CrestApps.Core.Telephony` (S1, S2, S4, S5 conversions). The Orchard `Telephony.Core` project is deleted if empty.
3. Move framework services from the `CrestApps.OrchardCore.Telephony` module per [appendix A](appendix-a-file-disposition.md) (default services, encrypted store, hub base, endpoint handlers, cycle). Create `Hubs/TelephonyHubBase<TClient>` from `TelephonyHub`: everything except the `[Authorize]` attribute and tenant name resolution (which now comes from `ITenantAccessor`) moves down; the Orchard `TelephonyHub : TelephonyHubBase<ITelephonyClient>` is a near-empty sealed class.
4. `DefaultTelephonyUserAccessor` becomes `OrchardCoreUserDirectory` usage; `ITelephonyUserAccessor` is removed in favour of `IUserAccessor` + `IUserDirectory` (update `DefaultTelephonyUserTokenStore`, `DefaultTelephonyAuthenticationService`, extension resolver).
5. `LocalEncryptedRecordingMediaStore` takes `LocalRecordingMediaStoreOptions` (root path, data-protection purpose) instead of Orchard `IOptions<ShellOptions>`/`ShellSettings`; Orchard's Startup computes the tenant path as today and passes it.
6. Store package: move `TelephonyExtensionIndex`, `TelephonyInteractionIndex`, `TelephonyUserConnectionIndex` and their providers, extract the schema SQL from `TelephonyExtensionIndexMigrations`, `TelephonyInteractionMigrations`, `TelephonyUserConnectionIndexMigrations` into `Indexes/Telephony/*SchemaBuilderExtensions.cs`, and rewrite the Orchard migrations to call them. Add `ConcurrentDocumentCatalog<T,TIndex>` (copy of `CrestApps.OrchardCore.YesSql.Core/Services/DocumentCatalog.cs`) plus the three migration helpers to `CrestApps.Core.Data.YesSql.ContactCenter`.
7. Add `TelephonyLegacyDocumentTypeNameMigrations` (appendix B table) to the Orchard Telephony module.
8. Rewire `CrestApps.OrchardCore.Telephony/Startup.cs`, `SoftPhoneCoreStartup`, `SoftPhoneWidgetStartup`, `SoftPhoneExtensionStartup`, `ContentsStartup` to the `AddCoreTelephony*` methods.
9. Tests: move `Telephony/DefaultTelephony*Tests`, `TelephonyCommandExecutorTests`, `TelephonyExtensionManagerTests`, `TelephonyInteractionStoreConcurrencyTests`, `TelephonyInteractionSynchronizationServiceTests`, `LocalEncryptedRecordingMediaStoreTests`, `RecordingMediaTenantEventsTests` (becomes initializer tests), `TelephonyAudioModeResolverTests`, `TelephonyCallQualityEvaluatorTests`, `TelephonyProviderCapabilityContractTests`, `TelephonyProviderOptionsTests`, `ExtensionCallingTests`, `DefaultDialDestinationPolicyTests`, `OutboundCallScreeningTests`, `TransferTargetPolicyTests`, `TelephonyCallControlBoundaryTests`, `TelephonyHubAuthorizationTests` (as hub-base tests), `ProviderNeutralContractArchitectureTests`, `VoiceIngressLayeringArchitectureTests`, `CallStateNamesJsSyncTests` (points at the resources project after W10; until then keep it Orchard), and the `Doubles` they need. `SoftPhoneControllerTests`, `TelephonyOAuthControllerTests`, `SoftPhoneExtensionEndpointsTests` (page part), `SoftPhoneWidgetSettingsTests` stay.
10. Public API baselines: `CrestApps.OrchardCore.Telephony.Abstractions.approved.txt`, `...Telephony.Core.approved.txt`, `...Telephony.approved.txt` are regenerated (they shrink); new baselines for `CrestApps.Core.Telephony.Abstractions` and `CrestApps.Core.Telephony` in the transition test project.

### W4 - Omnichannel (spike first)

W4.0 Spike (time-boxed, 2-3 days): design the CRM contracts of S11 from the Orchard usages (list every member of `ContentItem`, `ContentTypeDefinition`, `OmnichannelContactPart`, `OmnichannelSubjectPart`, `BagPart` contact methods, and text fields that `OmnichannelSubjectWriter`, `SubjectFlowSettingsService`, `OmnichannelAutomationHelper`, `SmsConversationService`, `SmsInboundProcessor`, `AutoReplyRouter`, `VoiceAgentConversationLoop`, `InboundContactLookup`, `DefaultSubjectActionExecutor`, `DefaultActivityDispositionService`, and `OmnichannelActivityStore` touch), write the contracts and an in-memory fake, refactor `OmnichannelSubjectWriter`, `SubjectFlowSettingsService`, `OmnichannelAutomationHelper` on a branch, and run the existing tests. Report: the final contract surface, whether `OmnichannelActivityStore` joins the contact index (then add `IOmnichannelActivityQueryContributor`), and which Orchard admin screens read subject fields dynamically.

W4.1 Abstractions: create `CrestApps.Core.Omnichannel.Abstractions` by moving models, store/manager/service interfaces, filter contexts, constants (without `Features`), and S20 DNC contracts from `CrestApps.OrchardCore.Omnichannel.Core` and `CrestApps.OrchardCore.DncRegistry.Abstractions`. Add the CRM model types and contracts from S11 (`ContactDefinition`, `ContactDefinitionSettings`, `OmnichannelContact`, `ContactPhoneNumber`, `ContactEmail`, `SubjectDefinition`, `SubjectDefinitionSettings`, `SubjectFieldDefinition`, `OmnichannelSubject`, `IContactDefinitionProvider`, `IOmnichannelContactResolver`, `IOmnichannelContactWriter`, `IOmnichannelContactSearch`, `ISubjectDefinitionProvider`, `IOmnichannelSubjectAccessor`); re-base `ISubjectFlowSettingsService` on `SubjectDefinition`; keep `SubjectFlowSettings` property names (`SubjectContentType` stays as the definition name for stored compatibility). Apply S7 to `OmnichannelMessage`. `OmnichannelActivityContainer` becomes `Activity` + `OmnichannelContact` + `SubjectDefinition` + `UserSummary`; `SubjectActionExecutionContext` carries `OmnichannelContact` + `OmnichannelSubject`; `CompleteOmnichannelActivityContainer` is an Orchard shape view model and stays.

W4.2 Primitive: move services from `Omnichannel.Core` and the headless services from `Omnichannel.Managements` listed in appendix A into `CrestApps.Core.Omnichannel`, refactored onto the contracts. Add the **default model** (`AddCoreOmnichannelContacts()`, `AddCoreOmnichannelSubjects()`): stores, managers, `CatalogEntryHandlerBase<T>` handlers (contact handler normalizes phone numbers with `IPhoneNumberService`, stamps `DoNot*Utc` with `TimeProvider`, validates required time zone per definition settings; subject definition handler validates unique field names and types; subject handler validates values against the schema), catalog-backed contract implementations, `DefinitionSubjectFlowSettingsService`, `DefaultContactActivityBatchLoader` over `IOmnichannelContactSearch`. `TransferToAgentTool` moves. `AutomatedActivitiesProcessorBackgroundTask` becomes a cycle. Framework services never reference the default stores directly, only the contracts.

W4.3 Orchard implementations in `CrestApps.OrchardCore.Omnichannel.Core`: `ContentTypeContactDefinitionProvider` (contact types = content types with `OmnichannelContactPart`; settings from `OmnichannelContactPartSettings`), `ContentItemOmnichannelContactResolver`/`Writer`/`Search` (over `IContentManager`, the contact-method bag, `OmnichannelContactIndex`), `ContentTypeSubjectDefinitionProvider` (subject types = content types with `OmnichannelSubjectPart`; fields = the type's text fields as today's `GetSubjectTextFields`), `ContentItemOmnichannelSubjectAccessor`, `ContentTypeSubjectFlowSettingsService` (today's `SubjectFlowSettingsService`), registered by `AddOrchardCoreOmnichannelContacts()`/`AddOrchardCoreOmnichannelSubjects()` from `OmnichannelActivitiesStartup`. Content parts, content indexes, `ContentDefinitionOmnichannelContactTypeProvider`, `OmnichannelContactDefinitionService`, the content-based `DefaultContactActivityBatchLoader` (Orchard keeps its own and replaces the framework loader) remain Orchard. Acceptance: every Orchard admin screen for contacts, subjects, subject flows, subject actions, dispositions, campaigns, and activities behaves exactly as before.

W4.4 Stores: `OmnichannelActivityIndex`, `OmnichannelActivityBatchIndex`, `CadenceIndex`, `OmnichannelMessageIndex` + providers + schema extensions + `YesSqlOmnichannelActivityStore` (Dapper via `IStore.Configuration`) into the store package, plus the default-model stores and indexes (`YesSqlContactDefinitionStore`, `YesSqlOmnichannelContactStore`, `YesSqlSubjectDefinitionStore`, `YesSqlOmnichannelSubjectStore`, indexes listed in [02](02-inventory-and-target-layout.md)) registered by `AddCoreOmnichannelContactStoresYesSql()`/`...SubjectStoresYesSql()`, which Orchard never calls. The Orchard content-item `OmnichannelContactIndex`/`OmnichannelContactCommunicationPreferenceIndex` stay Orchard with their current table names; the framework indexes use distinct table names (`OmnichannelContactDocumentIndex` style) so both can coexist in a database. Orchard migrations (`OmnichannelIndexMigration`, `OmnichannelActivityIndexMigrations`, `OmnichannelActivityBatchIndexMigrations`, `CadenceIndexMigrations`, `Omnichannel/Migrations/*`) call the extensions; add `OmnichannelLegacyDocumentTypeNameMigrations`.

W4.2b Time zones (D-14, S22): add `IContactTimeZoneResolver` + `PhoneNumberContactTimeZoneResolver` default (verify libphonenumber's region-to-time-zone data covers the need; otherwise ship the `TimeZoneMap` catalog with a default map seeded from the Orchard recipe) and call it from the framework contact handler; Orchard registers `TimeZoneMapContactTimeZoneResolver` over its TimeZones module and keeps `OmnichannelContactTimeZoneHandler`.

W4.5 Rewire `Omnichannel/Startup.cs`, `OmnichannelActivitiesStartup`, `ChannelEndpointsStartup`, `AISubjectFlowStartup`, `Omnichannel.Sms/Startup.cs`, `Omnichannel.Voice/Startup.cs` + `RealtimeVoiceStartup`, `Omnichannel.EventGrid/Startup.cs`.

W4.6 `CrestApps.Core.Omnichannel.Sms` and `CrestApps.Core.Omnichannel.Voice` (S1, S11, S12 conversions). The Twilio endpoint becomes `MapTwilioSmsWebhookEndpoint` with the validator in the framework.

W4.7 Tests: move `Core/Omnichannel/**`, `Modules/Omnichannel/*` except UI/security/permission/schema-convergence/content-import ones, `Modules/Omnichannel/Voice/**`, `Modules/Omnichannel/Managements/Services/{DefaultActivityDispositionServiceTests,DefaultSubjectActionExecutorTests,AutomatedActivityCompletionServiceTests,AutomatedVoiceActivitySettingsResolverTests,OmnichannelAutomationHelperTests,SubjectFlowSettingsServiceTests,SubjectFlowSettingsHandoffCompositionTests,OmnichannelHelperTests}` (if their subjects moved), `TransferToAgentToolTests`, `TwilioWebhookEndpointSignatureTests` (validator part). Regenerate baselines for `Omnichannel.Core`, `Omnichannel`, `Omnichannel.Managements`, `Omnichannel.Voice.Core`, `Omnichannel.Voice`.

### W5 - Contact Center abstractions and primitive (largest; split into sub-PRs by feature)

W5.1 Abstractions: `git mv` `CrestApps.OrchardCore.ContactCenter.Abstractions/**` except `ContactCenterProcessHealth*`, `ContactCenterProcessLivenessPathValidator` into `CrestApps.Core.ContactCenter.Abstractions`. Replace `ContactCenterConstants.Feature` with `ContactCenterCapabilities` (S18); the Orchard abstractions project keeps `ContactCenterConstants.Feature` and receives `ContactCenterPermissions` from `ContactCenter.Core`. Add `Security/ContactCenterOperations` (S8) and `Builders/CrestAppsContactCenterBuilder`.

W5.2 Core move, feature by feature (each its own PR, each with its `AddCoreContactCenter<Feature>()` and `AddCoreContactCenter<Feature>StoresYesSql()`, its index/schema extensions, its Orchard migration rewrite, its type-name rewrite entries, its `IBackgroundTask` wrappers, and its moved tests):

| Sub-PR | Scope | Orchard Startup rewired |
| --- | --- | --- |
| W5.2a | base: interactions, events, outbox, dedup, metrics, retention, projections, configuration cache (S6), scope executor default (S4), topology/coordination options, work manager (S18) | `Startup.cs` |
| W5.2b | agent services + agents + entitlements + business hours | `AgentServicesStartup`, `AgentsStartup`, `AgentEntitlementsStartup`, `BusinessHoursStartup` |
| W5.2c | queues, reservations, routing, work state, queue treatment, limits, callbacks (queued) | `QueuesStartup` |
| W5.2d | provider inbox, real-time (hub base, notifier, connection registry, hub scope context) | `ProviderInboxStartup`, `RealTimeStartup` |
| W5.2e | voice: router, call sessions, commands, provider events, transfers, offers, monitoring, assist, soft-phone event handler, recording governance | `VoiceStartup`, `VoiceSoftPhoneStartup`, `RecordingCoreStartup` |
| W5.2f | inbound voice + IVR + entry points + voice media | `InboundVoiceStartup`, `VoiceMediaStartup` |
| W5.2g | dialer + paced dialing + callbacks + compliance (default `ContactPreferenceDoNotCallRegistry` + fail-closed option, D-13) | `DialerStartup`, `DialerPacedStartup` |
| W5.2k | voice media endpoints (`MapContactCenterVoiceMediaEndpoints` from `VoiceMediaController`/`MyVoicemailGreetingController`, G7), agent sign-out handler (S21), client-configuration providers (S23) | `VoiceStartup`, `AgentsStartup`, resource configurations |
| W5.2h | recording, secure capture, supervision, analytics | `RecordingStartup`, `SecureCaptureStartup`, `SupervisionStartup`, `AnalyticsStartup` |
| W5.2i | health checks (framework `AddCoreContactCenterHealthChecks`, endpoint mapping) and startup checks (S14) | `ContactCenterHealthChecksStartup`, `ContactCenterQueuesHealthChecksStartup`, `VoiceHealthChecksStartup`, `ContactCenterRedisHealthCheckStartup` (Redis stays) |
| W5.2j | endpoints (`Endpoints/*` -> `MapContactCenter*Endpoints`) and `AgentDesktopStartup` (framework: workspace endpoints; Orchard: agent bar filter, resource configuration, builder implementation) | `AgentDesktopStartup` |

W5.3 Authorization (S8): introduce `ContactCenterOperations`, refactor the five authorization-consuming services, add the Orchard mapping handler, keep permission ids unchanged.

W5.4 Lifecycle: `ContactCenterFeatureLifecycleCoordinator`, `ContactCenterFeatureLifecycleHandler`, `ContactCenterFeatureWorkManager`, `ContactCenterFeatureWorkLifecycleParticipant`, `ContactCenterRealTimeLifecycleParticipant`, `ContactCenterVoiceLifecycleParticipant` move to the framework keyed on capabilities; Orchard's `ContactCenterFeatureLifecycleHandler` (or a new `ContactCenterFeatureCapabilityMapper : IFeatureEventHandler`) maps enabled features to capabilities on activation.

W5.5 Tests: move everything under `tests/.../Modules/ContactCenter` whose subject moved (the majority; see appendix A rules), including `Integration/*` (dialer harness), `Lifecycles/*`, `StateMachine/*`, `RollingUpgrade/*` (with the schema recorder), `Reports/ContactCenterReportTotalsTests`; keep controller/endpoint-page tests, `ContactCenterSetupRecipeTests`, workflow tests, `ContactCenterFeatureDependencyArchitectureTests`, `ContactCenterSoftPhoneResourceTests`, report provider tests, `ContactCenterHubSecurityTests` (Orchard hub), and `ContactCenterMigrationSqlTests`/`ContactCenterClaimMigrationTests`/`ContactCenterUniquenessMigrationTests`/`MigrationStartupBudgetTests` only if they exercise the Orchard `DataMigration` classes (if they exercise the SQL text, they move with `ContactCenterMigrationSql`).

W5.6 Regenerate Orchard baselines for `ContactCenter.Abstractions`, `ContactCenter.Core`; add framework baselines.

### W6 - SMS Portal

1. Abstractions: move `CrestApps.OrchardCore.Omnichannel.Sms.Portal.Abstractions/**` (S12 re-base of `ISmsDispatcher`/`ISmsDispatchProvider`; feature ids stay Orchard; add `SmsPortalOperations` for S8 and `CrestAppsSmsPortalBuilder`).
2. Primitive: move `Sms.Portal.Core/**` plus framework module services (S1, S2, S3, S5, S11, S12). `SmsPortalHubBase<TClient>` from `SmsPortalHub`; `SmsRealTimeNotifier<THub>`.
3. Stores: conversation/template/broadcast indexes, providers, `SmsPortalMigrationSql`, schema extensions, `SmsPortalStoreOptions`; Orchard `SmsConversationMigrations` rewritten; `SmsPortalLegacyDocumentTypeNameMigrations` added.
4. Rewire `Sms.Portal/Startup.cs`, `RoutedDistributionStartup`, `WorkDistributionStartup`.
5. Orchard keeps `SmsContactResolver`, `SmsContactTimeZoneResolver`, `SmsPhoneFieldButtonShapeTableProvider`, admin menu, permission provider, controllers, drivers, views, `SmsPortalHub`, `IBackgroundTask` wrappers, the `OrchardCore.Sms` adapter.
6. Tests: move `Telephony/Sms/**` except `SmsPortalAdminControllerTests` and `SmsRealTimeNotifierTests` (rewrite the latter against the generic notifier in the framework, keep an Orchard hub test). Baselines regenerated.

### W7 - Telnyx

1. Move `Telnyx.Core/**` and the framework parts of the Telnyx module into `CrestApps.Core.Telephony.Telnyx` (S1, S3, S5, S12, S13). Provider stores under `Data/YesSql`. Endpoint handlers as `MapTelnyx*`. `TelnyxSmsProvider` implements the framework `ISmsProvider`; the Orchard module adds the `OrchardCore.Sms.ISmsProvider` wrapper registered exactly where the current provider was.
2. Preserve the `TryAddScoped<ITelnyxInboundCallRouter, ...>` ordering rule (the Contact Center router must win when `AddContactCenterVoice()` is called; implement with `Replace` inside `AddCoreTelnyxContactCenterVoice()` so ordering no longer matters).
3. Rewire `Telnyx/Startup.cs`, `DialerStartup`, `TelnyxContactCenterMediaStartup`, `SmsStartup`, `AiVoiceStartup`. Migrations call framework schema extensions; add `TelnyxLegacyDocumentTypeNameMigrations`.
4. Tests: move `Telephony/Telnyx*Tests`, `Telnyx/SoftPhoneHealthMetricsTests`, `VoiceAgentMediaProviderTests`, `WebhookInboxHandlerReplayTests`, `VoiceEventFanOutIntegrationTests` (if provider-neutral).

### W7b - Twilio SMS (`CrestApps.Core.Sms.Twilio`, D-16)

1. Create the package with `TwilioOptions`, `TwilioSmsProvider : ISmsProvider` (Twilio REST `Messages.json`, basic auth, resilience pipeline, delivery result mapping), `TwilioRequestValidator` (moved from `Omnichannel.Sms/Twillio/TwillioRequestValidator.cs`), `TwilioInboundMessageParser`, `MapTwilioSmsWebhookEndpoint` (moved from `Omnichannel.Sms/Endpoints/TwilioWebhookEndpoint.cs`, S3/S5/S12 conversions: auth token from `IOptionsMonitor<TwilioOptions>`, no `ShellScope`, dispatch through `IOmnichannelEventHandler`s).
2. Orchard: `Omnichannel.Sms/Startup.cs` maps the framework endpoint; an `IConfigureOptions<TwilioOptions>` reads the Orchard Twilio SMS settings (`OrchardCore.Sms.Twilio`) so the existing settings screen keeps working; outbound sends still go through `OrchardCore.Sms` via the resolver adapter (S12), so no behaviour changes.
3. Tests: `TwilioWebhookEndpointSignatureTests` moves; add `TwilioSmsProviderTests` with a recorded HTTP handler (same style as the Telnyx API client tests).

### W8 - Asterisk and Dialpad

1. Asterisk: move everything except `Manifest.cs`, `Startup.cs` (rewired), `Drivers/AsteriskSettingsDisplayDriver`, `Views`, `AsteriskSettings` (site document) into `CrestApps.Core.Telephony.Asterisk`. `AsteriskRealtimeVoiceTenantEvents` becomes an `IStartupCheck`/hosted lifecycle service (S14); the Orchard tenant events wrapper stays. `DynamicProxyGenAssembly2` `InternalsVisibleTo` moves with the internals used by Moq.
2. Dialpad: move everything except manifest, startups, settings driver/views, `DialpadWebhookRegistrationController` into `CrestApps.Core.Telephony.Dialpad`.
3. Tests: move `Telephony/Asterisk*Tests`, `Telephony/ProviderContracts/**`, `Telephony/Cassettes/**` (as content files), `Modules/Dialpad/*` except `DialpadWebhookControllerTests`, `Doubles/FakeAsterisk*`.

### W9 - Optional packages (D-8)

`CrestApps.Core.Telephony.Azure` (blob recording store on `Azure.Storage.Blobs`; the Orchard module keeps using `OrchardCore.FileStorage.AzureBlob` until Phase 2 decides) and `CrestApps.Core.Omnichannel.Azure.EventGrid`. Skip if the schedule is tight; record the decision in the changelog.

### W10 - Resources project

1. Create `src/Modules/Transitions/CrestApps.ContactCenter.Resources` as a Razor class library modelled on `CrestApps.AI.Resources` (`Assets.json`, `package.json`, gulp task in the root `gulpfile.js`).
2. Copy (do not move yet) the Telephony and Contact Center `Assets/js` and `Assets/scss` sources plus the vendored `telnyx-webrtc` esbuild bundle from `CrestApps.OrchardCore.Resources` (G6); build to `wwwroot/scripts|styles|vendors`. Change the scripts to read one client-configuration JSON (S23) and keep an Orchard-compatible shim until the Orchard views render the same JSON.
3. Add `CallStateNamesJsSyncTests` against the resources copy. Orchard modules keep their own `wwwroot` in Phase 1 to avoid touching resource manifests; a follow-up in Phase 2 switches them to `_content/CrestApps.ContactCenter.Resources`.

### W11 - Orchard clean-up

1. Delete Orchard `*.Core`/`*.Abstractions` projects that became empty; otherwise leave them with only Orchard glue.
2. `CrestApps.OrchardCore.Cms.Core.Targets` (58 project references) is updated for deleted projects only; feature ids remain.
3. Every Orchard module `Startup` conforms to [04-registration-api.md](04-registration-api.md) section 4. Remove any leftover `services.Add*` that duplicates a framework registration (check with a service-collection snapshot test: resolve the tenant container in `FeatureActivationTests` and assert no duplicate `ImplementationType` for singletons).
4. Update `AGENTS.md` (Contact Center section) to describe the framework/Orchard split and the `Transitions` folders.
5. Architecture guard tests under `tests/CrestApps.OrchardCore.Tests/Architecture` and `Modules/ContactCenter/*Architecture*` are updated for the new assembly names; layering assertions (`Omnichannel` must not reference `ContactCenter`, `Telephony` must not reference `ContactCenter`, providers reference only abstractions plus `ContactCenter`) are re-pointed at the framework assemblies and copied into the transition test project.

### W12 - Test project consolidation and public API

1. `tests/Transitions/CrestApps.Core.ContactCenter.Tests` folders: `Hosting/`, `PhoneNumbers/`, `WebSockets/`, `Omnichannel/`, `Omnichannel.Sms/`, `Omnichannel.Voice/`, `Telephony/`, `Telephony.Telnyx/`, `Telephony.Asterisk/`, `Telephony.Dialpad/`, `ContactCenter/` (with `Integration`, `Lifecycles`, `StateMachine`, `RollingUpgrade`, `Reports`), `SmsPortal/`, `Data.YesSql/`, `PublicApi/`, `Support/`.
2. Replace Orchard doubles with framework ones: `StubClock` -> `FakeTimeProvider`; `FakeDistributedLock` -> `FakeDistributedLockProvider`; `SiteServiceFactory` -> `TestOptionsMonitor<T>`; `FakeUser`/`FakeTelephonyUserAccessor` -> `FakeUserDirectory` + `ClaimsPrincipal` helpers; `TestContactCenterScopeExecutor` moves as-is; `RecordingSchemaBuilder` moves; SQLite-backed store tests use `AddCoreYesSqlDataStore` with an in-memory SQLite connection and the framework schema extensions (no Orchard `DataMigration`).
3. Public API: the Orchard `PublicApiApprovalTests` governs the assemblies that remain in `src/` (baselines regenerated); the transition test project governs every `CrestApps.Core.*` assembly created in Phase 1 (new baselines committed).
4. `DistributedTests` and `FeatureActivationTests` keep their project references but are updated for moved types; they must pass unchanged in behaviour.

## 2. Suggested sequencing and effort

| Order | Workstream | Depends on | Relative size |
| --- | --- | --- | --- |
| 1 | W0 | - | S |
| 2 | W1 | W0 | M |
| 3 | W2 | W1 | S |
| 4 | W3 | W1, W2 | L |
| 5 | W4 (spike first) | W1, W2 | L |
| 6 | W5 (a..j) | W3, W4 | XL |
| 7 | W6 | W4, W5 | M |
| 8 | W7 | W3, W5, W6 | L |
| 9 | W8 | W3, W5 | L |
| 10 | W9 | W3, W4 | S (optional) |
| 11 | W10 | W3, W5 | S |
| 12 | W11 | all | M |
| 13 | W12 | all | M |

## 3. Gate after every workstream

```bash
# 1. Framework must be Orchard-free
grep -rlE "OrchardCore" src/Abstractions/Transitions src/Core/Transitions src/Modules/Transitions tests/Transitions --include=*.cs --include=*.csproj --include=*.props

# 2. Release build, warnings as errors (same flags as CI)
dotnet build CrestApps.OrchardCore.slnx -c Release -warnaserror /p:TreatWarningsAsErrors=true /p:RunAnalyzers=true /p:NuGetAudit=false

# 3. Tests
dotnet test tests/Transitions/CrestApps.Core.ContactCenter.Tests -c Release /p:NuGetAudit=false
dotnet test tests/CrestApps.OrchardCore.Tests -c Release /p:NuGetAudit=false
dotnet test tests/CrestApps.OrchardCore.ContactCenter.FeatureActivationTests -c Release /p:NuGetAudit=false
# DistributedTests need CONTACT_CENTER_REDIS_CONFIGURATION and CONTACT_CENTER_POSTGRES_CONNECTION (see memory/CI docs)
dotnet test tests/CrestApps.OrchardCore.ContactCenter.DistributedTests -c Release /p:NuGetAudit=false

# 4. Manifests unchanged
git diff main -- "src/Modules/**/Manifest.cs" | grep -E "^[+-]" | grep -vE "^(\+\+\+|---)" || echo "manifests unchanged"
```

Manual review checklist at the end of Phase 1 (before Phase 2):

- [ ] Every `Transitions` project has final name, namespace, package metadata, XML docs, sealed types.
- [ ] Every Orchard `Startup` starts with the framework call and contains only Orchard glue.
- [ ] Every moved document type has a type-name rewrite migration and an upgrade test from a `main` database (`tests/.../Migrations/SqliteSchemaSnapshot` style).
- [ ] `ContactCenterFeatureDependencyAuditTests`, `ContactCenterAdministrationSurfaceTests`, `ContactCenterConfigurationPortabilityTests` pass with no assertion changes.
- [ ] Soft phone, agent workspace, supervisor dashboard, SMS portal inbox, queue/agent/entry point/dialer admin screens work in `CrestApps.OrchardCore.Cms.Web` with Telnyx and the Asterisk web host.
- [ ] Docs changelog entry written.
