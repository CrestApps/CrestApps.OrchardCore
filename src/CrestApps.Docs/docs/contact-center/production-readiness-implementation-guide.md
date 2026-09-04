---
sidebar_label: Readiness — implementation guide
sidebar_position: 33
title: Production readiness — step-by-step implementation guide
description: File-level instructions, signatures, registrations, tests and verification commands for implementing the production readiness plan, written so an engineer or coding agent new to the repository can execute it item by item.
---

# Production readiness — implementation guide

This page turns the [Production Readiness Plan](production-readiness-plan.md) into executable instructions. Read
the workstream pages for the reasoning; follow this page for the mechanics. Items are numbered exactly as in the
workstream pages (A1…E12). Work through the phases in order; inside a phase, the order below is the dependency
order.

## 0. Working agreements (read before touching code)

### 0.1 Repository facts

| Fact | Value |
| --- | --- |
| Framework | .NET 10, Orchard Core 3 preview (packages from `nuget.cloudsmith.io`) |
| Solution layout | `src/Abstractions/*` (contracts, models), `src/Core/*` (services, stores, indexes), `src/Modules/*` (startups, controllers, endpoints, views, migrations), `tests/*` |
| Test framework | xunit v3 + Moq (`new Mock<T>()`, `Mock.Of<T>()`), `TestContext.Current.CancellationToken` for tokens |
| Main test project | `tests/CrestApps.OrchardCore.Tests` (folders `Modules/ContactCenter`, `Modules/Omnichannel`, `Modules/Telephony`, `Modules/Telnyx`, `Telephony/Sms`, `Telephony/Telnyx`, `PublicApi`) |
| Other test projects | `tests/CrestApps.OrchardCore.ContactCenter.FeatureActivationTests`, `...DistributedTests` (needs Redis + PostgreSQL env vars), `...Telephony.PlaywrightTests` |
| Public API gate | `tests/CrestApps.OrchardCore.Tests/PublicApi/PublicApiApprovalTests.cs` compares governed assemblies against `PublicApi/Baselines/*.approved.txt`; a changed public surface fails until the baseline is updated |
| Docs | `src/CrestApps.Docs` (Docusaurus 3, MDX). `.md` is parsed as MDX: never write `<`, `{` or `}` outside code spans, never use `### Title {#id}` |
| Coding rules | `AGENTS.md` at the repository root (formatting, null guards, constructor injection, no competitor product names, feature gating) |
| Front-end rules | No jQuery. `bootstrap-select` is the vanilla fork exposed as `window.Selectpicker`. Server-side live search appends real `option` elements and calls `.refresh()`. Reusable pickers: `ItemSelector` (Resources module) and `UserPicker` (Users module) |
| View-model rule | Never mark a view model `sealed` when it is passed to `Initialize<T>` in a display driver (Castle proxy cannot subclass it; the editor renders empty) |

### 0.2 Commands

```bash
# Build everything the tests need (5–10 minutes the first time)
dotnet build tests/CrestApps.OrchardCore.Tests/CrestApps.OrchardCore.Tests.csproj -c Debug

# Run one test class (use the class or namespace fragment)
dotnet test tests/CrestApps.OrchardCore.Tests/CrestApps.OrchardCore.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~SmsConversationAuthorization"

# Run the full unit suite (2–5 minutes)
dotnet test tests/CrestApps.OrchardCore.Tests/CrestApps.OrchardCore.Tests.csproj -c Debug --no-build

# Feature-activation matrix (tenant-level enable/disable)
dotnet test tests/CrestApps.OrchardCore.ContactCenter.FeatureActivationTests/CrestApps.OrchardCore.ContactCenter.FeatureActivationTests.csproj -c Debug

# Front-end assets after editing anything under a module's Assets folder
npm run rebuild

# Docs site (validates MDX)
cd src/CrestApps.Docs && npm run build
```

If `dotnet test --filter` does not select tests (xunit v3 under Microsoft.Testing.Platform), run the built test
assembly directly: `dotnet tests/CrestApps.OrchardCore.Tests/bin/Debug/net10.0/CrestApps.OrchardCore.Tests.dll --filter-class "*SmsConversationAuthorization*"`.

### 0.3 Rules for every item

1. **Write the tests first** and run them red before implementing. Put new tests next to the existing tests for the
   same area (see the table above). Name tests `Method_Scenario_Expectation`.
2. **Do not commit, stage, stash, push, or create branches.** Leave all changes in the working tree for review.
3. Keep public surface changes deliberate. After changing a governed assembly, run `PublicApiApprovalTests`; if the
   diff is intended, copy the generated text into the matching `*.approved.txt` (the test writes a `*.received.txt`
   next to it) and review the diff.
4. When adding an `IOptions` class, add an `IValidateOptions` implementation and a line in
   `configuration-deployment.md`; `ContactCenterConfigurationCoverageTests` fails otherwise.
5. When adding an index column, add it to the index class, the index provider, and a new migration step
   (`UpdateFrom{N}Async`) that alters the table and creates the SQL index; then add the index to the
   `ContactCenterMigrationSqlTests` style assertions if the module has them.
6. When adding a background task, decorate with `[BackgroundTask(Title, Schedule, Description, LockTimeout, LockExpiration)]`
   and register `services.AddSingleton<IBackgroundTask, T>()` in the owning feature startup.
7. Feature gating: a new capability is a new `[assembly: Feature]` in `Manifest.cs` plus a `[Feature(...)]` startup;
   dependency-only features set `EnabledByDependencyOnly = true`.
8. Never log message bodies or phone numbers; use `SanitizeLogValue()` for ids and the `IRedactorProvider`
   redactor (`LogDataClassifications.AddressSet`) for addresses.
9. After each item: build, run the item's tests, run the module's existing tests, run `PublicApiApprovalTests`,
   and write a short entry in the change log section at the end of this page's companion file
   `production-readiness-changelog.md` (create it on first use; MDX rules apply).

## 1. Phase P0 — security, tenancy, feature split

### C1. SMS conversation authorization

**Files to create**

- `src/Core/CrestApps.OrchardCore.Sms.Workspace.Core/Services/ISmsConversationAuthorizationService.cs`
- `src/Core/CrestApps.OrchardCore.Sms.Workspace.Core/Services/SmsConversationAuthorizationService.cs`
- `src/Core/CrestApps.OrchardCore.Sms.Workspace.Core/Models/SmsConversationOperation.cs`
- `src/Modules/CrestApps.OrchardCore.Sms.Workspace/Handlers/SmsConversationAuthorizationHandler.cs`
- `tests/CrestApps.OrchardCore.Tests/Telephony/Sms/SmsConversationAuthorizationServiceTests.cs`
- `tests/CrestApps.OrchardCore.Tests/Telephony/Sms/SmsWorkspaceAdminControllerTests.cs`

**Contracts**

```csharp
public enum SmsConversationOperation { View, Send, Claim, Close, Snooze, Transfer }

public interface ISmsConversationAuthorizationService
{
    Task<bool> AuthorizeAsync(ClaimsPrincipal principal, SmsConversation conversation, SmsConversationOperation operation, CancellationToken cancellationToken = default);
}
```

**Rules to implement (in this order, first match wins)**

1. Principal has `SmsWorkspacePermissions.ViewAllConversations` → allow every operation.
2. Resolve the caller's `AgentProfile` through `IAgentProfileManager.FindByUserIdAsync(userId)` where `userId` is
   `principal.FindFirstValue(ClaimTypes.NameIdentifier)`. No profile → deny.
3. Personal conversation (`OwnerType == Personal`): owner (`OwnerId == agent.ItemId`) or assignee
   (`AssignedAgentId == agent.ItemId`) → allow View/Send/Close/Snooze; unassigned personal thread → allow View and
   Claim (and Send, which claims); otherwise deny.
4. Queue conversation (`OwnerType == Queue`): agent is a member of `OwnerId` (`agent.QueueIds` contains it, or
   `IAgentEntitlementPolicy.AllowsQueue(agent, OwnerId)` when the entitlements feature is on — resolve the policy
   through constructor injection; it always has a registration, `PermissiveAgentEntitlementPolicy` by default) →
   allow View and Claim on pooled/unassigned, allow Send/Close/Snooze only when assigned to this agent; non-members
   deny.
5. Transfer requires `ViewAllConversations` (unchanged).

**Wiring**

- Register in `Sms.Workspace/Startup.cs`: `services.AddScoped<ISmsConversationAuthorizationService, SmsConversationAuthorizationService>();`
  and `services.AddScoped<IAuthorizationHandler, SmsConversationAuthorizationHandler>();` where the handler
  succeeds a `PermissionRequirement` for `UseSmsPortal` when the resource is an `SmsConversation` and the service
  allows the operation carried in `context.Resource` (wrap resource as `SmsConversationAuthorizationResource(conversation, operation)`).
  Mirror `Omnichannel.Managements/Handlers/OmnichannelActivityAuthorizationHandler.cs`.
- In `AdminController`: `Conversation`, `ThreadMessages` → View; `Send` → Send; `Claim` → Claim; `SetStatus` →
  Close (Closed) or Snooze (Snoozed) or View (Open); return `Forbid()` on denial. Replace the inline
  `UseSmsPortal` check with `_authorizationService.AuthorizeAsync(User, SmsWorkspacePermissions.UseSmsPortal, resource)`.
- In `SmsConversationService`: `SendAsync`, `ClaimAsync`, `SetStatusAsync`, `AssignAsync` gain a
  `ClaimsPrincipal principal` parameter (add to `SmsSendRequest` as `Principal`) and call the service; delete the
  static `IsAuthorized`.
- In `SmsPortalHub.OnConnectedAsync` nothing changes (groups are already scoped).
- Views: the display driver buttons (`SmsConversationDisplayDriver`) hide Claim/Close when the operation is denied
  (call the service in the driver, it is scoped).

**Tests** (write first)

- `SmsConversationAuthorizationServiceTests`: one `[Theory]` per rule row: supervisor allows all; foreign personal
  thread denied for View/Send/Close; owner allowed; unassigned personal Claim allowed; queue member Claim allowed on
  Pooled; queue non-member View denied; queue member Send denied when assigned to someone else; entitlement policy
  enforced when `EnforcingAgentEntitlementPolicy` is used.
- `SmsWorkspaceAdminControllerTests` (pattern: `Modules/Telephony/SoftPhoneControllerTests.cs`): build the
  controller with mocked `IAuthorizationService` returning failure for the resource → `ForbidResult` on
  `Conversation`, `ThreadMessages`, `Send`, `Claim`, `SetStatus`; success path returns `ViewResult`/redirect.

**Acceptance**: tests green; manually opening another agent's thread returns 403.

### C2. Real-time delivery notification targeting

**Files**: `src/Modules/CrestApps.OrchardCore.Sms.Workspace/Services/SmsRealTimeNotifier.cs`;
new `tests/CrestApps.OrchardCore.Tests/Telephony/Sms/SmsRealTimeNotifierTests.cs`;
new `tests/CrestApps.OrchardCore.Tests/SignalR/HubNotifierArchitectureTests.cs`.

**Steps**

1. Extend `SmsDeliveryNotification` (Abstractions) with `AssignedAgentId` and `OwnerQueueId`; populate them in
   `SmsConversationService.ApplyDeliveryReceiptAsync` from the conversation.
2. `MessageDeliveryUpdatedAsync` uses the existing private `Target(assignedAgentId, ownerQueueId)` selector
   (agent group → queue group → unassigned group) instead of `Clients.All`.
3. Architecture test: load every assembly whose name starts with `CrestApps.OrchardCore`, find types implementing a
   `*Notifier` in a `Services` namespace that reference `IHubContext`, and assert via `System.Reflection.Metadata`
   or IL string scan that `Clients.All` is not referenced (simplest reliable approach: read the source files under
   `src/**/Services/*Notifier.cs` at test time using the repository path helper used by
   `ContactCenterReportDataProvenanceArchitectureTests` and assert the text `Clients.All` is absent).

**Tests**: mirror `Modules/ContactCenter/ContactCenterRealTimeNotifierTests.cs`: two tenants, mock `IHubClients`,
assert the delivery notification reaches only the tenant-qualified agent group.

### C3. SMS Workspace feature split and project references

**Steps**

1. In `Sms.Workspace/Manifest.cs` add:

```csharp
[assembly: Feature(
    Id = SmsWorkspaceConstants.Feature.RoutedDistribution,   // "CrestApps.OrchardCore.Sms.Workspace.RoutedDistribution"
    Name = "SMS Workspace Routed Distribution",
    Description = "Push-assigns new department SMS conversations to the least-loaded available agent using Contact Center work distribution, and re-pools threads nobody picks up.",
    Category = "Communication",
    Dependencies = [SmsWorkspaceConstants.Feature.Workspace, ContactCenterConstants.Feature.Queues])]
```

2. Create `Sms.Workspace/RoutedDistributionStartup.cs` decorated with `[Feature(SmsWorkspaceConstants.Feature.RoutedDistribution)]`
   and move these registrations from `Startup.cs` into it: `RoutedQueueRouter`, `ISmsRoutingStrategy`,
   `ISmsRoutedReassignmentService`, `SmsRoutedReassignmentBackgroundTask`, `SmsRoutedDistributionOptions`.
3. `SmsEndpointRoutingDisplayDriver`: show the Routed distribution option only when the feature is enabled
   (`IShellFeaturesManager.GetEnabledFeaturesAsync()` contains the id); if a stored setting says Routed while the
   feature is off, treat as SharedPool (already the fallback in `NumberRouteRouter`).
4. `LeastLoadedSmsRoutingStrategy` and `SmsPortalHub`: replace `IActivityQueueManager`/`IInteractionManager`
   dependencies that the base feature needs with the new `IAgentQueueMembershipReader` (create in
   `ContactCenter.Abstractions/Services`, implement in `ContactCenter.Core` `AgentQueueMembershipReader` reading
   `AgentProfile.QueueIds` and `AllowedQueueIds`, register in `AgentServicesStartup`). The routed feature may keep
   `IActivityQueueManager` because it depends on Queues.
5. Remove `../CrestApps.OrchardCore.Omnichannel.Managements/CrestApps.OrchardCore.Omnichannel.Managements.csproj`
   from `Sms.Workspace.csproj`. Build; for each compile error, move the used type into `Omnichannel.Core` if it is
   a service/model, or replace UI helpers with the Resources `ItemSelector` endpoint pattern.
6. Add `SmsWorkspace` tenant profiles to `tests/CrestApps.OrchardCore.ContactCenter.FeatureActivationTests/ContactCenterTenantProfile.cs`
   and a test class `SmsWorkspaceFeatureActivationTests`: enable SMS Workspace alone → post a fake `SmsReceived`
   event through `IOmnichannelEventHandler` → a conversation exists; enable RoutedDistribution → Queues feature is
   enabled by dependency.
7. Add a rule to `ContactCenterFeatureDependencyArchitectureTests`: assembly `CrestApps.OrchardCore.Sms.Workspace`
   does not reference `CrestApps.OrchardCore.Omnichannel.Managements`.

### B1. Handoff tool name

`src/Core/CrestApps.OrchardCore.Omnichannel.Core/Services/OmnichannelHandoffHelper.cs`: replace the literal
`transfer_to_agent` in `BuildHandoffInstructions` (and the XML docs) with `TransferToAgentToolName`. Test in
`Modules/Omnichannel/OmnichannelHandoffHelperTests.cs`: instructions contain `OmnichannelHandoffHelper.TransferToAgentToolName`.

### B6. Terminal automated activities do not block human SMS threads

1. Add `src/Core/CrestApps.OrchardCore.Omnichannel.Core/Models/ActivityStatusExtensions.cs` with
   `public static bool IsTerminal(this ActivityStatus status) => status is Completed or Cancelled or Failed or Purged;`.
2. `SmsInboundProcessor.ProcessAsync`: replace the `is not (Completed or Cancelled)` check with `!IsTerminal()`.
3. `TelnyxAiVoiceConversationHandler` and `VoiceAgentHandoffService` use the same extension where they list statuses.
4. Tests: `Telephony/Sms/SmsInboundProcessorTests.cs` add `ProcessAsync_WhenAutomatedActivityFailed_CreatesHumanConversation`
   and the same for `Purged`.

### D1. Destination policy in Telephony

1. Move `ExternalDestinationPolicy` to `src/Abstractions/CrestApps.OrchardCore.Telephony.Abstractions/Services/IDialDestinationPolicy.cs`
   as an interface: `DialDestinationDecision Evaluate(string address, DialDestinationContext context)` with
   decision `Allowed`, `Emergency`, `Premium`, `Malformed`, `Blocked` and a reason string.
2. Default implementation `DefaultDialDestinationPolicy` in `Telephony.Core/Services` with a static table of
   emergency short codes by country (`911`, `112`, `999`, `000`, `110`, `119`, `100`, `102`, `108`, `113`, `117`,
   `118`, `122`, `133`, `190`, `191`, `192`, `193`, `194`, `997`, `998`) matched as the **whole dialed digit
   string** after stripping a trunk prefix, plus the existing premium prefixes, plus a tenant allow-list from
   `TelephonySettings.AllowedShortCodes`. Keep `ContactCenter.Core/ExternalDestinationPolicy` as a thin forwarder
   for one release, marked `[Obsolete]`.
3. `DefaultTelephonyService.DialAsync`, `TransferAsync`, `DialExtensionAsync` (for external targets) call the policy
   before screening and return `TelephonyResult.Failed(localized reason)` on any non-Allowed decision.
4. Register `TryAddScoped<IDialDestinationPolicy, DefaultDialDestinationPolicy>()` in `Telephony/Startup.cs`;
   Contact Center `VoiceStartup` decorates it with the approved-catalog rules when Voice is on.
5. Tests: `Modules/Telephony/TelephonyCallControlBoundaryTests.cs` add the refusal cases; new
   `DefaultDialDestinationPolicyTests` covering every code in the table, the allow-list, and E.164 numbers that
   merely end in `911` (must be allowed — this is the current suffix bug).
6. Update `voice-routing.md` "Current limitations" to remove the bypass statement once green.

### D2. Transfer authority for the soft phone (P0 slice)

P0 slice only: when Contact Center Voice is enabled, the soft phone transfer field must not accept a raw number.
Add `ITransferTargetPolicy` in Telephony Abstractions (`Task<TransferTargetDecision> ResolveAsync(string rawTarget, ClaimsPrincipal user)`),
default allows raw E.164 after `IDialDestinationPolicy`; Contact Center `VoiceStartup` replaces it with a
resolver that accepts only catalog entry ids, agent ids and queue ids (delegating to `TransferDestinationResolver`).
`TelephonyHub.Transfer` calls it. Full attended-transfer UI is A9 (P2).

## 2. Phase P1 — correctness under load

### C4. Inbound SMS idempotency and per-thread lock

1. Add `ProviderMessageId` to `OmnichannelMessageIndex` (+ provider in `OmnichannelMessageIndexProvider`, + migration
   in `Omnichannel.Managements/Migrations` creating `IDX_OmnichannelMessageIndex_Provider (Channel, ProviderMessageId)`).
2. Create `SmsInboundInboxHandler : IProviderWebhookInboxHandler` in `Sms.Workspace.Core/Services` with
   `TechnicalName = "sms-inbound"`, `ReplaySafety = GuardedByDurableStore`, payload = serialized
   `OmnichannelMessage`; it invokes `IEnumerable<IOmnichannelEventHandler>` exactly as the Telnyx SMS endpoint
   does today. Register it in `Sms.Workspace/Startup.cs` and in `Omnichannel.Sms/Startup.cs` (both consumers).
3. `IProviderWebhookInbox` is registered by Contact Center `VoiceStartup`; move the inbox registration
   (`IProviderWebhookInbox`, `IProviderWebhookInboxStore`, `ProviderWebhookInboxBackgroundTask`, its migration) to a
   new dependency-only feature `ContactCenterConstants.Feature.ProviderInbox` that Voice, SMS Workspace and SMS
   Automation depend on.
4. `TelnyxSmsWebhookEndpoint.HandleInboundAsync` and `TwilioWebhookEndpoint`: build the message, then
   `inbox.AcceptAsync(new ProviderWebhookInboxDelivery { ProviderName, DeliveryId = providerMessageId, HandlerName = "sms-inbound", Payload })`
   and `DispatchAsync` (catch `ConcurrencyException` like the voice endpoint). Delete the detached scope in the
   Twilio endpoint. `Duplicate` acceptance returns 200 without processing.
5. `SmsInboundProcessor.ProcessAsync`: wrap find-or-create and the roll-up in
   `IDistributedLock.TryAcquireLockAsync($"SmsConversation:{serviceAddress}:{contactAddress}", timeout, expiration)`
   from new `SmsWorkspaceOptions.ConversationLockTimeout/Expiration`; on lock failure throw so the inbox retries.
6. Unique SQL index `UQ_SmsConversationIndex_Addresses (ServiceAddress, ContactAddress)` via migration
   `UpdateFrom1Async`; catch the unique violation on create and re-read.
7. Tests: `SmsInboundProcessorTests` concurrency (two tasks, one conversation) using the shared SQLite pattern in
   `Modules/ContactCenter/AvailabilityStoreSharedDatabaseTests.cs`; `TwilioWebhookEndpointSignatureTests` extended
   for duplicate `MessageSid`; `TelnyxSmsWebhookEndpointTests` (new) for duplicate provider id.

### C5. Paged inbox and indexed lookups

1. `SmsConversationIndex` add `UnreadCount` (int), `AssignedUtc` (DateTime?); `OmnichannelMessageIndex` has
   `ProviderMessageId` from C4.
2. `ISmsConversationStore` add:

```csharp
Task<IReadOnlyList<SmsConversation>> QueryAsync(SmsInboxQuery query, CancellationToken cancellationToken = default);
Task<int> CountAsync(SmsInboxQuery query, CancellationToken cancellationToken = default);
```

   with `SmsInboxQuery { AgentId, QueueIds, IncludeAll, Filter (All/Mine/Unassigned), Skip, Take }`. Build the
   YesSql predicate once; visibility for non-supervisors is `AssignedAgentId == agentId OR (OwnerType == Personal AND OwnerId == agentId) OR (OwnerType == Queue AND OwnerId IN queueIds)`.
3. `AdminController.Index` uses `PagerSlim` (see `Omnichannel.Managements/Controllers/ActivitiesController.cs` for
   the pager pattern) with page size from `SmsWorkspaceOptions.InboxPageSize` (default 50); counts via `CountAsync`.
4. `GetMessagesAsync` takes `beforeUtc` and `take`; the thread view gets a "Load earlier" link.
5. `ApplyDeliveryReceiptAsync` queries `OmnichannelMessageIndex` by `ProviderMessageId` first.
6. Tests: `SmsInboxQueryPlanBudgetTests` modeled on `Modules/ContactCenter/AgentSessionQueryPlanBudgetTests.cs`;
   store paging tests.

### C6. Provider message id on send and outbound outbox

1. `ISmsDispatcher.SendAsync` returns `SmsDispatchResult { Succeeded, ProviderMessageId, Errors }`;
   `TelnyxSmsProvider` reads `data.id` from the Telnyx response; Twilio and ACS providers return their sid when
   available.
2. Create `SmsOutboundMessage` catalog item and `SmsOutboundOutboxBackgroundTask` (every minute; in-request first
   attempt remains so the UI is instant) with attempts, next-attempt backoff (1, 5, 15, 60 minutes), and a
   per-endpoint token bucket from `SmsWorkspaceOptions.MaxMessagesPerSecondPerEndpoint`.
3. `SmsConversationService.SendAsync` and `SendDirectAsync` persist the message as `Queued`, try once inline,
   and hand the failure to the outbox instead of marking `Failed` immediately; terminal failure marks `Failed`
   and notifies the thread.
4. Tests: `SmsDispatcherTests` id capture; `SmsOutboundOutboxTests` (retry schedule, rate limit, terminal);
   `SmsConversationServiceTests` receipt matching by provider id.

### A3. Availability counts flow into routing candidates

1. `ActivityRoutingCandidate` gets `AgentAvailability Availability` (constructor parameter); `ActivityRoutingContext`
   built from `IEnumerable<AgentAvailability>`.
2. `IActivityRoutingService.SelectAgentAsync(ActivityQueue, QueueItem, IEnumerable<AgentAvailability>, ...)`.
3. `CapacityRoutingStrategy` and `LeastBusyRoutingStrategy` read `candidate.Availability.ActiveInteractionCount`;
   remove `IInteractionManager` from both.
4. `ActivityAssignmentService.AssignNextCoreAsync` passes the availability collection.
5. `LeastLoadedSmsRoutingStrategy`: replace the per-agent `GetForAgentAsync` + `CountActiveByAgentAsync` with one
   grouped count query `ISmsConversationStore.CountOpenAssignedByAgentIdsAsync(ids)` and
   `IInteractionManager.CountActiveByAgentIdsAsync(ids)` (exists).
6. Tests: update strategy tests to build candidates with counts; add `RoutingRoundTripBudgetTests` asserting a
   constant query count for N candidates (pattern: `AgentWorkspaceRoundTripBudgetTests`).

### A4. Idle-since semantics

1. `AgentProfile.IdleSinceUtc` (DateTime?). Set in `AgentPresenceManagerService` when status becomes Available and
   `ActiveReservationId` is empty, and in `ContactCenterWorkStateManager` when work completes (wrap-up end) while
   Available. Clear when a reservation is accepted.
2. `LongestIdleRoutingStrategy` orders by `IdleSinceUtc ?? PresenceChangedUtc`; `RoundRobinRoutingStrategy`
   orders by new `AgentProfile.LastWorkCompletedUtc` (set at the same completion point) then `LastAssignedUtc`.
3. Migration not required (nullable properties on a document); add index columns only if a query needs them.
4. Tests in `AgentPresenceManagerServiceTests`, `ContactCenterWorkStateManagerTests`, strategy tests.

### A5. Timings as options, and debounced client sync

1. `ContactCenterCoordinationOptions` add `AssignmentLockTimeout` (10 s), `AssignmentLockExpiration` (30 s),
   `ReservationLockTimeout`, `ReservationLockExpiration`, `ReclaimLockWait` (50 ms), `MaxOfferAttempts` (25),
   `MaxReclaimPerOffer` (4), `QueuedWorkSyncLease` (2 s), `QueuedWorkSyncFallbackInterval` (30 s). Add
   `ContactCenterCoordinationOptionsValidator : IValidateOptions` and bind in Contact Center `Startup`.
2. Inject `IOptions<ContactCenterCoordinationOptions>` into `ActivityAssignmentService`, `ActivityReservationService`,
   `VoiceQueueOfferService`; delete the static fields.
3. `QueuedVoiceWorkOfferService.OfferForProfileAsync`: acquire `IDistributedLock` `ContactCenterQueuedWorkSync:{agentId}`
   with zero wait and `QueuedWorkSyncLease` expiration; if not acquired, return 0 (another sync is in flight or just
   ran).
4. `contact-center-soft-phone.js`: call `SyncQueuedVoiceWork` on connect, reconnect, presence change, and offer
   completion, plus a fallback timer using the interval exposed in the registration config
   (`AgentSoftPhoneEndpoints.HandleRegistrationConfigAsync` adds `queuedWorkSyncFallbackSeconds`).
5. Tests: `ContactCenterOptionsValidationTests`, `QueuedVoiceWorkOfferServiceTests` debounce, Playwright assertion
   on request count.

### B2. Scoped handoff turn

1. `Omnichannel.Core/Services/IOmnichannelHandoffTurn.cs`:

```csharp
public interface IOmnichannelHandoffTurn
{
    bool HandoffRequested { get; }
    string Reason { get; }
    void RequestHandoff(string reason);
    void Reset();
}
```

   Scoped implementation `OmnichannelHandoffTurn` registered in `Omnichannel.Managements/OmnichannelActivitiesStartup.cs`
   (`AddScoped`). Because a turn runs inside one scope per completion, `Reset()` is called by the handler before
   each completion.
2. `TransferToAgentTool.InvokeCoreAsync`: `arguments.Services.GetService<IOmnichannelHandoffTurn>()?.RequestHandoff(reason)`.
3. `SmsOmnichannelEventHandler` and `TelnyxAiVoiceConversationHandler`: inject `IOmnichannelHandoffTurn`, call
   `Reset()` before the completion, read after. Delete `OmnichannelHandoffTurnContext`.
4. Tests: `Modules/Omnichannel/TransferToAgentToolTests.cs` and `OmnichannelHandoffTurnContextTests.cs` rewritten
   against the scoped service (rename the latter `OmnichannelHandoffTurnTests`).

### B3. Voice handoff correctness and context

1. `VoiceAgentHandoffService`: constructor takes `IBusinessHoursGate` and `ICallbackService` directly (see E2 for
   null-object defaults), plus `IDistributedLock` and `IOptions<ContactCenterCoordinationOptions>`; wrap the body in
   the inbound lock key `ContactCenterInboundVoice:{provider}:{callId}`.
2. Do not overwrite `activity.Source`; set `activity.Kind = Call`, `InteractionType = Manual`, `AiEscalated = true`.
   Interaction `Direction` = the activity's original direction (`Outbound` when `Source` is a dialer/AI source; use
   `DialerActivitySourceHelper` to classify).
3. Add to `Interaction` (Core model): `HandoffSummary`, `HandoffReason`, `HandoffAiSessionId`; populate from the
   request. Surface them in `AgentDesktopSnapshot`/`PendingIncomingCallOffer` (`ContactCenterIncomingCallFactory`
   metadata) so `contact-center-agent-bar.js` and the soft phone offer panel can render "AI summary".
4. Return `OmnichannelHandoffResult` with a new `WaitingInQueue` disposition when no agent was offered; the AI
   voice handler speaks a distinct line per disposition (`Routed`, `WaitingInQueue`, `CallbackScheduled`).
5. Tests: new `Modules/ContactCenter/VoiceAgentHandoffServiceTests.cs` (outbound origin preserved; concurrent calls
   → one enqueue; context stored; after-hours callback once; second call after completion is a no-op).

### B4. SMS handoff through the router

Depends on C7 (below). Until C7 lands, minimum: `SmsAgentHandoffService` validates the queue exists via
`IAgentQueueMembershipReader`/queue catalog, and when the endpoint's `DistributionMode == Routed` and the routed
feature is enabled, calls `ISmsRoutingStrategy.SelectAgentAsync` to push-assign; otherwise pools. Tests listed on
the SMS page.

### E2. Null-object defaults instead of scanning and service location

For each contract: add the default in the feature that **declares** it, and `Replace` in the feature that implements it.

| Contract | Default (registered with `TryAddScoped`) | Where |
| --- | --- | --- |
| `IBusinessHoursGate` | `AlwaysOpenBusinessHoursGate` | `Omnichannel.Core` `AddOmnichannelCore` extension (declares the gate) |
| `ICallbackService` | `NoCallbackService` (returns null, promotes 0) | Contact Center base `Startup` |
| `IAgentWorkStateHealingService` | `NoAgentWorkStateHealingService` | `QueuesStartup` |
| `IDialerProfileManager` read side | new `IDialerProfileReader` with `NullDialerProfileReader` | `QueuesStartup`; Dialer feature replaces |
| `IQueuedVoiceWorkOfferService` | `NoQueuedVoiceWorkOfferService` | `VoiceStartup` |
| `IInboundVoiceService` | none (mandatory in Voice); `ReofferVoiceWorkHandler` takes it by constructor | `VoiceStartup` |

Then remove `IServiceProvider` from `VoiceAgentHandoffService`, `ReofferVoiceWorkHandler`,
`OmnichannelActivityAuthorizationHandler` (inject `IAuthorizationService` lazily through `Lazy<T>` if a cycle
appears), and every minimal-API handler that calls `httpContext.RequestServices.GetService` (add the parameter to the
handler signature; ASP.NET Core injects it). Add
`tests/CrestApps.OrchardCore.Tests/Architecture/DependencyInjectionArchitectureTests.cs` asserting: no constructor
parameter of type `IServiceProvider` in Core/Module assemblies except an allow-list
(`ContactCenterScopeExecutor`, `*ScopeContext`, `*BackgroundTask`, `ContactCenterHub`); no field of type
`IEnumerable<T>` whose only use is `FirstOrDefault()` (check by scanning source files for the pattern
`Services.FirstOrDefault()` / `Managers.FirstOrDefault()` within constructors).

### E3. Configuration coverage

Covered by A5 for Contact Center. For Omnichannel automation create `OmnichannelAutomationOptions`
(`ProcessorLeaseMilliseconds`, `ProcessorBatchSize`, `MaxActivitiesPerInvocation`, `MaxProcessingAttempts`,
`RetryDelayMinutes`) bound from `CrestApps:Omnichannel:Automation`, validated, and consumed by
`AutomatedActivitiesProcessorBackgroundTask` through `serviceProvider.GetRequiredService<IOptions<...>>()`.
Extend `OmnichannelConfigurationCoverageTests`.

### E4. Static state

- `OmnichannelHandoffTurnContext` — removed by B2.
- `SmsOmnichannelEventHandler._activeGenerations` → `IAutomatedConversationGate` (`Omnichannel.Core/Services`),
  implementation `InMemoryAutomatedConversationGate` registered `AddSingleton` **inside the tenant container**
  (Orchard startups register per tenant, so a plain `AddSingleton` in `Omnichannel.Sms/Startup.cs` is per tenant),
  with `TryBegin(sessionId, out registration)` and cancellation of the superseded generation; a `RedisAutomatedConversationGate`
  behind `[RequireFeatures("OrchardCore.Redis")]` using `IDistributedLock` can follow. Tests: `AutomatedConversationGateTests`.

## 3. Phase P2 — contact-center feature parity (outline with locations)

| Item | Where the code goes | Key new types |
| --- | --- | --- |
| A1 cross-queue arbitration | `ContactCenter.Core/Services/AgentWorkSelector.cs`, model `AgentQueueMembership` in `ContactCenter.Core/Models`, migration in `ContactCenter/Migrations/AgentProfileMigrations` | `IAgentWorkSelector` |
| A2 skills | `ContactCenter.Core/Models/AgentSkill.cs`, `QueueSkillRequirement.cs`; strategies in `ContactCenter.Core/Services` | `PreferredSkillsRoutingStrategy` |
| A6 queue treatment and callbacks | Abstractions `IQueueTreatmentProvider`, Core `QueueTreatmentPolicy`, `EstimatedWaitTimeCalculator`, `QueuedCallbackService`; Telnyx `TelnyxQueueTreatmentProvider`; new background task `QueueTreatmentBackgroundTask` (10-second cadence is not possible with cron; use a 1-minute task that loops with `Task.Delay` bounded by the lease, like `AutomatedActivitiesProcessorBackgroundTask`) | `QueueItem.NextTreatmentDueUtc`, `ActivityQueue.MaxWaitSeconds`, `OverflowTargets` |
| A7 IVR | `ContactCenter.Core/Models/IvrFlow.cs`, `IvrFlowStateMachine.cs`; provider gather in `TelnyxApiClient`; editor under `Views/EntryPoints` | `IIvrFlowRunner` |
| A8 caller priority | `ContactCenter.Core/Services/IInboundPriorityContributor.cs` chain used by `InboundVoiceCallProcessor.CreateActivityAsync` |  |
| A9 transfers | extend `ContactCenterTransferService` with `StartConsultAsync`, `CompleteAsync`, `CancelAsync`; soft phone `soft-phone.js` transfer view lists targets from `Admin/api/crestapps/contact-center/transfer-targets` | `TransferSession` |
| A10 predictive | `PredictiveDialerStrategy` next to `PowerDialerStrategy`; `DialerStrategyResolver` maps it; UI hides the mode until `DialerPaced` feature and abandonment policy configured |  |
| C7 SMS router | `Sms.Workspace.Core/Services/SmsConversationRouter.cs` wrapping the chain; `SmsConversationRollup.cs` | `ISmsConversationRouter` |
| C8 SMS availability and SLA | derive availability from `IAgentSessionManager` heartbeat; `SmsConversation.FirstResponseDueUtc`; sweep in the routed feature |  |
| C9 compliance | `Sms.Workspace.Core/Services/SmsKeywordPolicy.cs`; auto-reply step in the router; quiet-hours check in `SmsConversationService.SendAsync` using `IBusinessHoursGate` with contact time zone (`OmnichannelContactTimeZoneHandler` already resolves it) |  |
| D3 Telnyx tests | `tests/.../Telnyx/*` with `TelnyxApiHandler` double under `tests/.../Doubles` |  |
| D4 endpoint resolver | `Telnyx.Core/Services/TelnyxAgentEndpointResolver.cs` | `ITelnyxAgentEndpointResolver` |

## 4. Phase P3 — structural cleanup (outline)

- D5 `TelnyxApiClient`: `Telnyx.Core/Services/TelnyxApiClient.cs`, registered with
  `services.AddHttpClient<TelnyxApiClient>()` configured from `IOptionsMonitor<TelnyxOptions>`; migrate providers
  one method at a time behind the existing tests from D3.
- D6 soft phone modules: `Telephony/Assets/js/soft-phone/*.js`; update `gulpfile` entries for the module (see the
  repository root `gulpfile.js` and the `Assets` conventions used by `contact-center-*.js`); add `vitest` to the root
  `package.json` dev dependencies with `npm test` script and a CI step.
- D8 `Omnichannel.Voice` module: new project `src/Modules/CrestApps.OrchardCore.Omnichannel.Voice` (Manifest
  feature `CrestApps.OrchardCore.Omnichannel.Voice`, depends on AI Chat Core and Omnichannel Management), move the
  three Telnyx files, add `IVoiceAgentMediaProvider` to Telephony Abstractions, Telnyx implements it. Add the
  project to the solution and to `src/Startup/CrestApps.OrchardCore.Cms.Web` references like the other modules.
- E5/E6 duplication and splits: do each only with characterization tests first; keep public types stable or update
  baselines deliberately.
- E8 CI: in `.github/workflows/pr_ci.yml` add the FeatureActivationTests step (copy from `release_ci.yml`) and the
  Playwright job (needs `playwright install --with-deps chromium`).

## 5. Verification checklist per item

1. `dotnet build` of the test project succeeds with no new warnings.
2. New tests were red before the change and are green after; existing tests for the touched module are green.
3. `PublicApiApprovalTests` green (baselines updated only when the surface change is intended).
4. For UI changes: `npm run rebuild` succeeds and the relevant Playwright test (if any) passes.
5. For docs changes: `cd src/CrestApps.Docs && npm run build` succeeds.
6. A short entry appended to `production-readiness-changelog.md` (item id, files touched, tests added, anything
   deferred and why).
