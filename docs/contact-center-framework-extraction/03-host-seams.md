# 03 - Host seams: every Orchard Core dependency inside the moving code and its replacement

A "seam" is a place where moving code depends on Orchard Core. For each one this document gives: where it is used, the framework abstraction (name, project, shape), the framework default, the Orchard adapter, the sample-host implementation, and the test double. Implement the seams first (Phase 1 workstream W1), then move code onto them.

Rule of thumb: **the framework declares the contract and a safe default; Orchard registers an adapter with `services.Replace(...)` or `TryAdd` ordering; tests use a fake.**

## Seam table

| # | Orchard dependency | Found in | Framework replacement | Orchard adapter |
| --- | --- | --- | --- | --- |
| S1 | `OrchardCore.Modules.IClock` | 50 files in `ContactCenter.Core`, 26 in `Sms.Portal.Core`, 16 in `Telnyx.Core`, 7 in Telephony/Voice/PhoneNumbers cores, many module services and background tasks | `System.TimeProvider` | `ClockTimeProviderAdapter` (already exists in `CrestApps.OrchardCore.Core`) |
| S2 | `OrchardCore.Locking.Distributed.IDistributedLock` / `ILocker` | 8 files in `ContactCenter.Core` (reservations, call commands, dedup, metric rollup, consult transfer, ...), `Telephony.Core`, `Sms.Portal.Core`, background tasks | `CrestApps.Core.Locking.IDistributedLockProvider` | `OrchardCoreDistributedLockProvider` |
| S3 | `ShellSettings.Name`, `IShellSettingsManager` | hubs, Telnyx client state, work manager, process health, SMS portal | `CrestApps.Core.Hosting.ITenantAccessor` | `ShellSettingsTenantAccessor` |
| S4 | `ShellScope.Current`, `ShellScope.UsingChildScopeAsync`, `ShellScope.AddDeferredTask` | `ContactCenterScopeExecutor`, `ContactCenterConfigurationCacheInvalidationHandler`, Telephony.Core (1) | `IContactCenterScopeExecutor` stays as the contract; `CrestApps.Core.Hosting.IScopedWorkExecutor` + `IAfterCommitTaskQueue` as the generic seam | Orchard keeps `ContactCenterScopeExecutor` (ShellScope) and replaces the default |
| S5 | `OrchardCore.Settings.ISiteService` + `ISite.As<T>()` | 5 files in `ContactCenter.Core` (recording control, governance policy, secure capture, secure pause, transfer resolver), 4 in `Telnyx.Core`, 2 in `Sms.Portal.Core`, Telephony.Core | `IOptionsMonitor<T>` over plain options POCOs | `IConfigureOptions<T>` from site settings + change token source |
| S6 | `OrchardCore.Environment.Cache.ISignal` | `ContactCenterConfigurationCache` | `IContactCenterConfigurationChangeNotifier` + `IChangeToken` | Orchard signal handler forwards to the notifier |
| S7 | `OrchardCore.Entities.Entity` / `IEntity` | `TelephonyInteraction : Entity`, `OmnichannelMessage : Entity`, `Interaction : IEntity` | a plain `JsonObject Properties` property | none needed |
| S8 | `OrchardCore.Security.Permissions.Permission`, `IAuthorizationService` with permissions | `ContactCenterPermissions`, `TelephonyPermissions`, `SmsPortalPermissions`, `SupervisorQueueAuthorizationService`, `TransferDestinationResolver`, `CallControlAuthorizationService`, `AgentRecordingControlService`, `RecordingAccessGovernanceService`, `SmsConversationAuthorizationService`, hub `[Authorize]` checks | `OperationAuthorizationRequirement` constants + resource objects | permission-mapping `AuthorizationHandler`s |
| S9 | Orchard users (`IUser`, `UserManager<IUser>`, `IUserService`, `ITelephonyUserAccessor`) | `DefaultTelephonyUserAccessor`, agent profile creation, supervisor dashboard, SMS assignment, extensions directory | `IUserAccessor` (exists) + new `IUserDirectory` | `OrchardCoreUserDirectory` |
| S10 | `OrchardCore.BackgroundTasks.IBackgroundTask` | 16 Contact Center tasks, 4 SMS Portal, 2 SMS automation, 1 Omnichannel, 1 Telephony, 3 Telnyx, 3 Asterisk | `I<Name>Cycle` services + optional `IHostedService` runners | thin `IBackgroundTask` wrappers |
| S11 | `OrchardCore.ContentManagement.*` (contacts, subjects, `ContentItem`, `ContentPart`, `ContentTypeDefinition`, `IContentManager`, `IContentDefinitionManager`) | `Omnichannel.Core` (22 files), `Sms.Portal.Core` (3), `Voice.Core` (2), `InboundContactLookup`, `SmsContactResolver`, `Managements` definition services | a framework-owned CRM model (contact definitions, contacts, subject definitions with field schema and flow settings, subjects) behind contracts; the default model is opt-in (`AddContacts()`/`AddSubjects()`) | content-type implementations of the same contracts registered by `AddOrchardCoreOmnichannelContacts()`/`...Subjects()`; the default model is never registered in Orchard |
| S12 | `OrchardCore.Sms.*` (`ISmsProvider`, `ISmsProviderResolver`, `SmsMessage`, `SmsResult`) | `ISmsDispatcher`, `ISmsDispatchProvider`, `SmsDispatcher`, `TelnyxSmsProvider`, `SmsOmnichannelProcessor`, Twilio endpoint | `CrestApps.Core.Sms.Abstractions` | adapters in both directions |
| S13 | `OrchardCore.Environment.Shell.Configuration.IShellConfiguration` | Telnyx options, SMS portal options, automation options | `IConfiguration` section passed by the host | Orchard passes `IShellConfiguration.GetSection(...)` |
| S14 | `OrchardCore.Modules.IModularTenantEvents` | `ContactCenterTopologyValidator`, `SharedHealthCheckEndpointValidator`, `RecordingMediaTenantEvents`, `RecordingBlobContainerTenantEvents`, `AsteriskRealtimeVoiceTenantEvents` | explicit `IValidateOptions<T>` / `IStartupValidator` / `IHostedService` in the framework | Orchard `IModularTenantEvents` wrappers call the framework services on `ActivatedAsync`/`TerminatingAsync` |
| S15 | `OrchardCore.Data.IDbConnectionAccessor`, `StoreCollectionOptions`, `OrchardCore.Data.Migration.DataMigration` | `OmnichannelActivityStore` (Dapper), migrations, legacy type-name rewrites | `IStore.Configuration.ConnectionFactory.CreateConnection()`; `ContactCenterStoreOptions`/`SmsPortalStoreOptions` collection names; schema-builder extensions | `DataMigration` classes stay Orchard |
| S16 | `OrchardCore.Redis` (`IRedisService`) | `RedisRendezvousOwnerStore`, `ContactCenterRedisConnectivityHealthCheck`, backplane health | none in Phase 1 (D-10) | stays Orchard |
| S17 | `CrestApps.OrchardCore.Abstractions` helpers (`LogDataClassifications`, `DictionaryDocument<T>`, `ValidateTenantOptionsOnActivation`) | 2 + 1 usages in cores; startups | framework copies in `CrestApps.Core.Hosting.Abstractions` (Phase 2: `CrestApps.Core.Infrastructure.Abstractions`) | Orchard keeps its own; values identical |
| S18 | Feature ids inside services (`ContactCenterConstants.Feature.*` passed to `IContactCenterFeatureWorkManager.TryEnter`, lifecycle participants, hub work leases) | `ContactCenterHub`, `ContactCenterFeatureWorkManager`, lifecycle participants (Contact Center, Telnyx, Asterisk, Dialpad) | `ContactCenterCapabilities` constants | Orchard maps feature id to capability in its lifecycle handler |
| S19 | `OrchardCore.Sms` provider settings screens, `OrchardCore_Sms_Telnyx` configuration section | Telnyx SMS | framework `TelnyxSmsOptions` bound from any section | Orchard binds the legacy section name |
| S20 | `CrestApps.OrchardCore.DncRegistry.Abstractions` (`INationalDoNotCallRegistry`, `NumberSearchContext`, `DoNotCallScreeningException`) | dialer compliance screening in `ContactCenter.Core`/module | move these 3 Orchard-free files to `CrestApps.Core.Omnichannel.Abstractions/Compliance`; framework default `ContactPreferenceDoNotCallRegistry` (screens against `OmnichannelContact.DoNotCall`) with `ComplianceOptions.FailClosedWithoutNationalRegistry` (D-13) | DNC module implements the framework contract and replaces the default |
| S21 | `ContactCenterAgentSignOutCookieConfiguration` (cookie sign-out event signs the agent out of queues) | Contact Center module | `IAgentSignOutHandler.HandleAsync(ClaimsPrincipal)` in `CrestApps.Core.ContactCenter` (calls presence/session sign-out) | Orchard cookie configuration calls the handler; MVC host calls it from `CookieAuthenticationEvents.OnSigningOut` |
| S22 | TimeZones module (`TimeZoneMap`, `MappedTimeZoneSelectListProvider`) + `OmnichannelContactTimeZoneHandler` (content handler) | `Omnichannel.Managements` | `IContactTimeZoneResolver.ResolveAsync(PhoneNumber)` in `CrestApps.Core.Omnichannel.Abstractions`; default `PhoneNumberContactTimeZoneResolver` (libphonenumber region/area-code mapping); optional `TimeZoneMap` catalog in `CrestApps.Core.Omnichannel` mirroring the Orchard map model; the framework contact handler applies it when `AutoDetectTimeZone` is set (D-14) | Orchard registers `TimeZoneMapContactTimeZoneResolver` over its map and keeps the content handler |
| S23 | Resource configurations and view models that hand hub paths, endpoint URLs, antiforgery header names, and feature flags to the scripts | Telephony, Contact Center, SMS Portal modules | `SoftPhoneClientConfiguration`, `ContactCenterClientConfiguration`, `SmsPortalClientConfiguration` models + `I*ClientConfigurationProvider` services in the framework, built from `HubRouteManager`/endpoint options; the scripts in `CrestApps.ContactCenter.Resources` read one JSON blob | Orchard resource configurations serialize the same models; MVC views render them |

## S1 - Clock

- Replace constructor parameter `IClock clock` with `TimeProvider timeProvider`.
- `_clock.UtcNow` becomes `_timeProvider.GetUtcNow().UtcDateTime`. Keep `DateTime` (UTC kind) in models; do not switch stored properties to `DateTimeOffset`.
- Orchard already registers `TimeProvider` (`services.AddSingleton<TimeProvider, ClockTimeProviderAdapter>()` in the AI module). Move that registration to `CrestApps.OrchardCore.Core` so every Contact Center feature gets it regardless of the AI feature state (`TryAddSingleton`).
- Tests: add `Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`) to the transition test project and delete `StubClock`.

## S2 - Distributed lock

```csharp
// CrestApps.Core.Hosting.Abstractions/Locking/IDistributedLockProvider.cs
namespace CrestApps.Core.Locking;

public interface IDistributedLockProvider
{
    Task<(ILocker Locker, bool Locked)> TryAcquireLockAsync(string key, TimeSpan? expiration = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    Task<bool> IsLockAcquiredAsync(string key, CancellationToken cancellationToken = default);
}

public interface ILocker : IDisposable, IAsyncDisposable
{
}
```

- The shape intentionally mirrors the Orchard `IDistributedLock` members that the code base uses (`TryAcquireLockAsync(key, expiration, timeout)`, `AcquireLockAsync`, `IsLockAcquiredAsync`) so the adapter is a one-line delegation and call sites change only the injected type.
- Default: `LocalDistributedLockProvider` (singleton, per-key `SemaphoreSlim`, honours timeout and expiration) in `CrestApps.Core.Hosting`. Registered with `TryAddSingleton`.
- Orchard: `OrchardCoreDistributedLockProvider` wrapping `OrchardCore.Locking.Distributed.IDistributedLock`, registered with `services.Replace(ServiceDescriptor.Singleton<IDistributedLockProvider, OrchardCoreDistributedLockProvider>())` from `CrestApps.OrchardCore.Core` (so any feature that enables the framework gets it).
- Test double: port `tests/.../Telephony/Doubles/FakeDistributedLock.cs` to `FakeDistributedLockProvider` (keep `AcquiredKeys`, `WaitForAttemptAsync`).
- Health check: `ContactCenterDistributedLockHealthCheck` acquires through the provider.

## S3 - Tenant identity

```csharp
namespace CrestApps.Core.Hosting;

public interface ITenantAccessor
{
    string TenantName { get; }
}
```

- Default `SingleTenantAccessor` returns `"Default"`. Orchard `ShellSettingsTenantAccessor` returns `ShellSettings.Name`.
- Used by: `TelephonyHubBase`, `ContactCenterHubBase`, `SmsPortalHubBase` (group names via `TenantSignalRGroupName`), `TelnyxRecordingClientState`/`client_state` correlation, `TelnyxWebhookService`, `ContactCenterFeatureWorkManager`, `ContactCenterProcessLivenessPathValidator` (the Orchard part stays), `SmsPortal` outbox keys.
- Never persist the tenant name in a framework document unless the Orchard code already does (check each usage; `client_state` is transient).

## S4 - Scoped execution and after-commit work

- `IContactCenterScopeExecutor` (interface unchanged, moved to `CrestApps.Core.ContactCenter.Services`) is the contract used by 20+ services. Framework default `DefaultContactCenterScopeExecutor`:
  - `ExecuteAsync<TContext>` creates `IServiceProvider.CreateAsyncScope()` and resolves `TContext` from it.
  - `ScheduleAfterCommit` enqueues into a scoped `IAfterCommitTaskQueue` (from `CrestApps.Core.Hosting`) when one is available in the current scope and returns `true`; returns `false` when there is no ambient scope (callers already handle `false` by executing inline).
  - `AfterCommitStoreCommitter` decorates `IStoreCommitter` and drains the queue after a successful commit (registered by `AddCoreHosting()`; the sample host's MVC/SignalR/endpoint store-committer filters therefore run deferred work exactly where Orchard's `ShellScope` deferred tasks would).
- Orchard: keeps `ContactCenterScopeExecutor` (ShellScope) and replaces the default.
- `ContactCenterConfigurationCacheInvalidationHandler` is Orchard-only glue (it uses `ShellScope.AddDeferredTask`), implemented against S6.
- Telephony.Core's single `ShellScope` usage follows the same pattern (identify the file with `grep -rl ShellScope src/Core/CrestApps.OrchardCore.Telephony.Core`).

## S5 - Site settings become options

For each settings class read through `ISiteService`:

| Settings class | Framework options type (same property names) | Orchard bridge |
| --- | --- | --- |
| `ContactCenterRecordingSettings` | `ContactCenterRecordingOptions` (or keep the name; the class moves to `CrestApps.Core.ContactCenter.Models` unchanged) | `IConfigureOptions<ContactCenterRecordingSettings>` reading `ISiteService.GetSettingsAsync<ContactCenterRecordingSettings>()` + `AddSignalOptionsChangeTokenSource` so `IOptionsMonitor` refreshes when the site is saved |
| `SecureCaptureSettings` | same | same |
| `ContactCenterExternalTransferSettings` | same | same |
| `TelephonySettings` | already an options POCO with `TelephonySettingsConfiguration : IPostConfigureOptions` | keep |
| `TelnyxSettings`, `TelnyxSmsSettings` | `TelnyxOptions`, `TelnyxSmsOptions` already exist; the settings classes become Orchard-only site documents that feed the options | `TelnyxOptionsConfigurations`, `TelnyxSmsOptionsConfiguration` stay Orchard |
| `SmsPortalOptions`, `SmsKeywordReplySettings`, `SmsRoutedDistributionOptions` | already options | Orchard binds from `IShellConfiguration` sections; framework binds from `IConfiguration` |
| `AsteriskSettings`, `DialpadSettings`, `EventGridSettings` | `DefaultAsteriskOptions`, `DialpadOptions`, `EventGridOptions` already exist | settings display drivers stay Orchard |

Rule: framework services take `IOptionsMonitor<T>` (or `IOptions<T>` when refresh is irrelevant). No framework type may reference `ISiteService`, `ISite`, or `OrchardCore.Entities`.

## S6 - Configuration cache invalidation

`ContactCenterConfigurationCache` becomes `IMemoryCache`-backed with an `IContactCenterConfigurationChangeNotifier` (`NotifyChanged(string key)` + `IChangeToken Watch(string key)`) in the framework. Catalog handlers for queues, skills, entry points, dialer profiles, and business hours call `NotifyChanged` from `UpdatedAsync`/`DeletedAsync` (they already trigger the signal today). Orchard's `ContactCenterConfigurationCacheInvalidationHandler` stays only if a cross-node signal is required; if it is, it subscribes to `ISignal` and forwards to the notifier.

## S7 - Entities

- `TelephonyInteraction` (Telephony.Abstractions): replace `: Entity` with `public JsonObject Properties { get; set; } = [];` and add `TelephonyInteractionExtensions` (`Get<T>()`, `Put<T>()`, `Alter<T>()`) that mirror the `OrchardCore.Entities` helpers used today. Serialized JSON keeps the `Properties` object, so stored documents are untouched.
- `OmnichannelMessage`: same.
- `Interaction` already stores `EntityProperties` and only implements `IEntity` for the helpers; remove the interface, keep the property name `EntityProperties` as-is (check `[JsonPropertyName]` usage before renaming anything).
- Do **not** switch to `CrestApps.Core.ExtensibleEntity`: its `Properties` is `IDictionary<string, object>` and would change the serialized shape.

## S8 - Authorization

Framework side (`CrestApps.Core.ContactCenter.Abstractions/Security`, `CrestApps.Core.Telephony.Abstractions/Security`, `CrestApps.Core.Omnichannel.Sms.Portal.Abstractions/Security`):

```csharp
public static class ContactCenterOperations
{
    public static readonly OperationAuthorizationRequirement ManageQueues = new() { Name = nameof(ManageQueues) };
    public static readonly OperationAuthorizationRequirement SuperviseQueue = new() { Name = nameof(SuperviseQueue) };
    public static readonly OperationAuthorizationRequirement ControlCall = new() { Name = nameof(ControlCall) };
    public static readonly OperationAuthorizationRequirement MonitorCall = new() { Name = nameof(MonitorCall) };
    public static readonly OperationAuthorizationRequirement AccessRecording = new() { Name = nameof(AccessRecording) };
    public static readonly OperationAuthorizationRequirement EraseRecording = new() { Name = nameof(EraseRecording) };
    public static readonly OperationAuthorizationRequirement TransferExternally = new() { Name = nameof(TransferExternally) };
    // one entry per permission consulted by a framework service; derive the list from ContactCenterPermissions usages.
}
```

- Framework services call `IAuthorizationService.AuthorizeAsync(principal, resource, ContactCenterOperations.X)` where `resource` is the queue, interaction, recording, or SMS conversation object (or a small context record) so handlers can apply ownership rules.
- Orchard: `ContactCenterPermissions` moves to `CrestApps.OrchardCore.ContactCenter.Abstractions`; a `ContactCenterOperationAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, TResource>` maps each operation to the existing permission (`ContactCenterPermissions.ManageQueues` etc.) using the existing `IAuthorizationService.AuthorizeAsync(user, permission)` semantics. Existing permission ids and descriptions do not change.
- Sample host: role-based handlers in `Startup.Shared`.
- Hubs: `[Authorize]` stays on the concrete host hub; capability checks inside hub methods use the operations above.
- Tests: `FakeCallControlAuthorizationService` and `CallControlAuthorizationBoundaryTests` move; permission-mapping handler tests stay Orchard.

## S9 - Users

```csharp
namespace CrestApps.Core.Security;

public interface IUserDirectory
{
    Task<UserSummary> FindByIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<UserSummary> FindByNameAsync(string userName, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<UserSummary>> GetAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default);
    Task<PageResult<UserSummary>> SearchAsync(string term, int page, int pageSize, CancellationToken cancellationToken = default);
}

public sealed record UserSummary(string Id, string UserName, string DisplayName, string Email, bool IsEnabled);
```

- Before writing the contract, list every `UserManager<IUser>` / `IUser` usage in the Contact Center, Telephony, and SMS Portal modules (31 + 10 occurrences) and make sure each maps to a member: lookups by id and user name, batch lookup, paged search for pickers, role membership (`GetRolesAsync`) for supervisor checks, enabled flag, display name/email for notifications. Extend `UserSummary`/`IUserDirectory` accordingly rather than letting a framework service reach for the host's identity types.
- `ITelephonyUserAccessor` collapses to `IUserAccessor` + `IUserDirectory` (the Orchard `DefaultTelephonyUserAccessor` becomes the `OrchardCoreUserDirectory` adapter).
- Agent profile creation (`AgentProfileManager`), supervisor dashboard snapshots, SMS conversation assignment, telephony extension directory, `QueueSearchEndpoints`/user pickers use the directory.
- Sample host: `ClaimsUserDirectory` over its cookie users; Blazor host similarly.
- Test double: `FakeUserDirectory` replaces `FakeUser`/`FakeTelephonyUserAccessor`.

## S10 - Background work

Every `IBackgroundTask` splits into:

1. `I<Name>Cycle` + `<Name>Cycle` in the framework (scoped; `Task RunAsync(CancellationToken)` doing one pass, with the interval as an option on the feature options class).
2. An `IHostedService` runner registered only by `builder.AddBackgroundWorkers()` (one method per pillar, using a shared `CycleRunner<TCycle>` helper in `CrestApps.Core.Hosting` that creates a scope per pass, honours a `TimeProvider`-based interval, and takes the distributed lock named after the cycle so multi-node sample hosts do not double-run).
3. An Orchard `IBackgroundTask` (unchanged class name, `[BackgroundTask]` attribute and schedule) that resolves the cycle and runs it. Orchard never calls `AddBackgroundWorkers()`.

Mapping (Orchard task -> framework cycle):

| Orchard task | Framework cycle (project) |
| --- | --- |
| `AgentAvailabilityRecoveryBackgroundTask` | `AgentAvailabilityRecoveryCycle` (ContactCenter) |
| `AgentSessionCleanupBackgroundTask` | `AgentSessionCleanupCycle` |
| `CallbackDispatchBackgroundTask` | `CallbackDispatchCycle` |
| `ContactCenterMetricRollupBackgroundTask` | `ContactCenterMetricRollupCycle` |
| `ContactCenterRetentionBackgroundTask` | `ContactCenterRetentionCycle` |
| `DialerPacingBackgroundTask` | `DialerPacingCycle` |
| `DirectRingTimeoutBackgroundTask` | `DirectRingTimeoutCycle` |
| `OrphanedActivityRecoveryBackgroundTask` | `OrphanedActivityRecoveryCycle` |
| `OutboxDispatchBackgroundTask` | `OutboxDispatchCycle` |
| `ProviderCallStateReconciliationBackgroundTask` | `ProviderCallStateReconciliationCycle` |
| `ProviderCommandRecoveryBackgroundTask` | `ProviderCommandRecoveryCycle` |
| `ProviderWebhookInboxBackgroundTask` | `ProviderWebhookInboxCycle` |
| `QueueTreatmentBackgroundTask` | `QueueTreatmentCycle` |
| `ReservationExpiryBackgroundTask` | `ReservationExpiryCycle` |
| `SecureCaptureExpiryBackgroundTask` | `SecureCaptureExpiryCycle` |
| `SecurePauseAutoResumeBackgroundTask` | `SecurePauseAutoResumeCycle` |
| `SmsBroadcastBackgroundTask`, `SmsFirstResponseSlaBackgroundTask`, `SmsOutboundOutboxBackgroundTask`, `SmsRoutedReassignmentBackgroundTask` | matching cycles (Sms.Portal) |
| `SmsOwedReplyRecoveryBackgroundTask`, `SmsReEngagementBackgroundTask` | matching cycles (Omnichannel.Sms) |
| `AutomatedActivitiesProcessorBackgroundTask` | `AutomatedActivitiesProcessorCycle` (Omnichannel) |
| `TelephonyInteractionReconciliationBackgroundTask` | `TelephonyInteractionReconciliationCycle` (Telephony) |
| `TelnyxOrphanedCallReconciliationBackgroundTask`, `TelnyxRecordingIngestBackgroundTask`, `SoftPhoneHealthCanaryBackgroundTask` | matching cycles (Telnyx) |
| `AsteriskInboundReconciliationBackgroundTask`, `AsteriskPjsipCredentialCleanupBackgroundTask`, `AsteriskRecordingIngestBackgroundTask` | matching cycles (Asterisk) |

Where the Orchard task already delegates to a service (most do), the cycle is that service and the task stays a wrapper; only the scheduling glue changes. Tests named `*BackgroundTaskTests` that test the pass logic move and are renamed `*CycleTests`.

## S11 - Contacts, subjects, subject flows (highest risk; do a spike first)

### What Orchard has today

| Orchard concept | Where it lives | Framework equivalent |
| --- | --- | --- |
| Contact type | any content type carrying `OmnichannelContactPart` (`OmnichannelContactPartSettings`: `RequireTimeZone`, `AutoDetectTimeZone`, `UseDoNotCall/Sms/Chat/Email`) plus `OmnichannelContactInfoPart` (first/last name) and the `ContactMethods` bag of `PhoneNumberInfoPart`/`EmailInfoPart` items | `ContactDefinition` catalog item (`Name`, `DisplayText`, `ContactDefinitionSettings` with the same six properties, optional `Fields`) |
| Contact | content item of a contact type; indexed by `OmnichannelContactIndex` (time zone, normalized primary cell/home numbers, primary email) and `OmnichannelContactCommunicationPreferenceIndex` (`DoNot*` + UTC) | `OmnichannelContact` catalog item (`DefinitionName`, `FirstName`, `LastName`, `TimeZoneId`, `DoNotCall/Sms/Email/Chat` + `*Utc`, `ContactMethods`: `ContactPhoneNumber {Number, Extension, Type}` / `ContactEmail {Email}`, `Properties`) |
| Subject type ("subject flow") | any content type carrying `OmnichannelSubjectPart`; `OmnichannelSubjectPartSettings` (`Direction`, `InteractionType`, `Channel`, `ChannelEndpointId`, `DefaultCampaignId`, `RequireDisposition`) + `OmnichannelSubjectAISettings` on the type; `SubjectFlowSettingsService` projects them into `SubjectFlowSettings`; the AI writes the type's text fields (`OmnichannelSubjectWriter.GetSubjectTextFields`) | `SubjectDefinition` catalog item (`Name`, `DisplayText`, `SubjectDefinitionSettings` = the part settings, `AISettings` = `OmnichannelSubjectAISettings`, `Fields`: `SubjectFieldDefinition {Name, DisplayText, Type (Text, MultilineText, Boolean, Number, Date, Options), Required, AllowAIUpdate, Options[]}`); `DefinitionSubjectFlowSettingsService` projects it into `SubjectFlowSettings` |
| Subject | content item of a subject type | `OmnichannelSubject` catalog item (`DefinitionName`, `ContactId`, `Fields` as `JsonObject`, `Properties`) |
| Subject actions, dispositions, campaigns, cadences | catalog items already | unchanged; `SubjectAction.SubjectContentType` keeps its name and holds the definition name |

### Contracts (in `CrestApps.Core.Omnichannel.Abstractions`)

Every framework service that needs a contact or subject depends only on these:

```csharp
public interface IContactDefinitionProvider
{
    Task<IReadOnlyCollection<ContactDefinition>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ContactDefinition> FindAsync(string name, CancellationToken cancellationToken = default);
}

public interface IOmnichannelContactResolver
{
    Task<OmnichannelContact> FindByIdAsync(string contactId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<OmnichannelContact>> FindByPhoneNumberAsync(PhoneNumber phoneNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<OmnichannelContact>> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
}

public interface IOmnichannelContactSearch          // paging/filtering for batch loaders, pickers, duplicate lookup
{
    Task<PageResult<OmnichannelContact>> SearchAsync(ContactSearchQuery query, CancellationToken cancellationToken = default);
}

public interface IOmnichannelContactWriter
{
    Task<OmnichannelContact> CreateAsync(OmnichannelContact contact, CancellationToken cancellationToken = default);
    Task ApplyAsync(string contactId, OmnichannelContactChanges changes, CancellationToken cancellationToken = default); // name, time zone, DoNot* flags, add/replace email or phone
}

public interface ISubjectDefinitionProvider
{
    Task<IReadOnlyCollection<SubjectDefinition>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SubjectDefinition> FindAsync(string name, CancellationToken cancellationToken = default);
}

public interface IOmnichannelSubjectAccessor
{
    Task<OmnichannelSubject> GetAsync(string subjectId, CancellationToken cancellationToken = default);
    Task<OmnichannelSubject> CreateAsync(string definitionName, string contactId, IReadOnlyDictionary<string, object> fieldValues, CancellationToken cancellationToken = default);
    Task ApplyAsync(string subjectId, IReadOnlyDictionary<string, object> fieldValues, CancellationToken cancellationToken = default);
}

public interface ISubjectFlowSettingsService         // existing contract, re-based on SubjectDefinition
{
    Task<IReadOnlyList<SubjectFlowSettings>> GetConfiguredFlowSettingsAsync(CancellationToken cancellationToken = default);
    Task<SubjectFlowSettings> FindConfiguredFlowSettingsAsync(string subjectDefinitionName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubjectDefinition>> GetConfiguredSubjectDefinitionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubjectDefinition>> GetConfiguredSubjectDefinitionsAsync(SubjectDirection direction, CancellationToken cancellationToken = default);
    bool IsConfigured(SubjectFlowSettings flowSettings);
}
```

`IOmnichannelContactTypeProvider` stays and returns definition names (implemented by both providers).

### Default implementation (framework, opt-in, D-11)

`AddCoreOmnichannelContacts()` and `AddCoreOmnichannelSubjects()` in `CrestApps.Core.Omnichannel` register the catalog stores, managers, handlers, and the catalog-backed contract implementations; `AddCoreOmnichannelContactStoresYesSql()`/`...SubjectStoresYesSql()` in the store package register YesSql stores and indexes. This gives a plain ASP.NET Core host (and the Phase 3 MVC sample) full management of contact definitions, contacts, subject definitions (= subject flows), and subjects with the same validation Orchard applies today (required time zone per definition, normalized phone numbers via `IPhoneNumberService`, `DoNot*Utc` stamps via `TimeProvider`, subject values validated against the field schema, unique definition names).

### Orchard implementation (overrides, no default model registered)

`AddOrchardCoreOmnichannelContacts()`/`AddOrchardCoreOmnichannelSubjects()` in `CrestApps.OrchardCore.Omnichannel.Core` bind the same contracts to content types and content items:

- `ContentTypeContactDefinitionProvider`: contact definitions = content types with `OmnichannelContactPart`; settings from `OmnichannelContactPartSettings`.
- `ContentItemOmnichannelContactResolver`/`Search`/`Writer`: over `IContentManager`, the `ContactMethods` bag, `OmnichannelContactIndex`, `OmnichannelContactCommunicationPreferenceIndex`; `Writer.ApplyAsync` reuses today's `OmnichannelSubjectWriter.TryApplyContactEmail` logic and `OmnichannelContactPart.SetDoNot*`.
- `ContentTypeSubjectDefinitionProvider`: subject definitions = content types with `OmnichannelSubjectPart`; `Fields` = the type's text fields (today's `GetSubjectTextFields`), settings and AI settings from the part settings.
- `ContentItemOmnichannelSubjectAccessor`: reads/writes subject fields on the content item (today's `ApplySubjectFields`).
- `ContentTypeSubjectFlowSettingsService`: today's `SubjectFlowSettingsService` returning `SubjectDefinition` projections of `ContentTypeDefinition`.

Contacts and subjects therefore remain content types and content items in Orchard; every admin screen, content-type editor, import/export, and index keeps working unchanged. The framework's `OmnichannelContact`/`OmnichannelSubject` are projections in Orchard, never persisted there.

### Consumers refactored onto the contracts

`OmnichannelSubjectWriter`, `SubjectFlowSettingsService` (becomes the Orchard provider), `OmnichannelAutomationHelper`, `SmsConversationService`, `SmsInboundProcessor`, `AutoReplyRouter`, `VoiceAgentConversationLoop(.Conclusion)`, `InboundContactLookup`, `DefaultSubjectActionExecutor`, `DefaultActivityDispositionService`, `SubjectActionExecutionContext`, `OmnichannelActivityContainer`, `DefaultContactActivityBatchLoader` (framework version over `IOmnichannelContactSearch`; Orchard keeps its content-query version and replaces it). Their tests move and run against an in-memory fake of the contracts (and, for the default model, against the SQLite-backed catalog stores).

`OmnichannelActivityStore` (Dapper SQL over the activity index) must be checked for joins on the Orchard `OmnichannelContactIndex`; if present, introduce `IOmnichannelActivityQueryContributor` so the Orchard side adds contact-index joins for admin filters while the framework store stays content-free.

### Spike acceptance

`CrestApps.Core.Omnichannel`, `CrestApps.Core.Omnichannel.Sms.Portal`, and `CrestApps.Core.Omnichannel.Voice` compile with no `OrchardCore.ContentManagement*` reference; the existing `OmnichannelSubjectWriter*Tests`, `SmsConversationServiceTests`, `SmsInboundProcessorTests`, `VoiceAgentConversationLoopTests`, `SubjectFlowSettingsServiceTests` pass against the fake with unchanged assertions; the default model round-trips a contact and a subject through the SQLite-backed stores; and the Orchard contact/subject admin screens are unchanged in `FeatureActivationTests` (`ContactCenterAdministrationSurfaceTests`).

## S12 - SMS providers

`CrestApps.Core.Sms.Abstractions`:

```csharp
public interface ISmsProvider
{
    string Name { get; }
    Task<SmsResult> SendAsync(SmsMessage message, CancellationToken cancellationToken = default);
}

public interface ISmsProviderResolver
{
    Task<ISmsProvider> GetAsync(string name = null, CancellationToken cancellationToken = default);
}

public sealed class SmsMessage { public string To { get; set; } public string From { get; set; } public string Body { get; set; } public IList<string> MediaUrls { get; set; } = []; }
public sealed class SmsResult { public bool Succeeded { get; } public string ProviderMessageId { get; } public IReadOnlyCollection<string> Errors { get; } ... }
```

- Portal abstractions (`ISmsDispatcher`, `ISmsDispatchProvider`) are re-based on these types; `SmsDispatcher` resolves per-endpoint providers through `ISmsProviderResolver`.
- `TelnyxSmsProvider` implements the framework `ISmsProvider`; `CrestApps.OrchardCore.Telnyx` adds `OrchardCoreTelnyxSmsProvider : OrchardCore.Sms.ISmsProvider` delegating to it so the Orchard SMS settings screen still lists Telnyx and `OrchardCore.Sms` consumers still work.
- `CrestApps.OrchardCore.Core` registers `OrchardCoreSmsProviderResolver : ISmsProviderResolver` wrapping `OrchardCore.Sms.ISmsProviderResolver` so any Orchard SMS provider (Azure, Twilio, console) is available to the framework portal.
- `SmsOmnichannelProcessor` sends through the framework `ISmsProvider`.

## S13, S15 - Configuration and data access

- Options classes bind from `IConfiguration` sections supplied by the host: framework methods accept `IConfiguration configuration = null` (like `AddElasticsearch(configuration, ...)`); Orchard passes `_shellConfiguration.GetSection("CrestApps:ContactCenter")`, `"CrestApps:Sms:Workspace"`, `"CrestApps:Sms:Portal:KeywordReplies"`, `"CrestApps:Omnichannel:Automation"`, `"OrchardCore_Sms_Telnyx"` exactly as today.
- Dapper queries use `store.Configuration.ConnectionFactory.CreateConnection()` plus `SqlDialect`, `TablePrefix`, `Schema`, `TableNameConvention` from `IStore.Configuration` (all YesSql, no Orchard). Keep the SQL text identical (`ContactCenterMigrationSql`, `SmsPortalMigrationSql` move into the store package).
- Collection names come from `ContactCenterStoreOptions.CollectionName` (default `"ContactCenter"`) and `SmsPortalStoreOptions.CollectionName` (default `"SmsWorkspace"`); Orchard's `Startup` keeps `services.Configure<StoreCollectionOptions>(o => o.Collections.Add(...))`.

## S14 - Tenant lifecycle events

- Framework: `ContactCenterTopologyValidator` becomes `IValidateOptions<ContactCenterTopologyOptions>` + a `ContactCenterTopologyEvaluator` startup check exposed as `IContactCenterStartupCheck` (`Task ValidateAsync(CancellationToken)`); `BaseVoiceVerificationStartupCheck` and `SharedHealthCheckEndpointGuard` follow the same interface. `RecordingMediaTenantEvents` becomes `IRecordingMediaStoreInitializer` (`InitializeAsync`, `PurgeAsync`) implemented by the local and Azure stores.
- Sample host: an `IHostedService` runs all `IContactCenterStartupCheck`s at start.
- Orchard: one `IModularTenantEvents` per module calls the framework checks in `ActivatedAsync` and the purge hooks in `RemovingAsync`.

## S17 - Shared helpers

- `LogDataClassifications` (taxonomy `"CrestApps"`, classification `"Address"`, `AddressSet`) is copied into `CrestApps.Core.Hosting.Abstractions/Diagnostics` (Phase 2 target: `CrestApps.Core.Infrastructure.Abstractions`). `DataClassification` equality is structural, so Orchard and framework redactors interoperate. Framework `AddCoreTelephony()` registers the erasing redactor for the address set (`services.AddRedaction(b => b.SetRedactor<ErasingRedactor>(LogDataClassifications.AddressSet))`).
- `DictionaryDocument<T>` copy with an identical full name segment is unnecessary if the single usage in the cores can be replaced; check the usage first (`grep -rn DictionaryDocument src/Core/CrestApps.OrchardCore.ContactCenter.Core`). If it is a stored document, it must stay Orchard-side or get a type-name rewrite.

## S18 - Capabilities instead of feature ids

```csharp
namespace CrestApps.Core.ContactCenter;

public static class ContactCenterCapabilities
{
    public const string Core = "ContactCenter";
    public const string AgentServices = "ContactCenter.AgentServices";
    public const string Agents = "ContactCenter.Agents";
    public const string AgentEntitlements = "ContactCenter.AgentEntitlements";
    public const string BusinessHours = "ContactCenter.BusinessHours";
    public const string Queues = "ContactCenter.Queues";
    public const string Dialer = "ContactCenter.Dialer";
    public const string DialerPaced = "ContactCenter.Dialer.Paced";
    public const string ProviderInbox = "ContactCenter.ProviderInbox";
    public const string Voice = "ContactCenter.Voice";
    public const string InboundVoice = "ContactCenter.InboundVoice";
    public const string VoiceMedia = "ContactCenter.Voice.Media";
    public const string RecordingCore = "ContactCenter.Recording.Core";
    public const string Recording = "ContactCenter.Recording";
    public const string SecureCapture = "ContactCenter.SecureCapture";
    public const string Supervision = "ContactCenter.Supervision";
    public const string RealTime = "ContactCenter.RealTime";
}
```

- `IContactCenterFeatureWorkManager`, `IContactCenterFeatureLifecycleParticipant`, `ContactCenterFeatureLifecycleOptions`, and the provider lifecycle participants (Telnyx, Asterisk, Dialpad) key on capability names.
- Orchard `ContactCenterFeatureLifecycleHandler` maps `ContactCenterConstants.Feature.X` to `ContactCenterCapabilities.X` in one dictionary; the feature ids themselves stay in `CrestApps.OrchardCore.ContactCenter.Abstractions`.
- Verify nothing persists the feature id string (`grep -rn "Feature\." src/Core/CrestApps.OrchardCore.ContactCenter.Core/Models`) before renaming values; if a stored document carries a feature id, keep the legacy value as the capability value.

## Seams that need no abstraction

`ISession`/`IStore` (YesSql), `ILogger`, `IStringLocalizer`, `IOptions*`, `IHttpClientFactory` and resilience, `IDataProtectionProvider`, `IDistributedCache`, `IMemoryCache`, `IHubContext<THub, TClient>`, `IAuthorizationService`, `IHttpContextAccessor`, `Microsoft.Extensions.Compliance.Redaction`, `Microsoft.Extensions.Diagnostics.HealthChecks`, `System.Net.WebSockets`, `NodaTime` (only if the framework needs it; today it is used by `Omnichannel.Managements`, which stays Orchard).
