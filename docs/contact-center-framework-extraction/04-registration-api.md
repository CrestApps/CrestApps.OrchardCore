# 04 - Registration API: builders, feature methods, and the Orchard `Startup` shape

This is the public composition surface of the Contact Center Suite. It follows [01-reference-architecture.md](01-reference-architecture.md) section 3 exactly: sealed builder classes with a `Services` property, one extension method per feature, every builder method delegating to an `AddCore*` `IServiceCollection` method, stores added through `AddYesSqlStores()`.

## 1. Builder types

Phase 1 location: `src/Abstractions/Transitions/<pillar>.Abstractions/Builders/`. Phase 2: `CrestAppsContactCenterSuiteBuilder` moves into `CrestApps.Core.Abstractions/Builders/CrestAppsBuilder.cs`; pillar builders stay in their pillar abstractions.

| Builder | Returned by | Defined in |
| --- | --- | --- |
| `CrestAppsContactCenterSuiteBuilder` | `CrestAppsCoreBuilder.AddContactCenterSuite(Action<CrestAppsContactCenterSuiteBuilder>)` | `CrestApps.Core.Hosting.Abstractions` (Phase 1), `CrestApps.Core.Abstractions` (Phase 2) |
| `CrestAppsOmnichannelBuilder` | `suite.AddOmnichannel(...)` | `CrestApps.Core.Omnichannel.Abstractions` |
| `CrestAppsTelephonyBuilder` | `suite.AddTelephony(...)` | `CrestApps.Core.Telephony.Abstractions` |
| `CrestAppsContactCenterBuilder` | `suite.AddContactCenter(...)` | `CrestApps.Core.ContactCenter.Abstractions` |
| `CrestAppsSmsPortalBuilder` | `suite.AddSmsPortal(...)` | `CrestApps.Core.Omnichannel.Sms.Portal.Abstractions` |
| `CrestAppsTelnyxBuilder`, `CrestAppsAsteriskBuilder`, `CrestAppsDialpadBuilder` | `telephony.AddTelnyx(...)`, `.AddAsterisk(...)`, `.AddDialpad(...)` | the provider package (like `CrestApps.Core.AI.OpenAI` owns `AddOpenAI`) |

Every builder:

```csharp
public sealed class CrestAppsContactCenterBuilder
{
    public CrestAppsContactCenterBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    public IServiceCollection Services { get; }
}
```

## 2. Full composition (sample host)

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai.AddYesSqlStores().AddOpenAI())   // required only for AI SMS automation and Automated Voice
    .AddContactCenterSuite(suite => suite
        .AddPhoneNumbers()
        .AddWebSockets()
        .AddOmnichannel(omnichannel => omnichannel
            .AddYesSqlStores()
            .AddContacts()          // framework default contact model; Orchard binds content types instead
            .AddSubjects()          // framework default subject definitions/flows; Orchard binds content types instead
            .AddActivities()
            .AddChannelEndpoints()
            .AddCadences()
            .AddAutomation(builder.Configuration.GetSection("CrestApps:Omnichannel:Automation"))
            .AddSmsAutomation()
            .AddAutomatedVoice()
            .AddBackgroundWorkers())
        .AddTelephony(telephony => telephony
            .AddYesSqlStores()
            .AddExtensions()
            .AddSoftPhone()
            .AddLocalEncryptedRecordingStore(options => options.RootPath = Path.Combine(appDataPath, "Recordings"))
            .ConfigureHubOptions<TelephonyHub>()
            .AddTelnyx(builder.Configuration.GetSection("CrestApps:Telephony:Telnyx"), telnyx => telnyx
                .AddYesSqlStores()
                .AddSms()
                .AddContactCenterVoice()
                .AddContactCenterMedia()
                .AddAiVoice())
            .AddAsterisk(builder.Configuration.GetSection("CrestApps:Telephony:Asterisk"), asterisk => asterisk
                .AddYesSqlStores()
                .AddContactCenterVoice()
                .AddContactCenterMedia())
            .AddDialpad(builder.Configuration.GetSection("CrestApps:Telephony:Dialpad"), dialpad => dialpad
                .AddContactCenterVoice())
            .AddBackgroundWorkers())
        .AddContactCenter(contactCenter => contactCenter
            .AddYesSqlStores()
            .AddAgentServices()
            .AddAgents()
            .AddAgentEntitlements()
            .AddBusinessHours()
            .AddQueues()
            .AddProviderInbox()
            .AddRealTime()
            .ConfigureHubOptions<ContactCenterHub>()
            .AddRecordingGovernance()
            .AddVoice()
            .AddInboundVoice()
            .AddVoiceMedia()
            .AddDialer()
            .AddPacedDialing()
            .AddRecording()
            .AddSecureCapture()
            .AddSupervision()
            .AddHealthChecks()
            .AddBackgroundWorkers())
        .AddTwilioSms(builder.Configuration.GetSection("CrestApps:Sms:Twilio"))   // CrestApps.Core.Sms.Twilio (D-16); Telnyx SMS comes from telnyx.AddSms()
        .AddSmsPortal(portal => portal
            .AddYesSqlStores()
            .AddKeywordReplies(builder.Configuration.GetSection("CrestApps:Sms:Portal:KeywordReplies"))
            .AddRoutedDistribution()
            .ConfigureHubOptions<SmsPortalHub>()
            .AddBackgroundWorkers())
        .AddSignalR(addStoreCommitterFilter: true)));
```

Startup in the sample host (`Program.cs`) after `Build()`:

```csharp
await app.Services.InitializeYesSqlSchemaAsync();                       // existing AI sample step
await app.Services.RunContactCenterSuiteSchemaMigrationsAsync();        // versioned schema create/upgrade for every registered pillar (D-12)
```

Endpoint mapping in the sample host (`Program.cs`), one method per Orchard endpoint file:

```csharp
app.MapHub<TelephonyHub>("/hubs/telephony");
app.MapHub<ContactCenterHub>("/hubs/contact-center");
app.MapHub<SmsPortalHub>("/hubs/sms-portal");
app.MapSoftPhoneDialerEndpoints()
   .MapSoftPhoneExtensionConfigurationEndpoint()
   .MapContactCenterAgentWorkspaceEndpoints()
   .MapContactCenterAgentSoftPhoneEndpoints()
   .MapContactCenterVoiceOfferEndpoints()
   .MapContactCenterVoiceIngressEndpoint()
   .MapContactCenterSupervisorDashboardEndpoints()
   .MapContactCenterSecureCaptureEndpoints()
   .MapContactCenterQueueSearchEndpoints()
   .MapContactCenterRecordingErasureEndpoint()
   .MapContactCenterVoiceMediaEndpoints()        // governed recording/voicemail playback + greeting upload (G7)
   .MapContactCenterHealthEndpoints()
   .MapTelnyxWebhookEndpoints()
   .MapTelnyxMediaStreamEndpoint()
   .MapTelnyxSmsWebhookEndpoint()
   .MapDialpadWebhookEndpoint()
   .MapTwilioSmsWebhookEndpoint()
   .MapOmnichannelSubjectActionEndpoints();
```

Endpoint methods take the same route patterns as today (defaults equal to the Orchard routes) and accept an optional `Action<RouteHandlerBuilder>` so hosts can add authorization or antiforgery filters; the framework adds the store-committer endpoint filter and the existing antiforgery behaviour (`ContactCenterEndpointAntiforgery` moves with them).

## 3. Feature methods and what each registers

Each row names the builder method, the `AddCore*` method behind it, the Orchard feature whose `Startup` currently registers those services, and the services. The implementer must derive the exact registration list from the named `Startup` class (the lists below are the framework-eligible registrations seen during the survey; UI registrations are omitted and stay in Orchard). "Default" means `TryAdd`; "Handler" means `TryAddEnumerable`.

### Suite-level

| Method | `AddCore*` | Registers |
| --- | --- | --- |
| `AddContactCenterSuite(...)` | `AddCoreHosting()` | `TryAddSingleton<TimeProvider>(TimeProvider.System)`, `IDistributedLockProvider` -> `LocalDistributedLockProvider`, `ITenantAccessor` -> `SingleTenantAccessor`, `IScopedWorkExecutor`, scoped `IAfterCommitTaskQueue`, `IStoreCommitter` decoration, `IUserDirectory` -> `ClaimsUserDirectory`, `AddCatalogManagers()`, `AddCoreServices()` |
| `AddPhoneNumbers()` | `AddCorePhoneNumbers()` | `IPhoneNumberService` -> `DefaultPhoneNumberService` (default) |
| `AddWebSockets()` | `AddCoreWebSockets()` | `IWebSocketConnectionRegistry` -> `InMemoryWebSocketConnectionRegistry` (default), `IRendezvousOwnerStore` in-memory default, `WebSocketsNode` |
| `AddSignalR(pathPrefix, addStoreCommitterFilter)` | `AddCoreSignalR` (existing) | SignalR + `HubRouteManager` |
| `AddTwilioSms(IConfiguration)` | `AddCoreTwilioSms(IConfiguration)` | `TwilioOptions`, `ISmsProvider` `TwilioSmsProvider` (named "Twilio"), `TwilioRequestValidator`, `TwilioInboundMessageParser`; `MapTwilioSmsWebhookEndpoint` dispatches inbound messages through the Omnichannel event pipeline exactly as the Orchard endpoint does today |
| `AddReports(r => r.AddCsvExport().AddExcelExport())` (Phase 4) | `AddCoreReports()` | `IReportManager`, `IReportExportManager`, export formats, `AddReport<T>()` |

### Omnichannel (`CrestAppsOmnichannelBuilder`)

| Method | `AddCore*` | Orchard feature / Startup | Registers |
| --- | --- | --- | --- |
| `AddOmnichannel(...)` itself | `AddCoreOmnichannel()` | `CrestApps.OrchardCore.Omnichannel` (`Omnichannel/Startup.cs`) | `IOmnichannelEventHandler` chain host, `IOmnichannelProcessor` chain host, message store contract, `IBusinessHoursGate` -> `AlwaysOpenBusinessHoursGate` (default). No contact/subject bindings: a host must call `AddContacts()`/`AddSubjects()` (framework model) or the Orchard equivalents; `AddCoreOmnichannelActivities()` validates at startup (`IStartupCheck`) that the contact and subject contracts are bound and fails with a clear message otherwise |
| `AddActivities()` | `AddCoreOmnichannelActivities()` | `Omnichannel.Activities` (`OmnichannelActivitiesStartup`) | `AddCatalogs()` equivalents for `OmnichannelActivity`, `OmnichannelActivityBatch`, `OmnichannelCampaign`, `OmnichannelCampaignGroup`, `OmnichannelDisposition`, `SubjectAction`; managers (`IOmnichannelActivityManager`); catalog handlers (`OmnichannelDispositionHandler`, `OmnichannelCampaignHandler`, `OmnichannelCampaignGroupHandler`, `SubjectActionHandler`, `OmnichannelActivityBatchHandler`); `ISubjectFlowSettingsService`; `IActivityDispositionService` -> `DefaultActivityDispositionService`; `ISubjectActionExecutor` -> `DefaultSubjectActionExecutor`; `SubjectActionCatalog`; `IActivityBatchLoadCoordinator` -> `DefaultActivityBatchLoadCoordinator`; `IAutomatedActivityCompletionService`; `IAutomatedVoiceActivitySettingsResolver`; `SubjectActionOptions` and `ActivityBatchSourceOptions` defaults; `AutomatedActivitiesProcessorCycle` |
| `AddContacts()` | `AddCoreOmnichannelContacts()` | none (Orchard uses content types) | the framework default contact model (D-4/D-11): `IContactDefinitionStore/Manager` + handler, `IOmnichannelContactStore/Manager` + handler, and the catalog-backed `IContactDefinitionProvider`, `IOmnichannelContactResolver`, `IOmnichannelContactWriter`, `IOmnichannelContactSearch`, `IOmnichannelContactTypeProvider`. Orchard calls `AddOrchardCoreOmnichannelContacts()` (in `CrestApps.OrchardCore.Omnichannel.Core`) instead, binding the same five contracts to `OmnichannelContactPart`/content items |
| `AddSubjects()` | `AddCoreOmnichannelSubjects()` | none (Orchard uses content types) | `ISubjectDefinitionStore/Manager` + handler, `IOmnichannelSubjectStore/Manager` + handler, catalog-backed `ISubjectDefinitionProvider`, `IOmnichannelSubjectAccessor`, `ISubjectFlowSettingsService` (`DefinitionSubjectFlowSettingsService`). Orchard calls `AddOrchardCoreOmnichannelSubjects()` instead (`OmnichannelSubjectPart` content types, `ContentTypeSubjectFlowSettingsService`) |
| `AddChannelEndpoints()` | `AddCoreOmnichannelChannelEndpoints()` | `Omnichannel.ChannelEndpoints` (`ChannelEndpointsStartup`) | `IOmnichannelChannelEndpointStore/Manager`, `OmnichannelChannelEndpointHandler`, `ChannelEndpointSourceOptions`, `AddChannelEndpointSource(channel, ...)` (existing extension moves) |
| `AddCadences()` | `AddCoreOmnichannelCadences()` | part of `Managements` | `Cadence` catalog + `CadenceHandler` |
| `AddAutomation(IConfiguration)` | `AddCoreOmnichannelAutomation(IConfiguration)` | `AISubjectFlowStartup` (`[RequireFeatures("CrestApps.OrchardCore.AI")]`) | `OmnichannelAutomationOptions` (+ `OmnichannelAutomationOptionsValidator`), `IAutomatedConversationGate` -> `InMemoryAutomatedConversationGate`, `IOmnichannelHandoffTurn` -> `OmnichannelHandoffTurn` (default), `OmnichannelAutomationHelper`, `OmnichannelHandoffHelper`, `TransferToAgentTool` via `AddCoreAITool` |
| `AddSmsAutomation()` | `AddCoreOmnichannelSmsAutomation()` | `CrestApps.OrchardCore.Omnichannel.Sms` | `IOmnichannelProcessor` handler `SmsOmnichannelProcessor`, `IOmnichannelEventHandler` `SmsOmnichannelEventHandler`, `SmsOwedReplyRecoveryCycle`, `SmsReEngagementCycle`, Twilio validator + `ISmsInboundMessageParser`, redaction for the address set, embedded prompt templates |
| `AddAutomatedVoice()` | `AddCoreOmnichannelAutomatedVoice()` | `CrestApps.OrchardCore.Omnichannel.Voice` (`Startup`, `RealtimeVoiceStartup`) | `IVoiceAgentConversationLoop` -> `VoiceAgentConversationLoop`, `IRealtimeVoiceConversationRunner` -> `NoRealtimeVoiceConversationRunner` (default); `AddRealtimeAutomatedVoice()` replaces it with `RealtimeVoiceConversationRunner` (Orchard's `RealtimeVoiceStartup` calls this when the realtime AI feature is on) |
| `AddEventGrid(IConfiguration)` (optional) | `AddCoreOmnichannelEventGrid` | `Omnichannel.EventGrid` | `EventGridOptions` + endpoint handler |
| `AddYesSqlStores()` | `AddCoreOmnichannelStoresYesSql()` | migrations/index providers in `Omnichannel`, `Managements` | `IOmnichannelActivityStore` -> `YesSqlOmnichannelActivityStore`, batch/campaign/group/disposition/endpoint/cadence/subject-action/message stores, index providers (`OmnichannelActivityIndexProvider`, `OmnichannelActivityBatchIndexProvider`, `CadenceIndexProvider`, message index provider) |
| `AddBackgroundWorkers()` | `AddCoreOmnichannelBackgroundWorkers()` | n/a | hosted runners for the Omnichannel cycles |

### Telephony (`CrestAppsTelephonyBuilder`)

| Method | `AddCore*` | Orchard feature / Startup | Registers |
| --- | --- | --- | --- |
| `AddTelephony(...)` itself | `AddCoreTelephony()` | `CrestApps.OrchardCore.Telephony` (`Telephony/Startup.cs`) | `IProviderIdentityResolver` -> `ProviderIdentityResolver` (singleton default), redaction for `LogDataClassifications.AddressSet`, `IVoiceIngressGate` -> `VoiceIngressGate`, `INormalizedVoiceEventIngestor` -> `NormalizedVoiceEventIngestor`, `INormalizedVoiceEventHandler` handler `TelephonyCallHistoryVoiceEventHandler`, `ITelephonyProviderResolver` -> `DefaultTelephonyProviderResolver`, `IVoiceAgentMediaProviderResolver` -> `VoiceAgentMediaProviderResolver`, `IOutboundCallScreeningService` -> `DefaultOutboundCallScreeningService`, `IDialDestinationPolicy` -> `DefaultDialDestinationPolicy` (default), `ITransferTargetPolicy` -> `DefaultTransferTargetPolicy` (default), `ITelephonyService` -> `DefaultTelephonyService`, `ITelephonyCommandExecutor` -> `DefaultTelephonyCommandExecutor`, `IIncomingCallDispatcher` -> `DefaultIncomingCallDispatcher`, `TelephonySettings` options + `TelephonySettingsConfiguration` post-configure, `TelephonyProviderOptions`, `TelephonyCoordinationOptions`, `ITelephonyUserTokenStore` -> `DefaultTelephonyUserTokenStore`, `ITelephonyAuthenticationService` -> `DefaultTelephonyAuthenticationService`, `ITelephonyInteractionStore` -> `DefaultTelephonyInteractionStore`, `ITelephonyInteractionSynchronizationService` -> `TelephonyInteractionSynchronizationService`, `TelephonyInteractionReconciliationCycle`, `IRecordingMediaStore` default `UnconfiguredRecordingMediaStore` (throws with guidance) |
| `AddExtensions()` | `AddCoreTelephonyExtensions()` | same Startup (extension block) | `ITelephonyExtensionStore/Manager/Resolver` |
| `AddSoftPhone()` | `AddCoreTelephonySoftPhone()` | `Telephony.SoftPhone.Core` (`SoftPhoneCoreStartup`) | `SoftPhoneCountries`, `ISoftPhoneRegistrationConfigContributor` chain, soft phone config service, `TelephonyPkceGenerator`, soft phone dialer endpoint services |
| `AddLocalEncryptedRecordingStore(Action<LocalRecordingMediaStoreOptions>)` | `AddCoreTelephonyLocalRecordingStore` | recording store block in `Telephony/Startup.cs` (`services.AddSingleton<IRecordingMediaStore>(sp => ...)`) | `LocalEncryptedRecordingMediaStore` (singleton), `IRecordingMediaStoreInitializer` |
| `AddAzureBlobRecordingStore(IConfiguration)` (optional) | `AddCoreTelephonyAzureBlobRecordingStore` | `Telephony.Azure` | blob store + options |
| `AddProvider<TProvider>(name)` | `AddTelephonyProvider<T>` (existing in abstractions) | provider modules | adds a `TelephonyProviderTypeOptions` entry |
| `ConfigureHubOptions<THub>()` | `ConfigureCrestAppsTelephonyHubOptions<THub>()` | n/a | `HubOptions<THub>` (message size, timeouts as today) |
| `AddTelnyx(IConfiguration, Action<CrestAppsTelnyxBuilder>)` | `AddCoreTelnyx(IConfiguration)` | `CrestApps.OrchardCore.Telnyx` (`Telnyx/Startup.cs`) | named `HttpClient`s (`TelnyxConstants.ProviderTechnicalName`, `TelnyxApiClient`) with resilience, `TelnyxApiRetryPolicy`, `TelnyxOptions` from configuration, `AddTelephonyProvider<TelnyxTelephonyProvider>`, `ITelnyxWebhookService`, `ITelnyxProvisioningApiService`, `ITelnyxTelephonyCredentialIssuer`, `ITelnyxAgentEndpointResolver`, `ISoftPhoneCredentialRegistrar/Revoker`, `ISoftPhoneRegistrationConfigContributor`, `ITelnyxInboundCallRouter` -> `TelnyxDirectInboundCallRouter` (default; the Contact Center router replaces it via `AddContactCenterVoice()`), `IInboundVoiceDigitsSink` -> `NoInboundVoiceDigitsSink` (default), `TelnyxOrphanedCallReconciler` + cycle, `ISoftPhoneHealthMetrics` + canary cycle, `IProviderIdentityProvider` -> `TelnyxProviderIdentityProvider` |
| `telnyx.AddSms()` | `AddCoreTelnyxSms(IConfiguration)` | `Telnyx.Sms` (`SmsStartup`) | `TelnyxSmsOptions`, framework `ISmsProvider` `TelnyxSmsProvider`, `TelnyxSmsWebhookParser` |
| `telnyx.AddContactCenterVoice()` | `AddCoreTelnyxContactCenterVoice()` | `Telnyx/DialerStartup.cs` (`[RequireFeatures(ContactCenter.Voice)]`) | `IContactCenterVoiceProvider` `TelnyxContactCenterVoiceProvider`, `ContactCenterTelnyxInboundCallRouter` (replace), `IContactCenterFeatureLifecycleParticipant` `TelnyxContactCenterFeatureLifecycleParticipant`, `ITelnyxOutboundBridgeOrchestrator`, `IProviderWebhookInboxHandler` `TelnyxWebhookInboxHandler`, `TelnyxIvrProvider`, `TelnyxQueueTreatmentProvider`, `ITelnyxVoicemailRecordingStarter`, `ITelnyxRecordingIngestService` + job store contract + cycle |
| `telnyx.AddContactCenterMedia()` | `AddCoreTelnyxContactCenterMedia()` | `TelnyxContactCenterMediaStartup` | `IContactCenterVoiceMediaProvider` `TelnyxContactCenterVoiceMediaProvider`, `IVoiceMediaProvisioner` `TelnyxVoiceMediaProvisioner`, media stream endpoint services |
| `telnyx.AddAiVoice()` | `AddCoreTelnyxAiVoice()` | `Telnyx.AiVoice` (`AiVoiceStartup`) | `ITelnyxVoiceAgentClient`, `TelnyxVoiceAgentMediaProvider`, `TelnyxAiVoiceConversationHandler`, `IOmnichannelProcessor` `VoiceOmnichannelProcessor` |
| `telnyx.AddYesSqlStores()` | `AddCoreTelnyxStoresYesSql()` | migrations in the Telnyx module | `ITelnyxAgentCredentialStore`, `ITelnyxRecordingIngestJobStore` YesSql stores + index providers |
| `AddAsterisk(IConfiguration, Action<CrestAppsAsteriskBuilder>)`, `asterisk.AddContactCenterVoice()`, `asterisk.AddContactCenterMedia()`, `asterisk.AddYesSqlStores()` | `AddCoreAsterisk*` | `Asterisk/Startup.cs`, `AsteriskContactCenterVoiceStartup`, `AsteriskContactCenterMediaStartup` | named `HttpClient` (`AsteriskConstants.HttpClientName`), `DefaultAsteriskOptions` (+ validator, `ValidateOnStart`), ARI client, PJSIP credential issuer/lease store/realtime store, realtime listener + ingestion + dispatcher, providers, reconcilers, teardown, registries, cycles, stores |
| `AddDialpad(IConfiguration, Action<CrestAppsDialpadBuilder>)`, `dialpad.AddContactCenterVoice()` | `AddCoreDialpad*` | `DialPad/Startup.cs`, `DialerStartup.cs` | named `HttpClient`, `DialpadOptions`, `IDialpadWebhookService`, `IDialpadWebhookApiService`, `IDialpadInboundCallRouter` (direct default; Contact Center replaces), JWT validator, provider, contact center voice provider, lifecycle participant, inbox handler |
| `AddYesSqlStores()` | `AddCoreTelephonyStoresYesSql()` | Telephony migrations/index providers | extension, interaction, user-connection index providers and stores |
| `AddBackgroundWorkers()` | `AddCoreTelephonyBackgroundWorkers()` | n/a | runners for Telephony + provider cycles registered so far |

### Contact Center (`CrestAppsContactCenterBuilder`)

| Method | `AddCore*` | Orchard feature / Startup | Registers |
| --- | --- | --- | --- |
| `AddContactCenter(...)` itself | `AddCoreContactCenter()` | `CrestApps.OrchardCore.ContactCenter` (`ContactCenter/Startup.cs`, 33 registrations) | `ContactCenterCoordinationOptions` + validator, `ContactCenterTopologyOptions`/`ContactCenterTopologyState`, `IContactCenterStartupCheck` topology evaluator, interaction store/manager/event store/upcast service, event publisher (`DefaultContactCenterEventPublisher`), outbox + store + `OutboxDispatchCycle`, processed-event store + dedup service, projection checkpoint store, metrics stores/services + `ContactCenterMetricRollupCycle`, retention service + `ContactCenterRetentionCycle` + data governance catalog, `IContactCenterScopeExecutor` -> `DefaultContactCenterScopeExecutor` (default), `IProviderIdentityResolver` (default), `ICallbackService` -> `NoCallbackService` (default), `IAgentWorkStateHealingService` -> `NoAgentWorkStateHealingService` (default), `IQueuedVoiceWorkOfferService` -> `NoQueuedVoiceWorkOfferService` (default), `IDialerProfileReader` -> `NullDialerProfileReader` (default), `IBusinessHoursGate` -> `AlwaysOpenBusinessHoursGate` (default), `EntryPointResolverChain`, `IProviderCallStateSynchronizationService` -> no-op default + `Lazy<>`, `IContactCenterFeatureWorkManager` -> `ContactCenterFeatureWorkManager`, lifecycle coordinator, `ContactCenterCapabilities` options, `IContactCenterAgentBarBuilder` contract only |
| `AddAgentServices()` | `AddCoreContactCenterAgentServices()` | `ContactCenter.AgentServices` (`AgentServicesStartup`) | `IAgentProfileStore/Manager`, agent profile handler |
| `AddAgents()` | `AddCoreContactCenterAgents()` | `ContactCenter.Agents` (`AgentsStartup`) | `IAgentSignOutHandler` (S21; Orchard's cookie configuration and the MVC sign-out event call it), plus: | `IAgentAvailabilityService`, `IAgentPresenceManager` -> `AgentPresenceManagerService`, `IAgentSessionStore/Manager/Service`, `IAgentStateReasonCodeStore/Manager` + handler, `IAgentAvailabilityRecoveryService` + cycle, `AgentSessionCleanupCycle`, `AgentAvailabilityOptions`, `IAgentEntitlementPolicy` -> permissive default, `SoftPhoneCredentialRevocation` on logout |
| `AddAgentEntitlements()` | `AddCoreContactCenterAgentEntitlements()` | `ContactCenter.AgentEntitlements` (`AgentEntitlementsStartup`) | replaces `IAgentEntitlementPolicy` with the enforcing policy, `AgentAllowedQueue` store/index contract |
| `AddBusinessHours()` | `AddCoreContactCenterBusinessHours()` | `ContactCenter.BusinessHours` (`BusinessHoursStartup`) | `IBusinessHoursCalendarStore/Manager` + handler, `IBusinessHoursService` -> `DefaultBusinessHoursService`, `IBusinessHoursGate` -> `BusinessHoursGate` (replace) |
| `AddQueues()` | `AddCoreContactCenterQueues()` | `ContactCenter.Queues` (`QueuesStartup`, 22 registrations) | queue/group/skill stores, managers, handlers; `IQueueItemStore/Manager`, `QueueItemPrioritizer`; `IActivityReservationStore/Manager/Service` + `ReservationExpiryCycle`; `IActivityAssignmentService`, `IActivityRoutingService` + routing strategies (`CapacityRoutingStrategy`, `LeastBusyRoutingStrategy`, `LongestIdleRoutingStrategy`, `PreferredSkillsRoutingStrategy`, `RequiredSkillsRoutingStrategy`, `RoundRobinRoutingStrategy`, `StickyAgentRoutingStrategy`), `CampaignRoutingQueue`; `IAgentWorkStateHealingService` -> `AgentWorkStateHealingService` (replace); `IAgentWorkSelector`; `IQueuedCallbackService`; `IQueueTreatmentProvider` -> `NoQueueTreatmentProvider` (default); `IQueueTreatmentService` + `QueueTreatmentCycle`; `IQueueLimitService`; `IWaitingCallVoicemailSink` -> `NoWaitingCallVoicemailSink` (default); `IContactCenterConfigurationCache`; `EstimatedWaitTimeCalculator`; `OverflowScheduler`; work-state store/manager/service/projector; `IAgentQueueMembershipReader`; `DirectRingTimeoutCycle`; `OrphanedActivityRecoveryCycle`; lifecycle participant for `ContactCenterCapabilities.Queues` |
| `AddProviderInbox()` | `AddCoreContactCenterProviderInbox()` | `ContactCenter.ProviderInbox` (`ProviderInboxStartup`) | `IProviderWebhookInbox` -> `ProviderWebhookInbox`, store, `IProviderWebhookIngressLimiter`, `ProviderWebhookInboxCycle`, `ProviderWebhookIngressOptions` |
| `AddRealTime()` | `AddCoreContactCenterRealTime()` | `ContactCenter.RealTime` (`RealTimeStartup`) | `ContactCenterHubConnectionRegistry`, `IContactCenterRealTimeNotifier` -> `ContactCenterRealTimeNotifier<THub>` (registered by `ConfigureHubOptions<THub>()`/`UseHub<THub>()`), `IContactCenterEventHandler` handler `ContactCenterRealTimeEventHandler`, real-time lifecycle participant |
| `ConfigureHubOptions<THub>()` / `UseHub<THub>()` | `ConfigureCrestAppsContactCenterHub<THub>()` | n/a | `HubOptions<THub>` and binds the notifier to `IHubContext<THub, IContactCenterHubClient>` |
| `AddRecordingGovernance()` | `AddCoreContactCenterRecordingGovernance()` | `ContactCenter.Recording.Core` (`RecordingCoreStartup`) | `IRecordingAccessGovernanceService`, `IRecordingGovernancePolicy`, `IRecordingErasureGuard` |
| `AddVoice()` | `AddCoreContactCenterVoice()` | `ContactCenter.Voice` (`VoiceStartup`, `VoiceSoftPhoneStartup`) | `IVoiceContactCenterCallRouter` -> `VoiceContactCenterCallRouter`, call session store/manager, call topology projector, `IContactCenterVoiceProviderResolver`, `IContactCenterCallCommandService`, call control authorization, provider command store/manager/processor/state service + executors (`AnswerProviderCommandTypeExecutor`, `DialProviderCommandTypeExecutor`, `ProviderCallActionCommandTypeExecutor`, `RejectProviderCommandTypeExecutor`, `SendToVoicemailProviderCommandTypeExecutor`), `ProviderCommandRecoveryCycle`, provider voice event service/sink/inbox handler, offer synchronization, `IProviderCallStateSynchronizationService` (replace) + `ProviderCallStateReconciliationCycle`, transfer/consult/assist/monitoring services, `IPendingIncomingCallOfferService`, `IQueuedVoiceWorkOfferService` -> `QueuedVoiceWorkOfferService` (replace), `IVoiceQueueOfferService`, `IVoiceAgentHandoffService`, `IDirectHoldTimeoutService`, `IContactCenterAgentLegFailureService`, incoming call context provider/factory, `ContactCenterTransferTargetPolicy` (replace `ITransferTargetPolicy`), `ContactCenterManualCallScreener`, `ContactCenterSoftPhoneEventHandler`, reoffer/offer-on-availability handlers, voice lifecycle participant, `IContactCenterStartupCheck` base-voice verification |
| `AddInboundVoice()` | `AddCoreContactCenterInboundVoice()` | `ContactCenter.InboundVoice` (`InboundVoiceStartup`) | entry point store/manager/handler, resolvers (`EntryPointResolver`, `EntryPointFlowResolver`, `EntryPointRoutingPlanner`, ordered resolver chain), `IInboundVoiceService`, `IInboundVoiceCallProcessor` -> `InboundVoiceCallProcessor`, `IInboundContactLookup` -> `InboundContactLookup` (over the contact seam), inbound priority resolver + contributors (`RepeatCallerPriorityContributor`, `ReturningCallbackPriorityContributor`), IVR execution/state machine/validator, `IIvrProvider` -> `NoIvrProvider` (default), inbound event sink/probe/digits sink, `IWaitingCallVoicemailSink` -> `InboundVoiceWaitingCallVoicemailSink` (replace) |
| `AddVoiceMedia()` | `AddCoreContactCenterVoiceMedia()` | `ContactCenter.Voice.Media` (`VoiceMediaStartup`) | `IContactCenterVoiceMediaProviderResolver`, voice media item store/manager/handler |
| `AddDialer()` | `AddCoreContactCenterDialer()` | `ContactCenter.Dialer` (`DialerStartup`) | dialer profile store/manager/reader (replace `IDialerProfileReader`) + handler, `IDialerService`, `IDialerAttemptService`, `IDialerAttemptCompensationService`, `IDialerEligibilityService` -> `DefaultDialerEligibilityService`, `IDialerAbandonmentPolicyService`, `IDialerStrategyResolver` + manual/preview strategies, `ICallbackService` -> `CallbackService` (replace) + store/manager + `CallbackDispatchCycle`, `IProviderCommandDispatchValidator` -> `DialerProviderCommandDispatchValidator`, `ContactCenterActivityDialerContributor`, `ExternalDestinationPolicy`, `INationalDoNotCallRegistry` -> `ContactPreferenceDoNotCallRegistry` (default, D-13; Orchard's DNC module replaces it), `ManualDialingComplianceOptions`, `ContactCenterComplianceOptions` (with `FailClosedWithoutNationalRegistry`) |
| `AddPacedDialing()` | `AddCoreContactCenterPacedDialing()` | `ContactCenter.Dialer.Paced` (`DialerPacedStartup`) | `PowerDialerStrategy`, `ProgressiveDialerStrategy` (+ `PredictiveDialerStrategy`/`PredictiveDialerPacing` if registered today), `DialerPacingCycle` |
| `AddRecording()` | `AddCoreContactCenterRecording()` | `ContactCenter.Recording` (`RecordingStartup`, 12 registrations) | `IContactCenterRecordingService`, `IAgentRecordingControlService`, `ContactCenterRecordingSettings` options, recording event handlers (`RecordingMediaDeletionHandler`), `ISecurePauseAutoResumeService` + cycle |
| `AddSecureCapture()` | `AddCoreContactCenterSecureCapture()` | `ContactCenter.SecureCapture` (`SecureCaptureStartup`) | secure capture session store/manager/service, `ISecureCaptureTokenSink` -> `UnconfiguredSecureCaptureTokenSink` (default) / `MaskingSecureCaptureTokenSink`, `SecureCaptureSettings`, `SecureCaptureAccessToken`, `SecureCaptureExpiryCycle` |
| `AddSupervision()` | `AddCoreContactCenterSupervision()` | `ContactCenter.Supervision` (`SupervisionStartup`) | `ISupervisorQueueAuthorizationService`, `IContactCenterMonitoringService`, supervisor dashboard snapshot services |
| `AddAnalytics()` | `AddCoreContactCenterAnalytics()` | `AnalyticsStartup` (`[RequireFeatures(Queues, Reports)]`) | `IContactCenterReportingService`, report models; the `IReport` providers themselves stay Orchard |
| `AddHealthChecks(Action<IHealthChecksBuilder> = null)` | `AddCoreContactCenterHealthChecks()` | `ContactCenterHealthChecksStartup`, `ContactCenterQueuesHealthChecksStartup`, `VoiceHealthChecksStartup` | `ContactCenterHealthCheckOptions`, `NodeServingStateTracker`, `SharedHealthEndpointHazardState`, `SharedHealthCheckEndpointGuard`, all checks in `HealthChecks/` except Redis/backplane, `MapContactCenterHealthEndpoints`; the Orchard startups keep `AddContactCenterRedisHealthChecks()` |
| `AddYesSqlStores()` | `AddCoreContactCenterStoresYesSql()` | 27 index providers + 28 migrations | all `ContactCenter` collection stores and index providers, `ContactCenterStoreOptions` |
| `AddBackgroundWorkers()` | `AddCoreContactCenterBackgroundWorkers()` | n/a | runners for the 16 Contact Center cycles |

### SMS Portal (`CrestAppsSmsPortalBuilder`)

| Method | `AddCore*` | Orchard feature / Startup | Registers |
| --- | --- | --- | --- |
| `AddSmsPortal(IConfiguration = null, ...)` itself | `AddCoreSmsPortal(IConfiguration)` | `CrestApps.OrchardCore.Omnichannel.Sms.Portal` (`Startup.cs`) | `SmsPortalOptions`, conversation/template/broadcast store+manager+service+handlers, `ISmsConversationAuthorizationService` (+ `Lazy<>`), `ISmsConversationRouter` -> `SmsConversationRouter` + routers (`NumberRouteRouter`, `AutoReplyRouter`, `RoutedQueueRouter`), `ISmsQueuePolicyReader` -> `NullSmsQueuePolicyReader` (default), `ISmsFirstResponseSlaService` + cycle, `ISmsContactTimeZoneResolver` default (UTC) / contact seam, `SmsQuietHoursGuard`, `ISmsAgentAvailabilityService`, `ISmsAgentPresenceTracker` -> `DistributedCacheSmsAgentPresenceTracker`, `SmsInboundProcessor` (+ `ISmsInboundProcessor`, `IOmnichannelEventHandler`), `IProviderWebhookInboxHandler` `SmsInboundInboxHandler`, `ISmsContactResolver` default over `IOmnichannelContactResolver`, `ISmsRealTimeNotifier` -> `NullSmsRealTimeNotifier` (default), `ISmsRoutingStrategy` -> `NoSmsRoutingStrategy` (default), `IOmnichannelHandoffService` `SmsAgentHandoffService`, `ISmsOutboundOutbox` + cycle, `SmsBroadcastCycle`, `ISmsDispatcher` -> `SmsDispatcher`, `AddChannelEndpointSource(OmnichannelConstants.Channels.Sms, ...)`, redaction |
| `AddKeywordReplies(IConfiguration)` | `AddCoreSmsPortalKeywordReplies(IConfiguration)` | same Startup | `SmsKeywordReplySettings`, `SmsKeywordPolicy` |
| `AddRoutedDistribution()` | `AddCoreSmsPortalRoutedDistribution()` | `SmsPortal.RoutedDistribution` (`RoutedDistributionStartup`, `WorkDistributionStartup`) | `ISmsRoutingStrategy` -> `LeastLoadedSmsRoutingStrategy` (replace), `ISmsQueuePolicyReader` -> `ActivityQueueSmsQueuePolicyReader` (replace), `ISmsRoutedReassignmentService` + cycle, `SmsRoutedDistributionOptions` |
| `ConfigureHubOptions<THub>()` / `UseHub<THub>()` | `ConfigureCrestAppsSmsPortalHub<THub>()` | n/a | `HubOptions<THub>`, `ISmsRealTimeNotifier` -> `SmsRealTimeNotifier<THub>` (replace) |
| `AddYesSqlStores()` | `AddCoreSmsPortalStoresYesSql()` | migrations/index providers | conversation/template/broadcast stores + index providers, `SmsPortalStoreOptions` |
| `AddBackgroundWorkers()` | `AddCoreSmsPortalBackgroundWorkers()` | n/a | runners for the 4 portal cycles |

## 4. Orchard `Startup` shape after Phase 1

Before (`QueuesStartup.ConfigureServices`, abbreviated): 22 explicit registrations plus deployment/recipe/workflow/health startups.

After:

```csharp
[Feature(ContactCenterConstants.Feature.Queues)]
public sealed class QueuesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddCoreContactCenterQueues()
            .AddCoreContactCenterQueueStoresYesSql();   // index providers + stores for this feature's documents

        // Orchard-only glue.
        services.AddDataMigration<ActivityQueueIndexMigrations>()
            .AddDataMigration<ActivityQueueGroupIndexMigrations>()
            .AddDataMigration<ActivityReservationIndexMigrations>()
            .AddDataMigration<QueueItemIndexMigrations>()
            .AddDataMigration<ContactCenterSkillIndexMigrations>()
            .AddDataMigration<ContactCenterWorkStateIndexMigrations>()
            .AddDataMigration<ContactCenterQueuesLegacyDocumentTypeNameMigrations>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, QueueTreatmentBackgroundTask>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, ReservationExpiryBackgroundTask>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, DirectRingTimeoutBackgroundTask>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, OrphanedActivityRecoveryBackgroundTask>());
        services.AddDisplayDriver<ActivityQueue, ActivityQueueDisplayDriver>();
        services.AddDisplayDriver<ActivityQueueGroup, ActivityQueueGroupDisplayDriver>();
        services.AddDisplayDriver<ContactCenterSkill, ContactCenterSkillDisplayDriver>();
        services.AddNavigationProvider<ContactCenterAdminMenu>();
        services.AddPermissionProvider<ContactCenterQueuesPermissionProvider>();
        services.AddScoped<IAuthorizationHandler, ContactCenterQueueOperationAuthorizationHandler>();
    }
}
```

Rules for the rewrite:

1. The first statement of every `ConfigureServices` is the framework call(s). Everything after is Orchard-only.
2. Orchard never calls `AddBackgroundWorkers()`; it registers its `IBackgroundTask` wrappers instead.
3. `services.Replace(...)` is used only for Orchard adapters of the host seams (S2, S3, S4, S9, S12) and stays in `CrestApps.OrchardCore.Core`'s single `AddCrestAppsOrchardCoreHosting()` method, called from each pillar's base `Startup` (idempotent through `TryAdd`/`Replace`).
4. Per-feature `AddCore*StoresYesSql()` granularity mirrors the feature split so a feature's index providers register only when the feature is enabled (as today). The builder-level `AddYesSqlStores()` registers all of them at once for standalone hosts.
5. `Configure(IApplicationBuilder, IEndpointRouteBuilder, IServiceProvider)` calls the framework `Map*` endpoint methods (with Orchard's route prefix and authorization filters passed as options) and `routes.MapHub<ContactCenterHub>(SignalRHubRoutes.GetHubPath<ContactCenterHub>())` exactly as today.
6. Manifests are untouched.

Repeat for every feature startup listed in [appendix A](appendix-a-file-disposition.md).
