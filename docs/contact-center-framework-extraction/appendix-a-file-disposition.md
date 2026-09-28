# Appendix A - File and folder disposition

Legend: **M** = move to the framework (namespace per appendix B), **S** = stay in Orchard, **X** = split (part moves, part stays), **A** = adapt (moved but rewritten against a host seam; the seam id from [03-host-seams.md](03-host-seams.md) is given), **D** = delete (superseded by a framework type).

Disposition rules, applied in this order when a file is not listed explicitly:

1. Files under `Views`, `ViewModels` (Orchard editors), `Drivers`, `Recipes`, `Deployments`, `Workflows`, `Reports` (Orchard `IReport`), `Migrations`, `Manifest.cs`, `*Startup.cs`, `*AdminMenu.cs`, `*PermissionProvider.cs`, `*Permissions.cs`, `*ResourceConfiguration.cs`, `*ShapeTableProvider.cs`, `*DisplayDriver.cs`, `Filters` (MVC result filters for admin chrome), `ModelBinders`: **S**.
2. Files that use `ContentItem`/`ContentPart`/`IContentManager`/`IContentDefinitionManager`: **S** unless listed as **A** (S11).
3. `IBackgroundTask` classes: **X** - the pass logic becomes a framework cycle, the task class stays as a wrapper.
4. `Hubs/*Hub.cs`: **X** - base class moves, concrete hub stays. `I*HubClient.cs` and `*HubScopeContext.cs`: **M**.
5. `Endpoints/*`: **M** as `Map*` methods; Orchard `Configure` calls them.
6. `Indexes/*Index.cs`: **M** to the store package; `Indexes/*IndexProvider.cs`: **M** to the store package (or the provider package for Telnyx/Asterisk); `Migrations/*Migrations.cs`: **S** but rewritten to call `Create*IndexSchemaAsync`; `Migrations/*MigrationSql.cs`: **M** to the store package.
7. `Controllers/*`: **S**. `ViewModels` used only by endpoints (`AgentWorkspaceEndpoints.ViewModels.cs`): **M** as endpoint DTOs.
8. Everything else in `Services`, `Handlers`, `Models`, `HealthChecks`, `Telemetry`: **M**, applying S1-S20 as needed.
9. Tests: a test moves when the type under test moved; test doubles move with the tests that use them.

## A.1 `src/Abstractions`

| Project | Disposition |
| --- | --- |
| `CrestApps.OrchardCore.ContactCenter.Abstractions` | **X**: all `Models/*`, `Services/*`, `I*.cs`, `ContactCenterConstants.*` (except `Features`), `ContactCenterFeatureLifecycleOptions`, `ContactCenterHandlerReplaySafety`, `ContactCenterProcessLivenessOptions` -> **M**. `ContactCenterConstants.Features.cs` -> **S** (feature ids). `ContactCenterProcessHealthServiceCollectionExtensions`, `ContactCenterProcessHealthApplicationBuilderExtensions`, `ContactCenterProcessLivenessPathValidator` -> **X**: the middleware and options move, the `IShellSettingsManager` path validation stays as an Orchard `IStartupCheck`. Receives `ContactCenterPermissions` from Core (**S**) |
| `CrestApps.OrchardCore.Telephony.Abstractions` | **X**: everything **M** except `TelephonyPermissions.cs` (**S**) and `TelephonyConstants.Feature` (**S**, split out of `TelephonyConstants.cs`). `Models/TelephonyInteraction.cs` **A** (S7). `Extensions/TelephonyServiceCollectionExtensions.cs` **M** |
| `CrestApps.OrchardCore.Omnichannel.Sms.Portal.Abstractions` | **X**: `Models/*`, `Notifications/*` **M**; `Services/ISmsDispatcher.cs`, `ISmsDispatchProvider.cs` **A** (S12); `SmsPortalConstants.cs` **X** (feature ids stay) |
| `CrestApps.OrchardCore.PhoneNumbers.Abstractions` | **M** (all 10 files), project deleted |
| `CrestApps.OrchardCore.WebSockets.Abstractions` | **M** (2 files) into `CrestApps.Core.WebSockets`, project deleted |
| `CrestApps.OrchardCore.DncRegistry.Abstractions` | **X**: `INationalDoNotCallRegistry`, `NumberSearchContext`, `DoNotCallScreeningException` **M** to `CrestApps.Core.Omnichannel.Abstractions/Compliance`; the Orchard project becomes a forwarder or is deleted and the DNC modules reference the framework (S20) |
| `CrestApps.OrchardCore.Abstractions` | **S** (framework copies of `LogDataClassifications`, `DictionaryDocument<T>` per S17) |

## A.2 `src/Core`

### `CrestApps.OrchardCore.ContactCenter.Core` -> `CrestApps.Core.ContactCenter` (+ store package)

| Folder / file | Disposition |
| --- | --- |
| `Models/**` (139 + `Reports/` 16) | **M**. `Interaction.cs` **A** (S7: drop `IEntity`) |
| `Indexes/*Index.cs` (27) | **M** to `CrestApps.Core.Data.YesSql.ContactCenter/Indexes/ContactCenter` |
| `Services/*Store.cs` (all `DocumentCatalog` subclasses: `ActivityQueueStore`, `ActivityQueueGroupStore`, `ActivityReservationStore`, `AgentProfileStore`, `AgentSessionStore`, `AgentStateReasonCodeStore`, `BusinessHoursCalendarStore`, `CallSessionStore`, `CallbackRequestStore`, `ContactCenterEntryPointStore`, `ContactCenterMetricDeltaStore`, `ContactCenterMetricStore`, `ContactCenterOutboxStore`, `ContactCenterProcessedEventStore`, `ContactCenterProjectionCheckpointStore`, `ContactCenterSkillStore`, `ContactCenterWorkStateStore`, `DialerProfileStore`, `InteractionEventStore`, `InteractionStore`, `ProviderCommandStore`, `ProviderWebhookInboxStore`, `QueueItemStore`, `SecureCaptureSessionStore`, `VoiceMediaItemStore`) and `InteractionQueries`, `QueueItemQueries` | **M** to the store package as `YesSql*Store` (interfaces stay in the primitive) |
| `Services/I*.cs` | **M** to the primitive (`Services/`) |
| `Services/ContactCenterScopeExecutor.cs` | **S** (ShellScope adapter); framework gets `DefaultContactCenterScopeExecutor` (S4) |
| `Services/ContactCenterConfigurationCacheInvalidationHandler.cs` | **S** (S6) |
| `Services/ContactCenterConfigurationCache.cs` | **A** (S6) |
| `Services/{AgentRecordingControlService,RecordingGovernancePolicy,SecureCaptureService,SecurePauseAutoResumeService,TransferDestinationResolver}.cs` | **A** (S5 options; S8 for the authorization ones) |
| `Services/{SupervisorQueueAuthorizationService,CallControlAuthorizationService}.cs` | **A** (S8) |
| `Services/*` using `IDistributedLock` (`ActivityReservationService`, `ConsultTransferService`, `ContactCenterCallCommandService`, `ContactCenterEventDeduplicationService`, `ContactCenterMetricRollupService`, `AgentProfileLock`, `CallbackService`, `ProviderCommandProcessor`) | **A** (S2) |
| All other `Services/*.cs`, `Services/Retention/*` | **M** with S1 |
| `HealthChecks/*` | **M** except `ContactCenterRedisConnectivityHealthCheck` (lives in the module and stays **S**); `NodeServingStateTracker`, `SharedHealthCheckEndpointGuard`, `SharedHealthEndpointHazardState` **M** |
| `Telemetry/*` | **M** |
| `ContactCenterPermissions.cs` | **S** -> moved to the Orchard abstractions project |
| `ContactCenterStorage.cs` | **X**: `CollectionName` becomes `ContactCenterStoreOptions.CollectionName` (store package); the other constants move to `CrestApps.Core.ContactCenter/ContactCenterStorage.cs` (internal) |
| `AgentOfferKindHelper.cs`, `DialerActivitySourceHelper.cs`, `ExternalDestinationPolicy.cs` | **M** |
| `Properties/AssemblyInfo.cs` | **D** (InternalsVisibleTo declared in csproj for the new test project) |

### `CrestApps.OrchardCore.Omnichannel.Core` -> `CrestApps.Core.Omnichannel.Abstractions` / `CrestApps.Core.Omnichannel`

| Folder / file | Disposition |
| --- | --- |
| `Models/*` | **M** to Abstractions, except `OmnichannelContactPart`, `OmnichannelContactPartSettings`, `OmnichannelSubjectPart`, `OmnichannelSubjectPartSettings`, `PhoneNumberInfoPart`, `EmailInfoPart`, `OmnichannelContactInfoPart` (**S**); `OmnichannelActivityContainer`, `CompleteOmnichannelActivityContainer` **A** (S11); `OmnichannelMessage` **A** (S7); `SubjectActionExecutionContext` **A** (S11) |
| `Indexes/OmnichannelActivityIndex`, `OmnichannelActivityBatchIndex`, `CadenceIndex`, `OmnichannelMessageIndex` | **M** (store package) |
| `Indexes/OmnichannelContactIndex`, `OmnichannelContactCommunicationPreferenceIndex` | **S** |
| `Services/I*.cs`, `IOmnichannelEventHandler.cs`, `IOmnichannelProcessor.cs`, filter contexts | **M** to Abstractions |
| `Services/OmnichannelActivityStore.cs` | **A** (S15) -> store package `YesSqlOmnichannelActivityStore`; check for contact-index joins (S11) |
| `Services/OmnichannelActivityManager`, `OmnichannelChannelEndpointStore/Manager`, `OmnichannelAutomationHelper`, `OmnichannelAutomationOptionsValidator`, `OmnichannelHandoffHelper`, `OmnichannelHandoffTurn`, `OmnichannelSmsComplianceHelper`, `AlwaysOpenBusinessHoursGate`, `InMemoryAutomatedConversationGate` | **M** to the primitive |
| `Services/OmnichannelSubjectWriter.cs`, `SubjectFlowSettingsService.cs` | **A** (S11) |
| `Services/ContentDefinitionOmnichannelContactTypeProvider.cs` | **S** |
| `Extensions/OmnichannelChannelEndpointServiceCollectionExtensions.cs` | **M** |
| `OmnichannelConstants.cs` | **X**: `Features` stays, the rest moves |
| `StringExtensions.cs` | **M** to `CrestApps.Core.Support` if generic, else primitive |
| `ViewLocalizerExtensions.cs` | **S** |

### `CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core` -> `CrestApps.Core.Omnichannel.Sms.Portal`

| Folder / file | Disposition |
| --- | --- |
| `Models/*` | **M** (options classes keep names) |
| `Indexes/*` | **M** (store package `Indexes/SmsPortal`) |
| `Services/*Store.cs` (`SmsConversationStore`, `SmsTemplateStore`, `SmsBroadcastStore`) | **M** to the store package |
| `Services/{SmsConversationService,SmsInboundProcessor}.cs`, `Services/Routers/AutoReplyRouter.cs` | **A** (S11, S12) |
| `Services/SmsDispatcher.cs` | **A** (S12) |
| `Services/DistributedCacheSmsAgentPresenceTracker.cs` | **M** (uses `IDistributedCache` only) |
| `Services/SmsConversationAuthorizationService.cs` | **A** (S8) |
| everything else in `Services`, `Routers`, `Routing` | **M** with S1/S2/S3/S5 |
| `SmsPortalPermissions.cs` | **S** |
| `SmsPortalStorage.cs` | **X** -> `SmsPortalStoreOptions.CollectionName = "SmsWorkspace"` |

### `CrestApps.OrchardCore.Omnichannel.Voice.Core` -> `CrestApps.Core.Omnichannel.Voice`

All **M**; `VoiceAgentConversationLoop.cs` and `.Conclusion.cs` **A** (S11). `OmnichannelVoiceConstants.Feature` **S** (moves to the Orchard module).

### `CrestApps.OrchardCore.Telephony.Core` -> `CrestApps.Core.Telephony`

All **M** with S1/S2/S4/S5. `Indexes/*` -> store package `Indexes/Telephony`.

### `CrestApps.OrchardCore.Telnyx.Core` -> `CrestApps.Core.Telephony.Telnyx`

All **M** with S1/S3/S5/S13; `TelnyxSmsOptionsConfiguration` **S** if it reads Orchard SMS settings (otherwise **M**); `Indexes/*` -> `Data/YesSql` inside the provider package; `ContactCenterTelnyxInboundCallRouter`, `TelnyxContactCenterFeatureLifecycleParticipant` **A** (S18).

### `CrestApps.OrchardCore.PhoneNumbers.Core`

`Services/DefaultPhoneNumberService.cs`, `PhoneNumberServiceExtensions.cs` (if present here) **M**; everything else (verification part, settings, manager, handlers, permissions, index) **S**.

### `CrestApps.OrchardCore.SignalR.Core`

`TenantSignalRGroupName.cs`, `HubConnectionWork.cs` **M** (namespace `CrestApps.Core.SignalR`); `SignalRConstants.cs` **S**.

### `CrestApps.OrchardCore.YesSql.Core`

`Services/DocumentCatalog.cs` **M** as `ConcurrentDocumentCatalog<T,TIndex>` in the store package (the Orchard project keeps its copy for other consumers); `Migrations/*` **M** (copies) to the store package `Migrations/`.

## A.3 `src/Modules`

### `CrestApps.OrchardCore.ContactCenter` -> `CrestApps.Core.ContactCenter` (framework parts)

| Folder / file | Disposition |
| --- | --- |
| `*Startup.cs` (19) | **S**, rewired (04 section 4) |
| `Controllers/*` (15), `Views/**`, `ViewModels/*` (Orchard editors), `Drivers/**`, `Deployments/**`, `Recipes/**`, `Workflows/**`, `Reports/**` (Orchard `IReport` providers, drivers, view models; `Reports/Models` and `ContactCenterReportCapabilityRequirements`/`IContactCenterCapabilityDependentReport` are reviewed: framework if provider-neutral), `Filters/ContactCenterAgentBarFilter.cs`, `ModelBinders/IvrFlowJsonModelBinder.cs`, `Migrations/*Migrations.cs`, `Migrations/agent-state-reason-codes.recipe.json`, `Manifest.cs`, `README.md`, `Assets/**`, `wwwroot/**` (copied to the resources project in W10) | **S** |
| `Migrations/ContactCenterMigrationSql.cs` | **M** (store package) |
| `Indexes/*IndexProvider.cs` (27) | **M** (store package) |
| `Hubs/ContactCenterHub.cs` | **X** -> `ContactCenterHubBase<TClient>` (**M**) + sealed Orchard `ContactCenterHub` (**S**) |
| `Hubs/IContactCenterHubClient.cs`, `ContactCenterHubScopeContext.cs` | **M** |
| `Endpoints/*` (11) | **M** as `MapContactCenter*Endpoints` (`ContactCenterEndpointAntiforgery` **M**) |
| `BackgroundTasks/*` (16) | **X** (S10) |
| `Handlers/*` | **M** except `ContactCenterWorkflowEventHandler` (**S**), `RecordingMediaDeletionAuditTrailHandler` (**S**), `ContactCenterRealTimeEventScopeContext` (**M**) |
| `HealthChecks/ContactCenterBackplaneHealthCheck.cs`, `ContactCenterRedisConnectivityHealthCheck.cs` | **S** |
| `ContactCenterHealthCheckServiceCollectionExtensions.cs` | **X** (framework `AddCoreContactCenterHealthChecks`; Redis part stays) |
| `Services/BaseVoiceVerificationStartupCheck.cs` | **A** (S14) |
| `Services/ContactCenterAgentBarBuilder.cs`, `IContactCenterAgentBarBuilder.cs` | contract **M**, implementation **S** |
| `Services/ContactCenterAgentSignOutCookieConfiguration.cs`, `ContactCenterAuditTrailEventConfiguration.cs`, `ContactCenterAdminFormOptionsProvider.cs`, `*AdminMenu.cs` (10), `*PermissionProvider.cs` (2), `*ResourceConfiguration.cs` (3), `SharedHealthCheckEndpointValidator.cs` (Orchard tenant events; framework check exists), `ContactCenterTopologyValidator.cs` (**A** S14: evaluator moves, tenant events wrapper stays) | **S** |
| `Services/ContactCenterFeatureLifecycleCoordinator.cs`, `ContactCenterFeatureLifecycleHandler.cs`, `ContactCenterFeatureWorkLifecycleParticipant.cs`, `ContactCenterFeatureWorkManager.cs`, `ContactCenterRealTimeLifecycleParticipant.cs`, `ContactCenterVoiceLifecycleParticipant.cs` | **A** (S18); the feature-id-to-capability mapping stays in the Orchard `ContactCenterFeatureLifecycleHandler` |
| `Services/ContactCenterHubConnectionRegistry.cs`, `ContactCenterRealTimeNotifier.cs` (generic over `THub`), `ContactCenterHandoffQueueOptionsProvider.cs`, `ContactCenterIncomingCallContextProvider.cs`, `ContactCenterIncomingCallFactory.cs`, `ContactCenterManualCallScreener.cs`, `ContactCenterTransferTargetPolicy.cs`, `DefaultDialerEligibilityService.cs`, `DialerProviderCommandDispatchValidator.cs`, `DirectHoldTimeoutService.cs`, `InboundVoiceCallProcessor.cs`, `InboundVoiceWaitingCallVoicemailSink.cs`, `PendingIncomingCallOfferService.cs`, `QueuedVoiceWorkOfferScopeContext.cs`, `QueuedVoiceWorkOfferService.cs`, `RecordingErasureGuard.cs`, `VoiceAgentHandoffService.cs`, `VoiceContactCenterCallRouter.cs`, `VoiceQueueOfferService.cs` | **M** |
| `Services/InboundContactLookup.cs` | **A** (S11) |
| `Properties/AssemblyInfo.cs` | **S** |

### `CrestApps.OrchardCore.Telephony` -> `CrestApps.Core.Telephony` (framework parts)

| Folder / file | Disposition |
| --- | --- |
| `Startup.cs`, `Manifest.cs`, `Controllers/*`, `Drivers/*`, `Views/**`, `ViewModels/*`, `Models/SoftPhonePart.cs`, `Models/SoftPhoneWidgetSettings.cs`, `Migrations/*Migrations.cs`, `Filters/*`, `Services/{TelephonyAdminMenu,TelephonyExtensionsAdminMenu,TelephonyPermissionProvider,ResourceManagementOptionsConfiguration,PhoneFieldDialerShapeTableProvider,SoftPhoneWidgetPresenter,ISoftPhoneWidgetPresenter,RecordingMediaTenantEvents}.cs`, `Assets/**`, `wwwroot/**`, `README.md` | **S** (`RecordingMediaTenantEvents` calls the framework initializer, S14) |
| `Services/{DefaultTelephonyUserAccessor,ITelephonyUserAccessor}.cs` | **D** (S9) |
| `Services/TelephonySettingsConfiguration.cs` | **M** (post-configure of an options POCO; if it reads `ISiteService`, **S**) |
| `Models/TelephonyCoordinationOptions.cs`, `TelephonyUserConnections.cs` | **M** |
| all other `Services/*` (`ChunkedAead*Stream`, `DefaultIncomingCallDispatcher`, `DefaultOutboundCallScreeningService`, `DefaultTelephonyAuthenticationService`, `DefaultTelephonyCommandExecutor`, `DefaultTelephonyInteractionStore` (**M** to store package as YesSql implementation if it uses `ISession`), `DefaultTelephonyProviderResolver`, `DefaultTelephonyService`, `DefaultTelephonyUserTokenStore` (**A** S9), `LocalEncryptedRecordingMediaStore` (**A** options), `RecordingMediaCryptoFormat`, `SoftPhoneCountries`, `SoftPhoneCountry`, `TelephonyAddressNormalizer`, `TelephonyCallHistoryVoiceEventHandler`, `TelephonyInteractionSynchronizationService`, `TelephonyPkceGenerator`, `TelephonyUserPersistenceException`) | **M** |
| `Hubs/TelephonyHub.cs` | **X** -> `TelephonyHubBase<TClient>` + sealed Orchard hub |
| `Endpoints/SoftPhoneDialerEndpoints.cs` | **M**; `SoftPhoneExtensionEndpoints.cs` **X** (config endpoint moves, `/softphone` page stays) |
| `BackgroundTasks/TelephonyInteractionReconciliationBackgroundTask.cs` | **X** (S10) |
| `Indexes/*` | **M** (store package `Indexes/Telephony`, providers included) |

### `CrestApps.OrchardCore.Telnyx` -> `CrestApps.Core.Telephony.Telnyx` (framework parts)

`Startup.cs`, `AiVoiceStartup.cs`, `SmsStartup.cs`, `Manifest.cs`, `Controllers/TelnyxConnectController.cs`, `Drivers/*`, `Views/**`, `ViewModels/*`, `Migrations/*`, `Properties/*` **S**; `Services/{TelnyxOptionsConfigurations,TelnyxProviderOptionsConfigurations,TelnyxSmsProviderOptionsConfiguration}.cs` **S** (site settings bridges) unless they only bind configuration (then **M**); `Services/{TelnyxAiVoiceConversationHandler,TelnyxSmsProvider (A S12),TelnyxSmsWebhookParser,VoiceOmnichannelProcessor}.cs` **M**; `Endpoints/*` **M** as `MapTelnyx*`; `BackgroundTasks/*` **X**.

### `CrestApps.OrchardCore.Asterisk` -> `CrestApps.Core.Telephony.Asterisk`

`Startup.cs`, `Manifest.cs`, `AssemblyInfo.cs`, `Drivers/*`, `Views/**`, `ViewModels/*`, `Migrations/*`, `Models/AsteriskSettings.cs` (site document; `DefaultAsteriskOptions` is the framework options type), `README.md` **S**; `Services/AsteriskRealtimeVoiceTenantEvents.cs` **A** (S14); `Services/AsteriskContactCenterFeatureLifecycleParticipant.cs` **A** (S18); `Services/DefaultAsteriskOptionsConfiguration.cs` **X** (configuration binding moves, site-settings part stays); everything else in `Services`, `Models`, `Indexes` (to `Data/YesSql`), `Telemetry`, `BackgroundTasks` (**X**), `AsteriskConstants.cs` (feature id split) **M**.

### `CrestApps.OrchardCore.DialPad` -> `CrestApps.Core.Telephony.Dialpad`

`Startup.cs`, `DialerStartup.cs`, `Manifest.cs`, `Controllers/DialpadWebhookRegistrationController.cs`, `Drivers/*`, `Views/**`, `ViewModels/*`, `Models/DialpadSettings.cs` (site document), `Properties/*`, `README.md` **S**; `Services/DialpadOptionsConfigurations.cs`/`DialpadProviderOptionsConfigurations.cs` **X**; `DialpadContactCenterFeatureLifecycleParticipant` **A** (S18); everything else **M**; `Endpoints/DialpadWebhookEndpoint.cs` **M**.

### `CrestApps.OrchardCore.Omnichannel`

`Manifest.cs`, `Startup.cs`, `Views/**`, `Migrations/*` **S**; `Indexes/*` (message index provider) **M** (store package).

### `CrestApps.OrchardCore.Omnichannel.Managements`

| Folder / file | Disposition |
| --- | --- |
| `*Startup.cs`, `Manifest.cs`, `Controllers/*`, `Drivers/**`, `Views/**`, `ViewModels/*`, `Deployments/**`, `Recipes/**`, `Schemas/*`, `Migrations/*`, `Models/OmnichannelActivityExportPart.cs`, `OmnichannelContactImportOptionsPart.cs`, `Reports/*` (Orchard `IReport`), `Assets/**`, `wwwroot/**` | **S** |
| `Migrations/OmnichannelIndexMigration.cs`, `OmnichannelActivityIndexMigrations.cs`, `OmnichannelActivityBatchIndexMigrations.cs`, `CadenceIndexMigrations.cs` | **S** but rewritten to call the framework schema extensions; `ContactMethodMigrations.cs`, `OmnichannelContactsMigrations.cs` **S** unchanged (content) |
| `Indexes/CadenceIndexProvider.cs`, `OmnichannelActivityBatchIndexProvider.cs`, `OmnichannelActivityIndexProvider.cs` | **M**; `OmnichannelContactIndexProvider.cs` **S** |
| `Handlers/{CadenceHandler,OmnichannelActivityBatchHandler,OmnichannelCampaignGroupHandler,OmnichannelCampaignHandler,OmnichannelChannelEndpointHandler,OmnichannelDispositionHandler,SubjectActionHandler,BulkManageActivityFilterHandler,ListOmnichannelActivityFilterHandler}.cs` | **M** (catalog/filter handlers; review each for content usage, `TimeZoneListOmnichannelActivityFilterHandler` **S**) |
| `Handlers/{ContactActivityExportHandler,OmnichannelActivityAuthorizationHandler (S8 mapping),OmnichannelContactDefinitionHandler,OmnichannelContactImportRowFilter,OmnichannelContactPartContentImportHandler,OmnichannelContactTimeZoneHandler}.cs` | **S** |
| `Services/{AutomatedActivityCompletionService,AutomatedVoiceActivitySettingsResolver,DefaultActivityBatchLoadCoordinator,DefaultActivityDispositionService,DefaultSubjectActionExecutor,IActivityBatchLoadCoordinator,SubjectActionCatalog,ActivityPurgeHelper}.cs` | **M** (apply S11 where they touch content) |
| `Services/{AdminMenu,ChannelEndpointsAdminMenu,BulkActivityAdminFormOptionsProvider,DefaultContactActivityBatchLoader,IOmnichannelContactDuplicateLookupService,OmnichannelAIChatSessionAccessProvider,OmnichannelContactDefinitionService,OmnichannelContactDuplicateLookupService,OmnichannelContactListScope,OmnichannelContactPhoneContentsAdminListFilterProvider,OmnichannelContentTypeProvider,OmnichannelHelper (review),OmnichannelReportAggregator,OmnichannelReportQuery,OmnichannelSubjectButtonsShapeTableProvider,OmnichannelSubjectDefinitionService,OmnichannelSubjectPartIndexSettingsShapeTableProvider,PermissionProvider,PhoneNumberSearchTerm,ResourceManagementOptionsConfiguration}.cs` | **S** |
| `Tools/TransferToAgentTool.cs` | **M** |
| `BackgroundTasks/AutomatedActivitiesProcessorBackgroundTask.cs` | **X** |
| `Endpoints/SubjectActionEndpoints.cs` | **M** as `MapOmnichannelSubjectActionEndpoints` if content-free after S11; else **S** |

### `CrestApps.OrchardCore.Omnichannel.Sms`

`Manifest.cs`, `Startup.cs`, `Views/**`, `Migrations/*`, `Indexes/OminchannelActivityAIChatSessionIndex*` (**M** to the store package if the index is provider-neutral; it indexes `OmnichannelActivity` so **M**) ; `Services/SmsOmnichannelProcessor.cs`, `Handlers/SmsOmnichannelEventHandler.cs`, `Twillio/TwillioRequestValidator.cs`, `Templates/**` **M**; `Endpoints/TwilioWebhookEndpoint.cs` **M**; `BackgroundTasks/*` **X**.

### `CrestApps.OrchardCore.Omnichannel.Sms.Portal`

`Startup.cs`, `RoutedDistributionStartup.cs`, `WorkDistributionStartup.cs`, `Manifest.cs`, `Controllers/*`, `Drivers/*`, `Views/**`, `ViewModels/*`, `Migrations/SmsConversationMigrations.cs`, `Services/{SmsContactResolver,SmsContactTimeZoneResolver,SmsPhoneFieldButtonShapeTableProvider,SmsPortalAdminMenu,SmsPortalPermissionProvider}.cs`, `Handlers/SmsConversationAuthorizationHandler.cs` (S8 mapping) **S**; `Migrations/SmsPortalMigrationSql.cs` **M**; `Indexes/*Provider.cs` **M**; `Services/{ActivityQueueSmsQueuePolicyReader,SmsRealTimeNotifier (generic)}.cs`, `Handlers/{SmsBroadcastHandler,SmsTemplateHandler}.cs`, `Support/PhoneDisplayFormatter.cs`, `Hubs/ISmsPortalHubClient.cs` **M**; `Hubs/SmsPortalHub.cs` **X**; `BackgroundTasks/*` **X**.

### `CrestApps.OrchardCore.Omnichannel.Voice`, `CrestApps.OrchardCore.Omnichannel.EventGrid`, `CrestApps.OrchardCore.Telephony.Azure`, `CrestApps.OrchardCore.WebSockets`, `CrestApps.OrchardCore.PhoneNumbers`

`Manifest.cs`, `Startup.cs`, views, settings **S**. EventGrid `Endpoints/AzureEventGridEndpoint.cs`, `Models/EventGridOptions.cs` **M** (optional package); `Models/EventGridSettings.cs` **S**. Telephony.Azure services **M** only if the optional package is built (rewritten on `Azure.Storage.Blobs`); otherwise **S**. WebSockets `Services/{DistributedWebSocketConnectionRegistry,InMemoryWebSocketConnectionRegistry,IRendezvousOwnerStore,WebSocketsNode}.cs` **M**; `RedisRendezvousOwnerStore.cs` **S**; `WebSocketsConstants.cs` **X** (feature id stays).

## A.4 Tests (`tests/CrestApps.OrchardCore.Tests`)

Move (**M**) to `tests/Transitions/CrestApps.Core.ContactCenter.Tests` unless listed under stay.

Stay (**S**): `Modules/ContactCenter/{AgentSoftPhoneControllerTests,ContactCenterSetupRecipeTests,ContactCenterSoftPhoneResourceTests,ContactCenterHubSecurityTests,ContactCenterWorkflowEventHandlerTests,ContactCenterWorkflowEventTypeProviderTests,ContactCenterWorkflowTaskGuardTests,WorkflowFoldedScalarTests,ContactCenterFeatureDependencyArchitectureTests,ContactCenterOptionalDependencyTests (review),ContactCenterConfigurationCoverageTests (review: framework options vs Orchard sections),RecordingMediaDeletionAuditTrailHandlerTests,SupervisorDashboardCapabilityTests (if it tests the Orchard controller)}.cs`, `Modules/ContactCenter/Reports/{AgentWorkforceReportProviderTests,EnterpriseInteractionReportConcurrencyTests}.cs`, `Modules/Telephony/{SoftPhoneControllerTests,TelephonyOAuthControllerTests,SoftPhoneExtensionEndpointsTests (page part)}.cs`, `Telephony/{SoftPhoneWidgetSettingsTests,AsteriskWebSecurityTests,AsteriskBrowserAudioE2ETests (Asterisk web host)}.cs`, `Telephony/Sms/SmsPortalAdminControllerTests.cs`, `Modules/Omnichannel/{OmnichannelActivityCompletionViewSecurityTests,OmnichannelActivitySchemaConvergenceTests (Orchard migrations),OmnichannelPermissionsTests,ContactIndexProviderCollectionTests}.cs`, `Modules/Omnichannel/Managements/{ContactResolutionUiArchitectureTests,ActivityBatchDocumentIdentityTests (review),CatalogReportDisplayNamesTests}.cs`, `Modules/Omnichannel/Managements/Handlers/*` (content handlers), `Modules/Omnichannel/Managements/Indexes/*`, `Modules/Omnichannel/Managements/Reports/*`, `Modules/Omnichannel/Managements/Services/{OmnichannelAIChatSessionAccessProviderTests,OmnichannelContactDefinitionServiceTests,OmnichannelContactDuplicateLookupServiceTests,OmnichannelContactListScopeTests,OmnichannelContactPhoneContentsAdminListFilterProviderTests,OmnichannelContentTypeProviderTests,OmnichannelSubjectDefinitionServiceTests,PhoneNumberSearchTermTests}.cs`, `Modules/Dialpad/DialpadWebhookControllerTests.cs`, `Modules/PhoneNumbers/**`, `Modules/Telnyx/**` (review), `Architecture/**` (updated), `Migrations/**`, `PublicApi/**`, `Doubles/{BackPressuredBodyPipeFeature,EndlessStream,PayloadTooLargeStream,RefusedStream}.cs` (move if the endpoint tests move), `Telephony/Doubles/{SiteServiceFactory,StubClock,FakeDistributedLock,FakeUser,FakeTelephonyUserAccessor}.cs` (**D**, replaced by framework fakes), `WebSockets/**` (**M**), `SignalR/HubNotifierArchitectureTests.cs` (**S**), `SignalR/SignalRHubRoutesTests.cs` (**S**), `SignalR/TenantSignalRGroupNameTests.cs` (**M**).

`tests/CrestApps.OrchardCore.ContactCenter.DistributedTests`, `FeatureActivationTests`, `Telephony.PlaywrightTests`: **S**, references updated.

## A.5 Docs (`src/CrestApps.Docs/docs`)

All pages **S**; revised in Phase 2 per [06-phase-2-move-to-core.md](06-phase-2-move-to-core.md) section 2.1.
