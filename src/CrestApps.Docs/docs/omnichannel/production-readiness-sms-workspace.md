---
sidebar_label: Readiness — SMS Workspace
sidebar_position: 8
title: Production readiness — SMS Workspace and SMS automation
description: Findings and the test-first remediation plan for the human SMS Workspace (inbox, routing, delivery, compliance, UI) and the automated SMS conversation module, including the AI-to-human boundary.
---

# Production readiness — SMS Workspace and SMS automation

Part of the [Production Readiness Plan](../contact-center/production-readiness-plan.md). Workstream **C**.

## What exists today (verified)

`SmsInboundProcessor` (registered as an `IOmnichannelEventHandler`) turns an inbound `OmnichannelMessage` into an
`SmsConversation` through an ordered router chain: `ExistingConversationRouter` (200), `RoutedQueueRouter` (250,
push-assign via `LeastLoadedSmsRoutingStrategy`), `NumberRouteRouter` (300, agent or shared-pool queue from
`SmsEndpointRoutingSettings` on the channel endpoint), `FallbackRouter` (1000). Sends go through
`SmsConversationService` → `ISmsDispatcher` (per-endpoint provider). Real-time updates use `SmsPortalHub` with
tenant-scoped agent, queue and "unassigned" groups. A one-minute sweep (`SmsRoutedReassignmentService`) re-routes
or re-pools routed threads nobody opened within the pickup grace. Broadcasts and templates have their own catalogs.
The automated (AI) SMS path lives in `CrestApps.OrchardCore.Omnichannel.Sms` (`SmsOmnichannelEventHandler`, 1,390
lines) and yields the number to the human inbox after a handoff.

## C1. Insecure direct object reference on every conversation action (Critical)

**Evidence (`Sms.Workspace/Controllers/AdminController.cs`).** `Conversation`, `ThreadMessages`, `Claim`, `Send`,
`SetStatus` authorize only `SmsWorkspacePermissions.UseSmsPortal` and then load the conversation by id. Any portal
user can read another agent's personal thread, close it, or claim a pooled thread from a queue they are not a
member of. `SmsConversationService.IsAuthorized` (used only by `SendAsync`) returns true for every queue-owned
thread ("phase 1 authorizes any signed-in agent for a shared-pool number") and for every unassigned personal thread.
`SetStatusAsync` performs no check at all. `Transfer` requires `ViewAllConversations`, which is the only per-action
distinction.

**Target design.**

- `ISmsConversationAuthorizationService.AuthorizeAsync(principal, conversation, SmsConversationOperation)` with
  operations View, Send, Claim, Close, Transfer, Snooze. Rules: supervisors (`ViewAllConversations`) may do all;
  the assigned agent or personal owner may View/Send/Close/Snooze; members of the owning queue (entitlement via
  `IAgentEntitlementPolicy` when Agent Entitlements is enabled, membership otherwise) may View and Claim pooled or
  unassigned threads; nobody else.
- Implement as an Orchard `AuthorizationHandler` over a resource (mirroring
  `OmnichannelActivityAuthorizationHandler`) so the same rule applies in the controller, the hub, the endpoints
  and the display driver buttons.
- The controller and `SmsConversationService` both call it; the service is the last line of defence.

**Tests first.** `SmsConversationAuthorizationTests` (matrix of role × ownership × operation), and
`SmsWorkspaceAdminControllerTests` (none exist today) asserting `Forbid` for a foreign personal thread on each
action. Add an architecture test that every `AdminController` action loading a conversation calls the authorization
service (pattern used by `ContactCenterHubSecurityTests`).

**Acceptance.** An agent cannot open, send on, close, or claim a thread outside their ownership or queue membership;
the inbox only lists what `View` permits.

## C2. Cross-tenant and over-broad real-time broadcast (Critical, small)

**Evidence (`SmsRealTimeNotifier.MessageDeliveryUpdatedAsync`).** Uses `_hubContext.Clients.All`. Orchard maps the hub
per tenant but the SignalR hub type and backplane are shared, so delivery receipts (conversation id, message id,
status, error code) reach every connected client of every tenant, including users without portal permission.
`NewInboundMessage` and `ConversationAssigned` correctly use `TenantSignalRGroupName`.

**Target design.** Target the conversation's assigned-agent group, owning-queue group, or the unassigned group, exactly
like `Target(...)` does for inbound notifications; never `Clients.All`. Add an architecture test in the SignalR test
folder that forbids `Clients.All` in any CrestApps hub notifier.

**Tests first.** `SmsRealTimeNotifierTests` with a fake `IHubClients` asserting group targeting; the architecture
test above.

## C3. SMS Workspace fails at DI resolution without Work Distribution (Critical)

**Evidence.** `Sms.Workspace/Manifest.cs` declares dependencies on Channel Endpoints, Agent Services, `OrchardCore.Sms`
and `OrchardCore.SignalR` only. `LeastLoadedSmsRoutingStrategy` requires `IActivityQueueManager` (registered only in
`QueuesStartup`, the Work Distribution feature) and `IInteractionManager` (registered in the Contact Center base
`Startup`). `RoutedQueueRouter` is always registered and always part of the `IEnumerable<ISmsInboundRouter>` chain,
so on a tenant with SMS Workspace but without Work Distribution, resolving the chain throws and **every inbound SMS
is dropped**. The module project also references `CrestApps.OrchardCore.Omnichannel.Managements` (the full CRM
administration assembly) despite the manifest stating it reuses "not the Omnichannel Management administration".

**Target design.**

- Split routed distribution into its own feature `CrestApps.OrchardCore.Sms.Workspace.RoutedDistribution` that depends
  on `ContactCenterConstants.Feature.Queues`, registers `RoutedQueueRouter`, `LeastLoadedSmsRoutingStrategy`,
  `SmsRoutedReassignmentService`, the sweep, and the "Routed" option in the endpoint routing editor. The base feature
  keeps personal and shared-pool routing, which only need the agent directory.
- Queue membership for SMS in the base feature comes from an abstraction in Contact Center Abstractions
  (`IAgentQueueMembershipReader`) implemented by Agent Services, so the base feature never touches `ContactCenter.Core`
  routing types.
- Remove the `Omnichannel.Managements` project reference; whatever is used from it (contact search, display names)
  is moved to `Omnichannel.Core` or replaced with the reusable `ItemSelector`/`UserPicker` endpoints.

**Tests first.** Extend `ContactCenterFeatureActivationTests`/`ContactCenterFeatureDependencyAuditTests` to SMS
Workspace: (a) enable SMS Workspace alone → an inbound SMS creates a personal or pooled conversation; (b) enable the
routed feature without Work Distribution → feature dependency pulls it in; (c) `ContactCenterFeatureDependencyArchitectureTests`
gains a rule that `Sms.Workspace` references only `ContactCenter.Abstractions` (and `Core` only from the routed
feature assembly if it is kept in the same project, gated by startup).

**Acceptance.** Feature matrix green; the SMS Workspace assembly no longer references `Omnichannel.Managements`.

## C4. Inbound idempotency and per-thread serialization (High)

**Evidence.** `SmsInboundProcessor.ProcessAsync` does `FindByAddressesAsync` then `CreateAsync` with no lock and no
unique index on (ServiceAddress, ContactAddress); two texts arriving within the same second from a new number (or the
same text redelivered by the provider) create two conversations. Nothing deduplicates on the provider message id:
`TwilioWebhookEndpoint` acknowledges immediately and processes in a detached scope (`_ = backgroundScope.UsingAsync`)
with no dedupe on `MessageSid`; the Telnyx SMS webhook sets `OmnichannelEvent.Id` to the provider id but no store
enforces uniqueness. Voice already solved this with `IProviderWebhookInbox`.

**Target design.**

- Reuse the provider webhook inbox for SMS (`ProviderWebhookInboxDelivery` with provider + message id as the delivery
  id and an `sms-inbound` handler), so retries are absorbed and processing survives a crash. This also removes the
  fire-and-forget scope in the Twilio endpoint.
- Serialize per thread with `IDistributedLock` keyed `SmsConversation:{service}:{contact}` around find-or-create and
  the roll-up update.
- Add a unique index on `SmsConversationIndex (ServiceAddress, ContactAddress)` and on
  `OmnichannelMessageIndex (Channel, ProviderMessageId)`; the create path catches the unique violation and re-reads.

**Tests first.** `SmsInboundProcessorTests`: concurrent first messages create one conversation (use the existing
`AvailabilityStoreSharedDatabaseTests` shared-database pattern); a redelivered provider message id is persisted once;
`ContactCenterMigrationSqlTests`-style test for the new indexes; Twilio endpoint test that a retry with the same
`MessageSid` is accepted and not reprocessed.

## C5. Inbox and thread queries must be paged and indexed (High)

**Evidence.** `AdminController.Index` loads **all** conversations for supervisors (`GetAllAsync`) or merges per-agent
and per-queue lists in memory, computes tab counts in memory, and builds a display shape per row with no paging.
`Conversation` loads every message of a thread. `ApplyDeliveryReceiptAsync` loads every outbound message of a
conversation to find one by provider id. `GetRoutedAwaitingPickupAsync` filters by age in memory because
`AssignedUtc` is not indexed.

**Target design.** Add `SmsConversationIndex.UnreadCount`, `AssignedUtc`; add `OmnichannelMessageIndex.ProviderMessageId`;
introduce `ISmsConversationStore.QueryAsync(SmsInboxQuery)` (filter, visibility, sort, page) and
`CountAsync(SmsInboxQuery)`; page the inbox with `PagerSlim` (50 per page) and the thread with a "load earlier" cursor
on `CreatedUtc`. Delivery receipt lookup becomes a single indexed query.

**Tests first.** A `SmsInboxQueryPlanBudgetTests` modeled on `AgentSessionQueryPlanBudgetTests` (index seek on the
visibility predicate, one count query, one page query); store tests for paging and cursor stability.

## C6. Provider message id capture and outbound reliability (High)

**Evidence.** `SmsConversationService.SendAsync` calls the dispatcher synchronously inside the request, stores
`Sent` or `Failed`, and never records a provider message id (the Orchard `SmsResult` does not carry one), so
delivery receipts match "the newest outbound without a provider id". There is no retry, no throughput limiting, and no
per-provider rate awareness (10DLC/toll-free limits).

**Target design.**

- Extend `ISmsDispatcher` result with `ProviderMessageId` and have `TelnyxSmsProvider` (and the Twilio/ACS providers)
  return it; persist it on the message at send time.
- Introduce an outbound SMS outbox (reuse `ContactCenterOutbox` patterns): the request enqueues, a background
  dispatcher sends with exponential backoff, a per-endpoint token-bucket rate limit, and terminal failure after N
  attempts with a visible error on the bubble. The composer shows Queued → Sent → Delivered transitions over the hub.
- Group sends and broadcasts reuse the same outbox.

**Tests first.** `SmsDispatcherTests` for id capture; `SmsOutboundOutboxTests` (retry, backoff, rate limit,
terminal failure); `SmsConversationServiceTests` asserting a receipt matches by provider id even when two messages
were sent in the same second.

## C7. One conversation router for inbound, handoff and reassignment (High)

**Evidence.** Three code paths decide ownership independently: the inbound chain, `SmsAgentHandoffService`
(always Unassigned), and `SmsRoutedReassignmentService` (its own strategy call). `BuildPreview` is copied three
times; transcript import is duplicated between handoff and inbound.

**Target design.** `ISmsConversationRouter.RouteAsync(SmsRoutingContext)` wraps the existing `ISmsInboundRouter` chain
and is the only entry point; context carries the trigger (Inbound, Handoff, Reassignment, ManualTransfer). Extract
`SmsConversationRollup` (preview, unread, last message) into one helper. `SmsAgentHandoffService` and the sweep call
the router.

**Tests first.** Router tests per trigger; existing router tests kept; handoff tests updated (see B4).

## C8. Agent SMS availability tied to presence, and first-response SLAs (High)

**Evidence.** `SmsAgentAvailability` is a bag flag on `AgentProfile` toggled from the inbox; it is not tied to the
agent session or heartbeat, so an agent who closes the browser stays "available" for routed SMS until the five-minute
pickup sweep re-pools each thread. There is no first-response timer, no unread-age escalation, and no supervisor SLA
view. Routed mode is a silent push with no accept/decline.

**Target design.**

- Availability becomes a derived state: the flag **and** a live `AgentSession` heartbeat (reuse
  `AgentAvailabilityOptions.HeartbeatTimeout`); the hub connection for the portal registers a session the same way
  the Contact Center hub does. Sign-out clears it.
- Add `SmsConversation.FirstResponseDueUtc` (queue policy `FirstResponseTargetSeconds`) and an escalation policy
  (re-route, notify supervisor group, or bump priority) evaluated by the existing sweep at a 30-second cadence.
- Optional accept/decline for routed threads (configurable), reusing the reservation vocabulary conceptually but kept
  in the SMS store (no voice reservation coupling).

**Tests first.** `SmsAgentAvailabilityServiceTests` for the derived state; `SmsFirstResponseSlaTests`; sweep tests
for escalation.

## C9. Compliance: STOP/HELP/START, quiet hours, auto-reply (High)

**Evidence.** STOP closes the thread and flags the contact but sends no confirmation; HELP and START/UNSTOP are not
handled; `SmsEndpointRoutingSettings.AutoReplyMessage` is stored by the editor but never sent by any code path
(dead setting); human sends have no quiet-hours guard (the business-hours gate only covers automated cadences).

**Target design.** `ISmsKeywordPolicy` (Core) with STOP → confirmation + DoNotSms, HELP → configured help text,
START → clear DoNotSms and reopen; an after-hours/auto-reply step in the router that sends `AutoReplyMessage` once per
thread per day when configured; a quiet-hours warning in the composer (contact-local time) with a supervisor override
permission. Reuse `OmnichannelSmsComplianceHelper`.

**Tests first.** Keyword policy tests; router tests for auto-reply once-per-day; composer test that quiet hours warn
and require the permission.

## C10. Inbox and thread UI (Medium)

**Evidence.** `Conversation.cshtml` (331 lines) both polls every 7 seconds and subscribes to the hub; ~100 lines of
inline JavaScript across the Admin views; no optimistic send; no draft persistence; no message paging; contact
search re-implemented per view.

**Target design.** Move scripts to `Assets/js/sms-workspace.js` (built by the module's asset pipeline like the
Contact Center scripts); event-driven refresh only (poll fallback every 60 s when the hub is disconnected);
optimistic bubble with Queued state; local draft per thread; "load earlier" paging; reuse `ItemSelector` for
contact search. Add a Playwright spec set for the workspace to the existing Playwright project.

## C11. SMS automation module hygiene (Medium)

**Evidence (`Omnichannel.Sms/Handlers/SmsOmnichannelEventHandler.cs`).** 1,390 lines; a `static ConcurrentDictionary`
generation registry keyed by session id (process-wide, not tenant-aware, not shared across nodes); helpers
(`GetContactEmail`, `TryApplyContactEmail`, `GetSubjectTextFields`, `ApplySubjectFields`) duplicated verbatim in
`TelnyxAiVoiceConversationHandler`. The module depends on the Business Hours feature by string id.

**Target design.** Extract `IAutomatedConversationGate` (per-tenant singleton registered by DI, or distributed lock
when Redis is on) for single-active-generation; move the shared subject/contact helpers to `Omnichannel.Core`
(`SubjectFieldWriter`, `ContactEmailWriter`); split the handler into inbound intake, reply generation, and conclusion
services. Replace the string feature id with a constant from `ContactCenter.Abstractions` (already referenced by
`Omnichannel.Core`?) or move Business Hours abstractions to `Omnichannel.Core` where `IBusinessHoursGate` already
lives.

**Tests first.** Existing `OmnichannelAutomationHelperTests` stay; add `AutomatedConversationGateTests`, and
characterization tests for the extracted helpers so the voice handler and SMS handler share one tested
implementation.
