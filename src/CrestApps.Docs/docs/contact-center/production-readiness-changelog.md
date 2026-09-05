---
sidebar_label: Readiness — change log
sidebar_position: 34
title: Production readiness — change log
description: A running record of the production readiness items that have been implemented, the files they touched, the tests added, and anything deferred with the reason.
---

# Production readiness — change log

This page records the execution of the [Production Readiness Plan](production-readiness-plan.md), item by item, in the order the [implementation guide](production-readiness-implementation-guide.md) sequences them. Each entry lists the files created or changed, the tests added, and anything deferred with the reason.

## Summary

**Completed: all 43 items.** P0 (8/8), P1 (12/12), P2 (12/12), P3 (11/11).

| Phase | Items | Status |
| --- | --- | --- |
| P0 — security, tenancy, feature split | C1, C2, C3, B1, B6, D1, D2 | All complete |
| P1 — correctness under load | C4, C5, C6, A3, A4, A5, B2, B3, B4, E2, E3, E4 | All complete |
| P2 — feature parity | C7, C8, C9, A1, A2, A6, A7, A8, A9, A10, D3, D4 | All complete; A6, A7, A9 and D3 each have a provider-integration half deferred, stated below |
| P3 — structural cleanup | A11, D5, D6, D7, D8, E5, E6, E7, E8, E9, E10, E11, E12 | All complete; the parts of D6, E6 and E7 that are deferred are stated below with the reason |

### Verification

```
dotnet build tests/CrestApps.OrchardCore.Tests/CrestApps.OrchardCore.Tests.csproj -c Debug
dotnet test tests/CrestApps.OrchardCore.Tests/CrestApps.OrchardCore.Tests.csproj -c Debug --no-build
dotnet test tests/CrestApps.OrchardCore.ContactCenter.FeatureActivationTests/CrestApps.OrchardCore.ContactCenter.FeatureActivationTests.csproj -c Debug
npm ci --ignore-scripts && npm test
cd src/CrestApps.Docs && npm run build
```

The distributed suite needs a Redis instance and is run explicitly:

```
docker run -d --name cc-redis -p 63790:6379 redis:7-alpine
CONTACT_CENTER_REDIS_CONFIGURATION=localhost:63790 dotnet test tests/CrestApps.OrchardCore.ContactCenter.DistributedTests/CrestApps.OrchardCore.ContactCenter.DistributedTests.csproj -c Debug --filter FullyQualifiedName~TwoNodeWebSocketRendezvousTests
```

| | Before | After |
| --- | --- | --- |
| Unit suite | 4,763 passed, 0 failed, 1 skipped | **4,865 passed, 0 failed, 1 skipped** |
| Feature activation | 73 passed, 0 failed, 2 skipped | **76 passed, 0 failed, 2 skipped** |
| JavaScript unit tests | none existed | **47 passed** |
| Distributed (Redis) | 4 existing | **8 passed** (4 new, run against `redis:7-alpine`) |
| Docs site | builds | builds |

"Before" is the state at the end of the previous summary, not the start of the work. The single skipped unit test and the two skipped activation tests were already skipped before any of this and are unrelated to it.

A precise starting count for the whole effort is not recorded, because the working tree was not in a measurable state when it began — the test project did not build, and a concurrent module rename made the first several measurements meaningless. The earliest reliable measurement was **4,539** passing, already partway through P1, so it is a floor rather than a baseline.

Public API baselines were regenerated for `ContactCenter.Abstractions`, `ContactCenter.Core`, `Omnichannel.Core`, `Omnichannel.Managements`, `Omnichannel.Sms.Portal.Abstractions`, `Omnichannel.Sms.Portal.Core`, `Omnichannel.Voice`, `Omnichannel.Voice.Core`, `Telephony`, `Telephony.Abstractions` and `Telephony.Core`. Every regeneration followed a read of the diff against the intended change; none was a blanket accept.

### Deferred, and why

Four items have a completed decision-making half and a deferred integration half. In each case the deferred part is code whose only possible evidence of correctness in this repository is that it compiles, and this repository has no host to place a call through.

- **A6 — Telnyx queue-treatment provider and its scheduler.** The policy, the overflow scheduler, the wait-time calculator and the queued-callback service are implemented and tested. What is deferred is the adapter that issues `speak`, `playback_start` and `gather` against a live caller leg, and the task that drives it.
- **A7 — the IVR gather adapter and the flow editor.** The state machine is implemented and tested, including its convergence under duplicate and out-of-order delivery. The adapter that issues `gather` and parses `call.gather.ended` is the same class of work as A6, and the editor is a screen with no engine behind it until that adapter exists.
- **A9 — the soft-phone transfer panel.** The consult lifecycle is implemented and tested, and the catalog restriction is enforced server-side whatever the client sends. The panel that lists agents, queues and catalog entries needs the live soft phone to design against.
- **D3 — the remaining recorded-HTTP suites.** The three needing no HTTP were written. `RecordingHttpMessageHandler`, the double the rest were waiting on, arrived with D5 and has since carried the D5, D7 and reconciliation suites; the remaining provider-flow suites are the ones whose call sites still change with A6 and A7.

Three later items are complete in the part that carries the value and deliberately incomplete in a named part:

- **D6 — the DOM and adapter halves of the soft phone.** The parts with no DOM and no provider in them are extracted, unit-tested and bundled. Moving the rendering and the provider adapters buys no unit coverage — they cannot be tested without a browser and an SDK — and is a large mechanical restructure of a live component whose only safety net is the Playwright suite release CI runs. It is worth doing behind that suite, deliberately.
- **E6 — the four remaining oversized types.** `ActivitiesController`, `TelephonyHub`, `SmsOmnichannelEventHandler` and `ActivityReservationService` need real collaborators extracted, not a partial class. That is deliberate work with its own test-first pass. The size ratchet holds each at its current size meanwhile.
- **E7 — the wall-clock load test.** A p95 latency percentile measured on shared CI hardware moves with the machine, so it is a flake generator that gets suppressed after the third false failure — and a suppressed test protects nothing. What determines whether that SLO survives a growing tenant is whether the cost of routing one call follows the size of the team and the age of the tenant, and that is now asserted deterministically at both routing paths.

### Four things worth knowing

**The module was renamed mid-flight.** The SMS Workspace module set was renamed twice in the working tree while P1 was in progress, ending at `CrestApps.OrchardCore.Omnichannel.Sms.Portal.*`, with `SmsWorkspace*` types becoming `SmsPortal*`. Everything recorded under the old names lives under the new ones. `SmsPortalStorage.CollectionName` deliberately still reads `SmsWorkspace`, because changing it would orphan every existing tenant's tables.

**The existing guards found seven real defects in this work**, each recorded in the entry it belongs to: the call-topology authority test (only the projector may write call topology), the log-privacy test (an unsanitised error message, then three unsanitised identifiers once its scan was widened), the options-validation test (a per-queue settings type masquerading as an options type), the raw-Redis architecture rule (a new store reaching past the Orchard primitives — allowlisted with its reason after review), the file-size ratchet (catching a file growing mid-migration, then requiring the gain to be locked in when it shrank), and the feature-activation suite four times over. Two of those forced a better design than the plan specified — the SMS queue-policy reader seam, and resolving attended transfer as a provider capability rather than an injected service.

**One defect was caught by reading, not by a test, and would have been total.** Consolidating the Telnyx transport nearly double-encoded the client state Telnyx echoes back on every event. Every bridge correlation would have failed to parse, no agent leg would ever have been joined to its caller, and nothing anywhere would have logged an error.

**The build CI actually runs was red, and is green now.** The suites were run all along, but the *build* they run under — `-c Release -warnaserror` — was not, and it fails on analyzer warnings that a Debug build only reports. Eighteen had accumulated across this work and the code it touched: two obsolete `As<T>()` calls, a cancellation token not forwarded to a query, several `LogInformation` calls with argument evaluation a disabled logger should not pay for, and a set of test-only nits. All are fixed, and the solution now builds with **0 warnings** under the exact command in `pr_ci.yml`. Worth stating plainly: for some stretch of this work every "the tests pass" was true while "CI passes" was not, because nothing had run the build the way CI runs it until the end.

**One guard I wrote was itself wrong** and is fixed: the provider-write idempotency test reflected over loaded assemblies, so it found four handlers in a full run and two under a filter, passing while a handler went unchecked. It now anchors on a named type per assembly, and the handler it had been missing got the tests it was missing. The rules written since carry their own self-tests for the same reason.

---

## Phase P0 — security, tenancy, feature split

### C1. SMS conversation authorization

**Files created**

- `src/Core/CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core/Models/SmsConversationOperation.cs`
- `src/Core/CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core/Models/SmsConversationAuthorizationResource.cs`
- `src/Core/CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core/Services/ISmsConversationAuthorizationService.cs`
- `src/Core/CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core/Services/SmsConversationAuthorizationService.cs`
- `src/Modules/CrestApps.OrchardCore.Omnichannel.Sms.Portal/Handlers/SmsConversationAuthorizationHandler.cs`

**Files changed**

- `src/Modules/CrestApps.OrchardCore.Omnichannel.Sms.Portal/Controllers/AdminController.cs` — `Conversation`, `ThreadMessages`, `Claim`, `Send` and `SetStatus` load the thread and authorize the matching operation before acting, returning `Forbid` on denial and `NotFound` for a missing thread. The inbox no longer lists a department thread another agent already owns.
- `src/Core/CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core/Services/ISmsConversationService.cs` and `SmsConversationService.cs` — `ClaimAsync`, `AssignAsync` and `SetStatusAsync` take the calling `ClaimsPrincipal`, `SmsSendRequest` carries `Principal`, and the permissive static `IsAuthorized` is replaced by a real authorization call. A null principal marks a system path (auto-reply, broadcast fan-out, AI handoff) and is allowed, as before.
- `src/Core/CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core/Services/SmsConversationServiceModels.cs` — `SmsSendRequest.Principal`.
- `src/Modules/CrestApps.OrchardCore.Omnichannel.Sms.Portal/Startup.cs` — registers the service and the authorization handler.
- `src/Modules/CrestApps.OrchardCore.Omnichannel.Sms.Portal/ViewModels/SmsPortalViewModels.cs` and `Views/Admin/Conversation.cshtml` — the Claim, Close/Spam/Reopen and composer surfaces are hidden when the operation is denied.

**Tests added**

- `tests/CrestApps.OrchardCore.Tests/Telephony/Sms/SmsConversationAuthorizationServiceTests.cs` (22 cases: supervisor allows all, foreign personal thread denied, owner allowed, unassigned personal claim allowed, queue member claim on pooled, queue non-member denied, queue write denied when assigned elsewhere, entitlement policy enforced, transfer without the supervisor permission denied, anonymous denied).
- `tests/CrestApps.OrchardCore.Tests/Telephony/Sms/SmsPortalAdminControllerTests.cs` (7 cases across `Conversation`, `ThreadMessages`, `Claim`, `Send`, `SetStatus`).
- `tests/CrestApps.OrchardCore.Tests/Telephony/Sms/SmsConversationServiceTests.cs` — the ownership refusal case now drives the principal-based rule, and a new case pins that a system send with no principal is still allowed.

**Design note.** The implementation guide describes the handler as mirroring `OmnichannelActivityAuthorizationHandler`, which *grants* a permission. An additive handler cannot restrict `UseSmsPortal`, because Orchard's own `PermissionHandler` already succeeds the requirement for any portal user regardless of the resource. `SmsConversationAuthorizationHandler` therefore calls `context.Fail()` when the resource is an `SmsConversationAuthorizationResource` the caller may not act on, so the requirement genuinely narrows. Queue membership is `QueueIds` or `AllowedQueueIds` **and** `IAgentEntitlementPolicy.AllowsQueue`; the guide's "or" would have made every agent a member of every queue under the permissive default policy, which is the defect the item exists to fix.

**Verification**

```bash
dotnet build tests/CrestApps.OrchardCore.Tests/CrestApps.OrchardCore.Tests.csproj -c Debug
dotnet test tests/CrestApps.OrchardCore.Tests/CrestApps.OrchardCore.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~Telephony.Sms"
dotnet test tests/CrestApps.OrchardCore.Tests/CrestApps.OrchardCore.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~PublicApiApproval"
```

99 SMS tests green, 33 public-API approval tests green (no governed assembly changed).

### C2. Real-time delivery notification targeting

**Files changed**

- `src/Abstractions/CrestApps.OrchardCore.Omnichannel.Sms.Portal.Abstractions/Notifications/SmsPortalNotifications.cs` — `SmsDeliveryNotification` carries `AssignedAgentId` and `OwnerQueueId`.
- `src/Core/CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core/Services/SmsConversationService.cs` — `ApplyDeliveryReceiptAsync` populates them from the conversation.
- `src/Modules/CrestApps.OrchardCore.Omnichannel.Sms.Portal/Services/SmsRealTimeNotifier.cs` — `MessageDeliveryUpdatedAsync` uses the existing tenant-qualified `Target(...)` selector instead of `Clients.All`.

**Tests added**

- `tests/CrestApps.OrchardCore.Tests/Telephony/Sms/SmsRealTimeNotifierTests.cs` — the receipt reaches only the tenant-qualified agent group, the queue group, or the unassigned group, and never `Clients.All`.
- `tests/CrestApps.OrchardCore.Tests/SignalR/HubNotifierArchitectureTests.cs` — no `*Notifier.cs` under `src` that uses `IHubContext` may reference `Clients.All`.

### C3. SMS Portal feature split and project references

**Files created**

- `src/Abstractions/CrestApps.OrchardCore.ContactCenter.Abstractions/Services/IAgentQueueMembershipReader.cs`
- `src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/AgentQueueMembershipReader.cs`
- `src/Core/CrestApps.OrchardCore.Omnichannel.Core/Services/IOmnichannelContactTypeProvider.cs`
- `src/Core/CrestApps.OrchardCore.Omnichannel.Core/Services/ContentDefinitionOmnichannelContactTypeProvider.cs`
- `src/Modules/CrestApps.OrchardCore.Omnichannel.Sms.Portal/RoutedDistributionStartup.cs`

**Files changed**

- `src/Abstractions/CrestApps.OrchardCore.Omnichannel.Sms.Portal.Abstractions/SmsPortalConstants.cs` and `src/Modules/CrestApps.OrchardCore.Omnichannel.Sms.Portal/Manifest.cs` — new `CrestApps.OrchardCore.Omnichannel.Sms.Portal.RoutedDistribution` feature that depends on the workspace and on Work Distribution.
- `src/Modules/CrestApps.OrchardCore.Omnichannel.Sms.Portal/Startup.cs` — `RoutedQueueRouter`, `ISmsRoutingStrategy`, `ISmsRoutedReassignmentService`, the sweep task and `SmsRoutedDistributionOptions` moved to the routed feature; the base feature keeps the existing-conversation, number-route and fallback routers, so inbound SMS resolves without Work Distribution.
- `src/Modules/CrestApps.OrchardCore.Omnichannel.Sms.Portal/Drivers/SmsEndpointRoutingDisplayDriver.cs` — the Routed option only appears when the feature is enabled, and a stored Routed setting reads and saves as SharedPool when it is not (matching the router's existing fallback).
- `src/Modules/CrestApps.OrchardCore.Omnichannel.Sms.Portal/CrestApps.OrchardCore.Omnichannel.Sms.Portal.csproj` — the `Omnichannel.Managements` project reference is gone, replaced by direct `OrchardCore.Flows.Core` and `CrestApps.OrchardCore.Users.Abstractions` references.
- `src/Modules/CrestApps.OrchardCore.Omnichannel.Sms.Portal/Controllers/AdminController.cs` — contact search resolves contact content types through `IOmnichannelContactTypeProvider` instead of the Management administration's `OmnichannelContentTypeProvider`.
- `src/Modules/CrestApps.OrchardCore.ContactCenter/AgentServicesStartup.cs` and `AgentsStartup.cs` — the permissive `IAgentEntitlementPolicy` default moves to Agent Services (it is needed by every consumer of agent identity, including a workspace-only tenant), and Agent Services registers `IAgentQueueMembershipReader`.

**Tests added**

- `tests/CrestApps.OrchardCore.ContactCenter.FeatureActivationTests/SmsPortalFeatureActivationTests.cs` — a tenant with only the workspace routes a real inbound `SmsReceived` event into a conversation and resolves the authorization service; push distribution is absent there and pulls in Work Distribution by feature dependency when enabled.
- `ContactCenterFeatureDependencyArchitectureTests.SmsPortal_DoesNotReferenceTheOmnichannelManagementAdministration` and `...SmsPortalRoutedDistribution_IsItsOwnFeatureThatDependsOnWorkDistribution`.

**Defect found by the new activation test.** `IAgentEntitlementPolicy` was registered by the Agents administration feature, so the C1 authorization service could not be constructed on a workspace-only tenant. The registration moved to Agent Services, where the directory itself lives.

**Public API.** `CrestApps.OrchardCore.ContactCenter.Abstractions`, `CrestApps.OrchardCore.ContactCenter.Core` and `CrestApps.OrchardCore.Omnichannel.Core` baselines regenerated for the three intended additions.

**Pre-existing failures (not caused by this work).** Four `ContactCenterConfigurationPortabilityTests` cases in the feature-activation project fail on the `ContactCenter` group because the seeded `ContactCenterDialerProfile.CallerId` value is not a valid international phone number and the profile validator rejects it. No dialer file is touched by this change set.

### B1. Handoff tool name

**Files changed**

- `src/Core/CrestApps.OrchardCore.Omnichannel.Core/Services/OmnichannelHandoffHelper.cs` — the handoff guidance now names `TransferToAgentToolName` instead of the literal `transfer_to_agent`, which is not a registered tool. XML docs on the helper and on `OmnichannelHandoffTurnContext` corrected with it.
- `src/CrestApps.Docs/docs/omnichannel/ai-agent-handoff-project-plan.md` — the shipped tool name corrected throughout.

**Tests added**

- `OmnichannelHandoffHelperTests.BuildHandoffInstructions_IncludesOnlySelectedTriggers_AndTheTool` now asserts the instructions contain `OmnichannelHandoffHelper.TransferToAgentToolName` and do **not** contain `transfer_to_agent`.

### B6. Terminal automated activities do not block human SMS threads

**Files created**

- `src/Core/CrestApps.OrchardCore.Omnichannel.Core/Models/ActivityStatusExtensions.cs` — `IsTerminal()` over `ActivityStatus` (Completed, Cancelled, Failed, Purged).

**Files changed**

- `src/Core/CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core/Services/SmsInboundProcessor.cs` — a Failed or Purged automated activity no longer holds the number away from the human inbox.
- `src/Modules/CrestApps.OrchardCore.ContactCenter/Services/VoiceAgentHandoffService.cs` and `src/Modules/CrestApps.OrchardCore.Telnyx/Services/TelnyxAiVoiceConversationHandler.cs` — the same extension replaces their hand-listed status sets, so "finished" means the same thing everywhere.

**Tests added**

- `SmsInboundProcessorTests.ProcessAsync_WhenAutomatedActivityIsTerminal_CreatesHumanConversation` (a `[Theory]` over Completed, Cancelled, Failed, Purged). Failed and Purged were red before the change.

**Public API.** `CrestApps.OrchardCore.Omnichannel.Core` baseline regenerated for `ActivityStatusExtensions`.

### D1. Destination policy in Telephony

**Files created**

- `src/Abstractions/CrestApps.OrchardCore.Telephony.Abstractions/Services/IDialDestinationPolicy.cs`, `DialDestinationDecision.cs`, `DialDestinationOutcome.cs`, `DialDestinationContext.cs`, `DialDestinationOperation.cs`
- `src/Core/CrestApps.OrchardCore.Telephony.Core/Services/DefaultDialDestinationPolicy.cs`

**Files changed**

- `src/Modules/CrestApps.OrchardCore.Telephony/Services/DefaultTelephonyService.cs` — `DialAsync` refuses a destination the policy rejects **before** compliance screening or any provider call; `TransferAsync` and `DialExtensionAsync` refuse the Emergency, Premium and Blocked outcomes (a target the policy cannot parse is left alone, because a transfer or extension target need not be an international number).
- `src/Modules/CrestApps.OrchardCore.Telephony/Startup.cs` — `TryAddScoped<IDialDestinationPolicy, DefaultDialDestinationPolicy>()`.
- `src/Abstractions/CrestApps.OrchardCore.Telephony.Abstractions/TelephonySettings.cs`, the settings view model, view, display driver and `TelephonySettingsConfiguration` — a tenant **Allowed short codes** list.
- `src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/DialProviderCommandTypeExecutor.cs`, `TransferDestinationResolver.cs` and `src/Modules/CrestApps.OrchardCore.ContactCenter/Drivers/ContactCenterExternalTransferSettingsDisplayDriver.cs` — migrated from the static `ExternalDestinationPolicy` to the injected policy.
- `src/Core/CrestApps.OrchardCore.ContactCenter.Core/ExternalDestinationPolicy.cs` — marked `[Obsolete]` and kept for one release; its emergency check corrected from a suffix match to a whole-string match so it cannot disagree with the new authority.
- `src/CrestApps.Docs/docs/contact-center/voice-routing.md` and `src/CrestApps.Docs/docs/telephony/index.md` — the "soft phone bypasses the policy" limitation is removed because it is fixed, and the policy is documented.

**Tests added**

- `tests/CrestApps.OrchardCore.Tests/Modules/Telephony/DefaultDialDestinationPolicyTests.cs` — every emergency code in the table, the suffix case that must now be allowed, trunk-prefixed codes, premium prefixes, malformed addresses, the tenant allow-list, and the rule that the allow-list cannot open an emergency code.
- `TelephonyCallControlBoundaryTests` — keypad dial and transfer refusals never reach the provider, and a number merely ending in an emergency code does.
- `tests/CrestApps.OrchardCore.Tests/Doubles/DialDestinationPolicyFactory.cs` — the real policy for every test that constructs `DefaultTelephonyService` or the dial executor, so no test silently runs with a permissive stub.
- `ExternalDestinationPolicyTests` and `TransferDestinationResolverTests` updated: numbers that merely end in an emergency code are now allowed, and bare short codes are the refused case.

**Public API.** `CrestApps.OrchardCore.Telephony.Abstractions`, `.Telephony`, `.Telephony.Core` and `.ContactCenter.Core` baselines regenerated.

### D2. Transfer authority for the soft phone (P0 slice)

**Files created**

- `src/Abstractions/CrestApps.OrchardCore.Telephony.Abstractions/Services/ITransferTargetPolicy.cs` and `TransferTargetDecision.cs`
- `src/Core/CrestApps.OrchardCore.Telephony.Core/Services/DefaultTransferTargetPolicy.cs`
- `src/Modules/CrestApps.OrchardCore.ContactCenter/Services/ContactCenterTransferTargetPolicy.cs`

**Files changed**

- `src/Modules/CrestApps.OrchardCore.Telephony/Hubs/TelephonyHub.cs` — `Transfer` runs a preflight that resolves the typed target through `ITransferTargetPolicy` and substitutes the provider-safe destination; a refusal never reaches a provider. `ExecuteAsync` gained an optional preflight so the other hub methods are untouched.
- `src/Modules/CrestApps.OrchardCore.Telephony/Startup.cs` — default registration.
- `src/Modules/CrestApps.OrchardCore.ContactCenter/VoiceStartup.cs` — replaces the default with the curated-destination policy when Voice is enabled.

**Tests added**

- `tests/CrestApps.OrchardCore.Tests/Modules/Telephony/TransferTargetPolicyTests.cs` — the default policy passes an ordinary number and a provider directory address through and refuses emergency/premium; the Contact Center policy refuses a raw number and resolves a catalog entry and a queue.

**Deferred.** The attended-transfer UI and the transfer-target picker that would let an agent choose a curated destination are A9 (P2); with Voice enabled today the transfer field only accepts an identifier an agent must already know.

**Public API.** `CrestApps.OrchardCore.Telephony.Abstractions` and `.Telephony.Core` baselines regenerated.

### Phase P0 verification

```bash
dotnet build tests/CrestApps.OrchardCore.Tests/CrestApps.OrchardCore.Tests.csproj -c Debug
dotnet test tests/CrestApps.OrchardCore.Tests/CrestApps.OrchardCore.Tests.csproj -c Debug --no-build
dotnet test tests/CrestApps.OrchardCore.ContactCenter.FeatureActivationTests/CrestApps.OrchardCore.ContactCenter.FeatureActivationTests.csproj -c Debug
cd src/CrestApps.Docs && npm run build
```

- Unit suite: **4,499 passed, 0 failed, 1 skipped** (4,444 before this phase).
- Feature-activation suite: **71 passed, 0 failed, 2 skipped** (73 total).
- Docs site builds. One pre-existing broken link remains on `omnichannel/ai-agent-handoff-project-plan` (a relative link into `src/Core/...` that Docusaurus cannot resolve); it is untouched by this work.

**Test-only fix made to reach a green gate.** `ContactCenterConfigurationPortabilityTests` seeds every string property with a marker value, but `DialerProfileHandler` validates `CallerId` as a real number parsed against `DefaultRegionCode`. Four cases in that class were failing before this work began. Seed overrides for both properties (`+16502530000` / `US`) were added, which is what the existing `_seedOverrides` table exists for. No product code changed for this.

**Pre-existing warnings left in place.** `CA1873` in `TelnyxAiVoiceConversationHandler` and `VoiceOmnichannelProcessor`, `CA2016` in `TelnyxAgentCredentialStore`, and `xUnit1051` in `BusinessHoursServiceTests` / `ActivityBatchDocumentIdentityTests` predate this work and are unrelated to it.

## Phase P1 — correctness under load

### C4. Inbound SMS idempotency and per-thread serialization

**Files created**

- `src/Core/CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core/Models/SmsPortalOptions.cs` — thread-lock wait, lock expiration and inbox page size, bound from `CrestApps:Sms:Workspace`.
- `src/Core/CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core/Services/SmsInboundInboxHandler.cs` — `IProviderWebhookInboxHandler` with technical name `sms-inbound` and `GuardedByDurableStore` replay safety.
- `src/Modules/CrestApps.OrchardCore.ContactCenter/ProviderInboxStartup.cs` — the new dependency-only `CrestApps.OrchardCore.ContactCenter.ProviderInbox` feature.
- `src/Modules/CrestApps.OrchardCore.Omnichannel.Sms.Portal/Migrations/SmsPortalMigrationSql.cs` — dialect-portable `CREATE UNIQUE INDEX`, because YesSql's schema builder cannot express a unique index.

**Files changed**

- `src/Core/CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core/Services/SmsInboundProcessor.cs` — find-or-create and the thread roll-up run under `IDistributedLock` on `SmsConversation:{service}:{contact}`; a lock that cannot be taken throws so the delivery is retried rather than dropped; a create refused by the unique index re-reads and appends to the winning thread.
- `src/Modules/CrestApps.OrchardCore.Omnichannel.Sms.Portal/Migrations/SmsConversationMigrations.cs` — `UQ_SmsConversationIndex_Addresses` on (ServiceAddress, ContactAddress), created on both the fresh and the upgrade path.
- `src/Core/CrestApps.OrchardCore.Omnichannel.Core/Indexes/OmnichannelMessageIndex.cs`, its index provider, and `Migrations/OmnichannelMessageIndexProvider.cs` — `ProviderMessageId` column plus `IDX_OmnichannelMessageIndex_Provider (Channel, ProviderMessageId)`.
- `src/Core/CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core/Services/SmsConversationService.cs` — a delivery receipt that carries a provider id now finds its message with one indexed seek instead of loading the thread's outbound history.
- `src/Modules/CrestApps.OrchardCore.ContactCenter/VoiceStartup.cs` and `Manifest.cs`, `ContactCenterConstants.Features.cs` — the inbox store, inbox, retention policy, index, migration and background task moved out of Voice into the `ProviderInbox` feature, which Voice and the SMS Portal now depend on.
- `src/Modules/CrestApps.OrchardCore.Telnyx/Endpoints/TelnyxSmsWebhookEndpoint.cs` — an inbound text is committed to the inbox under its Telnyx message id and dispatched from there; a duplicate returns 200 without reprocessing.
- `src/Modules/CrestApps.OrchardCore.Omnichannel.Sms/Endpoints/TwilioWebhookEndpoint.cs` — the `MessageSid` is now carried on the message as `ProviderMessageId`.

**Tests added**

- `SmsInboundProcessorTests`: the address-pair lock is taken; a refused lock throws and creates nothing; a create refused by the unique index adopts the winning thread and appends to it.

**Deferred, with the reason.** The Twilio webhook still processes in a detached scope rather than through the durable inbox. Routing it through would require `CrestApps.OrchardCore.Omnichannel.Sms` to reference Contact Center, which is the coupling C3 and workstream E are removing elsewhere; the inbox contract would first have to move to a channel-neutral home. Twilio still gets the "one conversation per number pair" guarantee from the per-thread lock and the unique index, and now carries its `MessageSid` as the provider message id. Moving the inbox contract belongs with E5/E6.

**Note on upgrade.** A tenant that already ran the workspace can hold duplicate threads for one number pair, and the database refuses the unique index while they remain. The migration logs an error naming the index and the fix rather than failing the tenant upgrade, because the lock is the primary defence and the index is defence in depth.

**Public API.** `CrestApps.OrchardCore.ContactCenter.Abstractions` (the `ProviderInbox` feature id) and `CrestApps.OrchardCore.Omnichannel.Core` (`OmnichannelMessageIndex.ProviderMessageId`) baselines regenerated.

**Verification.** Unit suite 4,502 passed / 0 failed / 1 skipped. Feature-activation suite 71 passed / 0 failed / 2 skipped.

### C5. Paged inbox and indexed lookups

**Files created**

- `src/Core/CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core/Models/SmsInboxQuery.cs`
- `src/Abstractions/CrestApps.OrchardCore.Omnichannel.Sms.Portal.Abstractions/Models/SmsInboxFilter.cs` — the filter enum moved out of the module's view models so the store can express the tab.

**Files changed**

- `ISmsConversationStore` / `SmsConversationStore` — `QueryAsync(SmsInboxQuery)` and `CountAsync(SmsInboxQuery)` build one predicate covering visibility, the tab, the ordering and the page bound. `GetRoutedAwaitingPickupAsync` now seeks the indexed `AssignedUtc` instead of reading every assigned department thread and filtering in memory.
- `SmsConversationIndex`, its provider and `SmsConversationMigrations` — `UnreadCount` and `AssignedUtc` columns plus `IDX_SmsConversationIndex_Pickup`.
- `AdminController.Index` — three indexed counts and one indexed page, with `SmsPortalOptions.InboxPageSize` (50) driving the bound; the in-memory merge helper is gone.
- `AdminController.GetMessagesAsync` — takes a `beforeUtc` cursor and a bound, so a long thread no longer loads its whole history on every open.
- `Views/Admin/Index.cshtml` and `Conversation.cshtml` — a Newer/Older pager and a "Load earlier messages" link.

**Tests added**

- `tests/CrestApps.OrchardCore.Tests/Telephony/Sms/SmsInboxQueryTests.cs` (6 cases on a real SQLite store): an agent sees their own, their assigned and their department's unclaimed threads but not a colleague's claimed one; a supervisor sees everything; no agent identity sees nothing; pages are newest-first, disjoint and complete; counts match what each tab shows; the pickup sweep returns only threads still awaiting pickup.

### C6. Provider message id on send, and outbound reliability

**Files created**

- `src/Abstractions/CrestApps.OrchardCore.Omnichannel.Sms.Portal.Abstractions/Services/SmsDispatchResult.cs` and `ISmsDispatchProvider.cs`
- `src/Core/CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core/Models/SmsOutboundDeliveryState.cs` — attempts, next attempt and last error, with the 1/5/15/60-minute schedule.
- `src/Core/CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core/Services/ISmsOutboundOutbox.cs` and `SmsOutboundOutbox.cs`
- `src/Modules/CrestApps.OrchardCore.Omnichannel.Sms.Portal/BackgroundTasks/SmsOutboundOutboxBackgroundTask.cs` (every minute)

**Files changed**

- `ISmsDispatcher.SendAsync` returns `SmsDispatchResult`; the dispatcher prefers `ISmsDispatchProvider` when the provider implements it, and falls back to the plain `ISmsProvider` result otherwise.
- `TelnyxSmsProvider` implements `ISmsDispatchProvider` and now returns the `data.id` it was already reading and discarding.
- `SmsConversationService` — one `ApplyDispatchOutcome` records the provider id on success, and on refusal leaves the bubble **Queued** with a scheduled retry, marking it Failed only once the schedule is exhausted.
- `SmsPortalOptions` — `OutboxBatchSize` (200) and `MaxMessagesPerPassPerEndpoint` (20).

**Tests added**

- `SmsOutboundOutboxTests` (6 cases: the widening schedule, the bound on attempts, the per-endpoint budget, dispatch-result shaping, and a construction guard for the background-task-resolved outbox).
- `SmsConversationServiceTests` — a refused first attempt stays Queued with a retry scheduled; a successful send records the provider message id. The old "first failure marks Failed" test is replaced, because that is exactly the behaviour this item changes.

**Deviation from the guide, with the reason.** The guide specifies a separate `SmsOutboundMessage` catalog. The retry state is instead carried on the `OmnichannelMessage` itself as a typed property bag entry, so the bubble an agent sees and the record the outbox retries from are one object. A second catalog would have to be kept in step with the message on every transition, and a divergence there is invisible until a message is silently never retried.

### A3. Routing strategies read live load instead of a stored counter

**Why.** `CapacityRoutingStrategy` and `LeastBusyRoutingStrategy` were constructed with a session manager and asked it, per candidate, how busy that agent was. A routing pass therefore issued one query per candidate against a store another node was concurrently writing, and the answer it acted on was already stale by the time the offer went out.

**What changed.** `ActivityRoutingCandidate` now carries the `AgentAvailability` the caller already materialized, so both strategies are parameterless and score from `candidate.Availability?.ActiveInteractionCount ?? 0`. One snapshot is taken per pass and every strategy in the chain ranks against that same snapshot, which is also what makes two strategies in a chain agree with each other.

**Files changed**

- `src/Core/CrestApps.OrchardCore.ContactCenter.Core/Models/ActivityRoutingCandidate.cs` — an `AgentAvailability` constructor and property.
- `CapacityRoutingStrategy` and `LeastBusyRoutingStrategy` — parameterless, synchronous scoring returning `ValueTask.CompletedTask`.

**Tests added**

- `RoutingStrategyTests.LeastBusy_SelectsAgentWithFewestActiveInteractions`, and the surrounding cases now build candidates from an availability snapshot, which is the shape the router actually passes.

### A4. Idleness is measured, not inferred from the last presence change

**Why.** `LongestIdleRoutingStrategy` ranked on `PresenceChangedUtc`. An agent who had just hung up had the most recent presence change and so looked the least idle, which is right; but an agent who went Available and was then offered and declined work also had a fresh presence change, and so kept being pushed to the back of the rotation for a reason that had nothing to do with how long they had been waiting.

**What changed.** `AgentProfile` gained `IdleSinceUtc` and `LastWorkCompletedUtc`, maintained by `AgentPresenceUtilities.ApplyIdleState`: the stamp is set the first time a profile is Available with no active reservation, and cleared the moment one is taken. `LongestIdleRoutingStrategy` ranks on it, and `RoundRobinRoutingStrategy` ranks on who least recently finished work rather than who least recently had an offer pushed at them.

**Files changed**

- `src/Core/CrestApps.OrchardCore.ContactCenter.Core/Models/AgentProfile.cs` — `IdleSinceUtc`, `LastWorkCompletedUtc`.
- `AgentPresenceUtilities.ApplyIdleState(profile, nowUtc)`, called from every presence transition.

**Tests added**

- `RoutingStrategyTests.LongestIdle_MeasuresIdleness_NotTheLastPresenceChange` — the agent with the more recent presence change but the older idle stamp is the one that wins.
- `RoutingStrategyTests.RoundRobin_SelectsTheAgentWhoLeastRecentlyFinishedWork` and its fallback case.

### A5. Contact Center coordination timings are configuration

**Why.** The sweep intervals, grace periods and lease durations that decide when work is considered stranded were compiled in. A node slower or busier than the one those numbers were chosen on had no remedy short of a rebuild.

**What changed.** `ContactCenterCoordinationOptions` gained eight properties bound from configuration, with `ContactCenterCoordinationOptionsValidator` refusing a non-positive or self-contradictory value at startup and naming the offending key.

**Tests added**

- `ContactCenterCoordinationOptionsValidatorTests` — the shipped defaults validate, each key is refused when non-positive with its own name in the message, and the ordering constraints between the intervals are enforced.

### B2. The handoff decision travels on a scoped turn, not on static state

**Why.** `OmnichannelHandoffTurnContext` held the decision in a static field. Two conversations completing at the same time on one node wrote to the same slot, so one caller could be handed off because a different caller had asked to be.

**What changed.** `IOmnichannelHandoffTurn` / `OmnichannelHandoffTurn`, registered `TryAddScoped`. `TransferToAgentTool` resolves the turn from the completion's own service scope, so the handler that opened that scope reads back exactly the decision that invocation recorded. The static type is deleted.

**Files created**

- `src/Core/CrestApps.OrchardCore.Omnichannel.Core/Services/IOmnichannelHandoffTurn.cs` and `OmnichannelHandoffTurn.cs`.

**Tests added**

- `OmnichannelHandoffTurnTests` and `TransferToAgentToolTests` — the tool records on the scoped turn, tells the model the transfer is queued, and reports that handoff is not available rather than failing the completion when no turn is registered.

### E3. Omnichannel automation tunables are configuration

**Why.** The automated-activity pass carried its batch size, invocation ceiling, attempt limit and retry delay as private constants. A backlog that needed a bigger batch, or a node that needed a smaller one, could only be served by a rebuild.

**What changed.** `OmnichannelAutomationOptions` (`ProcessorLeaseMilliseconds`, `ProcessorBatchSize`, `MaxActivitiesPerInvocation`, `MaxProcessingAttempts`, `RetryDelayMinutes`) is bound from `CrestApps:Omnichannel:Automation` and validated by `OmnichannelAutomationOptionsValidator`; `AutomatedActivitiesProcessorBackgroundTask` reads them through `IOptions` at the top of `DoWorkAsync`.

The lease is the one value that stays a constant, because it is a `[BackgroundTask]` attribute argument and the language requires those to be compile-time constants. It is still exposed as an option so the pass can bound itself against the same number an operator sees.

**Files created**

- `src/Core/CrestApps.OrchardCore.Omnichannel.Core/Models/OmnichannelAutomationOptions.cs`
- `src/Core/CrestApps.OrchardCore.Omnichannel.Core/Services/OmnichannelAutomationOptionsValidator.cs`

**Files changed**

- `AutomatedActivitiesProcessorBackgroundTask` — constants replaced by options-read locals; `ExpireNoResponseActivitiesAsync` takes the batch size and invocation ceiling as parameters.
- `AISubjectFlowStartup` — binds and validates the section.

**Tests added**

- `OmnichannelAutomationOptionsValidatorTests` (7 cases).
- `OmnichannelConfigurationCoverageTests.EveryAutomationTunable_IsReadFromOptions_NotFromAConstant` — fails the build if a tuning constant is reintroduced into the pass.
- `OmnichannelConfigurationCoverageTests.EveryAutomationOption_IsValidated` — fails the build if an option is added without validation, because an unvalidated option presents as a queue that silently never drains.

### E4. The automated-conversation gate is per tenant, not per process

**Why.** The SMS event handler tracked in-flight generations in a `static` dictionary. That dictionary was shared by every tenant in the process, so one tenant's conversation could suppress another's reply.

**What changed.** `IAutomatedConversationGate` with `InMemoryAutomatedConversationGate`, registered `AddSingleton` inside the tenant container so it is shared by the scoped handlers that separate inbound webhooks create, and isolated from every other tenant. `TryBegin(sessionId, out registration)` admits one generation per conversation and cancels the superseded one.

**Files created**

- `src/Core/CrestApps.OrchardCore.Omnichannel.Core/Services/IAutomatedConversationGate.cs`, `IAutomatedConversationGeneration.cs`, `InMemoryAutomatedConversationGate.cs`.

**Tests added**

- `AutomatedConversationGateTests` — a second generation for the same conversation supersedes and cancels the first, different conversations do not interfere, and a completed registration releases the slot.

### E2. Optional contracts have defaults, so nothing resolves itself from the container

**Why.** A constructor that takes an `IServiceProvider` and resolves from it tells the container nothing about what the type needs. A tenant missing the feature that supplies the collaborator still constructs the type happily and fails later, deep inside a call, usually on a live interaction. Injecting `IEnumerable<TService>` only to call `FirstOrDefault()` is the same thing wearing a constructor parameter: it accepts zero implementations silently, and picks an arbitrary one when a second feature registers another.

**What changed.** Six contracts gained a null-object default, registered `TryAddScoped` by the feature that declares the contract and `Replace`d by the feature that implements it. `TryAdd` plus `Replace` is order-safe; a bare `AddScoped` in both places is not, because Orchard does not order startups.

| Contract | Default | Declared in | Replaced by |
| --- | --- | --- | --- |
| `IBusinessHoursGate` | `AlwaysOpenBusinessHoursGate` | Contact Center base `Startup` | `BusinessHoursStartup` |
| `ICallbackService` | `NoCallbackService` | Contact Center base `Startup` | `DialerStartup` |
| `IAgentWorkStateHealingService` | `NoAgentWorkStateHealingService` | Contact Center base `Startup` | `QueuesStartup` |
| `IQueuedVoiceWorkOfferService` | `NoQueuedVoiceWorkOfferService` | Contact Center base `Startup` | `InboundVoiceStartup` |
| `IDialerProfileReader` (new read side) | `NullDialerProfileReader` | Contact Center base `Startup` | `DialerStartup` (`DialerProfileManagerReader`) |
| `IProviderCallStateSynchronizationService` | `NoProviderCallStateSynchronizationService` | Contact Center base `Startup` | `VoiceStartup` |
| `ISmsRoutingStrategy` | `NoSmsRoutingStrategy` | SMS Portal `Startup` | `RoutedDistributionStartup` |

`IServiceProvider` was then removed from `VoiceAgentHandoffService`, `ReofferVoiceWorkHandler`, `OmnichannelActivityAuthorizationHandler`, `SmsConversationAuthorizationHandler` and `AgentWorkStateHealingService`, and the collection-and-first-element idiom from `AgentPresenceManagerService`, `QueuedVoiceWorkOfferService`, `ContactCenterCallCommandService`, `ContactCenterActivityDispositionHandler`, `ContactCenterHubScopeContext`, `QueuedVoiceWorkOfferScopeContext`, `AgentSoftPhoneEndpoints` and `OmnichannelActivityBatchDisplayDriver`.

**Three dependencies stay behind `Lazy<T>`, and each is a real cycle, not a convenience.**

- `ReofferVoiceWorkHandler` to `IInboundVoiceService`: inbound routing publishes events, and the publisher constructs this handler.
- `AgentWorkStateHealingService` to `IProviderCallStateSynchronizationService`: presence constructs healing, and synchronization ends up back at presence.
- `OmnichannelActivityAuthorizationHandler` and `SmsConversationAuthorizationHandler` to the authorization service they narrow: that service is what runs them. The SMS one was only discovered by the feature-activation suite, which resolved the chain and got a container-detected cycle; that is exactly the failure this item exists to move from run time to startup.

The dependency is declared in the constructor in every case, so the container still knows about it — what is deferred is construction, not the declaration.

**Two behaviours changed as a consequence, both deliberately.**

- The batch editor showed the business-hours picker whenever a gate was registered. With an always-open default that test would be true everywhere, so it now shows the picker when there are calendars to pick. A tenant that has the feature but has defined no calendars also stops seeing an empty picker.
- The SMS re-engagement pass declined to nudge when no gate was registered. It now declines when the activity names a calendar that nothing can evaluate: the feature is off, or the calendar was deleted. That preserves the conservative outcome for the case the old check was protecting and additionally catches a deleted calendar, which the old check missed.

**Tests added**

- `Architecture/DependencyInjectionArchitectureTests` — no `IServiceProvider` constructor parameter in Contact Center, Telephony or Omnichannel code outside a written exception list; none of the defaulted contracts is scanned for; and every exception has to state a reason longer than a shrug.
- `NullObjectDefaultTests` — what each default actually does, since that is now the behaviour of every tenant without the owning feature.
- `SmsPortalFeatureActivationTests.FreshTenant_SmsPortalAlone_NeverPushAssignsAConversation` replaces the old "does not register push distribution": the contract now resolves everywhere, so the meaningful assertion is that it selects nobody.

### B3. Voice handoff correctness and context

**Why.** Three separate defects in one method.

1. It overwrote `activity.Source` with `Inbound`. An escalated dialer call is still an outbound call, so campaign reporting credited the dial to inbound traffic and the interaction told the answering agent the customer had rung in.
2. It was not serialized. Provider webhooks are at-least-once, so two deliveries of one escalation arriving together each read the activity before the other wrote it, both passed the idempotency guard, and the caller was seated in the queue twice — or given two callbacks for one after-hours call.
3. It carried none of the automated conversation forward. An agent who answers an escalated call with no idea what the caller has already said starts the conversation over, which is the thing the automated leg existed to avoid.

**What changed.**

- The body is wrapped in the inbound call lock `ContactCenterInboundVoice:{provider}:{callId}` — the same key the inbound pipeline uses, so an escalation and a redelivered inbound event for one call cannot interleave either. Timeouts come from `ContactCenterCoordinationOptions` (A5).
- `Source` is left alone. `Kind`, `InteractionType` and `AiEscalated` are still set, and the interaction's `Direction` is derived from the activity's origin: outbound for a dialer or callback source, inbound otherwise, including when no source was recorded. Confining the change to the sources that actually placed a call keeps every other activity classified exactly as before.
- `Interaction` gained `HandoffSummary`, `HandoffReason` and `HandoffAiSessionId`, populated from the request (which gained `AiSessionId`) and surfaced through `ContactCenterIncomingCallFactory` metadata, so the ringing panel and the agent bar can show the AI summary and open the transcript.
- A new `HandoffDisposition.WaitingInQueue` distinguishes "an agent was offered this call" from "it is waiting for one". `TelnyxAiVoiceConversationHandler` speaks a different line for each: telling a caller they are being connected to someone nobody offered the call to leaves them listening to silence.

**Tests added** (`VoiceAgentHandoffServiceTests`, 8 new cases)

- The outbound origin of a dialer call survives the handoff, and its interaction is recorded outbound; an inbound call is still inbound.
- The summary, reason and AI session reach the interaction.
- No agent available reports `WaitingInQueue`; an available agent reports `Routed`.
- The inbound call lock is taken.
- A redelivered escalation enqueues once; a redelivered after-hours escalation schedules exactly one callback.

One existing assertion changed: the test that asserted `Source` becomes `Inbound` now asserts it is untouched, because that rewrite is the defect this item removes.

### B4. SMS handoff validates its queue and honours routed distribution

**Why.** Two gaps. A handoff to a queue identifier that resolves to nothing produced a conversation in no inbox at all — no agent sees it under a department they belong to, no supervisor sees it, and the customer waits for a reply the escalation promised. And escalations always pooled the thread, so a queue configured for routed (push) distribution silently behaved like a shared one for exactly the conversations that most needed a named owner.

**What changed.** `SmsAgentHandoffService` now resolves the target queue through `IActivityQueueManager` and refuses the handoff when it does not exist, and reads the endpoint's `SmsEndpointRoutingSettings`: on a routed queue target it asks `ISmsRoutingStrategy` for an agent and push-assigns the thread exactly as a fresh inbound message on that queue would, notifying that agent rather than the whole department. A routed queue with nobody available falls back to the shared pool rather than refusing, because the alternative is leaving the customer in an automated conversation that has already given up on them.

This is the minimum version the guide describes for B4 ahead of C7; when the SMS conversation router lands, this selection moves behind it.

**Tests added** (`SmsAgentHandoffServiceTests`, 5 new cases): a non-existent queue is refused and creates nothing; a routed queue push-assigns and marks the thread Assigned; a routed queue with nobody free falls back to the pool; a shared-pool queue never consults the strategy; the notification names the assigned agent.

### Phase P1 verification

```
dotnet build tests/CrestApps.OrchardCore.Tests/CrestApps.OrchardCore.Tests.csproj -c Debug
dotnet test tests/CrestApps.OrchardCore.Tests/CrestApps.OrchardCore.Tests.csproj -c Debug --no-build
dotnet test tests/CrestApps.OrchardCore.ContactCenter.FeatureActivationTests/CrestApps.OrchardCore.ContactCenter.FeatureActivationTests.csproj -c Debug
```

Unit suite: 4,577 tests, 0 failed, 1 skipped. Feature activation: 73 tests, 0 failed, 2 skipped.

Public API baselines re-approved in this phase, each after reading the diff: `ContactCenter.Core` (the null objects, the dialer profile reader, the rewired constructors, the handoff context on `Interaction`), `Omnichannel.Core` (`AlwaysOpenBusinessHoursGate`, `AiSessionId`, `WaitingInQueue`), `Omnichannel.Managements` (the automation configuration binding), `Omnichannel.Sms.Portal.Abstractions` and `Omnichannel.Sms.Portal.Core` (the rename, the immutable retry schedule, and the B4 constructor).

`HandoffDisposition` gained a member in the middle, which renumbers the two after it. The enum is only ever carried on a result object and is never persisted or sent over the wire, so nothing stored depends on the numbering.

### Module rename during this phase

The SMS Workspace module set was renamed twice in the working tree while this phase was in progress, ending at `CrestApps.OrchardCore.Omnichannel.Sms.Portal.*`, with `SmsWorkspace*` types becoming `SmsPortal*` and the feature constant `Feature.Workspace` becoming `Feature.Portal`. Everything recorded in the P0 section above under the old names lives under the new ones and behaves the same.

Three public API baselines were re-approved as a consequence:

- `Omnichannel.Sms.Portal.Abstractions` — the renamed feature constant.
- `Omnichannel.Sms.Portal.Core` — `SmsOutboundDeliveryState.RetryDelayMinutes` is now an `ImmutableArray<int>`. A public static array reports itself as read-only while still letting any holder assign to its elements, which the public API guard refuses.
- `Omnichannel.Managements` — the configuration binding added by E3.

## Phase P2 — contact-centre and SMS feature parity

### C7. One router decides who owns a conversation

**Why.** Ownership was decided in three places that had drifted apart: the inbound chain, `SmsAgentHandoffService` (which always pooled, so a routed department behaved like a shared one for escalations), and `SmsRoutedReassignmentService` (which called the strategy itself with its own attempt policy). A change to the rules reached one of them. Separately, the inbox roll-up — preview, unread count, stamps — was computed in four places with four slightly different truncation rules, so the same message read differently depending on which path wrote it.

**What changed.**

- `ISmsConversationRouter.RouteAsync(SmsRoutingContext)` is the single entry point. The context carries a `SmsRoutingTrigger` (`Inbound`, `Handoff`, `Reassignment`, `ManualTransfer`), so one chain serves every path and a router can see which one it is serving instead of inferring it.
- `SmsInboundRoutingContext` becomes `SmsRoutingContext`, gaining the trigger, the escalation's target queue, the reassignment budget, the agent to exclude, and the router that claimed the thread.
- Two chain members absorb the paths that used to route themselves: `HandoffQueueRouter` (order 120) places an escalation, push-assigning on a routed queue endpoint and pooling otherwise; `ReassignmentRouter` (order 110) places an unpicked thread, preferring another agent and falling back to `Pooled` once the attempt budget is spent.
- `SmsConversationRollup` is the one roll-up: `BuildPreview`, `ApplyInbound` (which takes an unread increment, so an escalation that imports a whole transcript counts every message rather than one), and `ApplyOutbound` (which leaves the unread count alone, because an agent's own message cannot make a thread more unread to them). The four private copies are deleted.

`SmsAgentHandoffService` and `SmsRoutedReassignmentService` keep what is genuinely theirs — validation, persistence, notification, and deciding when a thread is stale — and delegate the placement decision.

**Tests added**

- `SmsConversationRouterTests` — the chain runs in order and stops at the first claim; every trigger reaches the routers; an unclaimed thread stays unassigned rather than being attached to whichever router ran last.
- `SmsConversationRollupTests` — line-break collapsing, truncation, empty rather than null, unread bookkeeping inbound and outbound, and the transcript-import case.
- The existing router, handoff and sweep tests now build the real chain member behind the real router, so they exercise the placement policy where it now lives instead of a stub of it.

### C8. SMS availability is derived from presence, and first replies are on a clock

**Why.** `SmsAgentAvailability` was a flag on the agent profile that nothing else checked. An agent who closed the browser stayed available for routed SMS until the five-minute pickup sweep re-pooled each thread, and every message pushed at them in that window sat unanswered. Separately, nothing measured how long a customer waited for a first reply: no timer, no escalation, and so no way for a supervisor to find the threads going unanswered before the customer gave up.

**What changed.**

- `ISmsAgentAvailabilityService.IsAvailableAsync` derives availability from the stored flag **and** a live `AgentSession` heartbeat, using the same `AgentAvailabilityOptions.HeartbeatTimeout` voice presence uses, so an agent is not live for one channel and gone for the other. `LeastLoadedSmsRoutingStrategy` asks that instead of reading the flag.
- `ActivityQueue.FirstResponseTargetSeconds` is the queue policy; zero means the queue has not opted in, because inventing a target would fill the supervisor view with breaches nobody agreed to.
- `SmsConversation` gained `FirstResponseDueUtc`, `FirstRespondedUtc` and `FirstResponseBreached`. `ISmsFirstResponseSlaService` starts the clock when a thread is placed on a queue that has a target, leaves an existing deadline alone (the clock starts when the customer first waited, so a thread bounced between agents still breaches), stops it on the first reply, and announces overdue threads exactly once.
- `SmsFirstResponseSlaBackgroundTask` runs the sweep every 30 seconds by looping inside its minute, because cron cannot express a sub-minute cadence and a five-minute target should not be reported up to a minute late.
- The deadline is indexed (`IDX_SmsConversationIndex_FirstResponse`, migration version 4), so the sweep seeks the overdue threads rather than reading every open conversation twice a minute.

**Tests added**

- `SmsAgentAvailabilityServiceTests` — the flag without a session is not available, a session without the flag is not available, both with a fresh heartbeat is, and a stale heartbeat is not.
- `SmsFirstResponseSlaTests` — the deadline is set from the queue target, absent when there is no target, never pushed out by a re-placement, cleared on reply, and breached threads are announced once and only once.

### C9. Carrier keywords, auto-reply, and quiet hours

**Why.** Three compliance gaps. STOP closed the thread and flagged the contact but sent no confirmation, which the carrier rules require; HELP and START were not recognised at all, so a contact who had opted out had no supported way back in. `SmsEndpointRoutingSettings.AutoReplyMessage` was stored by the editor and sent by nothing, so an operator could configure an acknowledgement, watch it save, and watch every contact get silence. And human sends had no quiet-hours guard at all — only automated cadences were gated — so an agent working late could text a customer at three in the morning their time with nothing in the way.

**What changed.**

- `SmsKeywordPolicy` classifies STOP, HELP and START and says what to reply. A keyword is the whole message; matching a prefix opted people out of a service they were in the middle of asking about ("stop by the shop later"), so only trailing punctuation is tolerated. Each reply has a shipped default because a tenant that configured nothing still owes a contact who texts STOP an answer, and `SmsKeywordReplySettings` (bound from `CrestApps:Sms:Portal:KeywordReplies`) lets an operator say it in their own words.
- The confirmation is sent through the dispatcher rather than the conversation service, because the conversation service refuses to send to a contact who has opted out — and this is the one message that must still reach them.
- START reopens the thread as well as clearing the flag, so the contact's next message arrives with its history attached.
- `AutoReplyRouter` (order 50) sends the endpoint's auto-reply at most once a day per thread and never on a keyword. It never claims the conversation: the auto-reply is a side effect, not an ownership decision, and claiming would stop the routers that actually place the thread from running.
- `SmsQuietHoursGuard` evaluates the destination queue's calendar in the contact's local time and returns a decision the composer renders. It warns rather than blocks — a person who genuinely needs to reach a customer out of hours exists — but going ahead takes the new `SendSmsDuringQuietHours` permission, so it is a decision somebody made rather than one nobody noticed.

**A seam the plan did not call for, added because the feature-activation suite caught the alternative.** The SLA service and the quiet-hours guard both need to read a queue, but the SMS Portal feature deliberately does not depend on Work Distribution: an agent can own a number and text from it with no departments configured at all. Injecting `IActivityQueueManager` broke exactly that tenant, which the activation suite proved. `ISmsQueuePolicyReader` returns the two things the portal actually needs — the first-response target and the business-hours calendar — with `NullSmsQueuePolicyReader` reporting "not found" on a tenant with no queues and `ActivityQueueSmsQueuePolicyReader` replacing it under `[RequireFeatures(Queues)]`. `SmsAgentHandoffService` uses the same reader for its queue-existence check, which had the same latent problem.

**Tests added**

- `SmsKeywordPolicyTests` — every STOP synonym, HELP, START, trailing punctuation, and the prose that must not fire.
- `SmsAutoReplyRouterTests` — sent on the first message, not when unconfigured, at most once a day, again after a day, never on a keyword, and never claiming.
- `SmsQuietHoursTests` — inside hours, outside hours with a reason, no calendar means no warning, and the contact's own time zone is the one used.

### A1. Cross-queue arbitration for multi-queue agents

**Why.** `QueuedVoiceWorkOfferService` walked `agent.QueueIds` in stored order and offered from the first queue that had anything. An agent signed into Sales and Support was always served from whichever queue happened to be first in their profile, so a caller who had waited twenty minutes on the other one sat behind a caller who had just arrived. SLA aging on a queue nobody was looking at was invisible.

**What changed.**

- `AgentQueueMembership` (`QueueId`, `Priority`, `DelaySeconds`) is stored on `AgentProfile` alongside the bare `QueueIds` list, which is kept as the record of what the agent is signed into. A queue with no membership is served at priority zero with no delay, which is exactly how every agent behaved before.
- `IAgentWorkSelector` reads the head item of every queue the agent serves in one grouped query (`IQueueItemStore.GetHeadWaitingByQueueAsync`), scores each with the same `QueueItemPrioritizer.GetEffectivePriority` the queue itself routes on so SLA aging counts, and orders by membership priority, then effective priority, then how long the contact has waited.
- `QueuedVoiceWorkOfferService` calls the selector instead of looping. Reservation still runs through `AssignNextAsync(queueId)`, so the locking semantics are unchanged — what changed is which queue that call is made against.

The offer loop re-selects after each offer, because an agent can still be free (an offer may be declined or find nobody) and the next-best queue may have changed. It ends as soon as the agent is reserved, stops being available, the selected queue is paced-dialer work, or an offer produced nothing — the last two because selecting again would return the same queue forever.

**Tests added** (`AgentWorkSelectorTests`, 8 cases): the oldest caller wins across queues; membership priority outranks age; a delay holds a queue back and then releases it; SLA aging lets a long-waiting normal call beat a newer high-priority one; empty queues are skipped; nothing is selected when nothing is waiting; and an agent with only the legacy `QueueIds` list still gets served, because ignoring it would sign every existing agent out of every queue on upgrade.

### A2. Skills with proficiency, preference, and time-based relaxation

**Why.** `AgentProfile.Skills` and `ActivityQueue.RequiredSkills` were bare tags matched all-or-nothing. A queue could not say how much Spanish it needed, could not prefer a skill without demanding it, and — the one that actually hurts — could not let a requirement go once a caller had waited long enough, so a caller who needed a specialist nobody had available waited indefinitely rather than reaching a generalist.

**What changed.**

- `AgentSkill` (`SkillId`, `Proficiency` 1–5) and `QueueSkillRequirement` (`SkillId`, `MinimumProficiency`, `Required`, `RelaxAfterSeconds`), stored alongside the existing tag lists rather than replacing them.
- `SkillMatching` is the single reader both strategies go through, so a queue and an agent cannot disagree about what counts as the same skill or about what an untyped tag means: a bare tag on a queue is a hard requirement at proficiency 3 that never relaxes, and a bare tag on an agent is proficiency 3. That is exactly how a tag behaved before, which is what stops every skilled queue becoming unroutable the moment this ships.
- `RequiredSkillsRoutingStrategy` now enforces the minimum proficiency and drops a requirement once the contact has waited past its window. A requirement with **no** window is never dropped — it is one the queue meant absolutely, and relaxing it after some invented default would silently route a regulated call to somebody not qualified to take it. Which requirements were relaxed, and after how long, is recorded on the candidate's reasons, so a supervisor can see why an under-skilled agent got the call.
- `PreferredSkillsRoutingStrategy` (order 40) scores preferred skills and proficiency above the minimum, and never rejects anybody: a preference that eliminated candidates would be a requirement, and the queue said it was not. Holding a preferred skill at all outranks holding a different one expertly.

**Tests added** (`SkillRoutingTests`, 9 cases): below the minimum is rejected and exactly at it is accepted; relaxation fires after the window and not before; no window means never; legacy tags read as required at proficiency 3 on both sides; and preference ranks without rejecting, with proficiency breaking the tie.

### A6. Queue limits, in-queue treatment, and queued callback

**Why.** A caller on hold heard nothing and was told nothing: no position, no estimated wait, no music, and no way to ask for a callback instead of waiting, so they abandoned and the queue lost both the call and the context. Overflow was a single hop evaluated by a once-a-minute sweep, so a queue configured to overflow after twenty seconds actually overflowed somewhere between sixty and eighty.

**What changed.**

- `ActivityQueue` gained `MaxWaitSeconds` with `MaxWaitAction`, `MaxQueueSize` with `QueueFullAction`, an ordered `OverflowTargets` chain, and a `QueueTreatmentSettings` describing what callers hear. `QueueItem` gained the state that drives it: `TreatmentStepsPlayed`, `LastTreatmentUtc`, `CallbackOfferedUtc`, `CallbackAcceptedUtc`, `OverflowDueUtc`.
- `OverflowScheduler` takes the **furthest** hop whose threshold has passed, so a caller who has already waited past every tier lands at the last one instead of crawling through each while a sweep ticks. It refuses a queue the item has already visited and the queue it is already in, because two queues that overflow into each other would otherwise pass the caller in a circle that resets nothing. `GetNextDueUtc` gives a scheduler the instant to look again rather than polling every waiting item every minute. The legacy single-hop fields are still honoured, because dropping them would strand every caller the existing queues were built to hand on.
- `EstimatedWaitTimeCalculator` returns handle time × position ÷ available agents, clamped to a floor and a cap. It returns **null**, not a number, whenever the inputs cannot support one — no agents, no handle-time history, an impossible position — because an invented estimate is announced to the caller with exactly the same confidence as a real one. The floor exists because telling the next caller in line "no wait" is a promise the queue cannot keep.
- `QueueTreatmentPolicy` decides what is due: the welcome once, then the callback offer, then the periodic update. The offer comes **before** the first announcement, because it is only useful while the caller still has the patience to accept it. A queue that has configured nothing plays nothing: silence is the right default for a queue that never asked for its callers to be talked at.
- `QueuedCallbackService` schedules the callback with the caller's **original arrival time**, not the moment they pressed the key — a callback that costs someone their place is worse than waiting, because they hang up expecting to be treated as though they had held. It removes the item from the queue so no agent is offered a caller who has already gone, and refuses a second acceptance so a repeated key press or a redelivered DTMF event cannot produce two calls back.
- `IQueueTreatmentProvider` is the provider seam, with `NoQueueTreatmentProvider` as the default: a tenant with no voice provider is silent rather than throwing at somebody who is already on hold.

**Deferred, with the reason.** The Telnyx `IQueueTreatmentProvider` implementation (`speak`, `playback_start`, `gather` on the waiting leg) and the treatment background task that drives the policy against a live call are **not** implemented here. Both are only meaningful against a real provider media path, and this repository has no host to place a call through, so shipping them would mean shipping call-control code whose only evidence of working is that it compiles. The policy, the scheduler, the calculator and the callback service — everything that decides *what* should happen — are implemented and tested; what remains is the adapter that makes a caller hear it. The same reasoning applies to the treatment scheduler task, which has nothing to drive until the provider exists.

**A rename the guard asked for.** `QueueTreatmentOptions` was renamed `QueueTreatmentSettings` because `ContactCenterOptionsValidationTests` correctly refused it: a type named `*Options` is expected to be bound from deployment configuration and validated by `IValidateOptions`, and this is per-queue state edited in the admin UI. Naming it `Settings` puts it with `SmsEndpointRoutingSettings` where it belongs, rather than adding an exception to a guard that was right.

**Tests added**

- `EstimatedWaitTimeCalculatorTests` (6 cases): the formula, the floor at the head of the line, the cap, and the three cases that must return no estimate at all.
- `OverflowSchedulerTests` (9 cases): the threshold, the furthest hop, visited-queue and self-target loop prevention, the legacy single hop, and the due time.
- `QueueTreatmentPolicyTests` (7 cases): welcome once, announcement cadence, the callback offer ahead of the first announcement and only once, and silence when nothing is configured.
- `QueuedCallbackTests` (4 cases): place in line preserved, the caller removed from the queue, refusal with no number, and one callback for two acceptances.

### A8. Caller-based priority

**Why.** Priority came only from the entry point or the queue default. Contacts resolved through the CRM lookup were attached to the activity and then ignored, so a tenant could mark an account VIP, watch it resolve on every inbound call, and still have that caller queue behind everybody else.

**What changed.** `IInboundPriorityContributor` notices one reason a caller matters and returns null when it has nothing to say, so a tenant can add a reason without any contributor knowing about the others. `InboundPriorityResolver` takes the **strongest** contribution, not the last: contributors describe independent reasons, and letting a weak one cancel a strong one would mean registration order decided who got answered first. A contributor may raise the configured priority but never lower it, because the entry point is an explicit operator decision about that number.

A contributor that throws is skipped rather than propagated. It reads the CRM, and a caller must not be dropped because a lookup failed — landing at the configured priority is a worse outcome than the treatment they were owed and a far better one than not reaching anybody.

Two contributors ship: `ReturningCallbackPriorityContributor` (a caller returning a callback already waited once and gave up their place; sending them to the back turns a courtesy into an insult) and `RepeatCallerPriorityContributor` (somebody calling back within four hours did not get what they needed the first time).

**Tests added** (`InboundPriorityTests`, 9 cases): no contributors changes nothing; a contributor can raise; the strongest wins; none can lower; declining changes nothing; a throwing contributor does not stop the call being queued; and the repeat-caller window fires inside it, not outside it, and not for a first-time caller.

### A10. Predictive dialing implemented behind the abandonment cap

**Why.** `DialerMode.Predictive` was in the enum and on the profile editor while `DialerStrategyResolver` returned null for it, so an operator could select it and get a campaign that silently never dialled. The plan's own recommendation was to implement it with the abandonment policy as a hard gate.

**What changed.** `PredictiveDialerPacing.CalculatePace` is the safety mechanism: pace is agents × the profile's ratio while the measured abandonment rate is comfortably under the cap, then scales linearly down to one call per agent as the rate climbs toward it. It throttles **before** the cap rather than at it, because a breach has already happened to real callers by the time it is measurable, and a rolling rate falls far more slowly than it rises. At or past the cap, and whenever no agent is free, or the profile enforces no cap at all, the pace is one call per agent — the only pacing that cannot abandon, because there is somebody waiting for every call it places. A hard ceiling of three per agent applies however the profile is configured.

`PredictiveDialerStrategy` consults `IDialerAbandonmentPolicyService` before every cycle and dials nothing when it is not permitted: the policy fails closed, and guessing here means guessing with somebody's regulated campaign. Permitted-but-unmeasured paces as though the rate were at the cap, so a campaign with no history starts safe and opens up once there is a measurement to open it with.

`DialerStrategyBase.GetMaxAttemptsPerCycle` became `GetMaxAttemptsPerCycleAsync`. A mode whose pacing depends on measured statistics has to read them, and the alternative — computing the answer in `RunCycleAsync` and stashing it on a field — would leak it between cycles and across threads. The base loop also now treats a zero ceiling as a decision rather than clamping it up to one, because clamping would place the call the policy just refused.

**Tests added** (`PredictiveDialerPacingTests`, 7 cases): the ratio applies under the cap, throttles as the rate rises, collapses to one per agent at and past the cap, is nothing with no agents, respects the hard ceiling, and is one per agent when no cap is enforced.

### D4. One resolver for an agent's SIP endpoint

**Why.** `TelnyxContactCenterVoiceProvider.ResolveAgentEndpointAsync` and `TelnyxTelephonyProvider.Extensions.ResolveUserSipEndpointAsync` each took `live[0]` from the credential store, which is the newest **issued** credential. A browser that had re-registered under an older one was therefore dialled at an address nothing was listening on, and the call came back SIP 486: the agent's phone never rang and the caller heard busy. `TelnyxAgentCredentialSelection.OrderByDeliveryPreference` already existed and neither path used it.

**What changed.** `ITelnyxAgentEndpointResolver` is the single path both now use. It orders registered first, then most recently registered, then newest issued — the last of which only decides anything when nothing has registered, where the newest is the one the browser is most likely registering against right now. It skips credentials with no SIP username, logs the credential it chose at debug so a future incident can be read rather than guessed at, and warns when there is nothing to choose.

**Tests added** (`TelnyxAgentEndpointResolverTests`, 6 cases): a registered credential beats a newer unregistered one — the incident itself; the most recently registered wins among several; the newest issued is used when none has registered; and nothing resolves for no credentials, a credential with no username, or an empty user.

### A9. Attended transfer as three phases

**Why.** `ConsultCall` and `IContactCenterVoiceAttendedTransferProvider` both existed and nothing drove them, so agents had blind transfer and nothing else: the customer was dropped on somebody who had not agreed to take them, and if that person did not answer, the customer was gone.

**What changed.** `IConsultTransferService` runs the three phases an attended transfer actually has — consult, then complete or cancel — and records each against the call session, so a supervisor can see a customer held while their agent talks to someone else and reporting can tell a completed warm transfer from an abandoned consult.

The rules that matter: a second consult is refused while one is live, because an agent cannot be in two private conversations at once and the first destination would be left talking to nobody. Only a **connected** consult may be completed — completing an unanswered one hands the customer to a ringing phone and hangs up on them if it is never picked up, and the same check makes a double-clicked button place one transfer rather than two. Cancelling returns the customer to the agent they already had, which is the entire reason for consulting before committing.

**Two architecture guards changed the design, both correctly.**

- `CallTopologyAuthorityTests` refused `session.Consults.Add(...)`: live call topology is the projector's to write. The service now commands the provider and records the outcome through `CallTopologyProjector.StartConsult` / `AdvanceConsult`, which also ends the consult leg on a terminal state — something the hand-rolled version was not doing and would have left dangling.
- `ContactCenterOperationalLogPrivacyTests` refused an unsanitised `result.ErrorMessage`.

**One design change the feature-activation suite forced.** Injecting `IContactCenterVoiceAttendedTransferProvider` broke every tenant whose provider does not implement it, because nothing registers that interface as a service — it is a capability some voice providers have. The service now resolves the tenant's voice provider and tests for the capability, refusing the consult with a logged reason when the provider cannot hold a customer and ring a third party privately. That is a thing an agent needs to be told, not a container failure at startup.

**Tests added** (`ConsultTransferTests`, 7 cases): the consult is recorded; a second is refused; a connected consult completes and transfers; an unanswered one is refused; cancelling returns the customer; completing twice transfers once; and a destination the provider refuses produces no consult record.

**Deferred, with the reason.** The soft-phone transfer UI listing agents, queues and catalog entries — the presentation half of A9 — is not built here. The server side is complete and guarded (D2 already replaced the transfer target policy so the catalog is enforced whatever the client sends), but the panel itself needs the live soft phone to design against, and the JavaScript it belongs in is the file D6 is scheduled to split apart. Building it now would mean writing it twice.

### A7. IVR menus on entry points

**Why.** A `ContactCenterEntryPoint` mapped a number to exactly one queue or agent, so every caller to a shared line reached the same team and was routed onward by hand. There was no menu, no sub-menu, and no way for a caller to say what they wanted.

**What changed.** An optional `IvrFlow` on the entry point: a declarative tree of `IvrNode` menus, each with digit-to-`IvrAction` options (route to queue, route to agent, voicemail, external transfer, repeat, sub-menu), a retry budget and a fallback action. An entry point with no flow — or a flow with no nodes — returns `Done` and routes exactly as it always did, so nothing changes for a tenant that has not built a menu.

`IvrFlowStateMachine` is a pure function of the flow, the caller's state and one gather delivery. That matters because provider gather events are at-least-once and can arrive out of order:

- A delivery already applied re-plays the current prompt rather than advancing again, so a redelivered webhook does not take a caller two levels into a menu they navigated once — and re-playing rather than returning nothing means the line is not left silent.
- A delivery collected on a menu the caller has since left is ignored, so a late event cannot drag somebody who is deep in the support menu back to the top.
- Retries are bounded and fall through to the fallback. A caller who cannot work the menu — a rotary phone, a bad line, a language they do not read — still reaches a person instead of hearing the prompt until they give up. Silence counts as a failed attempt for the same reason.
- Attempts reset on entering a menu, because they belong to the menu; carrying them would punish somebody for one fumbled key press at the top for the rest of the call.

**Tests added** (`IvrFlowStateMachineTests`, 13 cases including a 3-case replay theory): no menu routes straight through; the root prompts; digits take their action; sub-menus descend; the same delivery twice advances once; a stale delivery is ignored; unmapped digits re-prompt; retries fall back; silence counts; attempts reset; and replaying an arbitrary prefix of the conversation converges on the same state the live run reached.

**Deferred, with the reason.** The Telnyx `gather` command mapping and the provider event wiring that feeds this machine are not implemented here, and neither is the flow editor. The machine is the part that has to be right and can be proven right without a provider; the adapter that issues `gather` and parses `call.gather.ended` is the same class of live-media work deferred in A6, and the editor is a screen with no engine behind it until that adapter exists.

### D3. Telnyx provider test coverage

**Why.** The Telnyx tree had 12 tests, all about AI-voice contact fields, while the Asterisk provider had more than forty files for comparable scope. Nothing at all covered the webhook signature validator, the call-event parser, or the credential ordering — the three pieces every call in the platform passes through.

**What changed.** Three new suites, chosen because each guards something that has already gone wrong or would go wrong silently:

- `TelnyxWebhookSignatureValidatorTests` (13 cases). This validator is the only thing between the public webhook endpoint and anybody who can guess the URL. The tests sign real Ed25519 payloads and assert that a tampered body, a signature reused with a different timestamp, a signature from a different account key, and a key of the wrong length are all refused — and that every missing or malformed input is a refusal rather than a pass, because the one thing a signature check must never do is let through something it did not verify. An empty body that was genuinely signed is accepted, since treating it as missing input would refuse valid deliveries.
- `TelnyxCallEventParserTests` (13 cases). The envelope, a hangup with `sip_hangup_cause` (the field that distinguishes "the agent was busy" from "nothing was registered at that address", and the one incidents are diagnosed from), and the caller id in both shapes Telnyx sends it — a bare string on some events and a nested object on others, where reading only one loses the number the platform matches contacts on. Eight malformed-body cases confirm refusal rather than an event object with empty fields the pipeline would act on.
- `TelnyxAgentCredentialSelectionTests` gained two cases alongside its existing three: every credential is kept so a caller walking the ordering has something to fall through to, and no credentials yields an empty result rather than a null on a path that runs while a customer is holding the line.

**A correction worth recording.** One test in this item asserted that an event with no `call_control_id` should parse. The parser refuses it, and the parser is right: every consumer downstream keys on that id, so accepting the event would put a record with empty fields on the bus and have the projector look for a call nobody named. The test was rewritten to assert the real contract. Separately, I overwrote `TelnyxAgentCredentialSelectionTests` with a near-duplicate before noticing it already existed; the original — which documents the live SIP 486 incident more precisely than my version did — was restored from git and only the two genuinely new cases were added to it.

**Deferred, with the reason.** The recorded-HTTP `TelnyxApiHandler` double and the suites that need it — bridge orchestrator, credential issuer cap and revoke, `ConnectToAgentAsync`, recording ingest, and the webhook endpoint's status codes — are not built here. They are the right tests, and the guide is right that they need a recorded handler; but that handler is the same object D5 introduces when it consolidates the four duplicated `CreateClient` implementations into a typed `TelnyxApiClient`. Writing the double now means writing it against four HTTP call sites that D5 deletes, and then writing it again. The three suites added here are the ones that need no HTTP at all.

## Phase P3 — structural cleanup

### E10. The log-privacy guard now covers the trees that carry message bodies

**Why.** `ContactCenterOperationalLogPrivacyTests` scanned Contact Center, Telephony, Asterisk, Dialpad and the SMS **automation** module — but not the SMS **Portal**, which is where message bodies and contact numbers actually flow, nor Telnyx, which handles addresses on every call. The guard was watching the quieter half of the problem.

**What changed.** Four folders added to the scan: `Omnichannel.Sms.Portal.Core`, `Omnichannel.Sms.Portal`, `Telnyx.Core` and `Telnyx`. It immediately found three unsanitised identifiers in code written earlier in this plan — two in `SmsAgentHandoffService` and one in `SmsConversationRouter` — which are now wrapped in `SanitizeLogValue()`. That is the guard doing its job on the first run, which is the argument for widening it.

### A11 / E9. Entry-point resolution is a chain, and two dead fields are gone

**Why.** `InboundVoiceCallProcessor` called `_entryPointResolvers.FirstOrDefault()`, so a second registered resolver — the way a feature adds its own entry-point source — was silently never asked, and a number only it knew about resolved to nothing while the caller was routed as though the tenant had never configured it. Separately, `SmsConversation.LabelIds` and `WindowExpiresUtc` had no UI, no index and no logic anywhere.

**What changed.** `EntryPointResolverChain` asks every registered resolver in order and takes the first plan, with `IOrderedEntryPointResolver` for a resolver that needs to say where it belongs (absent it runs at zero, which is what every current resolver does, so nothing changes for them). The chain is registered in the **base** Contact Center startup rather than the inbound feature, because the feature-activation suite proved the processor is constructed on tenants that have no resolvers at all — and a chain over nothing is a valid chain that resolves nothing, whereas injecting the interface directly made those tenants fail to construct. The two dead fields are removed rather than left as a promise the model was not keeping.

**Tests added** (`EntryPointResolverChainTests`, 5 cases): order is respected; a resolver that does not know the number is fallen through — the case that was broken; nothing resolves when nobody knows it; no resolvers at all is a normal state; and lookup and routing use the same chain, so a call cannot be recorded against one entry point and routed by another.

### E12. Two more engineering rules enforced by tests

Rules 1–4 already had guards. Two more now do.

**Rule 8 — file size, as a ratchet.** `FileSizeRatchetTests` records the ten files already over 800 lines with their current size and fails when one grows or a new file crosses the line. A hard limit would have meant either ten refactors before any unrelated change could land, or the rule being suppressed — and a suppressed rule stops anybody noticing the eleventh file. A second test prunes the list: when a recorded file is split, deleted, or merely shrinks, its entry has to be updated, or the limit quietly stops applying to that path forever.

**Rule 6 — every provider-originated write survives a duplicate delivery.** `ProviderWriteIdempotencyArchitectureTests` requires each `IProviderWebhookInboxHandler` to declare a replay contract other than `Unspecified`, **and** to have a test that names it and exercises a duplicate. A declaration nobody tested is a claim, and claims about idempotency are the ones that turn out to be wrong under load.

It found two handlers — `SmsInboundInboxHandler` and `DialpadWebhookInboxHandler` — claiming `GuardedByDurableStore` with nothing proving it, so `WebhookInboxHandlerReplayTests` was written: the same payload delivered twice saves under the same identity and raises the same provider event id, the forwarding handlers reach the guarded service both times, and an unreadable payload throws rather than being swallowed (the durable inbox retries in a fresh scope; returning quietly is how an inbound text disappears).

**A flaw in my own guard, worth recording.** The first version reflected over `AppDomain.CurrentDomain.GetAssemblies()`, which only sees assemblies the runtime has already loaded. It found four handlers in a full run and two under a filter — passing while `TelnyxWebhookInboxHandler` went unchecked. It now anchors on a named type per assembly to force the load, and the Telnyx handler, which every call in a Telnyx tenant flows through, got the replay tests it was missing.

### E8 (partial). The feature-activation suite runs on every pull request

**Why.** It ran only in `release_ci.yml`. The failures it catches — a service one feature registers and another injects, a dependency closure that no longer constructs, a migration that does not run — break a tenant the moment somebody enables a feature, and finding them at release time means finding them after the pull request that caused them has been forgotten. This plan hit that suite four separate times, each time on a defect no unit test could have seen.

**What changed.** `pr_ci.yml` gained the step, copied from `release_ci.yml`.

**Deferred, with the reason.** The Playwright job is not added to PR CI. It needs `playwright install --with-deps chromium` on every run, and the browser tests currently exercise the soft phone — the same file D6 is scheduled to split apart. Adding minutes to every pull request for a suite about to be restructured is a cost paid twice.

### E11. Documentation brought back in line with the code

- **`voice-routing.md`** — the "Current limitations" entry saying the approved-destination catalog "does not currently constrain which destination an agent can type" is no longer true, and has been replaced with what is now enforced: `ITransferTargetPolicy` resolves through the catalog when Contact Center Voice is enabled, so a raw number typed into the soft-phone transfer field is refused server-side whatever the client sends, with `IDialDestinationPolicy` underneath it on every path. The remaining gap — the transfer panel does not yet *list* agents, queues and catalog entries — is stated as the presentation work it is, rather than left implying the restriction is missing. Attended transfer and the entry-point resolver chain are added as facts about the product.
- **`production-support.md`** — the validated-settings table gained `CrestApps:Omnichannel:Automation`, `CrestApps:Sms:Portal` and `CrestApps:Sms:Portal:KeywordReplies`, each with the rule its validator enforces and why it matters.
- **`report-catalog.md`** — a new "AI escalations" section with three reports (containment and escalation, escalation reasons, post-escalation outcome). The data behind them already exists: `AiEscalated` on the activity, and `HandoffReason` / `HandoffSummary` / `HandoffAiSessionId` on the interaction. The third one exists because a high containment rate is not a good outcome if the escalations that do happen then go unanswered.

### D5. One typed client for Telnyx

**Why.** Five services each built their own `HttpClient`, set their own base address and bearer header, and read the provider's answer their own way — five places to fix a bug, five chances to disagree about what a 429 means, and **no retry anywhere**, so a rate-limited command was simply a call that did not happen and the customer heard silence. `SafeReadContentAsync` and `ReadDataStringAsync` were copied alongside them.

**What changed.** `TelnyxApiClient` is the one place that talks to Telnyx: `AnswerAsync`, `HangupAsync`, `OriginateAsync`, `BridgeAsync`, `TransferAsync`, `SpeakAsync`, `PlaybackAsync`, `GatherAsync`, `RecordStartAsync`, `GetCallAsync`, conference create/find/join, credential create/delete, and a general `PostCallActionAsync` for the actions with no named method yet. It is registered with `AddHttpClient<TelnyxApiClient>` so it keeps the module's existing resilience handler.

The decisions worth stating:

- **Results, not exceptions.** Every caller is holding a live call. An exception on a refused command abandons a customer mid-flow; a result the caller can inspect lets them fail deliberately and tell the agent why.
- **Retry is per command, because only the command knows.** Answer, hangup, bridge and lookups repeat harmlessly and are retried on 429, 408 and 5xx, honouring the provider's own `Retry-After` — it knows when its limit resets and this code does not. Originate, transfer, speak and record are **never** retried: a retry the provider had in fact accepted would dial the customer twice, move them twice, or say the same thing to them twice. A 4xx other than 429 is never retried either, because the provider understood and refused, and retrying a refusal only spends the budget the next real command needs.
- **A credential the provider has already forgotten is success.** Revocation runs on sign-out and on cap eviction, both of which race a credential that expired provider-side; treating that 404 as failure leaves a local record nobody can ever clean up — and that record still counts against the cap behind the LOGIN_FAILED incident.
- **`AdditionalFields` on the originate request.** Telnyx accepts far more on an origination than is worth naming, and a caller needing `outbound_voice_profile_id` should not have to reach for raw HTTP to send it — that is exactly how five copies of the transport came to exist.

**Migrated:** `TelnyxTelephonyCredentialIssuer`, `TelnyxContactCenterVoiceProvider` (and its recording partial), `TelnyxOutboundBridgeOrchestrator`, `TelnyxTelephonyProvider` (and its extensions partial), `TelnyxVoicemailRecordingStarter`. Every duplicated `CreateClient`, `SafeReadContentAsync` and `ReadDataStringAsync` in the call-control path is gone. The three remaining `CreateClient` copies are in the media-streaming, media-provisioning and AI-voice-agent clients, which address different Telnyx APIs and are not what this item consolidates.

**A bug this migration nearly introduced, caught before it shipped.** `TelnyxOutboundBridgeState.ToClientState()` already base64-encodes, and the client encodes again. Double-encoding produces a value Telnyx echoes back that decodes to base64 rather than to JSON, so **every bridge correlation would silently fail to parse and no agent leg would ever be joined to its caller** — a total outbound failure with no error anywhere. The state type gained `ToClientStateJson()`, the client owns the encoding exactly once, and callers hand it the raw form.

**Tests added**

- `RecordingHttpMessageHandler` — the recorded-HTTP double D3 was waiting on. It records verb, path, body and authorization, answers from a queued sequence or a fallback, and **throws on an unexpected request**, so a call the test did not describe is reported rather than silently answered.
- `TelnyxApiClientTests` (12 cases): the action path and bearer, escaping a call id containing path characters (a command otherwise issued against a different resource), origination returning the provider id, a failure carried as a result, retry on 429 and 5xx, no retry on 422, no retry on origination however it fails, a transport failure as a result, each of speak/playback/gather/transfer hitting its own action, credential create and delete, and the forgotten-credential 404.
- `TelnyxTelephonyCredentialIssuerTests` (6 cases) — written **before** the migration so it had something to migrate behind. It pins the cap eviction (oldest first, because the newest is the one the browser is most likely registered on), that a provider refusal stores nothing, that sign-out revokes everything, and that a forgotten credential is still revoked locally.

**One file left the ratchet's danger zone.** `TelnyxTelephonyProvider.cs` went from 875 to 812 lines as its duplicated transport was deleted. The ratchet caught it *growing* mid-migration and then required the gain to be locked in when it shrank — both halves of that guard earning their place within an hour of being written.

### D8. Automated voice belongs to the platform, not to a provider

**Why.** The automated voice conversation — render the greeting, listen, run the completion, decide whether to escalate, conclude the call with a summary and a disposition — lived inside the Telnyx module as a 998-line handler. None of that reasoning is about Telnyx. A second telephony provider could only offer automated voice by copying the whole thing, and every fix to the conversation would then have to be made twice and would eventually be made once. It also meant the conversation could not be tested at all without a Telnyx account and a real phone call.

**What changed.** The conversation moved to a new `CrestApps.OrchardCore.Omnichannel.Voice` module (**Automated Voice**), with the loop itself in `...Voice.Core` as `VoiceAgentConversationLoop`. What a provider contributes is now a small abstraction in Telephony Abstractions, `IVoiceAgentMediaProvider`: speak, start and stop transcription, gather a key press, hang up, plus the provider's own default text-to-speech voice. `IVoiceAgentMediaProviderResolver` picks the media for the call in hand, so a tenant with two providers cannot answer a call through the wrong one.

Telnyx now implements that abstraction (`TelnyxVoiceAgentMediaProvider`), and `TelnyxAiVoiceConversationHandler` is what remains of the old handler: an adapter that translates the four Telnyx event names into the loop's own vocabulary. It is 55 lines. That is the measure of the split — a second provider adds automated voice with a file that size rather than a copy of the conversation.

The decisions worth stating:

- **The event model is the loop's, not the provider's.** `VoiceAgentEvent` carries a kind (answered, speech ended, transcription, hangup), the provider's call id, which provider it is, the activity, and what the person said. Passing the provider's own event through would have put its vocabulary back in the loop by the second provider.
- **The default voice moved to the provider.** Which voices exist and what they are called is a fact about the provider, and the old handler hard-coded a Telnyx-specific one. The loop asks the media for its default and still prefers the activity's configured voice when there is one.
- **No media means silence, not an exception.** A call whose provider supplies no automated voice is left alone with a warning. Throwing would fail the provider's webhook and have it redelivered indefinitely; running the loop anyway would hold a person on a call nobody can speak on.
- **`ITelnyxVoiceAgentClient` shrank to the one thing that is genuinely Telnyx's.** Its speak, transcription and hangup members were a fourth copy of the transport D5 consolidated, and are now gone; what remains is placing the outbound call with the Telnyx connection, caller id and outbound voice profile — routed through `TelnyxApiClient` like everything else.

**A live defect fixed on the way through.** The media provider's `transcription_start` did not carry `transcription_engine` and `transcription_tracks: inbound`, which the old client set. Without the inbound restriction the assistant's own text-to-speech is transcribed back as if the person had said it, and the conversation answers itself. It is now set in the provider, with a test naming the reason.

**Tests added**

- `VoiceAgentConversationLoopTests` (13 cases) drive the loop through a `FakeVoiceAgentMediaProvider` that records instead of dialling: the person picks up and hears a greeting; the greeting is spoken **once** when the answered event is redelivered; the activity leaves `AwaitingCustomerAnswer`, so the background expiry pass cannot fail a call that is happening; the configured voice wins over the provider default; the assistant listens when it finishes speaking; a reply is spoken and listening stops first; an interim transcript is ignored; the same transcript delivered twice is answered once; a closing line finishes before the hangup rather than being cut off mid-word; an escalation is requested exactly once, and only after the closing line; a handoff with nowhere to go ends the call rather than leaving somebody listening to silence; and a call on a provider with no media is left alone.
- `VoiceAgentMediaProviderTests` (11 cases) cover the Telnyx implementation and the resolver: each command reaching its own action, a refusal reported rather than thrown, transcription restricted to the inbound track, the provider's default voice, and resolution by name — including that an unknown provider resolves to nothing rather than to whichever provider happens to be first.
- `AutomatedVoiceFeatureActivationTests` (3 cases) enable **only** the new feature on a fresh tenant with no provider installed: the loop resolves, the media resolver it depends on comes from Telephony, and an event for an unknown provider is declined quietly. This is the check that would have caught the loop being wired to something only a provider module registers.
- The two existing conclusion suites moved with the code as `VoiceAgentContactEmailTests` and `VoiceAgentSubjectFieldsTests` (12 cases).

**Deferred, with the reason.** The plan also named a loop test for "hangup concludes the call with a summary". The conclusion runs in a `ShellScope` deferred task and needs a live shell to be driven end to end, which is integration-suite work rather than unit-test work; its two write-back paths — the disposition summary's subject fields and the captured contact email — keep the deterministic coverage they already had in the moved suites.

**Public API.** `CrestApps.OrchardCore.Telephony.Abstractions` gained the two interfaces, `Telephony.Core` the resolver, and both new assemblies got their first baseline. Each was read before approval.

**Ratchet.** `TelnyxAiVoiceConversationHandler.cs` left the file-size ratchet entirely — 998 lines down to 55 — and the two new projects were added to the scanned set so the loop cannot quietly grow back into the same shape. It is split as `VoiceAgentConversationLoop.cs` (the turn loop, 635 lines) and `VoiceAgentConversationLoop.Conclusion.cs` (the post-call analysis and write-backs, 421 lines).

### D6. The soft phone's arithmetic is out of the 6,251-line file, and tested

**Why.** `soft-phone.js` was one 6,251-line IIFE: DOM rendering, provider adapters, telemetry, state and extension-window logic in a single scope, with **no JavaScript tests of any kind**. The only coverage was Playwright, which runs in release CI and needs a browser and a site. Everything in that file therefore shipped unverified — including the parts that are pure arithmetic and pure mapping, where a mistake is invisible in review and reaches an agent as a wrong number on their screen, a quality indicator that never warns, or a call that looks connected while it is still ringing.

**What changed.** The parts with no DOM and no provider in them moved into `Assets/js/soft-phone/`:

- `format.js` — the phone-number formatters, including the partial-number formatting the keypad runs on every keystroke.
- `quality.js` — the MOS estimate, the WebRTC stats parser, and the poor-call thresholds that are kept in step with the server-side evaluator.
- `telnyx-state.js` — the SDK call-state mapping, the terminal-state check, and the TURN check that a long diagnosis once turned on.
- `reconnect.js` — the reconnect schedule shared by the automatic reconnect and the manual restart loop.

They are concatenated ahead of `soft-phone.js` into the same bundle, so **resource registration does not change** — same script name, same URL, no integrity hash to recompute. Each file attaches to a shared namespace rather than exporting, which is what lets one source file run both in the browser bundle and under Vitest; a build step that produced a different artifact for the tests than for the browser would be testing something other than what ships.

`soft-phone.js` is 6,251 lines down to 6,028 and now names its dependencies at the top instead of defining them.

**A duplicate that had already gone wrong.** The call timer had two implementations: the shared `telephonyClient.formatDuration`, and a fallback copy inside the agent bar. They agreed for the first hour of a call and then disagreed — the shared one switches to `1:15:03`, the copy counted minutes upward as `75:03`. There is now one implementation, `Assets/js/shared/call-timer.js`, concatenated ahead of `telephony-client.js` which re-exports it, and the agent bar's copy is gone.

**Tests added** — `npm test` (Vitest), 47 cases across six files, written **before** the code moved so they characterize the behaviour as it shipped rather than as it ended up:

- `format.test.js` — full and partial national numbers, international numbers, short numbers left alone rather than bracketed as a fragment, and nothing formatted as the string "null".
- `quality.test.js` — a clean line scoring near the top of the scale, the score falling with latency, jitter counting roughly double, the floor at 1 rather than a negative, missing samples not turning the score into `NaN`, and the thresholds matching the server's. For the stats parser: the audio stream picked out from video, the codec and candidate types resolved, **the pair found through the transport when no pair is flagged `selected`** — which is Chrome, and therefore most agents — the busiest nominated pair as the last resort, empty strings rather than a throw on a report with nothing in it, and both spellings browsers use for the media type.
- `telnyx-state.test.js` — every pre-answer state as ringing, active as connected, held as no change (reporting a change there takes a held call off the dialer as though it ended), every ended state as disconnected, and an unknown state never guessed at.
- `reconnect.test.js` — the backoff, and the cap. Reading past the end of the schedule returns `undefined`, and a timer scheduled for `undefined` fires immediately, which turns the backoff into a tight retry loop against a hub that is already struggling.
- `call-timer.test.js` — the hour boundary the two implementations disagreed on, and a negative elapsed time (the timer is derived from a server clock offset, so it can briefly go negative) rendering as `0:00` rather than `-1:-3`.
- `bundle.test.js` — every helper is listed in `Assets.json` and listed **first**. The bundle is a concatenation, not a module graph: a helper that is not listed is simply absent from the browser, and the soft phone then loads, renders, and fails on the first number it formats. Nothing else in the repository would catch that before an agent did.

**CI.** `npm ci --ignore-scripts && npm test` runs in both PR and release CI. Both workflows pinned Node 15, which no current test runner supports; they are on Node 22.

**Deferred, with the reason.** The plan also named splitting the rendering into `ui/*.js` and the adapters into `adapters/*.js`. Those are the parts that touch the DOM and the provider SDKs, so moving them buys no unit coverage — it is a large mechanical restructure of a live component whose only safety net is the Playwright suite that release CI runs. It is worth doing behind that suite, deliberately, rather than at the end of a long change; the extraction here takes the part where the tests are worth having, and the file-size ratchet keeps the rest from growing further.

### D7. Two silences: a callback on the wrong node, and a call nobody has a record of

Both halves of this item are about a failure that produces no error anywhere — the worst kind to operate, because the only signal is a person on a call that is not working.

**The rendezvous registry across nodes.** When Telnyx dials back to the WebSocket that carries call audio, the callback lands wherever the load balancer sends it. The registry that pairs the callback with the call it belongs to was per-node, and answered "nothing pending" for a key another node was holding — the same answer it gives for a key that never existed and for one that expired. The call loses its audio and the log says nothing about why.

With the **Redis** feature enabled, `DistributedWebSocketConnectionRegistry` now records the owning node in Redis under a short expiry. It is worth being exact about what that does and does not buy: **a live socket cannot be moved between processes**, so the wrong node still cannot serve the callback. What changes is that it knows that is what happened, names the node that can, and — the part that matters most — **leaves the rendezvous intact**, because the provider's retry may well reach the right node, and tearing it down would let one misrouted callback end the call outright.

The decisions worth stating:

- **Redis going down must not take call audio with it.** An unreachable store degrades to exactly the per-node behaviour that was there before, which is correct whenever the callback returns to the node that started the call — the overwhelmingly common case. Failing the registration instead would turn a Redis blip into silent calls.
- **Two nodes cannot hold one key.** The claim is a set-if-absent, so a race is resolved by Redis rather than by whichever node writes last. The key is unguessable, so a collision is a bug, and letting both proceed would bind the callback to one call and leave the other with no media at all.
- **The record expires.** The starter releases the key on timeout, but a node that dies mid-call cannot; without an expiry its keys name a node that no longer exists, forever.
- **A separate ownership store, not Redis calls in the registry.** The registry owns the affinity policy — what to do when a socket arrives in the wrong place — and the store owns nothing but the shared record. That is what lets the policy be tested exhaustively in process and the store be tested against a real server.

**The calls nothing had a record of.** Reconciliation walks the platform's own interactions, so it can only repair calls there is a record of. A call placed in the moment before the process died has none: the person is connected, no webhook will ever create a record for them (the events correlate to an interaction that does not exist), and nothing in the system would ever notice. The documentation listed this as a known gap.

Telnyx does offer a way to see them — `GET /connections/{id}/active_calls` — so `TelnyxOrphanedCallReconciler` now asks the connection what it actually has up, every five minutes, and compares it with what the platform knows.

- **Reporting is the default.** An orphaned call may still be two people having a perfectly good conversation, and hanging up on them is the more destructive of the two mistakes. A deployment that would rather release somebody than leave them holding a call nothing can act on can switch **Calls with no local record** to **End call** on the Telnyx settings screen, which speaks an apology first — someone whose call ends mid-sentence with no explanation simply calls back.
- **A young call is not an orphan.** A call up for seconds is far more likely to be one whose interaction is still being written. Below a two-minute grace it is left alone; without that, this would hang up on calls that are working.
- **A refused listing means nothing.** An expired API key must not be read as "every call on this connection is lost". A failed listing ends the pass without acting on anything.

**Tests added**

- `DistributedWebSocketConnectionRegistryTests` (10 cases): a rendezvous handed over on its own node; the owning node recorded, and recorded with an expiry; a claim on the wrong node refused **and the rendezvous left for the node that owns it**; a key another node holds refused at registration; a claimed key not claimable twice; removal releasing it; and both degraded paths when the shared store is unreachable.
- `TwoNodeWebSocketRendezvousTests` (4 cases) in the distributed suite, against a real Redis — the part in-process tests cannot prove: that the claim is genuinely atomic when two nodes race it, and that a node which did not start a call learns so from the shared store. Run with `CONTACT_CENTER_REDIS_CONFIGURATION` pointing at a Redis instance; verified here against `redis:7-alpine` in Docker.
- `TelnyxActiveCallListingTests` (5 cases): the listing path, the page cursor, a continued page, a refusal reported rather than thrown, and an empty connection.
- `TelnyxOrphanedCallReconcilerTests` (8 cases): a known call left alone, an unknown one found, reporting leaving it connected, ending it speaking before hanging up, a young call skipped, every page walked, a refused listing acting on nothing, and an unconfigured tenant asking nothing at all.

**An architecture guard earned its place again.** `RedisRendezvousOwnerStore` tripped the rule against raw StackExchange.Redis outside the approved backplane. The guard is right to ask: the exception is on the allowlist with the reason — an atomic set-if-absent with an expiry has no higher-level Orchard primitive, and the distributed lock is exclusivity without an owner, while the whole point here is being able to name the node that holds the key.

### E5. The duplicated logic, consolidated

Four of the seven rows in this item were closed by earlier work (the SMS roll-up by C7, the Telnyx transport by D5, the automated voice loop by D8, the soft phone's shared modules by D6). These are the three that were left.

**The subject write-back, written twice.** `GetContactEmail`, `TryApplyContactEmail`, `GetSubjectTextFields` and `ApplySubjectFields` existed verbatim in the SMS handler and in the voice handler. Both copies carry fixes found the hard way — writing into a field's real `Text` structure rather than merging a model-authored content item, and upserting the contact email rather than appending a duplicate every time — and a fix applied to one of two copies is a fix that silently does not apply to half the product. There is now one `OmnichannelSubjectWriter` in `Omnichannel.Core`, and the two suites that covered the behaviour moved onto it.

**The two offer paths.** `OfferNextAsync` (take the next call off a queue) and `OfferToAgentAsync` (ring one named agent) each carried the same thirty lines: resolve the reserved agent, find the interaction, refuse the ones that cannot be offered, re-offer. They are now one `ApplyOfferAsync`, which reports the two cases the callers genuinely treat differently rather than deciding them: a reservation with no interaction, and a call that has already ended. A queue has more calls to try behind a dead one; a direct offer does not, because the caller asked for that agent and that activity.

`VoiceQueueOfferServiceTests` (13 cases) was written **first**, characterizing both paths — including the preview-dial reservation that must survive having no interaction yet, because releasing it takes the call away from the agent who was about to place it — so the consolidation is provably behaviour-preserving rather than argued to be.

**The failure result, written four times.** Every voice provider and the router in front of them had a private `Failure(errorCode, errorMessage)`. They now delegate to one factory on the result type. Two of the four copies **left the provider name unset**, so a Dialpad failure and a router failure reached the operator attributed to nobody — precisely when attribution is what they need. Dialpad now carries its name; the router deliberately passes none, because it fails before a provider has been chosen and that is itself the information.

### E6. The largest file in the Contact Center module, split

`AgentWorkspaceEndpoints.cs` was 924 lines of two different jobs: routing, authorization and response shape on one side; building the offer card, the live-call panel, and the labels and links on them on the other. They change for different reasons — a new field on the offer card is not a change to who may call the endpoint — so the presentation half moved to `AgentWorkspaceEndpoints.ViewModels.cs`. 575 and 390 lines, and one fewer entry on the size ratchet.

**Deferred, with the reason.** The remaining rows — `ActivitiesController` (1,544), `TelephonyHub` (1,412), `SmsOmnichannelEventHandler` (1,221 after E5 took its duplicates), `ActivityReservationService` (888) — are not file-size problems that a partial class fixes; splitting them usefully means extracting real collaborators (a hub filter, per-use-case services, a reservation transition committer) from live code whose behaviour is exercised by integration paths rather than by unit tests. That is deliberate work with its own test-first pass, not a tail-end tidy at the close of a long change. The ratchet holds each of them at its current size, which is what stops the problem growing while they wait.

### E7. What the routing paths cost

**The claim, checked.** The item listed several suspicions. Two were already false: the Omnichannel channel-endpoint lookup on the inbound path reads a shell-cached immutable document rather than querying per message, and the voice availability read was already batched. One was true and is fixed.

**`LeastLoadedSmsRoutingStrategy` was two problems at once.** To decide who was least busy it called `GetForAgentAsync` per candidate — which loads **every conversation the agent has ever been assigned**, years of closed threads included, to count the open ones — and did so once per agent, so routing a single message cost as many round trips as the queue has members. On a two-hundred-agent queue that is two hundred full-history loads before anybody is chosen, on the path every inbound message runs.

It is now two queries in total, whatever the size of the queue: one batched count of open assigned threads (indexed columns only, no conversation document loaded) and one batched count of live voice work, which already existed. `CountOpenAssignedAsync` has both a single-agent and a batched form, and a test asserts they agree — two ways to ask the same question is two chances to answer it differently.

**Budget tests, because the regression is invisible.** A per-agent version of any of this returns the identical answer in the identical order; only counting the calls distinguishes them. `SmsRoutingLoadBudgetTests` builds a two-hundred-agent queue and asserts one batched read each and **never** the single-agent APIs the batched ones replaced; `VoiceRoutingRoundTripBudgetTests` does the same for the voice availability read, and adds the empty-queue case (a queue nobody is signed into is the common out-of-hours state, and it should cost one read, not three). `SmsInboxQueryTests` proves the new query against a real SQLite database rather than a mock, including that closed and pooled threads are not counted as work in front of anybody.

**A guard for the shape of the problem.** Some collections are bounded by configuration — queues, dispositions, skills — and loading all of them is fine; the deployment sources have to. Others grow by one row per customer interaction. `UnboundedCollectionLoadTests` fails when a full load appears on one of the second kind in the Contact Center, Omnichannel or telephony projects. There are none today, which is the point: the call that introduces one looks exactly like the calls above it, and nothing else in the repository would object. The rule carries its own self-test, because a guard whose only evidence is that it passes is a guard nobody has checked.

**Deferred, with the reason.** The plan also named a load test driving 200 agents × 20 queues at 10 calls per second against Redis and PostgreSQL, asserting a p95 offer latency below 500 ms. A wall-clock percentile measured on shared CI hardware is a number that moves with the machine, so it is a flake generator that gets suppressed after the third false failure — and once suppressed it protects nothing. What actually determines whether that SLO holds as a tenant grows is the shape of the work: whether the cost of routing one call follows the size of the team and the age of the tenant. That is now asserted deterministically at both routing paths, and it is the assertion that would have caught the defect this item existed to find.

### A6 (completed). The queue talks to the people waiting in it

The policy, the wait-time calculator, the callback service and the overflow scheduler landed in P2; what was missing was everything that makes a caller actually hear any of it, and the wiring that made the scheduler apply.

**Overflow followed one hop, whatever was configured.** `OverflowScheduler` understood a chain of tiers and was tested, but the live path still read the legacy single-target field, so every tier past the first was configuration the product ignored. `OverflowDueAsync` now goes through the scheduler: furthest hop first, because somebody who has already waited past every tier belongs at the last one rather than crawling through each in turn while a sweep ticks; never a queue they have already been through, because a caller passed in a circle has their wait reset at every hop and never reaches anybody; and immediately when the queue is closed, because the wait threshold exists to give the queue a chance to answer and a closed queue is not going to.

**`TelnyxQueueTreatmentProvider`** is the half that reaches the caller: speak, hold music, and a prompt that collects a key. Two decisions worth stating — hold music **loops**, because music that plays once leaves the caller in silence for the rest of their wait, which is indistinguishable from a dropped call; and nothing here throws, because treatment sweeps every waiting caller and one leg that has just hung up would otherwise take the pass down and leave everybody else silent.

**`QueueTreatmentService`** is the part in between: it finds the caller's live leg, works out what there is to say, says it, and records that it was said. A caller with no live leg is skipped rather than marked treated — recording it would silently consume the welcome they never heard. An announcement with nothing in it is not spoken, because a queue that announces neither position nor wait has configured a cadence with no content and an empty sentence just interrupts the music. The callback offer is made once and recorded, because re-prompting somebody who already declined, every few seconds for the rest of their wait, is worse than never offering.

**The estimate is honest or absent.** With nobody working the queue there is nothing to extrapolate from, so no time is quoted at all rather than a number invented to fill the sentence. The handle time it extrapolates from is a configured expectation rather than a live measurement: the metric store counts events, not handle time, and deriving an average live would put a scan of ended interactions on a sweep that runs every few seconds for every queue.

**`QueueTreatmentBackgroundTask`** gives both jobs the precision the acceptance criterion asks for. Orchard schedules background tasks by cron, whose finest granularity is a minute, which is exactly why a queue set to overflow after twenty seconds overflowed somewhere between sixty and eighty. The task is scheduled every minute and sweeps repeatedly inside its own run, ten seconds apart, bounded well inside its distributed lock's expiration so a slow pass cannot outlive its lock and let another node sweep the same queues.

**Tests added**: `QueueTreatmentServiceTests` (10) and `TelnyxQueueTreatmentProviderTests` (6), plus three cases on `ActivityQueueServiceTests` for the chain, the loop refusal, and the queues configured before chains existed — that last one because dropping the single-target field would strand every caller the queues already in production were built to hand on.

### A7 (runtime complete). Callers can be given a menu

The flow state machine landed in P2 with its convergence properties; nothing played it to anybody.

- **`TelnyxIvrProvider`** issues the gather. Recorded audio wins over synthesized speech when the menu has it, because a tenant who recorded their menu did so precisely because they did not want it read out. One key per level: a menu that waits for several digits leaves the caller wondering whether it heard them.
- **`IvrExecutionService`** plays what the flow says is next and persists where the caller is **on the interaction**, so it survives a restart and so a redelivered gather is recognised. The digits offered to the provider come from the menu's own options, so it collects a key that means something rather than any key at all.
- **`Digits` on the call event.** Telnyx reports the key on `call.gather.ended` and nothing read it, so every caller would have registered as having pressed nothing and been sent to the fallback.
- **`InboundVoiceDigitsSink`** turns a key press into the caller being put somewhere. A press on a call this feature does not know is **not claimed**, because another feature on the same tenant may be collecting digits for its own reasons and reporting it handled would swallow their event. A prompt is not a destination: queueing somebody mid-menu puts them in line while they are still being asked where they want to go.
- Pressing **nothing** is still delivered to the flow. A caller who says nothing has made a missed choice, and the flow is what decides whether that repeats the menu or sends them onward; dropping the event leaves them in silence.

**A test I wrote asserted the wrong contract, and the product was right.** I expected a redelivered gather to report "ignored". The state machine deliberately re-plays the menu the caller is on, so a duplicate webhook leaves them hearing their options rather than silence, while still not taking them a level deeper. The test now pins that behaviour, including that the caller's position does not move.

**The activation suite caught the fifth real defect of this plan.** The IVR services were registered in the base Contact Center feature, but the resolver needs the entry point manager, which the Inbound Voice feature registers — so the base feature's own dependency closure could no longer construct its own services. They now live with the entry points they read, which is where they belonged.

**Deferred, with the reason.** The menu **editor** is not built. The engine now exists and a flow can be configured through a recipe or the API, but authoring a tree of prompts, keys and actions is a screen of its own, and building it in the same pass as the runtime would mean designing the UI against an engine whose behaviour had not yet been exercised anywhere.
