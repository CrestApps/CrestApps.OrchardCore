# Contact Center Module Architecture and Implementation Plan

> **Status:** Active; commercial production release is blocked. The independent production-readiness review dated 2026-07-13 found release-blocking tenant-isolation, feature-composition, concurrency, provider, security, and validation gaps. This is the durable, repository-tracked design and progress document for the Contact Center module set. It is referenced from `.github/copilot-instructions.md` so every AI session reviews it before doing Contact Center work.
>
> **How to use this document:**
>
> - Read the **Independent production-readiness review (2026-07-13)** and **Progress status** sections first. The production-readiness remediation gates override older completion claims when they conflict.
> - Treat the **Test-first remediation program** as the immediate source of truth. Do not resume advanced capability work until its predecessor release gates are complete.
> - Keep the **Progress status** section current after each meaningful change (what shipped, what is in progress, decisions made).
> - Never write competitor product names in code, comments, public docs, or identifiers. Adopt only the industry-standard concepts and terminology captured in the **Standard contact center terminology and metrics** section.
> - Respect the layer boundary: **CRM (Omnichannel) owns business work data, Contact Center owns orchestration, Telephony owns media execution.** `OmnichannelActivity` remains the universal work item. `Interaction` is communication history for one attempt and never owns workflow or disposition.

## Problem statement

Design an enterprise-grade Contact Center orchestration layer for the existing Orchard Core communications platform. The Contact Center must extend Omnichannel Management instead of introducing a separate work model, sit between CRM and Telephony, own routing and communication orchestration, and allow agents and supervisors to operate directly inside the CRM UI without depending on an external contact center system.

The document began as a domain and architecture plan. It now also records implementation evidence, production-readiness findings, release gates, and the required test-first remediation sequence.

## Historical starting baseline

The following baseline describes the codebase at the start of the Contact Center project. The later progress ledger and the 2026-07-13 independent review describe the current implementation.

### Existing Telephony boundary

- `src\Abstractions\CrestApps.OrchardCore.Telephony.Abstractions` defines provider-agnostic soft-phone abstractions such as provider, service, client callbacks, call requests, call references, capabilities, calls, call state, direction, and persisted telephony interactions.
- `src\Modules\CrestApps.OrchardCore.Telephony` registers the Telephony feature, the soft-phone feature, the SignalR hub, provider resolver, call-control service, authentication services, settings, permissions, and persisted call history.
- `TelephonyHub` exposes user-initiated call-control operations and pushes call state changes to the current soft-phone client through SignalR.
- `ITelephonyService` delegates dial, answer, reject, hangup, hold, resume, mute, transfer, merge, send digits, credentials, and provider capabilities to the configured provider.
- `TelephonyInteraction` stores provider-independent call history for the soft phone, but this is currently a call-centric history model, not a business interaction orchestration model.
- `src\Modules\CrestApps.OrchardCore.DialPad` is one provider module that implements the Telephony provider boundary.

Design implication: Telephony must remain the media and provider execution layer. Contact Center must not own media, provider authentication, or provider-specific call execution.

### Existing Omnichannel and CRM boundary

- `src\Modules\CrestApps.OrchardCore.Omnichannel` is the base omnichannel layer and configures the shared `Omnichannel` YesSql collection.
- `src\Core\CrestApps.OrchardCore.Omnichannel.Core` owns shared CRM communication models:
  - contacts via `OmnichannelContactPart`
  - activities via `OmnichannelActivity`
  - inventory loading via `OmnichannelActivityBatch`
  - campaigns via `OmnichannelCampaign`
  - dispositions via `OmnichannelDisposition`
  - channel endpoints via `OmnichannelChannelEndpoint`
  - subject flow settings via `SubjectFlowSettings`
  - subject actions via `SubjectAction`
  - messages and inbound omnichannel events
- `OmnichannelActivity` is the CRM task/work item and remains the universal unit of work for Contact Center. It stores channel, endpoint, manual or automated interaction type, AI session, contact, campaign, schedule, assignee, completion, disposition, subject content type, subject payload, urgency, and status.
- Current activity statuses are task-oriented and narrow: not started, awaiting agent response, awaiting customer answer, completed, and purged.
- `src\Modules\CrestApps.OrchardCore.Omnichannel.Managements` provides Interaction Center CRM screens for contacts, subject flows, campaigns, dispositions, channel endpoints, activities, batches, bulk activity management, and subject actions.
- Subject-driven behavior currently lives in Subject Flows and Subject Actions. The changelog states that the OrchardCore.Workflows dependency and previous workflow activities/events were removed from Omnichannel Management.
- `AutomatedActivitiesProcessorBackgroundTask` processes scheduled automated activities every five minutes and dispatches them to channel-specific `IOmnichannelProcessor` implementations.
- `src\Modules\CrestApps.OrchardCore.Omnichannel.Sms` provides automated SMS activity processing and inbound SMS event handling, including AI chat session integration and disposition/action completion.

Design implication: CRM remains the system of record for contacts, activities, campaigns, subjects, dispositions, subject actions, and the CRM timeline. Contact Center must not introduce a second work-item/task model. It extends activities with assignment/reservation and classification metadata, uses activities as queue and dialer inventory, records one or more `Interaction` history records for each activity, and routes all disposition changes through the CRM activity-disposition path.

Required CRM alignment:

- Contacts are customer records only. Dialers and queues never operate directly on contacts.
- Subjects are workflow/disposition definitions only. They provide scripts, subject actions, rules, and automation behavior for activities.
- Activities are the only business work items. They may be created with `AssignedToId = null` so dialers and routing can dynamically reserve and assign ownership later.
- Activities need Contact Center assignment metadata (for example assignment status, reservation id, reserved-by actor, reservation timestamps, and reservation expiry) so multiple dialer or routing instances cannot claim the same work.
- Activities need classification metadata such as kind (call, email, SMS, meeting, task) and source (manual, preview dial, power dial, progressive dial, predictive dial, callback, inbound, workflow, API). Workflows must ignore source and react only to the activity and final disposition.
- Load Inventory selects an activity source before showing the editor. Manual inventory loads require user assignment while loading; dialer inventory loads hide user selection and load unassigned activities with an available assignment status so the dialer can reserve and assign them later.
- Dispositions belong to activities, not interactions. Provider, agent, AI, and workflow outcomes must converge through a single activity disposition service before subject actions/workflows run.
- Dialer and voice services classify technical attempt outcomes first (for example no answer, busy, disconnected number, rejected, failed, voicemail). Subject-owned workflow then decides the business meaning by mapping those system outcomes to dispositions or retry/callback behavior. Dialer profiles never own business workflow.
- Contact Center must provide CRM-integrated agent surfaces where agents receive work. Queue/campaign sign-in, sign-out, and presence belong in the Telephony soft phone through display-driver tabs; broader desktop surfaces handle offers, active activity context, interaction history, wrap-up, required disposition, and supervisor/AI assistance from the CRM UI.
- PBX/voice providers that can do more than soft-phone call control may implement Contact Center voice-provider abstractions for dialer dialing, call assignment, provider-side queues, queue events, and PBX presence synchronization.

### Existing real-time boundary

- `src\Modules\CrestApps.OrchardCore.SignalR` provides the shared SignalR feature and hub registration pattern.
- Telephony already uses SignalR for soft-phone call-control requests and current-user call state updates.

Design implication: Contact Center should add its own real-time event stream for agent desktop, supervisor dashboard, and queue monitors, and it should consume or normalize Telephony events instead of overloading TelephonyHub with routing responsibilities.

### Historical gaps that initiated the project

- No communication-history Interaction domain object linked to CRM activities across voice sessions, future channels, routing, and analytics.
- CRM activities need nullable ownership, assignment/reservation metadata, and source/kind classification so dialers can work unassigned inventory safely.
- No queue model, queue membership, agent reservation, or routing decision model.
- No real-time agent presence or capacity model.
- No inbound routing orchestration.
- No outbound dialer orchestration beyond manual CRM activities and automated SMS processing.
- No CRM-integrated Contact Center agent surfaces for soft-phone sign-in/presence, current offer handling, active activity, injected phone controls, and wrap-up.
- No contact-center-level call session mapping between provider calls and business interactions.
- No Contact Center voice-provider abstraction that lets PBX providers participate in dialer dialing, call assignment, and provider-side queue behavior.
- No supervisor dashboard or live queue metric projection.
- No durable domain event stream/outbox for contact center orchestration.
- Existing OrchardCore.Workflows integration for Omnichannel Management has been removed; Contact Center needs an explicit workflow strategy.
- Existing CRM Activity statuses and manual/automated interaction types are too limited for contact center lifecycle management.

## Design principles

1. Activity first for work: `OmnichannelActivity` is the universal CRM work item for queues, dialers, wrap-up, dispositions, and workflows.
2. Interaction first for communication history: every voice call, chat, SMS, email, or future channel attempt is an Interaction linked to an Activity; an Interaction never owns workflow or disposition.
3. Source-driven activity loading: Load Inventory is the common activity-loading surface and supports source-specific UI/behavior for manual, dialer, callback, inbound, API, and future sources through Orchard display drivers and source options.
4. Contact Center owns orchestration: routing, queues, reservations, presence, dialer pacing, wrap-up, lifecycle, and operational metrics.
5. Telephony owns media execution: providers dial, answer, transfer, hang up, hold, resume, conference, and report provider call state.
6. Domain events connect components: components publish and subscribe to events instead of directly invoking each other’s internal workflows.
7. Feature-gated modularity: capabilities should be split into Orchard Core features so tenants can enable only the contact center capabilities they need.
8. Tenant isolation by default: all state, events, real-time groups, settings, permissions, and analytics projections are tenant-scoped.
9. Channel-agnostic core: voice is the MVP channel, but the same interaction, routing, queue, SLA, presence, and wrap-up concepts must support chat, SMS, email, and AI agents later.
10. Industry-standard naming: use generic contact center terms in code and public docs. Do not name implementation artifacts after competing products.
11. Durable resumability: keep a persistent project plan/progress document in the repository and reference it from `.github\copilot-instructions.md` so future AI sessions review it before changing Contact Center code.

## Proposed module and feature breakdown

### Shared abstractions and core services

1. `CrestApps.OrchardCore.ContactCenter.Abstractions`
   - Shared contracts and domain vocabulary for interactions, events, channel adapters, routing strategies, dialer strategies, real-time notifications, permissions, and feature constants.
   - Depends only on stable abstractions needed by providers and optional channel adapters.

2. `CrestApps.OrchardCore.ContactCenter.Core`
   - Domain models, stores, managers, event dispatcher contracts, projections, and reusable policies that are not themselves Orchard modules.
   - Uses tenant-local persistence patterns consistent with Omnichannel Core.

### Orchard modules and features

1. `CrestApps.OrchardCore.ContactCenter`
   - Headless Contact Center module and dependency root.
   - Core feature: interaction management, event log, tenant settings, and baseline permissions.
   - Must depend on the Omnichannel base/domain boundary, not the Omnichannel Managements administration feature.
   - Admin navigation and CRM-management UI integration belong in an explicit administration bridge feature.

2. `CrestApps.OrchardCore.ContactCenter.Queues`
   - Queues, queue membership, queue priorities, overflow rules, SLA settings, queue metrics, and queue monitor surfaces.

3. `CrestApps.OrchardCore.ContactCenter.Routing`
   - Routing engine, routing policies, routing strategies, reservation engine, business hours, skills, sticky-agent logic, and routing decision audit.

4. `CrestApps.OrchardCore.ContactCenter.Agents`
   - Agent profiles, presence, capacity, skill profiles, queue membership, agent reservations, and agent desktop state.

5. `CrestApps.OrchardCore.ContactCenter.Voice`
   - Voice channel adapter that integrates Contact Center with the Telephony module.
   - Owns call session mapping and Contact Center voice lifecycle projection.
   - Depends on Telephony but does not replace Telephony.
   - Exposes `IContactCenterVoiceProvider` extension points so PBX providers can implement dialer dialing, provider-side call assignment, provider queue placement, queue events, and PBX presence synchronization when supported.

6. `CrestApps.OrchardCore.ContactCenter.Dialer`
   - Outbound campaign dialing modes: manual, preview, power, progressive, and later predictive.
   - Pacing, retry, agent reservation before dialing, callback scheduling, DNC/compliance checks, and activity/campaign integration.

7. Contact Center after-call work and disposition lifecycle
   - This is not a separate aggregate or feature. It is the coordinated state of the active `Interaction`, `AgentSession`/presence, `OmnichannelActivity`, and source-neutral activity disposition path.
   - Owns wrap-up timestamps, timeout/recovery policy, required disposition validation, post-interaction completion, CRM activity updates, and capacity release without introducing `WrapUpSession`.

8. `CrestApps.OrchardCore.ContactCenter.Supervision`
   - Supervisor live monitoring, agent monitoring, queue controls, coaching/assist metadata, SLA alerts, and operational command permissions.
   - Live call-control primitives: silent monitor, whisper coaching, barge-in, and take-over, expressed as orchestration intents that Telephony/providers execute.

9. `CrestApps.OrchardCore.ContactCenter.AgentDesktop`
   - Agent desktop surfaces where agents receive activity offers, accept or reject work, see the active activity, use injected Telephony call controls, review interaction history, and complete wrap-up.
   - Queue/campaign sign-in, sign-out, and presence are contributed to the Telephony soft phone as display-driver tabs instead of a standalone navigation page.
   - Uses shapes, display drivers, placement, and SignalR snapshots/events so modules can inject provider controls, campaign panels, compliance warnings, AI assist, and supervisor coaching without replacing the desktop.

10. `CrestApps.OrchardCore.ContactCenter.EntryPoints`
   - Inbound entry points, DID/number-to-entry-point mapping, IVR/self-service decision flows, business-hours/holiday gating, queue selection, announcements, and screen-pop context.
   - Reuses subject flows and the optional Workflows bridge for decision logic instead of hardcoding IVR trees.

11. `CrestApps.OrchardCore.ContactCenter.Recording`
   - Recording orchestration: start/stop/pause/resume intents, consent capture, recording metadata, retention/disposal policy, and access auditing.
   - Stores recording metadata and references only; media capture and storage stay with Telephony/providers or a configured media store.

12. `CrestApps.OrchardCore.ContactCenter.Compliance`
   - Outbound calling windows, abandonment-rate caps, safe-harbor/abandon messaging, caller-ID/local-presence policy, list scrubbing/recycling, consent tracking, and suppression auditing.
   - Reuses existing DNC registry and contact communication preferences rather than duplicating them.

13. `CrestApps.OrchardCore.ContactCenter.Analytics`
   - Metric projections, historical reporting, SLA snapshots, campaign performance, agent performance, queue performance, adherence, and export-ready reporting data.

14. `CrestApps.OrchardCore.ContactCenter.Quality`
   - Optional quality management: evaluation forms/scorecards, recording review, calibration, and coaching records. Advanced phase.

15. `CrestApps.OrchardCore.ContactCenter.Workflows`
   - Optional OrchardCore.Workflows bridge for tenants that want workflow activities/events in addition to Subject Flows and Subject Actions.
   - Should be feature-gated because current Omnichannel Management intentionally removed its direct Workflows dependency.

16. `CrestApps.OrchardCore.ContactCenter.AI`
   - Optional AI assist, virtual agent, summarization, disposition suggestions, quality insights, and future AI routing recommendations.

17. `CrestApps.OrchardCore.ContactCenter.Deployment`
   - Recipes and deployment steps for queues, routing policies, skills, agent profiles, dialer profiles, entry points, recording/compliance policies, supervisor dashboards, and tenant defaults.

## Domain architecture overview

```text
CRM / Omnichannel Management
Contacts, Activities, Campaigns, Subjects, Dispositions, Subject Actions
        |
        | business context, activity lifecycle updates
        v
Contact Center Orchestration
Activity queues/reservations, routing, presence, dialer, wrap-up, interaction history, metrics
        |
        | call-control intents, call/session events
        v
Telephony
Soft phone, provider resolver, provider call execution, provider call state
        |
        v
Telephony Providers
Dial, answer, transfer, hold, resume, conference, hangup, provider webhooks
```

The CRM layer answers “who is the customer, what activity is the work item, what subject defines the workflow, what disposition was selected, and what subject action or automation should run.”

The Contact Center layer answers “which activity should be worked next, which queue owns it, which agent should reserve it, when should a dial occur, what communication history exists for the activity, and what real-time event should the UI see.”

The Telephony layer answers “how does the configured provider execute this call action and what is the provider’s current call state.”

## Component design

### 1. Interaction Management

| Area | Design |
| --- | --- |
| Purpose | Own durable communication history for CRM activities without becoming a second work-item model. |
| Responsibilities | Create interactions from inbound events, outbound dialer attempts, manual agent actions, callbacks, transfers, and future channels; link each interaction to exactly one CRM activity; maintain provider identifiers, participants, call legs, queue history, transfer history, timestamps, recording/transcript references, correlation ids, and technical metadata; expose activity communication history to agent and supervisor UX. |
| Data owned | Interaction, interaction participant, provider session references, communication status, call legs, queue history, transfer history, start/answer/end timestamps, recording and transcript references, technical metadata, correlation ids, tenant id, and audit metadata. |
| Events consumed | ActivityScheduled, ActivityReserved, ActivityDialingStarted, ActivityWorkStarted, InboundChannelEventReceived, DialerAttemptRequested, CallStarted, CallAnswered, CallEnded, TransferRequested, ChannelSessionEnded. |
| Events emitted | InteractionCreated, InteractionLinkedToActivity, InteractionStarted, InteractionUpdated, InteractionTransferred, InteractionEnded, InteractionFailed. |
| Interactions | Reads the CRM activity id as the work anchor; receives Telephony call session projections; notifies Real-Time UX, Analytics, Recording, Quality, and the CRM timeline. It does not own disposition, workflow, campaign, subject, priority, or business rules. |
| Why it exists | One activity can have many communication attempts (busy, no answer, connected). Interaction provides the durable communication history for those attempts while CRM Activity remains the business work item. |

### 2. Call Session Management

| Area | Design |
| --- | --- |
| Purpose | Maintain Contact Center’s voice-channel projection for active and historical calls without owning media execution. |
| Responsibilities | Map provider call identifiers to interactions; normalize call states; track voice session lifecycle; handle hold, resume, transfer, consult transfer, blind transfer, conference, and disconnect events; correlate provider sessions with CRM/contact center state; preserve provider metadata for troubleshooting. |
| Data owned | Call session, provider session id, provider name, interaction id, direction, from/to addresses, current normalized state, hold state, conference membership, transfer chain, start/answer/end timestamps, call duration, talk duration, hold duration, queue wait duration, and provider metadata. |
| Events consumed | CallDialRequested, TelephonyCallStateChanged, IncomingCallReceived, CallAnswered, CallRejected, CallHeld, CallResumed, CallTransferRequested, CallTransferred, CallMerged, CallEnded, ProviderCallFailed. |
| Events emitted | CallSessionCreated, CallStarted, CallRinging, CallAnswered, CallHeld, CallResumed, CallTransferStarted, CallTransferred, ConferenceStarted, CallEnded, CallSessionClosed. |
| Interactions | Receives call-control results and provider call events from Telephony; updates Interaction Management; informs Wrap-Up when voice work ends; feeds Analytics and Real-Time UX. |
| Why it exists | Telephony call state is provider/media truth, but Contact Center needs a business-oriented call session projection tied to interactions, queues, agents, and CRM activities. |

Call state lifecycle:

```text
Planned -> Dialing -> Ringing -> Connected -> OnHold -> Connected -> Ending -> Ended
       \             \          \                         \              \
        \             \          \                         \              Failed
         \             \          \                         Transferred
          \             NoAnswer   Rejected
           Canceled
```

### 3. Queue Management

| Area | Design |
| --- | --- |
| Purpose | Hold and prioritize work waiting for agents across inbound, outbound, callback, and future channels. |
| Responsibilities | Define queues; enqueue/dequeue interactions; maintain priorities; enforce queue eligibility; track wait time and SLA; apply overflow and escalation rules; manage queue memberships; expose live queue metrics; support reservation locks. |
| Data owned | Queue, queue type, queue membership, queue priority rules, overflow rules, SLA thresholds, queue item, queue item status, queue item age, reservation lock, and queue metric projection. |
| Events consumed | InteractionQueued, AgentPresenceChanged, AgentCapacityChanged, RoutingDecisionFailed, ReservationExpired, InteractionRequeued, QueueOverflowTriggered, BusinessHoursChanged. |
| Events emitted | QueueItemAdded, QueueItemUpdated, QueueItemReserved, QueueItemDequeued, QueueItemOverflowed, QueueSlaWarningRaised, QueueSlaBreached, QueueMetricsUpdated. |
| Interactions | Receives interactions from Interaction Management; provides eligible work to Routing Engine; uses Agent & Presence for available capacity; pushes live metrics to Real-Time UX and Supervisor layers. |
| Why it exists | Queues are the operational inventory of the contact center. They decouple work arrival from assignment and make SLA, priority, and overflow behavior explicit. |

Queue types:

- Static queues: explicitly configured work queues.
- Dynamic queues: rule-based queues resolved from subject, contact, campaign, channel, priority, geography, language, or custom metadata.
- Campaign-based queues: outbound work grouped by CRM campaign and dialer profile.
- Callback queues: scheduled customer callback work with due windows and customer preference constraints.

Agent reservation model:

- Routing creates a short-lived reservation before assignment is finalized.
- A reserved agent is temporarily removed from matching capacity.
- Reservation can be accepted, rejected, expired, canceled, or converted to assignment.
- Reservation events are durable and visible in supervisor/audit views.

### 4. Routing Engine

| Area | Design |
| --- | --- |
| Purpose | Make auditable, policy-driven decisions about which agent, queue, workflow branch, or overflow path should handle an interaction. |
| Responsibilities | Evaluate routing policies; match skills and queue membership; calculate priority; enforce business hours; apply sticky-agent preference; choose routing strategy; reserve an agent; publish decision results; explain routing outcomes for audit and supervisor troubleshooting. |
| Data owned | Routing policy, routing rule, skill requirement, strategy settings, routing decision, decision reason, routing attempt, route score, escalation path, and routing audit trail. |
| Events consumed | InteractionQueued, RoutingRequested, QueueItemAdded, AgentPresenceChanged, AgentCapacityChanged, ReservationExpired, BusinessHoursChanged, WorkflowRoutingDecisionReturned. |
| Events emitted | RoutingStarted, RoutingDecisionMade, RoutingDecisionFailed, AgentReserved, InteractionRouted, InteractionRequeued, OverflowRequested. |
| Interactions | Reads queues, agent presence, skills, capacity, CRM activity metadata, subject flow settings, and business hours; calls Workflow Integration only through a feature boundary; writes decisions back to Interaction Management and Queue Management. |
| Why it exists | Routing logic must be centralized and auditable. It cannot live in Telephony, CRM screens, or provider-specific code. |

Routing methods:

- Skills-based: match required skills and proficiency levels.
- Priority: rank by urgency, campaign priority, SLA age, customer tier, callback due time, and manual overrides.
- Sticky agent: prefer the last successful agent, account owner, or assigned CRM user when available and allowed.
- Round robin: fair distribution inside a queue or skill pool.
- Least busy: prefer agents with the lowest active capacity usage.
- Longest idle: prefer the available agent idle for the longest time.
- Business hours: route, defer, callback, overflow, or play after-hours behavior based on queue calendars.
- Workflow-based: optional workflows can return route target, priority, required skills, IVR-like branch, or callback intent.

Decision execution:

```text
InteractionQueued
  -> RoutingStarted
  -> Candidate queues and agents resolved
  -> Rules, skills, priority, business hours, and workflow decisions evaluated
  -> Best candidate selected
  -> Agent reservation created
  -> Reservation accepted or expired
  -> InteractionAssigned or InteractionRequeued
```

### 5. Agent & Presence Management

| Area | Design |
| --- | --- |
| Purpose | Track agent availability, capacity, queue membership, skills, and real-time state. |
| Responsibilities | Maintain agent profile; publish presence changes; calculate channel capacity; track current interactions; enforce wrap-up and break states; provide availability to routing; drive agent desktop state. |
| Data owned | Agent profile, queue membership, skill profile, presence state, capacity profile, active capacity usage, current reservation, current interactions, last activity timestamp, supervisor/team assignment. |
| Events consumed | AgentSignedIn, AgentSignedOut, PresenceSetRequested, InteractionAssigned, InteractionAccepted, InteractionStarted, CallAnswered, CallEnded, WrapUpStarted, WrapUpCompleted, ReservationExpired, BreakStarted, BreakEnded. |
| Events emitted | AgentPresenceChanged, AgentCapacityChanged, AgentReserved, AgentReleased, AgentStateChanged, AgentSkillUpdated, AgentQueueMembershipChanged. |
| Interactions | Provides candidate availability to Routing Engine; receives session events from Interaction and Call Session Management; pushes state to Real-Time UX and Supervisor layer. |
| Why it exists | Contact centers depend on accurate live agent state. Routing, dialer pacing, supervisor monitoring, and agent UX all require a single tenant-scoped presence model. |

Presence states:

- Offline
- Available
- Busy
- Reserved
- Ringing
- On interaction
- On hold
- Wrap-up
- Break
- Training
- Meeting
- Away
- Do not disturb
- After-hours unavailable

Capacity model:

- Voice normally consumes full voice capacity.
- Future channels can use weighted capacity, such as multiple chats or SMS threads.
- Capacity is per channel and per agent profile.
- Routing must reserve capacity before offering work.

Agent desktop and soft-phone controls:

- Agents receive Contact Center work through CRM-integrated agent surfaces rather than a standalone sign-in navigation page.
- The Telephony soft phone is the home for queue sign-in, campaign sign-in, sign-out, and presence/reason selection. Contact Center contributes sign-in controls as soft-phone display-driver tabs and presence as a soft-phone header dropdown when its features are enabled.
- Request break is system-approved, not manager-approved. If no route or reservation is in progress, the request is granted immediately as Break. If a routing decision or reservation is already in progress, the assignment continues and Break is granted automatically when the in-flight work releases.
- Future agent desktop surfaces show current reservation/offer, active activity, customer/contact summary, interaction history, wrap-up, required disposition, and supervisor/AI assistance.
- Agents can opt into queues and campaigns they are permitted to handle. Managers configure routing skills, queue membership, campaign assignment, dialing mode, priority, and capacity rules. Agents must not self-select routing skills from the soft phone because skills are supervisor/admin-owned eligibility data.
- Inbound queues, callback queues, preview dial queues, power/progressive/predictive campaigns, and future channels all deliver activity offers through the same agent-offer model.
- The agent desktop and soft phone are shape-driven: Telephony owns the soft phone and call controls, Contact Center injects sign-in, reservation, wrap-up, and presence tabs or panels, providers inject provider-specific call state, and optional modules inject AI assist, compliance, or supervisor coaching.

### 6. Campaign Dialer

| Area | Design |
| --- | --- |
| Purpose | Orchestrate outbound work from CRM activities while respecting agent capacity, pacing, retries, compliance, and customer preferences. |
| Responsibilities | Select eligible unassigned or available activities; reserve activities and agents before dialing when required; assign owner only after reservation/acceptance or answer-prediction rules allow it; choose dialing mode; enforce pacing; apply retry rules; schedule callbacks; update CRM activity assignment status; create interaction communication-history records for each attempt; request Telephony dial actions through the voice adapter. |
| Data owned | Dialer profile, dialing mode, dialer run, dialer attempt, pacing settings, retry policy, callback policy, compliance checks, suppression results, campaign queue state, and dialer metrics. |
| Events consumed | DialerRunStarted, AgentCapacityChanged, QueueMetricsUpdated, ActivityEligibleForDialing, DialerAttemptCompleted, CallAnswered, CallEnded, DispositionSelected, CallbackScheduled. |
| Events emitted | DialerAttemptScheduled, AgentReservedForDial, DialerAttemptStarted, OutboundDialRequested, DialerAttemptConnected, DialerAttemptFailed, DialerAttemptNoAnswer, DialerAttemptCompleted, DialerRunPaused, DialerRunCompleted. |
| Interactions | Reads CRM activities first, then resolves contact, subject flow, endpoint, DNC/compliance flags, and time zone from that activity context; creates outbound interaction history records per attempt; reserves agents through Routing; asks Telephony through Contact Center Voice to dial. Dialers never operate directly on contacts. |
| Why it exists | Outbound dialing is orchestration-heavy and should not be embedded in CRM activity screens or Telephony providers. |

Dialing modes:

- Manual: agent chooses and places the call from CRM UI.
- Preview: agent reviews contact/activity first, then accepts or skips before dialing.
- Power: system reserves agents and dials a controlled number of calls per available agent.
- Progressive: system automatically dials when an agent becomes available, one call per reserved agent.
- Predictive: future advanced mode that forecasts answer rates and agent availability to dial ahead safely.

MVP dialer modes:

- Manual and preview first.
- Power dialer second.
- Progressive after stable routing and reservations.
- Predictive only after reliable historical metrics and abandonment controls exist.

### 7. Disposition & After-Call Work Management

| Area | Design |
| --- | --- |
| Purpose | Govern post-communication activity completion, required outcomes, after-call-work timing, notes, CRM activity updates, and follow-up automation. |
| Responsibilities | Move the interaction and agent session into after-call work when communication ends; enforce required activity disposition rules by queue/subject/campaign; track wrap-up duration; save notes and outcome; update CRM activity through `IActivityDispositionService`; trigger subject actions and optional workflows; recover abandoned wrap-up safely; release agent capacity when completion or timeout policy reaches a terminal result. |
| Data owned | Wrap-up timestamps on the Interaction, after-call-work state on the AgentSession/presence projection, required disposition policy, disposition source, notes, completion state, timeout/recovery policy, and validation results. No separate `WrapUpSession` aggregate is used. |
| Events consumed | CallEnded, ChannelSessionEnded, InteractionWorkCompleted, DispositionSelected, WrapUpTimerExpired, AgentSubmittedWrapUp. |
| Events emitted | WrapUpStarted, DispositionRequired, DispositionSelected, WrapUpCompleted, ActivityCompleted, SubjectActionsRequested, PostInteractionWorkflowRequested, AgentReleased. |
| Interactions | Updates CRM Activity and Subject data; executes existing Subject Actions; optionally emits OrchardCore workflow events; informs Agent Presence and Analytics. It never dispositions an Interaction. |
| Why it exists | Contact center work is not complete when communication ends. After-call work ensures business outcomes are captured consistently through the Activity and agents are released at the correct time without duplicating the work item or introducing a redundant aggregate. |

Activity disposition service:

- `IActivityDispositionService` is the only path for agent, provider, AI, workflow, dialer, and system outcomes to modify activity disposition or completion state.
- It validates the disposition against the activity's Subject and configured Subject Actions.
- It updates `OmnichannelActivity`, records audit metadata, publishes Contact Center/CRM domain events, and triggers existing Subject Actions or optional Workflow bridge behavior.
- Telephony, provider adapters, soft-phone UI, and dialers must not update `OmnichannelActivity.DispositionId`, completion fields, or workflow outcomes directly.
- Workflow execution remains source-agnostic: workflows see the final Activity + Disposition, not whether the outcome came from an agent, provider, AI, or system process.

### 8. Event-Driven Architecture

| Area | Design |
| --- | --- |
| Purpose | Decouple Contact Center components while preserving an auditable lifecycle history. |
| Responsibilities | Define domain events; publish events after state transitions; maintain tenant-local event log; support durable outbox processing; create read models/projections; stream selected events to SignalR; provide correlation and replay for debugging. |
| Data owned | Domain event envelope, event payload, event version, aggregate id, interaction id, tenant id, correlation id, causation id, actor, source component, timestamp, dispatch status, and projection checkpoints. |
| Events consumed | All Contact Center domain events and selected Telephony/Omnichannel events. |
| Events emitted | ProjectionUpdated, RealTimeNotificationRequested, EventDispatchFailed, EventDispatchRetried. |
| Interactions | Used by every component as the communication mechanism; avoids direct coupling between routing, queues, presence, dialer, wrap-up, analytics, and UX. |
| Why it exists | Contact center lifecycle is multi-step, asynchronous, and auditable. Events make that lifecycle reliable and observable. |

Core domain events:

- InteractionCreated
- InteractionLinkedToActivity
- InteractionQueued
- InteractionRouted
- InteractionAssigned
- InteractionAccepted
- InteractionRejected
- InteractionRequeued
- InteractionStarted
- InteractionTransferred
- InteractionCompleted
- InteractionAbandoned
- QueueItemAdded
- QueueItemReserved
- QueueItemDequeued
- QueueMetricsUpdated
- RoutingStarted
- RoutingDecisionMade
- RoutingDecisionFailed
- AgentPresenceChanged
- AgentCapacityChanged
- AgentReserved
- AgentReleased
- CallSessionCreated
- CallStarted
- CallRinging
- CallAnswered
- CallHeld
- CallResumed
- CallTransferRequested
- CallTransferred
- ConferenceStarted
- CallEnded
- DialerRunStarted
- DialerAttemptScheduled
- DialerAttemptStarted
- DialerAttemptCompleted
- CallbackScheduled
- WrapUpStarted
- DispositionRequired
- DispositionSelected
- WrapUpCompleted
- ActivityCompleted
- WorkflowRequested
- WorkflowCompleted
- SlaWarningRaised
- SlaBreached

Event envelope requirements:

- Tenant id
- Event id
- Event type
- Schema version
- Aggregate type and id
- Interaction id when available
- Correlation id
- Causation id
- Actor user id or system actor
- Source component
- UTC timestamp
- Idempotency key for external/provider-originated events

### 9. Provider Abstraction Boundary

| Area | Design |
| --- | --- |
| Purpose | Preserve a clean separation between Contact Center business orchestration and Telephony provider/media execution. |
| Responsibilities | Define which layer owns routing, call actions, call state, provider metadata, business activity state, and provider-side contact center capabilities. |
| Data owned | Boundary contracts, channel adapter mappings, provider call references, and normalized channel session metadata. |
| Events consumed | RoutingDecisionMade, OutboundDialRequested, AgentAcceptedInteraction, TelephonyCallStateChanged, ProviderWebhookReceived. |
| Events emitted | CallControlRequested, CallControlAccepted, CallControlFailed, ChannelSessionEventReceived, ProviderEventNormalized. |
| Interactions | Contact Center Voice talks to Telephony through provider-agnostic Telephony services for soft-phone/media control. PBX providers that support provider-side dialer or queue behavior can also implement `IContactCenterVoiceProvider`; Contact Center still owns the routing/campaign/reservation decision and sends provider-neutral intents to the provider adapter. Telephony providers never read Contact Center queues or routing policies directly. |
| Why it exists | This prevents business logic from leaking into providers and prevents Telephony from becoming an orchestration engine. |

Ownership rules:

| Responsibility | Owner |
| --- | --- |
| Routing decisions | Contact Center |
| Queue selection | Contact Center |
| Agent reservation | Contact Center |
| Agent presence/capacity | Contact Center |
| CRM activity lifecycle | CRM plus Contact Center orchestration |
| Call-control request intent | Contact Center or agent UI |
| Provider call execution | Telephony |
| Provider dialer dial execution | PBX provider through `IContactCenterVoiceProvider` when supported, otherwise Telephony dial |
| Provider-side call assignment | PBX provider through `IContactCenterVoiceProvider`; Contact Center owns the assignment decision |
| Provider-side queue placement | PBX provider through `IContactCenterVoiceProvider`; Contact Center owns the queue decision |
| Provider authentication | Telephony |
| Provider/media state truth | Telephony/provider |
| CRM activity/work state truth | CRM plus Contact Center orchestration |
| Call session business projection | Contact Center Voice |
| Persistent call history currently used by soft phone | Telephony |
| Enterprise interaction history and analytics | Contact Center |

Provider extension contract:

- `IContactCenterVoiceProvider` is optional and complements `ITelephonyProvider`; it does not replace soft-phone call control.
- Providers implement it when they can dial on behalf of a dialer, assign provider calls to agents, place/move calls in provider-side queues, publish queue events, or synchronize PBX presence.
- Contact Center remains the brain: it chooses the activity, queue, agent, campaign, dialer mode, and compliance gates, then asks the provider to execute a specific operation.

### 10. Workflow Integration

| Area | Design |
| --- | --- |
| Purpose | Allow tenant-specific business logic to participate in routing and lifecycle automation without hardcoding customer behavior into Contact Center services. |
| Responsibilities | Integrate with current Subject Flows and Subject Actions first; add optional OrchardCore.Workflows bridge for advanced tenants; support pre-call routing decisions, IVR-like branching, post-call automation, callback scheduling, and disposition-driven activity creation. |
| Data owned | Workflow trigger definitions, workflow invocation records, workflow outputs, routing workflow results, callback workflow results, and failure/audit records. |
| Events consumed | InteractionCreated, InboundInteractionReceived, RoutingStarted, CallEnded, WrapUpCompleted, DispositionSelected, CallbackRequested. |
| Events emitted | WorkflowRequested, WorkflowCompleted, WorkflowFailed, RoutingAttributesUpdated, CallbackScheduled, SubjectActionExecutionRequested. |
| Interactions | Reads and updates CRM context; returns routing attributes to Routing Engine; triggers Subject Actions through the existing Omnichannel model; emits workflow events for optional workflows. |
| Why it exists | Enterprise contact center behavior varies by tenant, queue, subject, campaign, compliance rules, and customer lifecycle. Workflow integration keeps orchestration extensible. |

Workflow strategy:

- MVP: use existing Subject Flows and Subject Actions as the primary CRM workflow mechanism.
- Near-term: add Contact Center lifecycle events that can execute Subject Actions at wrap-up and activity completion.
- Future optional feature: add an OrchardCore.Workflows bridge that depends on Workflows only when enabled.
- Do not reintroduce Workflows as a hard dependency of Omnichannel Management.

Workflow use cases:

- Pre-call routing: enrich interaction with priority, skills, queue, sticky agent, or suppression reason.
- IVR-like decisions: classify inbound voice intent, language, department, or callback preference before routing.
- Post-call automation: create follow-up activity, update contact fields, notify internal users, schedule callback, or mark DNC preference.
- Callback scheduling: convert missed/abandoned/ineligible work into scheduled callback queue items.

### 11. Real-Time UX Model

| Area | Design |
| --- | --- |
| Purpose | Deliver low-latency operational state to agents, supervisors, and queue monitors. |
| Responsibilities | Stream domain event projections to SignalR groups; isolate tenant/user/queue/team streams; provide current snapshots on reconnect; support event ordering and idempotent UI updates. |
| Data owned | Real-time subscription model, client group mapping, last-seen event cursor, UI projection payloads, and reconnect snapshot metadata. |
| Events consumed | InteractionUpdated, QueueMetricsUpdated, AgentPresenceChanged, AgentReserved, CallSessionUpdated, WrapUpStarted, WrapUpCompleted, SlaBreached, DialerMetricsUpdated. |
| Events emitted | AgentDesktopUpdated, SupervisorDashboardUpdated, QueueMonitorUpdated, ToastNotificationRequested, ClientSnapshotAvailable. |
| Interactions | Receives projection events from the event dispatcher; pushes to CRM agent desktop, supervisor dashboards, and queue monitor widgets; does not own domain state. |
| Why it exists | Agent and supervisor UX must reflect current contact center state without polling and without coupling screens to domain services. |

Real-time streams:

- Agent stream: current reservation, active interaction, call session state, wrap-up timer, next activity, disposition requirements, errors.
- Supervisor stream: agent states, active interactions, queue depths, SLA warnings, dialer state, campaign progress.
- Queue monitor stream: queue depth, oldest item age, average wait, answer rate, abandoned count, staffed agents, available agents.
- Interaction stream: full lifecycle updates for an interaction detail screen.

SignalR grouping model:

- Per tenant shell boundary.
- Per user for agent desktop.
- Per queue for queue monitors.
- Per supervisor team for supervisor dashboards.
- Per interaction for detail views.

UI extensibility requirements:

- Use Orchard Core Display Management only: shapes, display drivers, placement, templates, and shape alternates.
- CRUD screens for queues, routing policies, dialer profiles, agent profiles, entry points, and other Contact Center settings should follow the AI Profile UI pattern: controller loads a catalog entry through a manager, builds list rows with `IDisplayManager<T>.BuildDisplayAsync(..., "SummaryAdmin")`, builds editors with `BuildEditorAsync`/`UpdateEditorAsync`, and leaves sections extensible through display drivers.
- Agent desktop and supervisor surfaces should be shape composition points, not custom rendering frameworks. Telephony can inject call controls, Contact Center can inject presence/reservation/wrap-up shapes, providers can inject provider-specific settings, and supervision/analytics modules can inject live queue metrics through placement.
- Contact Center should extend existing Omnichannel activity shapes instead of replacing them. Activity list/detail/complete screens should expose display-driver groups and placement zones for reservation state, next-work actions, interaction history, dialer controls, wrap-up, and supervisor decorations.
- New entity models that need extensibility should inherit from Orchard-compatible `Entity` infrastructure (for catalog entries this is provided through `CatalogItem`) so modules can use `entity.Put(...)` and `entity.TryGet(...)` metadata the same way AI profiles and Orchard users do.

### 12. Supervisor & Analytics Layer

| Area | Design |
| --- | --- |
| Purpose | Provide live operational oversight and historical performance reporting. |
| Responsibilities | Build live dashboards; aggregate metrics; alert on SLA risk; expose agent monitoring; track campaign performance; provide drill-downs into interactions, queues, and outcomes. |
| Data owned | Live queue metrics, agent state metrics, SLA snapshots, call metrics, interaction metrics, campaign metrics, dialer metrics, supervisor audit records, and historical metric projections. |
| Events consumed | InteractionCreated, InteractionQueued, InteractionAssigned, InteractionCompleted, CallAnswered, CallEnded, AgentPresenceChanged, WrapUpCompleted, DispositionSelected, QueueMetricsUpdated, DialerAttemptCompleted, SlaBreached. |
| Events emitted | SupervisorAlertRaised, MetricProjectionUpdated, SlaTrendUpdated, CampaignPerformanceUpdated. |
| Interactions | Reads event log/projections; pushes real-time updates through SignalR; supports export/reporting surfaces and future BI integration. |
| Why it exists | Supervisors need operational control and accountability. Analytics also feeds future dialer pacing, staffing, routing optimization, and AI insights. |

Monitoring capabilities:

- Live queue depth, oldest wait, average wait, service level, abandon rate, answer rate.
- Agent live state, active interaction, idle time, handle time, wrap-up time, occupancy, capacity utilization.
- SLA warning and breach tracking by queue, campaign, priority, and subject.
- Call metrics: talk time, hold time, queue time, ring time, transfer count, conference count, completion outcome.
- Campaign performance: attempts, connects, no answers, retries, callbacks, conversion outcomes, disposition mix.
- Dialer safety: pacing, abandonment, no-agent events, retry exhaustion, compliance suppression.

### 13. Security & Multi-Tenancy

| Area | Design |
| --- | --- |
| Purpose | Ensure tenant isolation, role-based access, queue-level authorization, and auditability. |
| Responsibilities | Scope data by tenant; define permissions; protect real-time groups; enforce queue membership and supervisor permissions; audit sensitive actions; isolate provider metadata; validate channel actions. |
| Data owned | Permission definitions, role stereotypes, queue access policies, supervisor scope, audit event records, and security projection metadata. |
| Events consumed | UserRoleChanged, QueueMembershipChanged, AgentProfileUpdated, SupervisorActionRequested, InteractionAccessRequested, ProviderActionRequested. |
| Events emitted | AccessDenied, AuditRecorded, SupervisorActionAudited, SecurityPolicyChanged. |
| Interactions | Works with Orchard Core roles, users, authorization, tenants, content item access, Contact Center queues, and CRM activity permissions. |
| Why it exists | Contact center data contains customer information, agent performance data, provider metadata, and operational controls. Security must be designed into every boundary. |

Permission model:

- Contact Center Agent: use agent desktop, update own presence, accept/reject own reservations, complete assigned interactions.
- Contact Center Supervisor: monitor assigned queues/teams, view live dashboards, assist/reassign/override within scope.
- Queue Manager: manage queues, queue membership, skills, priorities, overflow, and SLA settings.
- Dialer Manager: manage dialer profiles, start/pause campaigns, view dialer metrics, configure retry/pacing.
- Contact Center Administrator: manage all Contact Center settings and permissions.
- Auditor/Analyst: view historical interaction and metric reports without operational controls.

Tenant isolation:

- Tenant-local stores and indexes.
- Tenant-local event log and projections.
- Tenant-scoped SignalR hub routing.
- Tenant-scoped settings, features, recipes, and permissions.
- No cross-tenant queue, event, provider, or interaction access.

### 14. Inbound Entry Points, IVR & Self-Service

| Area | Design |
| --- | --- |
| Purpose | Define how inbound contacts enter the contact center, get qualified, and reach the right queue or self-service path before an agent is involved. |
| Responsibilities | Map provider numbers/DIDs to entry points; run business-hours and holiday gating; run IVR-like decision flows; collect caller intent, language, and identifiers; resolve the contact for screen pop; select the target queue, skills, and priority; offer callback or voicemail when appropriate. |
| Data owned | Entry point, number/DID mapping, IVR/self-service flow definition, prompt/announcement metadata, collected caller attributes, entry-point routing result, and self-service outcome. |
| Events consumed | InboundChannelEventReceived, ProviderInboundCallReceived, BusinessHoursChanged, WorkflowRoutingDecisionReturned, SelfServiceCompleted. |
| Events emitted | EntryPointMatched, SelfServiceStarted, SelfServiceCompleted, CallerIdentified, ScreenPopRequested, InteractionQualified, InteractionQueued, CallbackOffered, VoicemailRequested. |
| Interactions | Receives normalized inbound events from Contact Center Voice; resolves CRM contact; optionally calls subject flows or the Workflows bridge for IVR-like branching; hands the qualified interaction to Queue Management and Routing. |
| Why it exists | Inbound contact centers need a configurable front door that performs qualification, business-hours/holiday handling, screen pop, and self-service before consuming agent capacity. |

Inbound entry behaviors:

- Number/DID to entry-point mapping per tenant.
- Business-hours and holiday calendars with open, closed, and special-day behavior.
- IVR-like menu, intent capture, language selection, and authentication hooks.
- Screen pop: resolve the contact and surface the 360 view to the agent on answer.
- Self-service deflection, callback offer, and voicemail fallback.

### 15. Call Recording & Compliance Recording

| Area | Design |
| --- | --- |
| Purpose | Orchestrate recording lifecycle and recording governance without owning media capture. |
| Responsibilities | Emit start/stop/pause/resume recording intents; capture consent state; enforce pause/resume around sensitive data (for example payment capture); track recording metadata and references; apply retention and disposal policy; audit recording access and playback. |
| Data owned | Recording session, recording reference/location pointer, consent state, recording state (recording, paused, stopped), retention policy result, and recording access audit. |
| Events consumed | CallStarted, CallAnswered, SensitiveDataEntryStarted, SensitiveDataEntryEnded, CallEnded, ConsentCaptured, RetentionPolicyChanged. |
| Events emitted | RecordingStarted, RecordingPaused, RecordingResumed, RecordingStopped, RecordingStored, RecordingRetentionScheduled, RecordingPurged, RecordingAccessAudited. |
| Interactions | Sends recording intents through Contact Center Voice to Telephony/providers; links recording metadata to the interaction and call session; feeds Quality and Analytics; enforces retention with Data Governance. |
| Why it exists | Recording, consent, pause/resume for sensitive data, retention, and access auditing are core enterprise contact center requirements that must be orchestrated and audited even though media stays in the provider/media layer. |

### 16. Live Monitoring & Supervisor Call Control

| Area | Design |
| --- | --- |
| Purpose | Let supervisors observe and intervene on live interactions within their scope. |
| Responsibilities | Provide silent monitor (listen only), whisper (coach the agent only), barge-in (join the call), and take-over (replace the agent); enforce supervisor scope and permissions; audit every monitoring action; surface live coaching context. |
| Data owned | Monitor session, monitor mode, supervisor/agent/interaction references, monitor start/end, and monitoring audit record. |
| Events consumed | SupervisorMonitorRequested, SupervisorWhisperRequested, SupervisorBargeRequested, SupervisorTakeOverRequested, CallEnded, AgentStateChanged. |
| Events emitted | MonitorSessionStarted, WhisperStarted, BargeStarted, TakeOverStarted, MonitorSessionEnded, SupervisorActionAudited. |
| Interactions | Expresses monitor/whisper/barge/take-over as orchestration intents that Telephony/providers execute when the provider supports them; updates Real-Time UX and audit. |
| Why it exists | Live monitoring and intervention are standard supervisor capabilities for coaching, quality, and escalation, and they must be permissioned and fully audited. |

Provider capability awareness:

- Monitoring primitives depend on provider capabilities, similar to the existing Telephony capability flags.
- When a provider cannot support a primitive, the UI must hide or disable it instead of failing silently.

### 17. Outbound Compliance & List Management

| Area | Design |
| --- | --- |
| Purpose | Keep outbound dialing within legal, contractual, and customer-preference constraints. |
| Responsibilities | Enforce allowed calling windows by contact time zone and region; cap abandonment rate for power/predictive modes; play safe-harbor/abandon messaging when no agent connects; manage caller-ID and local-presence policy; scrub against DNC and communication preferences; manage retry limits, cool-down, and list recycling; record suppression reasons. |
| Data owned | Compliance policy, calling-window rules, abandonment thresholds, caller-ID/local-presence policy, suppression result, list recycling state, and compliance audit record. |
| Events consumed | ActivityEligibleForDialing, DialerAttemptCompleted, CallAbandoned, RetentionPolicyChanged, CommunicationPreferenceChanged. |
| Events emitted | DialBlockedByCompliance, CallingWindowViolationPrevented, AbandonmentThresholdReached, SafeHarborMessagePlayed, SuppressionRecorded, ListRecycled. |
| Interactions | Gates the Campaign Dialer before dialing; reuses DNC registry and contact communication preferences; feeds compliance metrics to Analytics. |
| Why it exists | Power and predictive dialing are unsafe without enforced calling windows, abandonment caps, and suppression auditing. Compliance must be a first-class gate, not an afterthought. |

### 18. Quality Management (advanced)

| Area | Design |
| --- | --- |
| Purpose | Evaluate and improve interaction quality. |
| Responsibilities | Define evaluation forms/scorecards; sample and assign interactions for review; link recordings, transcripts, and disposition; capture scores, calibration, and coaching; feed performance and training. |
| Data owned | Evaluation form, evaluation assignment, evaluation result, calibration session, and coaching record. |
| Events consumed | InteractionCompleted, RecordingStored, WrapUpCompleted, DispositionSelected. |
| Events emitted | EvaluationAssigned, EvaluationCompleted, CoachingRecorded, CalibrationCompleted. |
| Interactions | Reads recordings/transcripts/dispositions; feeds Analytics and agent performance; integrates with optional AI quality scoring. |
| Why it exists | Quality management is a standard enterprise contact center pillar and a natural consumer of recordings, transcripts, and dispositions once the core platform is stable. |

## Conceptual data model

The data model below is conceptual only.

| Entity | Purpose | Key relationships |
| --- | --- | --- |
| OmnichannelActivity | Universal CRM work item | Links to Contact, Campaign, Subject, Disposition, assignment/reservation metadata, ActivityKind, ActivitySource, and zero or more Interactions |
| ContactCenterWorkState | Versioned volatile orchestration state for one activity; target replacement for mutable queue/reservation/assignment fields embedded in the CRM activity | Keyed by OmnichannelActivity id; links to current QueueItem, ActivityReservation, assigned AgentSession, active Interaction, and compare-and-set version |
| Interaction | Communication-history record for one activity attempt | Links to OmnichannelActivity, provider session/call id, queue history, transfer history, call legs, recording/transcript references |
| InteractionParticipant | Customer, agent, supervisor, AI agent, external party | Belongs to Interaction |
| InteractionEvent | Durable domain event history | References Interaction and event envelope |
| ChannelSession | Generic per-channel session projection | Voice session now; chat/SMS/email later |
| CallSession | Voice-specific channel session | References Interaction, Telephony provider call id, agent, queue |
| Queue | Work container | Owns queue items, membership, SLA, routing policy |
| QueueItem | Enqueued activity waiting for assignment | References OmnichannelActivity and Queue |
| QueueMembership | Agent-to-queue relationship | References Queue and AgentProfile |
| AgentProfile | Contact center agent configuration | References Orchard user, skills, capacity, teams |
| AgentPresence | Live agent state | References AgentProfile and active reservations/activities/interactions |
| AgentSkill | Agent skill and proficiency | References AgentProfile and Skill |
| Skill | Routeable capability | Referenced by queues, policies, agents |
| AgentReservation | Temporary assignment lock | References AgentProfile, OmnichannelActivity, QueueItem |
| RoutingPolicy | Rules and strategy for routing | References Queue, Skills, BusinessHours, Workflow hooks |
| RoutingDecision | Auditable result of routing | References OmnichannelActivity, Queue, Agent, score, reason |
| DialerProfile | Outbound dialing configuration | References Campaign, Queue, pacing and retry policy |
| DialerRun | Execution instance for outbound campaign work | References DialerProfile and campaign/activity set |
| DialerAttempt | Single outbound attempt | References DialerRun, Interaction, Activity, CallSession |
| After-call work state | Post-communication completion state, represented by Interaction wrap-up timestamps plus AgentSession/presence | References OmnichannelActivity, latest Interaction, Agent, required disposition and timeout/recovery policy; it is not a separate aggregate |
| ActivityDispositionRequest | Source-neutral disposition command | References OmnichannelActivity, Disposition, source, actor, notes |
| MetricSnapshot | Aggregated operational analytics | References queue, agent, campaign, time bucket |
| EntryPoint | Inbound front door configuration | References number/DID mapping, IVR flow, business hours, target queues |
| NumberMapping | Provider number/DID to entry point | References EntryPoint and channel endpoint |
| IvrFlowDefinition | Self-service/IVR decision flow | Referenced by EntryPoint; may delegate to subject flows or workflows |
| BusinessHoursCalendar | Open/closed and holiday schedule | Referenced by queues, entry points, routing |
| CallbackRequest | Scheduled or queued callback | References Interaction, Contact, Queue, due window |
| RecordingSession | Recording lifecycle and reference | References Interaction, CallSession, consent, retention |
| MonitorSession | Supervisor live monitoring action | References Supervisor, Agent, Interaction, monitor mode |
| AgentStateReason | Reason code for presence/not-ready/break | References AgentProfile and presence state |
| CompliancePolicy | Outbound calling constraints | References calling windows, abandonment caps, caller-ID policy |
| RetentionPolicy | Data retention/disposal rules | References recordings, events, interactions, PII fields |
| EvaluationForm | Quality scorecard definition | Referenced by evaluation assignments and results |
| AuditRecord | Security and operational audit | References actor, action, target, tenant, correlation id |

Relationship overview:

```text
Contact ContentItem
  -> OmnichannelActivity
       -> ContactCenterWorkState / QueueItem / AgentReservation
       -> Activity Disposition -> Subject Actions / Workflow bridge
       -> Interaction*
            -> CallSession / Provider session
            -> QueueHistory / TransferHistory / CallLegs
            -> Recording / Transcript reference
            -> InteractionEvent*
```

## Interaction lifecycle diagram

```text
Created
  -> LinkedToActivity
  -> Queued
  -> Routing
  -> Reserved
  -> Assigned
  -> Offered
  -> Accepted
  -> Active
  -> WorkCompleted
  -> WrapUp
  -> Completed

Alternative paths:
Queued -> Overflowed -> Queued
Queued -> Abandoned
Reserved -> ReservationExpired -> Queued
Offered -> Rejected -> Queued
Active -> Transferred -> Routing
Active -> Failed -> WrapUp or Completed
WorkCompleted -> CallbackScheduled -> Queued
```

## Inbound call routing sequence

```text
1. Telephony/provider receives inbound voice event.
2. Contact Center Voice normalizes the provider event into an inbound channel session event.
3. Interaction Management creates or finds the Interaction.
4. CRM context is resolved: contact, campaign, subject, previous owner, communication preferences.
5. Pre-routing workflow or subject-flow rules enrich priority, skills, queue, language, and callback options.
6. Queue Management enqueues the interaction.
7. Routing Engine evaluates business hours, queue rules, skills, priority, sticky agent, capacity, and strategy.
8. Agent & Presence creates a reservation for the selected agent.
9. Real-Time UX offers the interaction to the agent desktop.
10. Agent accepts, and Contact Center asks Telephony to answer/connect/bridge according to the provider capability.
11. Call Session Management tracks voice lifecycle events.
12. When the call ends, the Interaction and AgentSession enter after-call work and record wrap-up start.
13. Agent selects disposition and completes required fields.
14. CRM activity is updated and Subject Actions or optional workflows run.
15. Analytics projections and supervisor dashboards update.
```

## Outbound dialing sequence

```text
1. CRM campaign/inventory load produces eligible phone activities.
2. Campaign Dialer selects eligible activities using schedule, timezone, DNC/compliance, retry, and priority rules.
3. Dialer mode determines whether an agent previews first or the system reserves an agent before dialing.
4. Routing Engine reserves an eligible agent and capacity.
5. Interaction Management creates the outbound Interaction and links it to the CRM Activity.
6. Contact Center Voice requests Telephony to dial using the configured channel endpoint/caller id.
7. Telephony executes the provider dial action and returns provider call state.
8. Call Session Management maps provider call id to Interaction.
9. Agent desktop receives real-time dial/call state updates.
10. Outcome is classified: connected, no answer, busy, failed, canceled, voicemail, callback, or completed.
11. Agent-handled calls use normal wrap-up/disposition selection; unattended/system-ended calls use subject-level system outcome mappings to choose the disposition or retry/callback path.
12. CRM Activity, retry policy, callback schedule, subject actions, and campaign metrics update.
13. Agent capacity is released or the next dialer reservation begins.
```

## Event flow architecture

```text
Domain command
  -> Domain service validates and changes aggregate state
  -> Domain event appended to tenant event log
  -> Outbox dispatches event to handlers
  -> Handlers update projections and trigger next orchestration step
  -> Real-time notifier streams projection event to SignalR groups
  -> Analytics projector updates live and historical metrics
```

Rules:

- Components do not mutate each other’s owned data directly.
- Every cross-component transition is represented by an event.
- Every event is tenant-scoped, versioned, correlated, and idempotent.
- External/provider events are normalized before they enter the Contact Center event stream.
- Real-time notifications are projections of domain events, not the source of truth.
- Analytics are projections and can be rebuilt from event history.

## Standard contact center terminology and metrics

Use these industry-standard terms for type, member, event, metric, and permission naming. Do not name any code artifact, comment, or public doc after a competing product; use only the generic vocabulary below.

Core domain vocabulary:

- Interaction: any customer engagement across any channel.
- Channel session: a per-channel session (voice call, chat, SMS thread, email) attached to an interaction.
- Queue: a container of interactions waiting for handling.
- Reservation (offer): a short-lived hold that offers an interaction to an agent before assignment.
- Presence (agent state): the agent's current availability state.
- Capacity: how much concurrent work an agent can handle, per channel.
- Disposition (wrap code / outcome code): the business outcome selected at completion.
- Wrap-up (after-call work, ACW): post-interaction completion time.
- Entry point: the inbound front door that qualifies and routes a contact.
- Skill: a routeable capability with optional proficiency.

Operational metrics (use these names):

- Service Level (SL): percent of interactions answered within a target threshold (for example, 80/20).
- Average Speed of Answer (ASA): average queue wait before answer.
- Average Handle Time (AHT): talk time plus hold time plus wrap-up time.
- Average Talk Time and Average Hold Time.
- Abandon Rate: percent of queued interactions abandoned before answer.
- Occupancy: percent of logged-in time spent handling interactions.
- Adherence and Shrinkage: schedule adherence and non-productive time (workforce metrics).
- First Contact Resolution (FCR): resolved on the first interaction.
- Answer Rate, Connect Rate, and Right-Party-Contact (RPC) rate (outbound).
- Abandonment Rate cap (outbound dialing safety threshold).
- Queue depth, oldest-in-queue age, and estimated wait time (EWT).

Agent states (canonical set):

- Offline, Available, Reserved, Ringing, On interaction, On hold, Wrap-up, Not ready (with reason code), Break (with reason code), Training, Meeting, Away, Do not disturb, After-hours unavailable.

Transfer and conference taxonomy:

- Blind (cold) transfer, Consultative (warm) transfer, Transfer to agent, Transfer to queue, Transfer to external number, Transfer to IVR/entry point.
- Conference: add party, drop party, supervisor-initiated conference (barge).

## MVP scope

The MVP should prove that agents can operate voice contact center work fully inside CRM while preserving the Telephony boundary.

MVP includes:

1. Extended `OmnichannelActivity` as the universal work item with nullable owner, activity kind/source, assignment status, and reservation metadata.
2. Durable Interaction entity as communication history linked to OmnichannelActivity, with provider ids, timestamps, queue/transfer/call-leg history, recording/transcript references, and technical metadata.
3. Contact Center event log and baseline projections.
4. Static queues over activities with priority, SLA thresholds, and queue metrics.
5. Agent profiles, queue membership, presence, and single-voice-session capacity.
6. Activity and agent reservation model.
7. Basic routing: queue membership, available agents, priority, longest idle, round robin, business hours, and sticky assigned user preference.
8. Voice channel adapter over existing Telephony abstractions.
9. Call session mapping from Telephony call id to Interaction.
10. Manual and preview outbound dialing that selects Activities, not Contacts.
11. Power dialing after reservations are stable.
12. After-call-work timing and recovery, required activity disposition, notes, CRM activity completion, Subject Action execution through `IActivityDispositionService`, and deterministic capacity release without a separate `WrapUpSession`.
13. Agent desktop CRM integration for next work, current activity, interaction history, call controls, and wrap-up.
14. Supervisor live queue and agent monitor.
15. Tenant-scoped permissions and audit trail.
16. Documentation and persistent project plan reference in `.github\copilot-instructions.md`.
17. A basic inbound entry point: number/DID to queue mapping, business-hours/holiday gating, and screen pop of the resolved contact on answer.
18. Canonical agent state set with not-ready and break reason codes.

MVP excludes (deferred to later phases, not dropped):

- Call recording, pause/resume, and media storage (Phase 9).
- Live monitoring, whisper, barge, and take-over (Phase 9).
- Full IVR/self-service designer; MVP ships only basic entry-point gating (Phase 8).
- Outbound compliance hardening: calling-window enforcement, abandonment caps, safe-harbor messaging, AMD, local presence (Phase 10).
- Progressive and predictive dialing (Phase 13).
- Quality management, evaluation forms, and speech/AI quality scoring (Phase 13).
- Chat/SMS/email agent routing and AI virtual-agent handoff (Phase 13).
- Workforce management, forecasting, and adherence scheduling.
- External BI connectors and data warehouse export beyond built-in reports.
- Multi-region active-active contact center scaling (single-region scale-out is Phase 12).

## Phased delivery plan

### Phase 0: Project governance and durable planning

Goals:

- Create a repository-tracked Contact Center design and progress document.
- Add a reference to that document in `.github\copilot-instructions.md`.
- Establish naming policy that avoids competitor names in code and public docs.
- Document current boundaries: CRM owns business work data, Contact Center owns orchestration, Telephony owns media execution.
- Create initial docs page under `src\CrestApps.Docs\docs\contact-center`.

Deliverables:

- Durable project plan/progress document.
- Copilot instruction pointer.
- Contact Center docs landing page.
- Initial feature/module map.

### Phase 1: Domain foundation

Goals:

- Introduce Contact Center abstractions and core domain vocabulary.
- Establish `OmnichannelActivity` as the universal work item and Interaction as communication history.
- Define event envelope, event log, and event projection strategy.
- Extend existing Omnichannel activities with nullable ownership, classification, assignment, and reservation metadata.
- Define baseline permissions and tenant settings.

Deliverables:

- Contact Center base feature.
- Conceptual migrations/indexes for interaction history and event history.
- Communication-history projection.
- Activity extension model for assignment/reservation/classification.
- Initial `IActivityDispositionService` contract for source-neutral activity disposition.
- Baseline docs and changelog updates.
- Unit tests for lifecycle rules and event envelopes.

### Phase 2: Agent, presence, queue, and reservation foundation

Goals:

- Add agent profiles, queue membership, skills, presence, and capacity.
- Add static queues, queue priorities, SLA thresholds, and queue item lifecycle.
- Add reservation model and expiration behavior.
- Add real-time presence and queue updates.

Deliverables:

- Agent management feature.
- Queue management feature.
- Reservation lifecycle.
- Queue metric projections.
- Agent and supervisor permissions.
- Tests for presence transitions, reservation conflicts, queue ordering, and tenant isolation.

### Phase 3: Routing MVP

Goals:

- Add routing policies and strategy pipeline.
- Support longest idle, round robin, priority, sticky assigned user, business hours, and skills.
- Produce auditable routing decisions.
- Requeue or overflow when no agent is available.

Deliverables:

- Routing engine feature.
- Routing decision records.
- Routing audit view.
- Queue overflow model.
- Tests for routing strategies, tie breaking, business hours, and reservation failures.

### Phase 4: Voice integration with Telephony

Goals:

- Add Contact Center Voice feature that depends on Telephony.
- Map provider call/session identifiers to Contact Center interactions.
- Normalize call session events into Contact Center domain events.
- Define a provider inbound-event normalization boundary so inbound calls and provider transports (webhook, WebSocket, or both) enter Contact Center reliably.
- Keep Telephony as the execution and provider state boundary.
- Surface current interaction and call state in CRM UI.

Deliverables:

- Voice channel adapter.
- Call session model.
- Inbound voice event ingress and normalization strategy.
- Outbound call-control integration.
- Transfer and conference taxonomy (blind, consultative, to agent/queue/external).
- Real-time voice session projection.
- Tests for call mapping, state transitions, transfer, hold/resume, conference, and end-call wrap-up start.

### Phase 5: Outbound dialer MVP

Goals:

- Use CRM campaigns, activities, contacts, subject flows, endpoints, and dispositions as dialer inputs.
- Add manual and preview dialing.
- Add power dialing after reservations are stable.
- Add retry rules, callback scheduling, timezone checks, DNC/communication-preference suppression, and pacing safeguards.

Deliverables:

- Dialer profiles as execution policies over CRM campaign/activity inventory, not as a replacement for campaigns, subjects, inventory loads, or activity configuration.
- Dialer run and attempt projections.
- Preview dialing agent UX.
- Power dialing service.
- Callback request model and callback queue.
- Campaign and activity updates.
- Tests for eligibility, suppression, retries, pacing, reservation-before-dial, and callback scheduling.

Note: full outbound compliance (calling-window enforcement, abandonment caps, safe-harbor messaging, answering-machine detection, and caller-ID/local presence) is hardened in Phase 10. Phase 5 only enforces the existing DNC and communication-preference suppression and basic timezone checks.

### Phase 6: Wrap-up and disposition lifecycle

Goals:

- Start wrap-up after interaction work ends.
- Enforce required disposition rules by queue, subject, and campaign.
- Track wrap-up time, notes, selected disposition, and completion.
- Execute existing Subject Actions and optionally emit workflow events.
- Recover browser/session loss during after-call work and release capacity only after a deterministic completion or timeout policy.

Deliverables:

- Interaction/AgentSession after-call-work lifecycle; no separate `WrapUpSession` aggregate or hard feature boundary.
- Required disposition policies.
- Activity completion integration.
- Subject Action integration.
- Timeout, recovery, and abandoned-session behavior.
- Tests for required dispositions, wrap-up release, activity update, and subject action execution.

### Phase 7: Agent desktop and supervisor real-time UX

Goals:

- Add CRM-integrated agent desktop surfaces for available work, active interaction, call controls, customer context, subject form, and wrap-up.
- Add screen pop of the resolved contact 360 view on interaction answer.
- Add canonical agent states with not-ready and break reason codes.
- Add supervisor live dashboard for queues, agents, SLA, active interactions, and dialer state.
- Add queue monitor (wallboard) view with estimated wait time.

Deliverables:

- Agent desktop feature.
- Screen pop integration.
- Agent state and reason-code model.
- Supervisor feature.
- Queue monitor feature.
- SignalR stream contracts and reconnect snapshots.
- UI docs and permissions.
- Playwright coverage for core agent and supervisor flows.

### Phase 8: Inbound entry points, IVR and self-service

Goals:

- Add inbound entry points and number/DID-to-entry-point mapping.
- Add business-hours and holiday calendars with open, closed, and special-day behavior.
- Add IVR-like decision flows (menu, intent, language, authentication hooks) that can delegate to subject flows or the Workflows bridge.
- Add self-service deflection, callback offer, and voicemail fallback.
- Promote the basic MVP entry point into a configurable front door.

Deliverables:

- Entry point and number-mapping model.
- Business-hours/holiday calendar model.
- IVR/self-service flow definition and runtime.
- Callback offer and voicemail fallback.
- Tests for entry-point matching, business-hours/holiday gating, self-service outcomes, and queue selection.

### Phase 9: Recording and live monitoring

Goals:

- Add recording orchestration: start/stop/pause/resume intents, consent capture, and recording metadata.
- Add pause/resume around sensitive data entry (for example payment capture).
- Add live monitoring: silent monitor, whisper, barge-in, and take-over, gated by provider capabilities and supervisor scope.
- Audit all recording access and monitoring actions.

Deliverables:

- Recording session model and recording intents.
- Consent and recording-state tracking.
- Monitor session model and supervisor call-control intents.
- Provider-capability gating for recording and monitoring.
- Recording and monitoring audit records.
- Tests for recording lifecycle, pause/resume, monitor modes, scope enforcement, and audit.

### Phase 10: Outbound compliance hardening

Goals:

- Enforce allowed calling windows by contact time zone and region.
- Cap abandonment rate for power and predictive dialing and play safe-harbor/abandon messaging.
- Add answering-machine detection handling and outcome classification.
- Add caller-ID and local-presence policy.
- Add list scrubbing, retry cool-down, and list recycling with suppression auditing.

Deliverables:

- Compliance policy model and calling-window rules.
- Abandonment-rate caps and safe-harbor messaging.
- Answering-machine detection outcome handling.
- Caller-ID/local-presence policy.
- List recycling and suppression auditing.
- Tests for calling-window enforcement, abandonment caps, AMD outcomes, caller-ID selection, and suppression auditing.

### Phase 11: Optional Workflow bridge

Goals:

- Add feature-gated OrchardCore.Workflows integration without reintroducing Workflows as a hard dependency of Omnichannel Management.
- Emit workflow events for routing, IVR-like decisions, wrap-up completion, callback scheduling, and SLA breach.
- Allow workflows to return route attributes, priority, skills, callback time, or suppression result.

Deliverables:

- Contact Center Workflows feature.
- Workflow events and tasks.
- Workflow result model.
- Failure and timeout behavior.
- Tests for workflow result handling and fallback routing.

### Phase 12: Analytics and operations

Goals:

- Build historical metric projections.
- Add reports for queue, agent, interaction, call, wrap-up, campaign, and dialer performance.
- Add SLA trend analysis and operational alerts.
- Add export surfaces.

Deliverables:

- Analytics feature.
- Metric snapshots.
- Supervisor reporting.
- Campaign performance reports.
- SLA alerts.
- Tests for projection correctness and rebuild behavior.

### Phase 13: Scale-out, resilience and data governance

Goals:

- Validate multi-node operation: SignalR backplane, distributed reservation locking, and single-writer guarantees for queue/presence transitions.
- Add provider/media resilience and failover behavior and stale-reservation/stale-session cleanup.
- Add data retention and disposal for recordings, events, and interaction history.
- Add PII handling, redaction, and right-to-erasure support aligned with existing platform conventions.

Deliverables:

- Scale-out validation and backplane configuration guidance.
- Distributed locking and reconnection/cleanup behavior.
- Retention and disposal jobs for recordings, events, and interactions.
- PII redaction and erasure support.
- Load/soak test guidance and tests for cleanup, retention, and failover.

### Phase 14: Advanced capabilities

Goals:

- Add progressive dialing.
- Add predictive dialing only after enough safe metrics and abandonment controls exist.
- Add future non-voice channel routing.
- Add AI assist, summarization, next-best-action, sentiment, disposition suggestion, and virtual agent handoff.
- Add provider-neutral bidirectional media sessions for AI voice, with capability advertisement and provider-specific media transports.
- Add advanced supervisor controls and quality workflows.

Deliverables:

- Progressive dialer.
- Predictive dialer safety design.
- Chat/SMS/email routing adapters.
- AI assistance feature.
- Bidirectional voice-media provider contract and validated reference-provider adapter.
- Advanced analytics and quality insights.

## Cross-cutting architecture requirements

### Persistence

- Contact Center state should follow existing tenant-local Orchard/YesSql patterns.
- Interaction event history should be durable and replayable for projections.
- Operational live state should be reconstructable from durable events and current snapshots.
- Analytics projections should be rebuildable.

### Idempotency

- Provider events, dialer attempts, reservations, workflow results, and real-time reconnect snapshots must be idempotent.
- Events should carry idempotency keys when sourced from external systems.

### Concurrency

- Agent reservations are the concurrency boundary for assignment.
- Dialer attempts must not dial without valid capacity/reservation when a mode requires agent availability.
- Queue dequeue and reservation conversion must be atomic at the domain level.

### Observability

- Every routing decision should explain the selected queue, agent, strategy, score, and skipped candidates.
- Every dialer attempt should explain eligibility, suppression, pacing, and final outcome.
- Every workflow or subject action execution should be auditable.
- Every provider event should retain enough sanitized metadata for troubleshooting without exposing secrets.

### Compliance

- Reuse existing contact communication preferences and DNC integration.
- Respect contact time zone for outbound calls.
- Provide suppression reasons for audit.
- Keep sensitive provider and customer data out of logs.

### Documentation

- Every phase that changes behavior must update `src\CrestApps.Docs`.
- The changelog file matching `VersionPrefix` must be updated.
- The persistent Contact Center project plan/progress document must be updated as work progresses.

### Scale-out and high availability

- Contact Center is real-time and stateful, so it must work across multiple application nodes.
- SignalR requires a backplane (for example Redis) for multi-node real-time delivery; reconnect snapshots must rebuild client state.
- Reservation, queue dequeue, and presence transitions need distributed locking or a single-writer strategy so two nodes cannot assign the same work.
- Live operational state must be reconstructable from durable events plus current snapshots after a node restart.

### Resilience and failover

- Provider/media outages must degrade gracefully: stop dialing, hold queued work, and surface status instead of losing interactions.
- Stale reservations, orphaned call sessions, and disconnected agents must be detected and cleaned up automatically.
- Inbound provider events must be idempotent and survive retries and out-of-order delivery.
- Dialer pacing must back off automatically when provider errors or abandonment thresholds rise.

### Data retention and privacy

- Recordings, transcripts, events, and interaction history must honor configurable retention and disposal policies.
- PII must be redactable, and right-to-erasure must be supported in line with existing platform conventions.
- Provider metadata and customer data must be kept out of logs, and recording/playback access must be audited.

### Testing and validation strategy

- Unit tests for lifecycle rules, routing strategies, reservation concurrency, dialer eligibility/suppression, compliance windows, and wrap-up enforcement.
- Integration tests for end-to-end inbound and outbound flows against the in-memory/stub Telephony provider used by existing Telephony tests.
- Playwright tests for agent desktop and supervisor flows, following the existing `CrestApps.OrchardCore.Telephony.PlaywrightTests` pattern.
- Projection rebuild tests proving analytics and live state can be reconstructed from the event log.
- Load/soak tests for queue throughput, reservation contention, and real-time fan-out before enabling power/predictive dialing.
- Follow repository validation: `npm run rebuild` for assets, `dotnet build` with warnings-as-errors, and targeted `dotnet test`.

### Migration strategy

- Breaking changes are acceptable, so `OmnichannelActivity` is expanded rather than replaced.
- Chosen direction: `OmnichannelActivity` remains the universal work item. `Interaction` links to an activity and stores communication history only; it does not wrap, replace, or disposition the activity.
- Provide data migrations that add activity classification and assignment/reservation columns, then backfill sensible defaults for existing activities.
- Keep the existing automated SMS activity processing working, or migrate it to create interaction history deliberately, not accidentally.
- The existing Telephony `TelephonyInteraction` history stays for the soft phone; Contact Center adds its own interaction history rather than repurposing it.

## Extensibility strategy

### Future channels

Voice is only the first channel. The domain model should support:

- Chat sessions.
- SMS threads.
- Email conversations.
- AI agent sessions.
- Co-browsing or screen-share metadata.
- Future custom channels.

The extension point is a channel adapter that can:

- Create or attach a channel session to an interaction.
- Normalize channel events into Contact Center domain events.
- Execute allowed channel actions through the owning channel/provider module.
- Provide channel-specific capacity rules.
- Provide channel-specific SLA and wrap-up behavior.

### AI agents

AI should be modeled as a participant or assistant, not as a replacement for the Interaction:

- AI pre-routing classification.
- AI agent self-service before human routing.
- AI summarization after interaction.
- AI disposition and next-action suggestions.
- AI quality and compliance signals.
- Human takeover and AI-to-agent handoff.

### Routing strategies

Routing strategies should be plug-in based:

- Built-in strategies: priority, longest idle, least busy, round robin, sticky agent, business hours, skills.
- Optional strategies: workflow-directed, AI-assisted, account-owner, geographic, language, customer tier.
- Custom strategies should return explainable scores and reasons.

### Dialer strategies

Dialer modes should be strategy-based:

- Manual.
- Preview.
- Power.
- Progressive.
- Predictive.

All dialer strategies must share compliance checks, retry policies, callbacks, pacing safeguards, reservations, and event output.

## Known design risks and decisions to validate

1. Current Omnichannel Management removed OrchardCore.Workflows. The plan keeps Subject Flows and Subject Actions as the default workflow model and adds Workflows as an optional Contact Center feature.
2. Existing `OmnichannelActivity` statuses are not rich enough for Contact Center. Breaking changes are acceptable, so the activity lifecycle can be expanded or bridged with Interaction state.
3. Telephony currently records soft-phone call history and pushes current-user call state. Contact Center needs a separate business interaction history and may need Telephony/provider event ingress improvements for inbound routing.
4. The Telephony abstraction may need a provider event normalization boundary so inbound calls and provider transports such as webhooks or WebSockets can enter Contact Center reliably.
5. Contact Center real-time events should not reuse TelephonyHub for routing or supervisor data. A separate Contact Center real-time stream is needed.
6. Dialer pacing and predictive dialing require reliable historical metrics before advanced modes are safe.
7. Queue and presence accuracy depend on robust disconnect/reconnect handling and stale reservation cleanup.
8. A persistent repo-tracked plan is needed because the session plan file is not enough for long-running multi-session work.
9. Recording, pause/resume, and live monitoring (whisper/barge/take-over) depend on provider capabilities; capability flags and graceful degradation are required, mirroring the existing Telephony capability model.
10. Outbound power and predictive dialing are legally sensitive; calling windows, abandonment-rate caps, and safe-harbor messaging must gate dialing before those modes are enabled.
11. Real-time, stateful operation requires a SignalR backplane and distributed reservation locking for multi-node deployments; single-node assumptions must not leak into the design.
12. Recording and PII introduce consent, retention, and right-to-erasure obligations that must be designed in from the recording phase, not added later.
13. Inbound routing depends on reliable provider inbound events/webhooks; the Telephony provider boundary may need a normalization/ingress improvement to support entry points and screen pop.

## Project todos

1. Establish durable project planning and instructions references.
2. Create Contact Center documentation landing page and architecture overview.
3. Extend `OmnichannelActivity` as the universal work item and introduce Interaction as communication history.
4. Add domain event log and projection model.
5. Integrate Contact Center orchestration with existing Omnichannel activities, contacts, campaigns, subjects, dispositions, and subject actions.
6. Add agent profile, presence, capacity, queue membership, and skills concepts.
7. Add queue management, queue item lifecycle, priorities, SLA thresholds, overflow, and metrics.
8. Add reservation-based routing with initial routing strategies.
9. Add Contact Center Voice adapter and call session mapping over Telephony.
10. Add inbound voice routing flow.
11. Add outbound manual and preview dialing.
12. Add power dialer and campaign pacing safeguards.
13. Add wrap-up and required disposition enforcement.
14. Add real-time agent desktop, supervisor dashboard, and queue monitor streams.
15. Add security, roles, queue permissions, and audit logging.
16. Add inbound entry points, business-hours/holiday calendars, IVR/self-service, screen pop, and callback/voicemail fallback.
17. Add recording orchestration and live monitoring (silent monitor, whisper, barge, take-over) with consent and audit.
18. Add outbound compliance hardening: calling windows, abandonment caps, safe-harbor messaging, AMD, caller-ID/local presence, and list recycling.
19. Add optional OrchardCore.Workflows bridge.
20. Add analytics, reports, and metric projections.
21. Add scale-out/high-availability validation, resilience/failover, and data retention/privacy.
22. Add quality management, future channel adapters, and AI assistance after the voice MVP is stable.

## Design review: closing the gap to a state-of-the-art dialer

> **Historical and superseded by the 2026-07-13 independent production-readiness review.** Retained to explain prior implementation decisions; do not action this section when it conflicts with the target domain model, R0-R9 remediation program, or production release gates below.
>
> Added 2026-06-30 after a review of the then-shipped Contact Center code against industry-standard cloud contact center and dialer capabilities.

### Verdict

The layering is correct: CRM/Omnichannel Management is the work source, Contact Center is the orchestration layer, Telephony is the media/provider execution layer. The domain skeleton (activity extension, interaction history, queues, reservations, presence, routing strategies, durable event log, voice/dialer provider seams, disposition unification) is a sound MVP foundation.

It is **not yet a working dialer**. The step that actually connects a customer to the selected agent (media delivery/bridging) is missing on both inbound and outbound, and the safety, real-time UX, capacity, concurrency, and operational layers that define an enterprise dialer are not yet built. The work below is grouped by severity and mapped to the existing phase plan.

### P0 — Correctness blockers (the dialer cannot operate safely without these)

1. **Media is never delivered to the selected agent (inbound and outbound).** This is the single most important gap.
   - Inbound: `VoiceContactCenterCallRouter.RouteInboundAsync` creates the activity/interaction, enqueues, reserves an agent, and offers a ringing modal; `VoiceController.AcceptOffer` flips the interaction to `Connected` — but nothing tells the provider to bridge the live customer call to the chosen agent's device/extension. The customer is never connected to that specific agent.
   - Outbound: `DialerService.TryDialAsync` asks the provider to dial the customer but never bridges the reserved agent onto the answered call.
   - Refs: `src/Modules/CrestApps.OrchardCore.ContactCenter/Services/VoiceContactCenterCallRouter.cs`, `src/Modules/CrestApps.OrchardCore.ContactCenter/Controllers/VoiceController.cs`, `src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/DialerService.cs`.
   - Fix: add an explicit call-delivery contract on the voice provider (see provider redesign below) supporting both delivery models, plus a Contact Center call-command service that performs reserve → accept → connect/bridge → track as one orchestrated, audited transition.

2. **"Answer" is two uncoordinated, best-effort client actions.** The soft-phone `answerIncoming()` fires a fire-and-forget POST to the Contact Center accept URL (errors swallowed by `.catch(() => {})`) and then separately invokes the Telephony `Answer` hub method for media. If the accept POST fails, the media still answers, the reservation later expires, and the same live call is re-offered to another agent while this agent is already talking.
   - Refs: `src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone.js` (`answerIncoming`, `postLifecycle`), `VoiceController.cs`.
   - Fix: a single agent "Accept" must drive one server-side command that accepts the reservation, connects the media, and updates interaction/activity/events atomically; decline/timeout releases the reservation and re-offers.

3. **The voice-provider abstraction is too thin and conflates "can dial" with "is a full ACD provider."** `IContactCenterVoiceProvider` exposes only `DialAsync`/`AssignCallAsync`/`QueueCallAsync`; the sole implementation (`DialPadContactCenterVoiceProvider`) advertises just `DialerDial` and returns `not_supported` for assignment/queue. There is no normalized provider call-event contract, no contact-center-level answer/hangup/hold/transfer/conference, no recording/monitoring intents, and no declaration of how the provider delivers media to agents.
   - Fix: redesign the provider boundary (see below).

4. **Unsafe/unsupported dialer modes are exposed.** `DialerMode` advertises Progressive and Predictive; `DialerService` runs everything except Manual/Preview, and Progressive/Predictive currently fall into a one-call-per-cycle path with no answer-rate forecasting, abandonment caps, AMD, or pacing feedback.
   - Refs: `src/Abstractions/CrestApps.OrchardCore.ContactCenter.Abstractions/Models/DialerMode.cs`, `DialerService.cs`.
   - Fix: make each mode a gated `IDialerStrategy`; hide unsupported modes in the editor and reject them server-side; hard-block Predictive (and cap Power) until compliance + reliable historical metrics exist.

5. **Outbound compliance is configured but not enforced.** `DialerProfile.RespectDoNotCall`, `RetryDelayMinutes`, and the do-not-call/communication-preference and contact-time-zone rules are persisted and shown in the editor, but `DialerService` only checks `MaxAttempts`/`PreferredDestination`. DNC, communication preferences, calling windows, retry cool-down, and suppression auditing are not applied.
   - Refs: `src/Core/CrestApps.OrchardCore.ContactCenter.Core/Models/DialerProfile.cs`, `DialerService.cs`.
   - Fix: an `IDialerEligibilityService` / compliance gate that runs before every attempt and records an auditable suppression reason.

6. **Agent capacity is ignored.** `AgentProfile.MaxConcurrentInteractions` exists but `AgentProfileStore.ListAvailableForQueueAsync` selects agents purely on `PresenceStatus == Available`; routing never counts active interactions, so an agent can be offered new work while already busy and weighted multi-channel capacity cannot work later.
   - Refs: `src/Core/CrestApps.OrchardCore.ContactCenter.Core/Models/AgentProfile.cs`, `src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/AgentProfileStore.cs`, `ActivityAssignmentService.cs`.
   - Fix: track active capacity usage on a live agent-session record and enforce it during candidate selection.

7. **Assignment and dequeue are not concurrency-safe for multi-node.** Only agent sign-in takes a distributed lock; `ActivityAssignmentService.AssignNextAsync` does read-top-item → select-agent → reserve as separate steps with no lock or compare-and-set, so two nodes (or the reservation-expiry task plus an inbound call) can double-assign the same item or agent.
   - Refs: `ActivityAssignmentService.cs`, `src/Modules/CrestApps.OrchardCore.ContactCenter/BackgroundTasks/ReservationExpiryBackgroundTask.cs`.
   - Fix: per-queue (or per-item + per-agent) distributed lock or single-writer assignment, plus optimistic concurrency on reservation creation.

8. **The existing activity-completion path is incompatible with the Contact Center lifecycle.** `ActivitiesController.CompleteAsync` only proceeds when `activity.Status == ActivityStatus.NotStated`, but Contact Center sets `AwaitingAgentResponse`, `Dialing`, `InProgress`, `Failed`, etc., so contact-center work cannot be completed through the current CRM screen.
   - Refs: `src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Controllers/ActivitiesController.cs`.
   - Fix: drive completion through a Contact Center wrap-up service that accepts contact-center states and routes to the source-neutral `IActivityDispositionService`.

### P1 — Enterprise gaps (required to be "state of the art")

| # | Gap | Refs | Recommendation |
| --- | --- | --- | --- |
| 9 | No standalone **CallSession** aggregate / provider-event normalization; provider call state, legs, hold/transfer/conference, and talk/hold/wait durations are not projected. | no `CallSession` type exists; `Interaction.cs` | Add `CallSession` + normalized `ProviderVoiceEvent` ingestion with duration metrics and leg/transfer chains. |
| 10 | No **wrap-up** lifecycle: no timer, required-disposition policy, auto-close, structured notes, or capacity release after wrap-up. | `DefaultActivityDispositionService.cs` | Superseded: persist after-call deadline/recovery on Interaction/`ContactCenterWorkState`, enforce per-queue/subject/campaign required-disposition rules, and release capacity on completion/timeout without adding `WrapUpSession`. |
| 11 | **Routing** is required-skills + longest-idle over a single queue. No routing policy, overflow, business-hours/holiday gating, sticky agent, skill proficiency, language, customer tier, SLA aging, or bullseye/skill-relaxation expansion. | `ActivityRoutingService.cs`, `RequiredSkillsRoutingStrategy.cs`, `LongestIdleRoutingStrategy.cs` | Add `RoutingPolicy`, `QueueMembership`, proficiency levels, business-hours calendars, overflow targets, and sticky-agent + priority/SLA strategies. |
| 12 | **Inbound front door** maps one DID → one queue (or a single unmapped fallback). No IVR/self-service, business-hours/holiday gating, menu/intent/language capture, callback offer, voicemail fallback, estimated wait/position, or ambiguous-contact handling (router blindly takes `contactIds[0]`). | `VoiceContactCenterCallRouter.cs`, `src/Modules/CrestApps.OrchardCore.ContactCenter/Services/InboundContactLookup.cs` | Build Entry Points: number mapping, hours/holidays, IVR flow, callback/voicemail, pre-routing enrichment, screen-pop on connect, and a multi-match disambiguation rule. |
| 13 | **Agent live state** is mixed into `AgentProfile` (admin-owned skills/queues + volatile presence/reservation), and there is no heartbeat, so a closed browser leaves an agent `Available` and offers route to a dead client. | `AgentProfile.cs`, `AgentPresenceManagerService.cs` | Split admin `AgentProfile` from a live `AgentSession` with SignalR connection heartbeat, stale-session cleanup, and capacity counters. |
| 14 | **Offer latency**: queue reservation timeout defaults to 30s but `ReservationExpiryBackgroundTask` runs on a one-minute cron, so a missed offer can block re-offer for up to a minute. | `ActivityQueue.cs`, `ReservationExpiryBackgroundTask.cs` | Drive the offer timeout from the real-time layer (per-reservation timer) and use the background task only as a safety net. |
| 15 | **No real-time agent desktop**; agent UX is soft-phone sign-in + an incoming modal. | `Startup.cs`, `ContactCenterSoftPhoneWidgetDisplayDriver.cs` | Build a CRM-integrated agent desktop (offer → accept+connect → customer 360 → script/subject → call controls → wrap-up) with reconnect snapshots. |
| 16 | **No supervisor/ops surfaces**: no live queue/agent/dialer dashboards, SLA alerts, wallboards, campaign pause/resume, agent-state control, or monitor/whisper/barge/takeover. | — | Add SignalR-backed supervisor dashboards + scoped, audited live call-control intents gated by provider capability. |
| 17 | **Eventing is not an outbox.** `DefaultContactCenterEventPublisher` persists the event then runs handlers inline; failures are logged but never retried, and there is no projection checkpoint or replay. | `DefaultContactCenterEventPublisher.cs` | Add outbox dispatch state + retry/backoff, projection checkpoints, mandatory idempotency keys on provider-sourced events, and rebuildable projections. |
| 18 | **Provider ingress is not provider-grade.** `VoiceIngressController` is `[Authorize]` + `ManageInteractions`, so it only accepts pre-normalized internal posts, not signed provider webhooks. | `VoiceIngressController.cs` | Add per-provider webhook adapters that validate provider signatures, dedupe, and normalize to `ProviderVoiceEvent` before entering the pipeline. |

### P2 — Important but deferrable

- Analytics/reporting projections (queue, agent, campaign, interaction, wrap-up, dialer) with rebuild + export.
- Recording orchestration (start/stop/pause-resume, consent, retention, access audit) and quality management (scorecards, calibration, AI scoring).
- Callback model (`CallbackRequest`) and callback queues.
- Multi-channel routing (chat/SMS/email/AI) over the same interaction/queue/capacity model.
- AI assist (pre-routing classification, summarization, disposition suggestion, sentiment, virtual agent + handoff).

### Recommended voice-provider boundary redesign

Replace the thin `IContactCenterVoiceProvider` with a capability-described boundary that (a) declares **how the provider delivers media to an agent** and (b) exposes the full contact-center call lifecycle, while Telephony keeps soft-phone/media execution and provider authentication.

- **Delivery model capability (critical).** Providers fall into two families and the orchestration must branch on this:
  - `AgentDeviceNative` (soft-phone/WebRTC, e.g. DialPad): the customer call already rings the agent's registered client; Contact Center reserves/offers/tracks and tells the provider which agent/extension to ring, but does not bridge media itself.
  - `ServerSideAcd` (PBX/CCaaS queue): the provider parks/queues the live call and Contact Center issues `AssignCall`/`Bridge` to connect it to the selected agent.
- **Lifecycle operations (capability-gated, provider-neutral intents):** `Dial`, `Bridge/Connect`, `Answer`, `Hangup`, `Hold`/`Resume`, `Transfer` (blind/consultative, to agent/queue/external), `Conference`, `SendDigits`, `Park`, `Recording` (start/stop/pause/resume), `Monitor`/`Whisper`/`Barge`/`TakeOver`. Every advertised capability must have an executable provider contract; a flag without an invokable operation is invalid.
- **Inbound + state events:** a normalized `ProviderVoiceEvent` (provider event id, idempotency key, call id, leg id, normalized state, from/to, queue/agent hints, timestamps, sanitized raw metadata) delivered through an `IProviderVoiceEventHandler`, replacing the current assumption that inbound always arrives as a fully-formed `InboundVoiceEvent` posted to an authorized endpoint.
- **Capability contracts** must cover the above so the agent desktop and supervisor UI hide unsupported actions exactly like `TelephonyCapabilities` does today. Prefer small executable interfaces per capability family over one ever-growing provider interface.

### Execution order (maps onto existing phases)

1. **Voice foundation hardening (completes Phase 4):** provider boundary redesign + delivery models, `CallSession` aggregate, `ProviderVoiceEvent` normalization, and the unified Contact Center call-command service that delivers media to the agent on inbound accept and outbound answer. *(P0 #1, #2, #3; P1 #9)*
2. **Assignment safety (hardens Phases 2/3):** distributed-lock/single-writer assignment, capacity enforcement, live `AgentSession` + heartbeat + stale cleanup, real-time offer timeout. *(P0 #6, #7; P1 #13, #14)*
3. **After-call work + completion unification (completes Phase 6):** Interaction/AgentSession after-call-work state, required-disposition policies, timeout/recovery, capacity release, and the contact-center-aware completion path through `IActivityDispositionService`; no separate `WrapUpSession` aggregate. *(P0 #8; P1 #10)*
4. **Dialer safety (completes Phase 5, pulls forward Phase 10 essentials):** strategy-per-mode, eligibility/compliance gate (DNC, preferences, calling windows, retry cool-down, suppression audit), cap Power, block Predictive. *(P0 #4, #5)*
5. **Agent desktop + supervisor real-time UX (Phase 7):** CRM-integrated cockpit, supervisor dashboards, queue monitor, live call-control intents. *(P1 #15, #16)*
6. **Eventing/outbox + provider webhooks (hardens Phase 1, extends Phase 4):** outbox, projections, idempotency, signed webhook adapters. *(P1 #17, #18)*
7. **Inbound entry points/IVR (Phase 8), recording/monitoring (Phase 9), compliance hardening (Phase 10), and analytics (Phase 12)** proceed per the existing phase plan once the above is stable.

## Independent production-readiness review (2026-07-13)

> This review supersedes any older phase or gap item that claims completion while conflicting evidence below remains open. A historical checkmark means an implementation increment shipped; it does not mean the capability is approved for commercial production.

### Board verdict

**Commercial production release is rejected until every P0 gate is closed by automated evidence.** The codebase has a valuable architectural foundation, a clean warnings-as-errors build, substantial unit coverage, and a real provider/eventing implementation. It is not yet safe to claim multi-tenant isolation, multi-node correctness, regulated automated dialing, production observability, executable recording/monitoring, or enterprise scale.

The fundamental ownership model remains approved:

- Omnichannel/CRM owns the business work item, contact, campaign, subject flow, disposition, and business actions.
- Contact Center owns queueing, routing, reservations, agent orchestration, after-call work, interaction history, provider-command coordination, and operational projections.
- Telephony/providers own call/media execution, provider authentication, and provider state truth.
- `OmnichannelActivity` remains the universal business work item. A separate Contact Center work item must not be introduced.

The implementation strategy is revised in four important ways:

1. Canonical mutable state plus database compare-and-set is the correctness authority. Distributed locks are an optimization and contention-control mechanism, never the sole invariant.
2. The event log remains an audit/integration stream with transactional outbox/inbox semantics. Do not claim event sourcing until every projection has versioned replay, checkpoints, deterministic rebuild, and operational tooling.
3. Volatile orchestration state should migrate out of the large CRM activity document into a versioned `ContactCenterWorkState` keyed by activity id, while stable business classification and final disposition remain on `OmnichannelActivity`. The migration must name one authority per field and phase; it must not rely on unfenced dual writes.
4. Advanced capabilities must be unavailable unless a provider registers an executable capability contract. A capability flag and a success-shaped domain event are not proof that media work occurred.

### Validation baseline and review limits

| Evidence | Result | Interpretation |
| --- | --- | --- |
| Strict Release build | 84 projects, 0 warnings, 0 errors with warnings-as-errors and analyzers | Compiler and analyzer hygiene is strong; runtime composition and distributed correctness remain unproven. |
| Unit suite | 1,439 passed, 0 failed, 0 skipped | Good regression foundation, but most tests are in-process and do not prove tenant shells, databases, multiple nodes, or real providers. |
| Telephony Playwright suite | 24 passed, 0 failed | Validates the isolated soft-phone asset against a custom test host; it does not boot an Orchard tenant or exercise Contact Center features, permissions, queues, routing, reconnect, or supervisor flows. |
| Feature activation testing | No complete shell/tenant matrix exists | A feature can compile while failing as soon as a tenant enables a legal manifest combination. |
| Distributed/load/chaos testing | No repeatable Contact Center harness exists | No multi-node, network-partition, provider-outage, Redis-outage, rolling-upgrade, or scale claim is approved. |
| Azure-specific review | Tool-limited by explicit user decision because the required Azure best-practices tool was unavailable | Azure topology, identity, networking, service limits, and managed-service recommendations require a separate tool-backed validation before an Azure reference architecture is published. |

### P0 commercial release blockers

| Gate | Finding and evidence | Required production outcome |
| --- | --- | --- |
| PR0 — Tenant isolation | `ContactCenterHub.SupervisorsGroup` is the global name `cc:supervisors`, and `ContactCenterRealTimeNotifier` broadcasts to it and targets `Clients.User(userId)` without a tenant component (`Hubs/ContactCenterHub.cs:30,94-99`; `Services/ContactCenterRealTimeNotifier.cs:32-86`). These identities can collide when tenant shells share a hub lifetime manager or production backplane. | Centralize tenant-qualified group names and tenant-qualified user identity using an immutable shell key, such as a tenant-aware `IUserIdProvider` or tenant-qualified user groups. The isolation proof must run with the production backplane enabled because in-memory shell separation can mask cross-node collisions. |
| PR1 — Queue/campaign authorization | Hub membership commands accept arbitrary queue and campaign ids and persist them without manager-owned entitlement checks (`Hubs/ContactCenterHub.cs:220-244`; `AgentPresenceManagerService.cs:55-107,124-160`). Routing trusts those memberships. | Separate administrative entitlements from session opt-in. Authorize every requested membership and group subscription server-side; an agent can never self-enroll in restricted work. |
| PR2 — Feature composition | The base feature depends on `CrestApps.OrchardCore.Omnichannel.Managements`; `QueuesStartup` registers a Telephony soft-phone driver that requires `HubRouteManager`, but Queues declares neither Telephony nor SignalR/RealTime; Voice depends on `Telephony.SoftPhone`, coupling server orchestration to UI (`ContactCenter/Manifest.cs`; `ContactCenter/Startup.cs:210-262,337-420`; `ContactCenterSoftPhoneWidgetDisplayDriver.cs:24-80`). | Make the base headless, split server voice from soft-phone integration, and make every startup registration belong to a manifest feature whose declared transitive dependencies provide every required service. |
| PR3 — Agent availability and after-call recovery | Disconnect marks the `AgentSession` offline, stale cleanup scans online sessions, and routing selects `AgentProfile` by Available presence without requiring a live session (`ContactCenterHub.cs:113-126`; `AgentSessionStore.cs:35-40`; `AgentProfileStore.cs:40-45`). A crash during after-call work can also leave a nonterminal interaction consuming capacity indefinitely. | Route from canonical eligibility that combines manager-owned `QueueMembership` with `AgentAvailability` (approved liveness contributor + valid presence + capacity) and session opt-in. Persist `WrapUpDeadlineUtc` and required-disposition state on `ContactCenterWorkState` or Interaction, and sweep it server-side so capacity release never depends on a connected client. |
| PR4 — Atomic assignment and dialing | Reservation/enqueue paths are read-then-write without database uniqueness/CAS, and `DialerAttemptService` can dial before reservation acceptance; failed acceptance has no provider hangup compensation (`ActivityReservationService.cs:79-156`; `DialerAttemptService.cs:93-118,145-170`). | Add row versions/conditional writes and unique active queue-item/reservation constraints. Persist the command intent and accepted reservation before provider execution, use idempotent command ids, and compensate partial provider success. |
| PR5 — Atomic provider ingest and deploy-safe outbox | Provider-event deduplication is check-then-insert and CallSession lacks a unique provider-call identity (`ProviderVoiceEventService.cs:125-147,221-257`; `CallSessionIndexMigrations.cs:32-47`). Provider aliases have already required canonicalization repairs. Outbox completed-handler keys use assembly-qualified names plus registration index (`ContactCenterOutbox.cs:172`), so a deployment can replay already completed handlers. | Canonicalize provider identity before deriving keys; store aliases only as diagnostics. Add a transactional provider inbox, unique `(CanonicalProviderKey, ProviderCallId)` and event-id constraints, migration/repair for alias duplicates, monotonic compare-and-set state updates, and stable explicit versioned handler ids. Poison work must not block later due messages. |
| PR6 — Multi-node provider and real-time ownership | Every node can subscribe to the same Asterisk tenant event stream, while fallback Telephony mutation has no single-writer/monotonic sequence guard (`AsteriskRealtimeVoiceListener.cs:139-149`; `AsteriskRealtimeVoiceEventDispatcher.cs`). SignalR has no configured/proven backplane (`CrestApps.OrchardCore.SignalR/Startup.cs:26-38`; `CrestApps.Aspire.AppHost/Program.cs:18,26-35`). | Make duplicate ingestion transactionally harmless through canonical idempotency, provider sequence/high-water marks, and monotonic state as the correctness invariant. A listener lease/election may reduce load but is not the correctness authority. Ship and test a supported SignalR backplane before any multi-node claim. |
| PR7 — Secrets and PII | The shipped Asterisk listener logs a URI containing `api_key=user:password` (`AsteriskRealtimeVoiceListener.cs:139-146`; `AsteriskSettingsUtilities.cs:157-175`). Telephony/SMS logs can include raw addresses. | Remove credential-bearing URI logs and centralize structured PII classification/redaction with negative log tests. Raw secrets, customer addresses, and stable personal identifiers must not enter normal logs. |
| PR7a — Development-host containment | The Asterisk dashboard development host exposes diagnostics, call origination, hangup, and bridge deletion anonymously (`CrestApps.OrchardCore.Asterisk.Web/Program.cs:27-82`). | Guarantee the host is excluded from production artifacts and clearly marked development-only. Add a Production startup guard and local binding as defense in depth; only add full privileged auth if the host is intentionally distributed. |
| PR8 — CRM attribution correctness | Inbound contact lookup can return multiple matches and the router chooses `contactIds[0]` without ordering or ambiguity state (`InboundContactLookup.cs:31-68`; `VoiceContactCenterCallRouter.cs:464-469`). | Persist unresolved/single/ambiguous resolution. Do not assign the activity to a contact or run contact-bound subject actions until ambiguity is resolved explicitly. |
| PR9 — Capability truth | Recording and supervisor monitoring services change state/publish success events, but provider contracts expose no recording or monitor/whisper/barge/take-over operations (`ContactCenterRecordingService.cs:28-79`; `ContactCenterMonitoringService.cs:33-74`; `IContactCenterVoiceProvider.cs`). | GA-Core must fail closed: hide and reject Recording, Monitoring, and BidirectionalMedia until each capability has a separately certified executable contract. If any is included in the initial GA profile, provider execution, durable session, consent/retention/access audit, and end-to-end media proof become R8 prerequisites. |
| PR10 — Regulated outbound safety | The current eligibility window is hour-only and omits day-of-week, holidays, minute precision, regional policy, abandonment caps, safe-harbor behavior, and AMD (`DefaultDialerEligibilityService.cs:153-173`). | Reuse business-hours calendars, validate configurations, add regional compliance and rolling abandonment policies, and keep automatic Power/Progressive/Predictive modes unavailable until certified by tests and legal/product policy. |
| PR11 — Production proof | No Orchard feature-shell matrix, Contact Center Orchard browser suite, supported-database concurrency matrix, multi-node test, load/soak test, or chaos gate exists. | Release only when all gates below run automatically in CI or a documented release pipeline and publish retained evidence. |
| PR12 — Internet-facing provider ingress | Anonymous webhook endpoints buffer whole bodies and tie orchestration to request cancellation (`ProviderVoiceWebhookEndpoint.cs:14-45`; `DialPadWebhookController.cs:19-24,63-85`). | Before enabling any route, enforce provider-specific body/header and rate/concurrency limits, freshness/replay policy, authentication before processing, server-owned durable inbox acceptance independent of client disconnect, and 2xx only after inbox commit. |

### P1 enterprise requirements

| Area | Evidence-backed gap | Target |
| --- | --- | --- |
| Package and feature boundaries | Provider modules reference Contact Center implementation assemblies; Asterisk registers Contact Center adapters from its base feature; the Workflows bridge is activated only through `[RequireFeatures("OrchardCore.Workflows")]` and has no independently selectable Contact Center feature; Omnichannel Managements resolves optional dialer services dynamically. | Providers reference stable abstractions only. Add explicit Asterisk/DialPad Contact Center adapter features, a real `ContactCenter.Workflows` feature, and Omnichannel-owned contributor contracts instead of reverse references/service location. |
| Database constraints and indexes | Hot queue, outbox, and active-call queries are not led by their filter columns; routing loads broad sets and performs per-agent active-count queries. | Normalize queue membership/capacity counters, use bounded top-N queries, add provider-specific covering indexes, and verify query plans on every supported database. |
| Background work | Callback promotion and provider reconciliation are unbounded and duplicate-prone; one poison outbox item can block later due work. | Claim bounded batches with owner/lease expiry, checkpoint provider scans, rate-limit external lookups, continue past poison work, and expose backlog/age metrics. |
| Analytics/projections | Metric increments have no processed-event guard/checkpoint and rebuild tooling is incomplete. | Add projection versions, unique `(Projection, EventId)` processing records, atomic upserts, replay checkpoints, drift detection, and rebuild operations. |
| Observability and health | No Contact Center `ActivitySource`, `Meter`, liveness, or readiness model exists. | Add traces, counters/histograms, structured correlation, and health states for database, outbox/dead letters, task lag, provider connectivity, listener lease, and backplane. |
| Web/API hardening | Stored subject text reaches `innerHTML` in activity completion preview, and the authenticated raw `InboundVoiceEvent` endpoint duplicates the signed provider ingress under a broad interaction-management permission. | Construct UI with encoded DOM/text APIs and enforce CSP-compatible rendering. Unify production provider ingress and isolate simulators behind a development-only host or dedicated integration permission/credential. |
| Provider command semantics | SignalR client disconnect can cancel state-changing PBX commands; Asterisk RTP needs a documented secure network boundary and sequence-aware jitter/reorder behavior; media stop ignores caller cancellation while acquiring its lock. | Use server-owned idempotent command timeouts, explicit secure media deployment requirements, jitter/reorder tests, and linked bounded shutdown cancellation. |
| Data governance and upgrades | Retention covers only interaction events; call sessions, Telephony history, metrics, recordings, and PII erasure remain. Destructive migrations are not proven safe for rolling deployment. | Define classification/retention/erasure per entity and use expand-migrate-contract, or explicitly require and document downtime. |
| Public API and DI | Concrete orchestration types are public without extension need; post-commit handlers and the hub hide dependencies behind service location/child scopes. | Audit and baseline the public API, internalize implementation types where framework discovery permits, and make post-commit/shell-scope execution explicit and testable. |
| Browser and CI coverage | The existing Playwright host does not boot Orchard Contact Center, and primary workflows do not run the browser suite or future feature/provider/load gates. | Add Orchard-hosted agent/supervisor/provider E2E and layered CI with actionable diagnostics and retained artifacts. |

### Target domain and persistence model

| Model | Ownership and invariant |
| --- | --- |
| `OmnichannelActivity` | Universal CRM business work item. Owns contact/campaign/subject/disposition, stable kind/source classification, business schedule, and final business completion. It does not become a queue/reservation/session aggregate. |
| `ContactCenterWorkState` | Single-row-per-activity versioned orchestration authority. Owns current queue item, assignment/reservation linkage, routing/capacity state, active attempt pointer, `WrapUpDeadlineUtc`, required-disposition state, and compare-and-set/fencing version. Its migration uses add → backfill → verify → read cutover → write cutover → contract, with explicit N/N-1 behavior, divergence repair, and rollback point; no unfenced dual write is allowed. |
| `Interaction` | Immutable identity plus evolving communication-attempt history linked to one activity. Owns channel, participants, technical outcome, wrap-up timestamps, and terminal lifecycle, but never the business disposition. |
| `CallSession` | Voice-session projection with unique provider identity, provider sequence/high-water mark, legs, hold/conference/transfer state, and durations. |
| `AgentProfile` | Administrative configuration: user, skills/proficiency, teams, non-queue permissions metadata, and capacity policy. Queue entitlements live in QueueMembership; no volatile connection or active-reservation state belongs here. |
| `AgentSession` | Live tenant-scoped presence, connections, selected memberships within entitlement, current capacity usage, after-call state, heartbeat, and fencing/version token. |
| `QueueMembership` | Manager-owned normalized entitlement/configuration in the Queues feature rather than a mutable string/id list on the profile. Session-selected queue ids are separate and must be validated against this entitlement. |
| `AgentAvailability` projection | Headless Availability-owned liveness, presence, capacity, and active reservation/interaction projection. It does not own queue definitions or entitlements. Routing forms an eligible candidate by joining AgentAvailability with QueueMembership and validated session opt-in. |
| Provider inbox/command outbox | Durable idempotency and command coordination boundary. A provider event is acknowledged only after durable inbox acceptance; a provider command uses a stable command id and explicit `Pending`, `Claimed/Fenced`, `Sent`, `OutcomeUnknown`, `Confirmed`, `Compensating`, and terminal states. Unknown outcomes are queried/reconciled before retry; the platform pauses work rather than risking a duplicate customer action. |

Persistence rules:

- Enforce one `ContactCenterWorkState` per activity through an unconditional portable key, then use its version/fencing token for queue/reservation/assignment compare-and-set. Add provider-specific indexes only as optimizations; do not depend on filtered unique indexes that cannot be modeled consistently across supported databases.
- Use database compare-and-set or conditional update as the final authority. A lock or listener lease expiring must not permit an invariant violation.
- Keep transactions short and never hold a database transaction across provider/network calls.
- Commit the owning domain state change and its outbox record atomically in the same tenant database transaction. Cross-context CRM/Contact Center changes use an idempotent saga with explicit reconciliation rather than best-effort synchronous dual writes. Provider/network handlers run after commit, have stable ids, and document idempotency and compensation behavior.
- Add explicit indexes from observed query predicates and validate plans on SQLite, PostgreSQL, and SQL Server before declaring those databases supported for Contact Center.
- For every replayable projection, define source-event retention horizon, checkpoint/version, replacement snapshot/archive, legal-hold and erasure transformation, and the rebuild guarantee after source events are purged. Purge is blocked until recovery from the retained source is proven.
- Do not introduce Elasticsearch as a transactional dependency. It may later serve transcript/history search through an optional projection with explicit mappings, aliases, bulk indexing, replay, and outage degradation.

### Target Orchard Core feature graph

The target graph below is normative. Names may be adjusted during implementation, but the boundaries and dependency direction may not.

```text
CrestApps.OrchardCore.ContactCenter
  -> CrestApps.OrchardCore.Omnichannel

CrestApps.OrchardCore.ContactCenter.Admin
  -> ContactCenter
  -> CrestApps.OrchardCore.Omnichannel.Managements

ContactCenter.Agents
  -> ContactCenter

ContactCenter.Availability
  -> ContactCenter.Agents

ContactCenter.Queues
  -> ContactCenter.Availability

ContactCenter.Routing
  -> ContactCenter.Queues

ContactCenter.RealTime
  -> ContactCenter.Queues
  -> ContactCenter.Availability
  -> CrestApps.OrchardCore.SignalR

ContactCenter.Voice
  -> ContactCenter.Routing
  -> CrestApps.OrchardCore.Telephony

ContactCenter.Voice.SoftPhone
  -> ContactCenter.Voice
  -> ContactCenter.RealTime
  -> CrestApps.OrchardCore.Telephony.SoftPhone

ContactCenter.AgentDesktop
  -> ContactCenter.Availability
  -> ContactCenter.RealTime
  -> ContactCenter.Voice.SoftPhone
  -> CrestApps.OrchardCore.Omnichannel.Managements

ContactCenter.Dialer
  -> ContactCenter.Voice
  -> ContactCenter.Routing

ContactCenter.Compliance
  -> ContactCenter.Dialer

ContactCenter.Dialer.Automated
  -> ContactCenter.Dialer
  -> ContactCenter.Compliance

ContactCenter.EntryPoints
  -> ContactCenter.Voice
  -> ContactCenter.Routing

ContactCenter.Supervision
  -> ContactCenter.RealTime
  -> ContactCenter.Voice

ContactCenter.Recording
  -> ContactCenter.Voice

ContactCenter.Voice.Media
  -> ContactCenter.Voice

ContactCenter.Analytics
  -> ContactCenter
  -> only the capability features whose projections it actually consumes

ContactCenter.Workflows
  -> ContactCenter
  -> OrchardCore.Workflows

ContactCenter.Deployment
  -> explicit capability features represented by each recipe/deployment step

Asterisk
  -> Telephony

Asterisk.ContactCenterVoice
  -> Asterisk
  -> ContactCenter.Voice

Asterisk.ContactCenterMedia
  -> Asterisk.ContactCenterVoice
  -> ContactCenter.Voice.Media

DialPad
  -> Telephony

DialPad.ContactCenterVoice
  -> DialPad
  -> ContactCenter.Voice
```

Feature rules:

- Every `StartupBase` that contributes an optional capability must have an owning `[Feature(...)]`; `[RequireFeatures]` alone is not an independently selectable feature.
- Every required constructor dependency must be provided by the feature's declared transitive dependency graph. `IEnumerable<T>` and service location must not be used to hide a required feature relationship.
- Optional integration uses contributor/provider contracts owned by the lower-level bounded context. Omnichannel Managements must not reference Contact Center implementation to discover optional dialer behavior.
- `ContactCenter.Availability` owns AgentSession, fencing, heartbeat timestamps, cleanup, after-call deadlines, presence/capacity, and AgentAvailability without depending on SignalR. RealTime and provider-presence adapters are liveness contributors. If no approved contributor reports a live agent, Voice/Routing must hold or reject work rather than assume availability.
- `ContactCenter.Queues` owns QueueMembership entitlements and validates session-selected queue ids. Routing joins QueueMembership with AgentAvailability; Availability must not depend upward on queue definitions.
- Server-side Voice must operate without SoftPhone or RealTime. Soft-phone display drivers and hub URLs belong only to `Voice.SoftPhone`.
- Agent Workspace/CRM surfaces belong to `ContactCenter.AgentDesktop`, not Queues, Voice, or the headless base.
- `ContactCenter.Dialer` supports only Manual/Preview policies in GA-Core. Power/Progressive services and UI belong to `ContactCenter.Dialer.Automated`, which hard-depends on `ContactCenter.Compliance`; Predictive remains a later separately certified feature.
- Provider base features must operate without Contact Center. Contact Center adapters and media transports are separately enabled.
- A host with two tenants must support different legal feature sets simultaneously without shared static state, group names, settings, caches, listeners, or provider credentials.
- Maintain a versioned finite support matrix of GA feature sets, provider capability sets, database/version combinations, and topologies. Generate activation tests from it and reject or clearly mark unlisted combinations as unsupported.
- Every disable/re-enable path must define quiesce, drain, listener shutdown, active-work disposition, pending-command handling, and reconciliation. Test Voice, RealTime, Dialer, provider adapters, and media features while offers/calls/outbox work are active.

### Test-first remediation program

Every item follows red → green → refactor: add a deterministic failing characterization or invariant test first, make the smallest correctness change, then refactor behind the now-green tests. Do not combine feature-boundary rewrites with domain behavior changes unless an integration test proves both old and new behavior.

#### R0 — Freeze the support contract, objectives, ownership, and failure reproductions

Tests first:

- Re-run and pin the exact baseline commands, commit/worktree identity, counts, and artifacts at R0 start; the review baseline was 1,439 unit tests and 24 Telephony browser tests, but future work must not assume those counts remain static.
- Publish the finite GA support matrix: legal tenant feature sets, provider capability sets, databases/versions, node/backplane topology, prohibited combinations, and the initial supported capacity tier.
- Define percentile SLOs, error budgets, dependency limits, RPO/RTO, and acceptance owners before index, telemetry, or load work begins.
- Create a PR-to-test control matrix with a DRI, approver, test id, CI job, provider/database/topology, invariant, and retained evidence for every P0/P1 gate.
- **R0a in-process/shared-database reproductions:** add failing tests for two-shell tenant isolation and authorization; legal feature activation; disconnected-agent and wrap-up capacity; double reservation using two service providers over one shared database; provider-call orphan and `OutcomeUnknown`; duplicate/out-of-order/canonical-provider events; rolling-version outbox handler replay; ambiguous contact attribution; fake recording/monitoring success; webhook limits; secret/PII logs; and development-host exposure.
- **R0b distributed reproductions:** when the production Redis/backplane configuration does not yet exist, record the single-host two-shell proof as an explicit dependency with its owner and evidence target; R1 lands the backplane configuration and fixture while proving tenant-qualified groups/users. Track the genuine multi-process duplicate provider stream, listener lease loss, node failure, and network-partition failures; R2 expands the R1 fixture to two or more processes so R3/R4 can turn each into a red test before changing its behavior.
- Add an architecture test that extracts manifests/startup ownership and detects required services from undeclared features.

Exit: every P0 has an owned falsification test or an explicit R0b harness dependency, the finite support/capacity contract and SLOs are approved, and no production code is refactored before its applicable red test exists.

#### R1 — Contain security and tenant-isolation defects

Changes:

- Tenant-qualify every SignalR group and subscription.
- Add manager-owned queue/campaign entitlements and enforce them on all hub, endpoint, controller, recipe, and routing paths.
- Remove secret-bearing and raw-PII logs; add centralized structured redaction tests.
- Make the Asterisk simulator development-only/local-only or fully authenticated and authorized; remove committed production-like credentials.
- Replace unsafe `innerHTML` construction with encoded DOM APIs and add CSP-compatible XSS tests.
- Before any provider webhook is enabled, enforce body/header limits, rate/concurrency limits, freshness/replay policy, authentication, durable inbox acceptance independent of request cancellation, and 2xx only after inbox commit.

Exit: two-tenant isolation tests with the production backplane, authorization boundary tests, secret/PII log snapshots, development-host security tests, XSS tests, and provider-ingress abuse/cancellation tests pass.

#### R2 — Correct the feature and package graph without changing behavior

Changes:

- [x] Split the headless base from Omnichannel Managements admin integration.
- Introduce explicit Availability, Routing, Voice.SoftPhone, AgentDesktop, Compliance, Dialer.Automated, Workflows, provider Contact Center adapter, and provider media features.
- Move provider-facing contracts into stable abstractions; eliminate provider references to Contact Center implementation.
- Replace reverse Omnichannel-to-Contact-Center discovery with Omnichannel-owned contributors.
- Make post-commit handler execution and Orchard shell child scopes explicit abstractions instead of scattered service location.
- Expand the R1 single-host backplane fixture into the minimal two-node Orchard/Redis-backplane/provider-listener harness needed by distributed R0b scenarios.
- Define per-feature quiesce/drain/re-enable contracts and test inactive/idle enable-disable behavior before moving registrations. Defer active-call, pending-command, and in-flight-outbox quiesce proof to R3 after durable command state exists.

Exit: every support-matrix feature set boots in a fresh tenant with only declared dependencies, unlisted combinations are rejected or unsupported explicitly, idle disable/re-enable is clean, quiesce contracts exist, the two-node harness runs, and two tenants with different feature sets coexist.

#### R3 — Make assignment, provider commands, and event ingestion atomic

Changes:

- Add row versions, unique active constraints, and compare-and-set queue/reservation transitions.
- Add canonical `AgentAvailability` and fix disconnect/heartbeat/after-call recovery.
- Persist accepted reservation and provider command intent before execution; add idempotent command ids and compensation.
- Add the durable provider-command state machine (`Pending`, `Claimed/Fenced`, `Sent`, `OutcomeUnknown`, `Confirmed`, `Compensating`, terminal), query/reconcile before retry, and pause rather than redial when the outcome cannot be proven.
- Add canonical provider identity, provider inbox uniqueness, provider-call uniqueness, sequence/high-water handling, alias migration/repair, and stable outbox handler ids.
- Isolate poison messages and make every handler's idempotency contract explicit.
- Define atomic domain-state-plus-outbox commits and idempotent cross-context sagas; eliminate inline provider/network dispatch from mutating requests.
- Implement and test active-work quiesce/drain/re-enable for Voice, Dialer, RealTime, provider adapters, and outbox processing using the durable command/inbox state.

Exit: concurrency tests with multiple service providers/nodes produce exactly one reservation, one provider command, one valid state transition, and no capacity leak under cancellation, timeout, retry, duplicate, reordering, deployment, or active feature disable/re-enable.

#### R4 — Make provider capabilities executable and provider streams highly available

Changes:

- Split voice capabilities into executable call-control, queue/assignment, transfer/conference, recording, monitoring, and media contracts.
- Hide all unavailable controls and make success depend on provider confirmation.
- Add one listener owner/lease per tenant/provider stream or prove duplicate ingestion harmless; preserve monotonic provider truth.
- Decouple PBX mutations from SignalR connection cancellation and use bounded server-owned command timeouts.
- For GA-Core, fail closed and hide BidirectionalMedia unless it is in the approved support matrix. If media is included, define the secure RTP deployment boundary and implement/test sequence-aware reorder/jitter handling; otherwise defer transport certification to R9.

Exit: Asterisk and DialPad provider contract suites pass; two-node duplicate-stream and listener-lease-loss tests preserve one monotonic call state; unsupported capabilities cannot be invoked or displayed; any GA media capability has its separate certification.

#### R5 — Correct CRM attribution and regulated outbound behavior

Changes:

- Add ambiguous contact resolution and defer contact-bound business actions until resolved.
- Terminalize or avoid creating active-looking work for closed/unroutable entry points.
- Reuse business-hours calendars for outbound windows; add minute/day/holiday/regional rules, config validation, abandonment caps, safe-harbor behavior, and AMD outcome mapping.
- Move Power/Progressive services, settings, profiles, and UI behind `ContactCenter.Dialer.Automated`, which cannot enable without `ContactCenter.Compliance`; keep Manual/Preview on the base Dialer support profile.
- Keep automated dialing modes disabled until their specific compliance and pacing tests pass.

Exit: CRM attribution tests and the approved compliance policy suite pass with auditable suppression reasons and no silent fallback.

#### R6 — Scale persistence, background work, and projections

Changes:

- Add query-aligned indexes and normalized queue membership/capacity data.
- Replace broad loads/N+1 counts with bounded top-N queries and aggregate SQL.
- Claim callbacks, reconciliation work, outbox items, and retention batches with bounded leases/checkpoints.
- Add projection processed-event keys, atomic upserts, replay versions/checkpoints, rebuild, drift detection, and poison isolation.
- Resolve enum/raw-SQL portability and validate every supported database.

Exit: database matrix, query-count/plan budgets, duplicate/crash recovery, replay, and target workload tests pass.

#### R7 — Add production operations, privacy, and upgrade safety

Changes:

- Add OpenTelemetry traces/metrics and health endpoints for storage, provider streams, outbox/dead letters, scheduler lag, listener lease, and SignalR backplane.
- Wire and document the supported multi-node backplane.
- Define per-entity data classification, retention, erasure, recording access audit, and backup/restore behavior.
- Align every purge/erasure policy with projection replay horizons, retained snapshots/archives, legal holds, and post-purge rebuild guarantees.
- Convert incompatible migrations to expand-migrate-contract or document a downtime requirement.
- Add runbooks for SQL, Redis/backplane, provider, node, and network failures plus rolling/blue-green deployment.

Exit: health/telemetry contracts, erasure/retention, backup/restore, mixed-version upgrade, and failure-injection tests pass.

#### R8 — Prove end-to-end behavior and release capacity

Changes:

- Add an Orchard-hosted Contact Center Playwright project covering feature enablement, agent sign-in, inbound offer, accept/decline/timeout, call-state recovery, after-call work/disposition, supervisor controls, authorization failures, reconnect, and two-tenant isolation.
- Add multi-node provider/SignalR integration, load, soak, and chaos suites.
- Run browser, provider-contract, database, feature-matrix, security, and performance gates in CI/release workflows with retained logs/traces/results.
- Certify the initial GA support matrix and one approved capacity tier; publish explicit limits and unsupported combinations.

Exit: all production release gates pass for the exact versions/configurations being shipped.

#### R9 — Resume advanced capability work only after R0-R8

- Complete executable recording/monitoring, IVR/self-service, quality, workforce, AI conversation, and non-voice channel adapters only after their prerequisite contracts and release gates exist.
- Separately certify higher scale tiers, million-record cardinality, sharding, regional failover, and optional media capabilities; these certifications expand the published support envelope without weakening GA-Core correctness.
- Prove a second channel before generalizing a channel adapter framework; avoid speculative abstractions.
- Do not enable Predictive dialing until historical metrics, forecasting validation, abandonment controls, legal policy, and sustained production evidence are approved.

### Required automated test portfolio

| Suite | Minimum scope |
| --- | --- |
| Domain invariant tests | Every state transition, terminal-state monotonicity, reservation/capacity invariant, after-call recovery, disposition rule, contact ambiguity, and compliance decision. |
| Manifest/static architecture tests | Every startup belongs to a feature; every dependency exists; provider assemblies reference only approved abstractions; public API baseline; forbidden reverse references and service-locator patterns. |
| Orchard feature activation matrix | Generate from the versioned finite support matrix: fresh tenant enable/disable for each listed feature set with only declared dependencies; migrations, routes, drivers, resources, navigation, background tasks, recipes, and deployment steps resolve; two tenants use different combinations; live-work quiesce/drain/re-enable behavior is deterministic. |
| Provider contract tests | Shared suite for every provider: canonical identity/aliases, idempotent commands/events, lost-response `OutcomeUnknown` reconciliation, duplicate/out-of-order handling, capability/operation consistency, timeout/cancellation, transfer/conference, restart/reconciliation, webhook authentication/limits/freshness, and sanitized diagnostics. |
| Database integration matrix | SQLite, PostgreSQL, and SQL Server for unique constraints, compare-and-set, indexes/query plans, migration/rollback policy, concurrency, retention, and replay. |
| Multi-tenant security tests | SignalR groups, settings, secrets, queues, interactions, reports, provider streams, caches, logs, and authorization boundaries. |
| Orchard browser E2E | Agent, supervisor, admin, reconnect, authorization, feature enablement, provider degradation, after-call work, and accessibility-critical flows in the real Orchard host. |
| Distributed-system tests | Two or more nodes, backplane, listener lease, duplicate events, lock expiry, node crash, rolling deployment, network partition, slow provider, and retry storm. |
| Performance tests | Routing/offer latency, queue throughput, reservation contention, outbox drain, reconciliation, report aggregation, SignalR fan-out, allocations, query counts, and provider-rate limits at approved workload tiers. |
| Soak/chaos tests | Sustained load with SQL, Redis/backplane, provider, DNS, network, and node failures; verify bounded recovery, no duplicate customer action, no lost terminal event, and no permanent capacity leak. |
| Security tests | Cross-tenant isolation, permission escalation, CSRF/XSS, webhook size/rate/signature/replay policy, SSRF/open redirect checks, secret/PII log snapshots, dependency scanning, and development-host exposure. |
| Upgrade/operations tests | Expand-contract mixed-version run, backup/restore, retention/erasure, health/readiness, telemetry emission, alert thresholds, and runbook exercises. |

### Capacity and service-level acceptance

User-count claims must distinguish registered users/contacts from concurrent agents and active interactions. A single unpartitioned cluster is not expected to serve one million concurrent voice agents.

- GA-Core must certify at least one explicitly approved concurrent-agent tier from the R0 support matrix and publish its queued-item, active-interaction, event-rate, provider, database, and SignalR assumptions. The product must reject unsupported claims rather than imply unbounded scale.
- Maintain separate scale-certification milestones for 100, 1,000, and 10,000 concurrent agents. A tier becomes supported only after its own load/soak/resilience evidence passes; higher tiers do not silently inherit lower-tier approval.
- Maintain independent data-cardinality certifications for 100,000 and 1,000,000 contacts, activities, interactions, events, and report-period rows because registered/data volume is not equivalent to concurrent voice load.
- Require an explicit sharding/tenant-placement and regional-failover design before any claim above the largest tested single-cluster tier.
- Define and approve numeric SLOs before R8 begins for routing decision latency, offer delivery latency, provider-webhook durable acknowledgement, command completion, reconnect recovery, outbox age, background-task lag, report latency, availability, RPO, and RTO.
- No benchmark is valid unless it records hardware/service tiers, database provider/version, node count, tenant distribution, feature set, provider simulator behavior, dataset shape, warm-up, duration, percentiles, errors, allocations, query counts, and telemetry.

### Production release gates

1. **Security:** no open P0/P1 cross-tenant, authorization, secret, PII, webhook, XSS, or development-host findings; threat model and security review approved.
2. **Feature safety:** every feature and supported combination passes the tenant activation matrix with only declared dependencies; provider base features work without Contact Center.
3. **Correctness:** database constraints/CAS, provider inbox/outbox, stable handler ids, agent availability, after-call recovery, and compensation tests pass under concurrency and retry.
4. **Provider truth:** every advertised capability has an executable contract and provider test; unsupported UI is absent; recording/monitoring remain disabled until end-to-end media execution and governance pass.
5. **Multi-node:** cross-node SignalR, provider listener ownership, duplicate event handling, reconnect, rolling deployment, and node-loss tests pass.
6. **Data:** supported database matrix, query-plan budgets, retention/erasure, replay/rebuild, backup/restore, and migration policy pass.
7. **Resilience:** SQL, Redis/backplane, provider, network, and node failure exercises meet approved recovery objectives without duplicate customer action or permanent capacity loss.
8. **Performance:** the initial GA capacity tier meets its numeric SLOs through load and soak tests with no unbounded queue, task, memory, connection, or retry growth; higher tiers remain unsupported until separately certified.
9. **Operations:** health, readiness, telemetry, alerts, dashboards, runbooks, capacity guidance, and support matrix are complete.
10. **Quality:** unit, feature, provider, database, browser, security, distributed, performance, chaos, and upgrade suites run in the release pipeline with retained evidence.
11. **Documentation:** public setup, configuration, permissions, provider development, network/media, HA, backup/restore, privacy, migration, troubleshooting, and limitations docs match tested behavior.
12. **Independent approval:** named engineering, security, SRE, privacy/legal, product, and documentation approvers review the evidence for the release profile. Tool/model reviews are advisory inputs and their dispositions remain recorded below.

### Technology decisions and non-goals

- Do not adopt full event sourcing merely because an event log exists. Canonical state plus transactional outbox/inbox is simpler and safer for the current maturity.
- Do not add Elasticsearch to routing, assignment, provider ingest, or other correctness paths. Add it only as an optional search projection after mappings, aliases, bulk indexing, replay, and outage behavior are tested.
- Do not chase Native AOT or aggressive trimming while Orchard Core's dynamic module, shape, recipe, and reflection model is the deployment foundation. Keep APIs analyzer-clean and trimming-aware where practical, but treat runtime compatibility with Orchard as authoritative.
- Do not apply `Span<T>`, pooling, source generators, records, or newer C# syntax mechanically. Use profiling and domain semantics; immutability/value objects are valuable where they enforce invariants, while persistence/framework models must remain compatible.
- `ConfigureAwait(false)` is not a blanket requirement in ASP.NET Core/Orchard application code. Cancellation ownership, bounded timeouts, and not coupling durable mutations to client disconnect are the actual release concerns.
- Multi-region active-active, workforce forecasting/scheduling, quality management, and AI voice are not GA scope until the single-region multi-node foundation and release gates pass.

### Independent model challenge record

- **Claude Opus 4.8:** independently verified the release rejection and the highest-severity source evidence. Accepted challenges added tenant-qualified SignalR user routing with the production backplane, a durable `WrapUpDeadlineUtc` without resurrecting `WrapUpSession`, canonical provider identity before uniqueness, split R0a/R0b reproductions, portable single-row WorkState CAS, atomic state-plus-outbox/cross-context saga rules, historical-section supersession, and separate GA versus scale-certification envelopes.
- **GPT-5.6 Terra:** independently stress-tested executability. Accepted challenges added a headless Availability feature, explicit WorkState migration/cutover/rollback phases, a finite support matrix, feature quiesce/drain/re-enable behavior, durable provider `OutcomeUnknown` command semantics, provider-ingress limits/durable acknowledgement in R1, replay-horizon/retention coupling, SLO and ownership decisions in R0, and fail-closed GA-Core treatment for optional recording/monitoring/media capabilities.
- **Recommendations rejected or narrowed:** full event sourcing, Elasticsearch in correctness paths, resurrecting a `WrapUpSession` aggregate, treating listener/distributed locks as correctness authority, and gating the first supported GA profile on every higher-scale certification. Advanced media transport work is required only when BidirectionalMedia is included in the approved release profile; otherwise the capability remains unavailable.
- **Final confirmation:** Claude Opus 4.8 and GPT-5.6 Terra re-read the incorporated plan and both returned `solid`, with no remaining blocking dependency, ownership, sequencing, testability, capacity, or release-gate contradiction. The plan is approved as executable test-first; product release remains blocked until the P0/R0-R8 evidence gates pass.

## Progress status

Keep this section current. Use the checklist below to track phase-level progress; add dated notes under "Change log" for meaningful decisions.

### Production-readiness remediation checklist

This checklist is authoritative for current execution order. Historical phase and G1-G8 checkmarks below record implementation increments only; every affected capability remains production-incomplete until the corresponding remediation and release gates pass.

- [x] **R0 — Support contract, SLOs, ownership, and failure reproductions:** publish the finite GA matrix and PR-to-test ownership, pin baselines, complete R0a tests, and track R0b harness dependencies.
  - [x] Exact clean-tree baseline pinned in `.github/contact-center/R0-BASELINE.md`: commit `ccb1076d`, strict build, 1,472 unit tests, 24 Telephony browser tests, asset/docs builds, toolchain versions, and test-artifact hashes.
  - [x] Finite versioned GA support/capacity matrix and prohibited combinations published in `.github/contact-center/support-matrix.v1.json` and public production-support documentation, with machine-readable contract tests.
  - [x] Versioned SLOs, error budgets, dependency limits, RPO/RTO, DRI roles, and approver roles published in `.github/contact-center/service-objectives.v1.json`, with contract tests and public documentation. Named people remain a release-candidate assignment gate.
  - [x] PR-to-test control matrix for every P0/P1 gate published in `.github/contact-center/pr-test-control-matrix.v1.json` (41 gates: C001-C008, D001-D009, F001-F006, O001-O006, S001-S005, T001-T003, V001-V004), each resolving a DRI role, approver roles, test id, CI job, provider/database/topology context, falsifiable invariant, and retained evidence location, with contract tests. Most CI jobs remain planned; only the currently enforced/partially enforced gates are marked implemented/partial.
  - [x] R0a in-process/shared-database failure reproductions and feature-dependency architecture test.
    - [x] Two-shell in-process tenant isolation and permission-gated Contact Center group-join tests.
    - [x] Static legal feature dependency-closure and undeclared-service characterization test; live tenant activation remains tracked by T001.
    - [x] Disconnected-agent and after-call capacity recovery characterization tests: last-session disconnect leaves an `Available` profile routable, assignment has no live-session dependency, and after-call state has no persisted server-swept deadline. R3 must invert these tests with canonical availability and deterministic deadline recovery.
    - [x] Shared-database double-reservation characterization using two independent service providers and YesSql sessions over one SQLite database; synchronized read-then-write commits persist two distinct pending reservations for the same work when the shared test lock permits overlapping holders. R3 must invert this with portable CAS and unique-active constraints.
    - [x] Provider-call orphan and `OutcomeUnknown` characterizations: failed post-dial reservation acceptance discards the successful provider call identity, while a lost provider response is treated as a definitive failure and removes the work. R3 must invert these with durable command intent, reconciliation, and idempotent compensation.
    - [x] Duplicate/out-of-order/canonical-provider event characterizations: concurrent idempotency checks both persist and enqueue the same event, equal-timestamp delivery regresses Connected to Ringing, and provider aliases mutate stored identity. R3 must invert these with inbox uniqueness, sequence/high-water rules, and canonical provider keys.
    - [x] Rolling-version outbox-handler characterizations: registration reorder replays an already completed handler because the checkpoint includes its prior index, and one poison due item blocks later messages in the batch. R3 must invert these with stable versioned handler ids and per-message isolation.
    - [x] Ambiguous contact attribution characterization: when inbound lookup returns two valid contacts, the router immediately persists the first result as the activity attribution and never loads the second. R5 must invert this with explicit resolution state and blocked contact-bound actions.
    - [x] Fake recording/monitoring success characterization tests.
    - [x] Webhook body-limit and request-disconnect tests.
    - [x] Secret/PII log characterizations: Asterisk credential URI redaction is covered, while centralized Telephony/SMS/Contact Center snapshots prove the shared sanitization helper preserves E.164 addresses, agent ids, and token-shaped values and representative logging paths still emit raw customer/agent identifiers. R1 must invert these with centralized classification/redaction and negative snapshots.
    - [x] Development-host Production startup guard tests.
  - [x] R0b Redis/backplane and multi-process harness dependency ledger published in `.github/contact-center/r0b-harness-dependency-ledger.v1.json`, mapping seven distributed scenarios to control ids, blocking phases, required infrastructure, current unit evidence, and retained evidence targets without claiming the harnesses are implemented or certified.
- [~] **R1 — Security and tenant isolation:** tenant-qualified SignalR, queue/campaign entitlements, secret/PII redaction, simulator containment, XSS correction.
  - [x] Tenant-qualified Contact Center and Telephony user/queue/supervisor SignalR destinations, including authorized group joins and provider-event projections.
  - [x] Asterisk ARI credential-log redaction and development-only simulator containment.
  - [x] Stored CRM workflow-preview text rendered through safe DOM text/attribute APIs with a regression test that forbids `innerHTML` in the completion view.
  - [x] Generic Contact Center and DialPad webhook request bodies bounded to 1 MiB with HTTP 413 responses, and authenticated state-changing processing detached from caller disconnect cancellation.
  - [x] Manager-owned queue/campaign entitlements enforced by the soft-phone UI, controller, hub, central presence service, live-session snapshots, routing, and active SignalR queue subscriptions; unauthorized legacy/imported memberships fail closed.
  - [x] Centralized operational PII redaction: a shared `OperationalLogRedactor`/`OperationalLogFieldKind`/`OperationalLogIdentifierCategory` classification API in `CrestApps.OrchardCore.Abstractions` pseudonymizes stable identifiers with a process-local keyed HMAC and fully redacts customer addresses, secrets/token-shaped values, complete metadata dictionaries, free-form request/provider-response text, and exception messages/inner-exception data while retaining exception type and a bounded, control-sanitized stack-frame summary, migrated across Contact Center Core, Telephony, Asterisk, DialPad, and Omnichannel.Sms, with negative log tests proving sentinel E.164 numbers, user/agent/call ids, secrets, raw exceptions, and attacker-controlled stack-trace text never appear while CR/LF log-forging protection remains.
  - [~] Remaining webhook ingress controls:
    - [x] Tenant-local pre-buffering concurrency limits and authenticated per-provider token-bucket rate limits for generic Contact Center and DialPad webhook routes, with HTTP 429/`Retry-After`, fail-fast option validation, and negative tests proving unauthenticated or over-limit deliveries never enter state-changing processing.
    - [x] Provider-signed freshness window for generic Contact Center and DialPad webhooks, rejecting missing, malformed, non-UTC, stale, and excessively future timestamps before state-changing processing; durable replay uniqueness remains part of inbox acceptance.
    - [x] Tenant-local YesSql provider webhook inbox commits normalized generic and DialPad deliveries under a distributed provider-delivery idempotency lock before HTTP 2xx, then dispatches independently of caller cancellation with immediate processing, retry/backoff, poison-message isolation, and dead-lettering.
  - [x] Soft-phone queue/campaign selectors use Orchard's named `bootstrap-select` resource through the `contact-center-soft-phone` dependency graph, and initialize only explicitly marked Work-tab controls so the script is emitted once and list-management filters are not reinitialized.
- [~] **R2 — Orchard feature and package graph:** headless base and Availability, explicit optional/provider features, stable abstraction references, finite activation matrix, quiesce/re-enable behavior, and two-node harness.
  - [x] The base `CrestApps.OrchardCore.ContactCenter` feature now depends only on headless Omnichannel infrastructure; the independently selectable `CrestApps.OrchardCore.ContactCenter.Admin` feature owns the Omnichannel Managements dependency, and the architecture ledger no longer records FDV001.
  - [x] Server-side `CrestApps.OrchardCore.ContactCenter.Voice` now depends on provider-agnostic Telephony rather than the soft-phone UI; `CrestApps.OrchardCore.ContactCenter.Voice.SoftPhone` explicitly owns the Telephony soft-phone and real-time call-state projection, and the architecture ledger no longer records FDV002.
  - [x] Queues no longer registers soft-phone display, resource, or endpoint infrastructure. `Voice.SoftPhone` owns the complete integration under declared Voice, RealTime, and Telephony Soft Phone dependencies, leaving the architecture ledger with zero known feature-dependency violations and closing FDV003.
  - [x] `CrestApps.OrchardCore.ContactCenter.Workflows` is now an independently selectable feature with explicit base Contact Center and `OrchardCore.Workflows` dependencies; the workflow activity and domain-event bridge no longer activate implicitly through `[RequireFeatures]`.
  - [x] `CrestApps.OrchardCore.ContactCenter.Availability` now owns agent presence, durable sessions, heartbeat state, stale-session cleanup, and logout synchronization without depending on SignalR. Queues and RealTime explicitly depend on Availability; RealTime retains only transport projections and hub surfaces.
  - [x] `CrestApps.OrchardCore.ContactCenter.Routing` now independently owns routing strategies and assignment orchestration over Queues. Voice depends on Routing rather than Queues, Dialer explicitly declares Voice and Routing, and the exact activation closures are pinned by architecture tests.
  - [x] `CrestApps.OrchardCore.ContactCenter.AgentDesktop` now independently owns the CRM-integrated workspace controller, endpoints, and navigation under explicit Availability, RealTime, Voice.SoftPhone, and Omnichannel Management dependencies.
  - [x] `CrestApps.OrchardCore.ContactCenter.Supervision` now independently owns the live dashboard controller, endpoints, monitoring surface, and navigation under explicit RealTime and Voice dependencies. RealTime retains only shared transport and event projection concerns.
  - [x] `CrestApps.OrchardCore.ContactCenter.Compliance` now owns outbound eligibility and attempt execution, while `CrestApps.OrchardCore.ContactCenter.Dialer.Automated` hard-depends on Compliance and owns Power/Progressive strategies, pacing, and automated batch registration. Base Dialer no longer starts automated work.
  - [x] `CrestApps.OrchardCore.ContactCenter.EntryPoints` now owns inbound entry-point administration, resolution, queue ingress, and entry-point-aware offer services under explicit Voice and Routing dependencies. Voice retains its provider-neutral inbound call service and remains independently activatable by consuming entry-point qualification only when contributed.
  - [x] `CrestApps.OrchardCore.ContactCenter.Recording` now independently owns recording orchestration and recording-state events over Voice. Executable provider capability enforcement remains an R4 gate.
- [ ] **R3 — Atomic consistency and agent lifecycle:** database CAS/constraints, canonical availability, after-call recovery, provider command outbox/compensation, provider inbox, stable handler ids.
- [ ] **R4 — Executable provider capabilities and high availability:** recording/monitoring contracts, provider stream ownership, monotonic events, command cancellation, media transport hardening.
- [ ] **R5 — CRM attribution and regulated outbound:** ambiguous-contact workflow, terminal unroutable records, full calling calendars, abandonment/safe-harbor/AMD policy, automated-mode gates.
- [ ] **R6 — Persistence, background work, projections, and scale:** indexes, bounded claims/batches, projection replay/checkpoints, supported-database matrix, load budgets.
  - [x] Reporting foundation declares filter dimensions per report instead of classifying reports through hard-coded identifier lists; duplicate report/export names fail activation; table rows distinguish detail, subtotal, and grand-total semantics.
  - [x] Queue groups provide catalog-only organization and current-membership report filtering/aggregation without changing routing, SLA inheritance, entitlements, or queue behavior.
- [ ] **R7 — Operations, privacy, and upgrades:** OpenTelemetry, health/readiness, SignalR backplane, retention/erasure, backup/restore, expand-contract migrations, failure runbooks.
- [ ] **R8 — End-to-end production proof:** Orchard Contact Center Playwright, multi-node, load/soak/chaos, security, upgrade, and release-pipeline evidence.
- [ ] **R9 — Advanced capabilities:** resume IVR, recording/monitoring, quality, workforce, AI, non-voice, and Predictive work only after R0-R8.

### Phase checklist

- The checkboxes below are historical delivery markers, not commercial-readiness approval. The 2026-07-13 review reopened Phase 1 eventing, Phase 2/3 availability and concurrency, Phase 4 provider/feature boundaries, Phase 5/10 compliance, Phase 6 after-call recovery, Phase 7 tenant isolation, Phase 9 executable media, Phase 11 feature gating, Phase 12 projections, and Phase 13 scale/data governance.
- [x] **Phase 0 — Project governance and durable planning**
  - [x] Durable repo-tracked plan at `.github/contact-center/PLAN.md`
  - [x] Pointer added to `.github/copilot-instructions.md`
  - [x] Public docs landing page under `src/CrestApps.Docs/docs/contact-center`
  - [~] Historical module/feature map recorded; the production target graph and activation matrix were reopened by R2.
- [~] **Phase 1 — Domain foundation** (implementation shipped; provider inbox, stable outbox handlers, projection replay, and headless feature boundary reopened)
  - [x] `CrestApps.OrchardCore.ContactCenter.Abstractions` (constants, channel/direction/status/priority/role enums, event vocabulary)
  - [x] `OmnichannelActivity` extended with activity kind/source, assignment status, and reservation metadata so CRM activities remain the universal work item
  - [x] Load Inventory UI changed to source-first creation with Manual and Dialer sources; Dialer inventory loads load unassigned activities for later reservation
  - [x] `IActivityDispositionService` contract added as the source-neutral path for activity dispositions
  - [x] `CrestApps.OrchardCore.ContactCenter.Core` (Interaction + InteractionParticipant + InteractionEvent models, indexes, stores, `IInteractionManager`, event publisher, permissions)
  - [x] `CrestApps.OrchardCore.ContactCenter` base module (Startup, index providers, migrations, permission provider)
  - [x] Registered in `.slnx` and the `Cms.Core.Targets` bundle
  - [x] 13 unit tests (event envelope, event publisher dispatch/idempotency/resilience, interaction manager lifecycle, entity metadata extensibility)
  - [x] Docs landing page + `v2.0.0` changelog entry
- [~] **Phase 2 — Agent, presence, queue, and reservation foundation** (implementation shipped; canonical online availability and database-enforced reservation invariants reopened)
  - [x] `Agents` feature: AgentProfile (presence, capacity, skills, queue/campaign membership), store/manager/index, presence manager, soft-phone queue/campaign sign-in/out
  - [x] `Queues` feature: ActivityQueue, QueueItem, ActivityReservation models/stores/managers/indexes; queue + reservation lifecycle (reserve/accept/reject/expire); reservation-expiry background task
  - [x] Queue-group catalog with permission-gated admin CRUD and optional queue association for organization/reporting only
  - [x] Availability-based assignment service (longest-idle agent ↔ highest-priority item); agent/queue/dialer permissions; admin menu + CRUD UI; unit tests
  - [x] Assignment safety (G2 P0 core): per-queue distributed-lock single-writer assignment (P0 #7) and `MaxConcurrentInteractions` capacity enforcement via `CapacityRoutingStrategy` (P0 #6)
- [~] **Phase 3 — Routing MVP** (policy-based routing shipped; normalized queue membership/proficiency, bounded SQL routing, canonical availability, and scale validation reopened)
- [x] Phase 4 — Voice integration with Telephony (`Voice` feature: Voice Contact Center Call Router (`IVoiceContactCenterCallRouter`) for inbound and outbound voice routing, inbound voice ingress + normalization boundary (`InboundVoiceEvent`/`IInboundVoiceService` compatibility), inbound activity+subject+interaction creation, queue→endpoint routing, agent offer routing, outbound provider dispatch through `IContactCenterVoiceProvider`, and the Telephony soft-phone incoming-call modal with `IIncomingCallContextProvider`/`IIncomingCallDispatcher` extensibility + voicemail capability; **G1 backend shipped: provider delivery models (`AgentDeviceNative`/`ServerSideAcd`) + `ConnectToAgentAsync`, `CallSession` aggregate, normalized `ProviderVoiceEvent`/`IProviderVoiceEventService` ingestion, and the authoritative `IContactCenterCallCommandService` that accepts the reservation, bridges media, and advances interaction+call-session together. all G1 items shipped: the soft-phone JS accept-then-answer coordination now awaits the server accept and only answers the device when `RequiresDeviceAnswer` (asset rebuilt), per-provider signed webhook adapters emit `ProviderVoiceEvent`, and the blind/consultative transfer + conference taxonomy landed** — see "Design review" P0 #1, #2, #3 and P1 #9)
- [~] Phase 5 — Outbound dialer MVP (`Dialer` feature: profiles, modes, power/progressive pacing, dialer batch sources, outbound calls routed through the Voice Contact Center Call Router, DialPad Contact Center Voice provider. **G4 dialer safety shipped (2026-06-30):** each mode is now a dedicated `IDialerStrategy` (Predictive disabled in editor + rejected server-side + refused at runtime; Power hard-capped via `PowerDialerStrategy.MaxCallsPerAgent`); the new `IDialerEligibilityService` compliance gate runs before every attempt and audits `DialSuppressed` (destination, max-attempts, retry cool-down, contact do-not-call, calling window in the contact's time zone, and national DNC registries); single-attempt logic moved to `IDialerAttemptService`. Remaining: callback scheduling (`CallbackRequest`) + callback queues, and dialer run/attempt projections.)
- [x] Phase 6 — Disposition lifecycle (the **Subject Flow is the single decision controller** for CRM, inbound, and outbound: every activity carries a Subject + Subject Flow, and completion routes through the source-neutral `IActivityDispositionService`, which applies the disposition, marks the activity `Completed` regardless of its prior contact-center state — resolving P0 #8 — and runs the disposition-driven Subject Actions. A subject flow can require a disposition (`SubjectFlowSettings.RequireDisposition`), enforced centrally so completion is blocked until a disposition is chosen. **Design decision (2026-06-30):** the separate `WrapUp`/`WrapUpSession` concept added earlier was removed as redundant with disposition + subject flow; after-call work is represented by agent presence (`WrapUp`) plus the active/wrap-up interaction on the Agent Workspace, not by a separate domain aggregate.)
- [~] Phase 7 — Agent desktop and supervisor real-time UX (**Agent state reason codes shipped (2026-06-30)** + **real-time SignalR foundation shipped (2026-06-30):** the new `RealTime` feature adds the `ContactCenterHub` (user/queue/`cc:supervisors` groups + `WatchQueue`/`UnwatchQueue`), a live `AgentSession` aggregate split from `AgentProfile`, a `Heartbeat`-driven stale-session cleanup background task that signs out dead clients, a `GetSnapshot` reconnect snapshot (`AgentDesktopSnapshot`), the `ContactCenterRealTimeEventHandler` that broadcasts presence/offer/queue events, a `contact-center-realtime` client script, and the `MonitorContactCenter` permission + Supervisor role. **agent desktop + supervisor dashboard shipped 2026-07-01** (see G5), and **supervisor live-engagement buttons + after-call presence release shipped 2026-07-01**. **2026-07-08 hardening:** when voice is enabled, signing in to a queue or returning to `Available` now immediately offers any already-waiting inbound voice work, the soft-phone incoming-call modal now resolves reservation lifecycle posts correctly from its offer URLs instead of waiting on workspace-only form posts, queue sign-out now clears queue/campaign membership cleanly, Orchard logout now defers the same Contact Center sign-out path until the Orchard logout succeeds, expired reservations keep signed-out agents offline instead of bouncing them back to `Available`, and the soft phone restores an in-progress call after reconnect or page reload from the persisted interaction history. **2026-07-08 soft-phone sign-out fix:** queue sign-in/sign-out now also synchronizes the live `AgentSession` membership used by the real-time layer, and Contact Center domain events are dispatched through deferred outbox fan-out after persistence so slow workflow or notifier handlers cannot leave the soft-phone sign-out postback hanging. **2026-07-09 soft-phone UX hardening:** the widget now preserves the selected footer tab across page reloads, measures the Keypad view and reuses that natural height across Keypad/Recent/Work, scrolls non-keypad tabs inside the shared panel height when needed, suppresses the transient no-provider warning until the real connection status is known, and re-checks already-waiting inbound voice work once the soft phone reconnects so queued calls are offered after a refresh. **2026-07-09 queue sign-in follow-up:** both the soft-phone queue sign-in controller and the availability handler now resolve queued-voice re-offer services lazily instead of constructing the full voice graph during handler/controller activation, which removes the new sign-in loading regression while preserving the already-waiting inbound-call recovery path. **2026-07-09 inbound offer recovery fix:** when a timed-out voice reservation is re-queued, the stale ringing interaction assignment is now cleared before the work returns to the queue so the agent does not stay falsely at capacity and block the next offer. **2026-07-09 stale-offer repair:** queued-voice reconnect recovery now also clears orphaned ringing interactions that no longer have an active reservation, cancels stale pending reservations whose queue item or interaction no longer reflects a live ringing offer, and ignores unassigned `Created` interactions for capacity, so abandoned offer fragments cannot keep future inbound calls from routing. **2026-07-09 pending-offer restore:** the soft phone now restores the currently ringing inbound offer from the active reservation after refresh or reconnect, reuses the same incoming-call context/lifecycle URLs, and keeps the modal visible until accept, decline, or authoritative timeout. **2026-07-09 hub-driven soft-phone sign-in:** queue sign-in, sign-out, queued-voice recovery, and pending-offer restoration in the soft-phone Work tab now flow through `ContactCenterHub` instead of a page reload, queue-group membership is updated on the live connection immediately, and the incoming modal suppresses duplicate or superseded ringing offers once the agent is already handling the call. **2026-07-09 centralized self-healing:** sign-in, sign-out, and queued-voice availability recovery now share an invariant-based healer that clears stale `ActiveReservationId` pointers, cancels ghost pending reservations, and re-queues active assigned interactions that survive restarts or partial failures in an impossible state, so orphaned voice work is reclaimed before it can block future inbound routing. **2026-07-09 live-offer + Asterisk accept follow-up:** queued voice offers now trigger an immediate soft-phone modal refresh from the Contact Center hub's live `OfferReceived`/`OfferRevoked` events, server-side provider-only voice offers now answer the underlying telephony call during authoritative accept so Asterisk-backed calls stay visible and controllable after the agent accepts them, inbound telephony offers are persisted into Recent history as soon as they are dispatched, and an accepted call is no longer cleared back to `Ready` if the hub's revoke event races with the accept response. **2026-07-10 call-session soft-phone sync:** assigned-agent Contact Center call-session events now upsert the Telephony soft-phone interaction history and push `CallStateChanged` through the Telephony hub, so server-side disconnects and other normalized call-state transitions clear or update the soft phone immediately instead of leaving it stuck on the last client-initiated state. **2026-07-10 richer provider-event normalization:** normalized provider events now also carry mute, recording lifecycle, and conference topology details, and Contact Center emits more detailed real-time-friendly events (`CallHeld`, `CallResumed`, `CallMuted`, `CallUnmuted`, `Recording*`, `CallConferenceChanged`) so present and future providers can keep the soft phone and agent desktop synchronized from richer server truth. **2026-07-10 provider-truth offer reconciliation:** device-native offer accept now revalidates the provider's live call state before it accepts, no longer marks the interaction connected before the provider says it is connected, and a new provider-ended-offer reconciler clears stale queue, reservation, and agent state as soon as a pre-connect call ends. **2026-07-10 Asterisk tenant-stream sync:** the Asterisk module now runs a tenant-scoped ARI event listener through Orchard shell events, maps server-side channel changes into normalized voice events, and also closes plain Telephony soft-phone interactions in real time when Asterisk disconnects them outside the browser. **2026-07-11 provider-authoritative soft-phone state:** Telephony command responses no longer mutate browser state or broadcast `CallStateChanged`; provider events and provider-state lookups now drive all active call transitions, including server-side accept, hold, mute, and hangup recovery. **2026-07-11 event-ordering hardening:** command-triggered active-call polling was removed, page-restoration lookups now yield to newer provider events, stale terminal events cannot clear a newer call id, and Asterisk lookup verifies the channel still exists after its multi-request snapshot. **2026-07-11 inbound terminal repair:** provider-event ingestion now recovers provider-name aliases by call id, canonicalizes the live provider identity, moves answered calls into wrap-up, and completes their assigned queue item so a missed provider-name match cannot leave the agent Busy or block later queue work. **2026-07-11 event-pipeline + self-healing hardening:** the Asterisk live listener now isolates every event dispatch (a malformed payload, unroutable event, or transient tenant-scope failure while the shell reloads is logged and skipped instead of tearing down the WebSocket and dropping subsequent real-time events) and subscribes to all application events (`subscribeAll`) so every channel change reaches provider-truth ingest; ended-offer reconciliation now cancels **every** non-terminal reservation bound to the activity (not just the queue-referenced one) and clears stale agent reservation pointers even for answered calls; the healer never requeues a provider-backed **ringing** interaction (it is left under provider control and released only when provider truth confirms it ended); and manual presence changes self-heal against provider truth so an agent parked as **Busy** after a call the provider already ended can return to **Available** immediately while a genuinely live call is still preserved. **2026-07-12 agent workflow completion:** soft-phone presence transitions now use the Contact Center hub without page reloads, keypad numbers are formatted and persisted while normalized values are dialed, Enter starts a call, the Work selectors use explicit empty placeholders, and the shared activity-completion screen renders contact details, editable subject context, activity metadata, notes, dispositions, and scheduled subject actions. Live Asterisk validation covered Power wrap-up/disposition, manual dialing, manual CRM completion, sign-out/sign-in, and presence transitions. **2026-07-13 multi-call refinement:** conference controls are selection-driven, active-call rows are compact, the bounded Keypad scrolls, dial input clears safely, Asterisk clears stale hold markers on conference merge, conference transfer requires one explicit interaction selection, and capability-gated Asterisk/DialPad transfer directories are available. **2026-07-13 multi-party follow-up:** the merge contract accepts any number of selected call ids, Asterisk bridges all selected channels, DialPad merges each additional call sequentially, every participant is labeled **In conference**, and Recent numbers are formatted. Remaining: reason-code deployment-plan step, broader automated dialer outcome coverage, browser coverage for supervisor controls, and broader provider adoption of the tenant-safe live-stream pattern where a backend requires server-side sockets.)
- [~] Phase 8 — Inbound entry points, IVR and self-service (**entry point layer shipped 2026-07-01:** `ContactCenterEntryPoint` catalog — DID→queue mapping, priority, business-hours gating, closed action hold/voicemail/overflow/reject, welcome/closed messages; `IEntryPointResolver`/`EntryPointRoutingPlanner` + router integration; **Interaction Center → Entry points** admin CRUD. **Local test harness shipped 2026-07-08:** `src/Startup/CrestApps.OrchardCore.Asterisk.Web` signs in to Orchard, originates one or more Asterisk channels into the configured Stasis application, waits for the matching `StasisStart` events, and then forwards normalized `InboundVoiceEvent` payloads using the real Asterisk channel ids so routing and agent offers can be exercised through the local PBX flow. The sample dashboard now also surfaces provider-tracked hold/mute state plus inferred party counts, moves live notifications beside the raw ARI payload drill-down to free more width for active-call tables, and lowers the default polling cadence to one second so state changes show up faster. The simulator now makes it explicit that **To address** drives inbound queue or entry-point routing while **Caller number seed** only changes caller identity generation. **2026-07-08 queue hardening:** queues now expose an unanswered-offer policy so timed-out voice offers can requeue, send the live call to voicemail, or reject the live call; when no live provider call exists the timeout safely falls back to requeue. **2026-07-11 Asterisk sample synchronization:** the simulator now enforces the configured provider identity instead of accepting a stale form alias, and the dashboard refreshes independent ARI resources plus per-channel hold/mute enrichment concurrently after a short live-event coalescing window. Remaining: multi-step IVR/self-service decision trees, menu/DTMF capture, estimated-wait/position announcements, and ambiguous-contact disambiguation.)
- [~] Phase 9 — Recording and live monitoring (**orchestration shipped 2026-07-01:** `IContactCenterRecordingService` tracks `Interaction.RecordingState` (none/recording/paused/stopped) with `Recording*` events; `IContactCenterMonitoringService.EngageAsync` performs scoped, audited monitor/whisper/barge/take-over gated by new `Recording`/`Monitor`/`Whisper`/`Barge`/`TakeOver` provider capability flags, publishing `SupervisorMonitorStarted`. Remaining: provider media execution of recording + monitoring, consent/retention policy, recording storage/access audit, and quality-management scorecards.)
- [~] Phase 10 — Outbound compliance hardening (**callbacks shipped 2026-07-01:** `CallbackRequest` model + store/manager + `ICallbackService` schedule/promote-due + per-minute `CallbackDispatchBackgroundTask` that turns due callbacks into outbound `Callback`-source activities and enqueues them. Remaining: abandonment-rate caps, answering-machine detection outcomes, calling-window calendars, and predictive metrics before Predictive can be re-enabled.)
- [x] Phase 11 — Optional Workflow bridge (**shipped 2026-07-01:** feature-gated `[RequireFeatures("OrchardCore.Workflows")]` bridge — a `ContactCenterEvent` workflow event activity (optional event-type filter; Matched/Ignored outcomes; exposes event type + interaction/aggregate/actor as input) and a `ContactCenterWorkflowEventHandler` that triggers it for every published domain event via `IWorkflowManager`.)
- [~] Phase 12 — Analytics and operations (**daily event-metrics projection shipped 2026-07-01:** `ContactCenterEventMetric` + `IContactCenterMetricsService` (record + range summary) + `ContactCenterMetricsProjectionHandler` projecting every domain event into rebuildable per-day, per-event-type counts in the base feature. **Reusable Reports framework shipped 2026-07-02:** new `CrestApps.OrchardCore.Reports` module (`IReport` registry + display-driver-extensible `ReportFilter` with a from/to range + uniform `ReportDocument` renderer + pluggable `IReportExportFormat`/CSV) surfaced under a single top-level **Reports** admin menu grouped by category. **2026-07-12 enterprise report catalog:** Contact Center Analytics and Omnichannel Management now contribute 79 runnable reports organized for executives, operations, queues/routing, agents, workforce/payroll, billing/usage, CRM/campaigns, compliance/audit, and technical/IT. Shared formulas keep inbound offered, abandonment, ASA, AHT, transfer rate, recording/transcript coverage, service level, completion, attempt, observed presence, utilization, occupancy, cycle-time, and usage metrics consistent, with tenant-local date/time and dimensional filters applied consistently to display and exports. Presence-derived timecards and billing reports expose auditable measured durations/counts without inventing schedules, pay rates, prices, or invoice totals. **2026-07-12 executive charts:** the reusable report document now supports responsive line, bar, stacked-bar, and doughnut sections with tabular CSV/Excel fallbacks, and the executive performance dashboard combines KPI hero cards with daily demand/outcome, channel-mix, queue-SLA, and agent-workload charts. **2026-07-13 report display-name correction:** campaign and disposition reports continue to resolve catalog display names, while user table cells now render the cached Orchard `UserDisplayName` shape directly. The username remains the default text and the optional User Display Name feature replaces only the text subshape through `IDisplayNameProvider`; stable user ids remain grouping keys, and usernames are resolved at report time rather than stored in activity indexes. **2026-07-13 campaign-group aggregation:** Omnichannel now owns a Campaign Group catalog and optional campaign association; Contact Center and CRM campaign filters can select a group, and campaign reports include group aggregates while retaining individual campaign rows. Group membership is resolved at report time from the campaign catalog. **2026-07-13 queue-group aggregation:** Contact Center queue groups organize the catalog and report output only; queue-oriented reports filter interactions through each queue's current group membership, Queue Usage includes group aggregates plus a semantic grand total, and no routing, SLA, entitlement, or queue behavior inherits from the group. **2026-07-13 semantic report totals:** every Contact Center aggregate table includes a `GrandTotal` row recomputed from the filtered raw population; queue-group, campaign-group, and daily timecard layers include `Subtotal` rows; raw detail/event tables remain unmodified; and CSV/Excel retain all rows. Report module code now follows `Reports/Drivers`, `Reports/Models`, `Reports/Providers`, `Reports/Services`, and `Reports/ViewModels` namespaces. **2026-07-12 wrap-up analytics:** interactions now persist wrap-up start/completion timestamps, Call Insights reports total wrap-up time and includes wrap-up in average handle time, Agent Productivity reports average and total wrap-up, and durable presence events capture previous/current/requested state plus queue/campaign memberships for future adherence and staffing calculations. Remaining: projected multi-million-row aggregation, SLA/adherence trend snapshots, operational alerts, scheduled delivery, linked drill-down navigation, per-agent self-service report scoping, and safe decomposition of the enterprise provider into cohesive interaction-performance, agent, usage/compliance, and detail/audit builders.)
- [~] Phase 13 — Scale-out, resilience and data governance (**event retention shipped 2026-07-01:** `IContactCenterRetentionService` batch-purges interaction events older than a cutoff, driven by a daily `ContactCenterRetentionBackgroundTask` from `ContactCenterRetentionOptions.InteractionEventRetentionDays` (config-bound; 0 = keep forever). Multi-node safety via per-queue/per-user distributed locks already shipped in G2. **2026-07-10 voice restart reconciliation:** providers can now expose per-call live-state lookup through `ITelephonyCallStateProvider`, Contact Center runs a reconciliation pass during tenant activation and on a periodic background task, and active provider-backed interactions are revalidated against current provider truth after restarts so stale queued or assigned voice work is cleared before routing continues. **2026-07-11 plain Telephony recovery:** Telephony now performs the same provider-authoritative recovery during tenant activation, every minute, on Asterisk listener reconnect, and when the soft phone restores; confirmed missing provider calls delete orphaned in-progress Telephony records and disconnect clients, while transient lookup failures retain state for retry. Remaining: SignalR backplane guidance/config, projection rebuild tooling, PII redaction + right-to-erasure, retention for call sessions/metrics/recordings, and full replay/bootstrap strategies for providers that need server-pushed event streams.)
- [~] Phase 14 — Advanced capabilities (**AI assist seam shipped 2026-07-01:** `IContactCenterAssistProvider` + `IContactCenterAssistService` orchestrate optional summarization + disposition-suggestion providers by order, decoupled from any specific AI provider. **AI voice media foundation shipped 2026-07-13:** voice providers can advertise `BidirectionalMedia`; the optional `IContactCenterVoiceMediaProvider`/`IContactCenterVoiceMediaSession` contracts describe negotiated PCM/G.711 formats and asynchronous incoming/outgoing frames; the resolver requires both the advertised capability and a matching implementation; and Asterisk now supplies the reference ARI External Media RTP/UDP adapter with G.711 mu-law framing, bridge attachment, and cleanup. **Transport validation added 2026-07-13:** scripted ARI lifecycle tests cover bridge reuse/creation, External Media parameters, endpoint resolution, owned-resource cleanup, cancelled-open cleanup, and cleanup-failure exception preservation; loopback UDP tests cover RTP sender filtering, malformed-packet rejection, receive/send framing, sequence/timestamp continuity, and stop semantics; and an automated outbound reminder batch verifies call-insight and campaign-summary report aggregation. **Live Asterisk bridge simulation shipped 2026-07-13:** the standalone dashboard originates two configurable Stasis endpoints, waits for both channels, connects them through a real mixing bridge, reports the created resources, and cleans up partial failures. The Aspire dialplan provides two synthetic tone-producing Local endpoints, and the same UI accepts registered PJSIP endpoints for two-device testing. A live container run confirmed four Local channel legs, two connected logical calls, and one shared mixing bridge. DialPad remains unavailable for AI media. Remaining: transport-neutral AI conversation engine, no-human-reservation AI dialer path, media-session recovery/capacity, concrete AI provider (summaries/disposition/sentiment), virtual-agent handoff, and AI routing recommendations.)

### Gap-closure backlog (from the 2026-06-30 design review)

> **Historical and superseded by the 2026-07-13 R0-R9 remediation checklist.** Checkmarks record shipped increments, not current production approval.

Ordered by the former design-review execution order. Numbers reference the historical P0/P1 findings.

- [x] **G1 — Voice foundation hardening (completes Phase 4):** redesign the voice-provider boundary with delivery models (`AgentDeviceNative` vs `ServerSideAcd`), capability-gated lifecycle ops, and a normalized `ProviderVoiceEvent`/`IProviderVoiceEventHandler`; add a `CallSession` aggregate; add a unified Contact Center call-command service that delivers media to the agent on inbound accept and outbound answer as one atomic, audited transition. *(P0 #1, #2, #3; P1 #9)* — **Backend shipped (2026-06-30):** `VoiceProviderDeliveryModel` + `ContactCenterConnectRequest` + `ConnectToAgentAsync` + `AgentConnect` capability on `IContactCenterVoiceProvider` (DialPad declares `AgentDeviceNative`); `CallSession` model/index/store/manager/migration registered in the base feature; `ProviderVoiceEvent` + `IProviderVoiceEventService` idempotent ingestion that advances interaction+call-session and bridges answered outbound calls on server-side ACD; `IContactCenterCallCommandService` accept/decline wired into `VoiceController`; +5 unit tests (41 ContactCenter tests pass, clean `-warnaserror` build). **Completed (2026-07-01):** the soft-phone JS now awaits the Contact Center accept and only answers the agent device when the accepted offer reports `RequiresDeviceAnswer`, so a lost race no longer answers a re-offered call (asset rebuilt); per-provider signed webhook adapters and the transfer/conference taxonomy shipped earlier the same day. G1 is complete.
- [~] **G2 — Assignment safety (hardens Phases 2/3):** distributed-lock/single-writer assignment + optimistic concurrency on reservation; enforce `MaxConcurrentInteractions`; split a live `AgentSession` from `AgentProfile` with SignalR heartbeat + stale cleanup; drive offer timeout from the real-time layer. *(P0 #6, #7; P1 #13, #14)* — **P0 core shipped (2026-06-30):** per-queue distributed-lock single-writer assignment in `ActivityAssignmentService` (both `AssignNextAsync` and `AssignQueueAsync` acquire the lock; inbound `OfferNextAsync` routes through the same path), and `MaxConcurrentInteractions` enforcement via the new `CapacityRoutingStrategy` (Order 20, between required-skills and longest-idle) backed by `IInteractionManager.CountActiveByAgentAsync`. +6 unit tests (47 ContactCenter tests pass, clean `-warnaserror` build). **P1 #13 shipped (2026-06-30):** the live `AgentSession` aggregate (model/index/store/manager + `IAgentSessionService`) is now split from `AgentProfile`, the `ContactCenterHub` registers each SignalR connection on the session with a per-user distributed lock, the client sends a `Heartbeat` every 30s, and the `AgentSessionCleanupBackgroundTask` signs out + deletes sessions whose heartbeat is older than 90s so routing stops targeting a dead client (a brief reconnect is tolerated by the grace window). **Remaining:** the real-time per-reservation offer timeout (P1 #14) driven from the desktop (the SignalR foundation + `ServerTimeUtc`/`ExpiresUtc` on the offer notification now exist; the background reservation-expiry task remains the safety net). **(Compare-and-set on reservation creation shipped 2026-07-01: `ReserveAsync` re-reads the queue item and aborts unless it is still `Waiting`.)**
- [x] **G3 — Completion unification via the Subject Flow (completes Phase 6):** make the Subject Flow the single decision controller and route every completion through `IActivityDispositionService`. *(P0 #8; P1 #10)* — **Shipped (2026-06-30):** completion already flows through the source-neutral `IActivityDispositionService` for CRM, inbound, and outbound, which marks the activity `Completed` regardless of its prior contact-center state (P0 #8) and runs the disposition-driven Subject Actions. Added `SubjectFlowSettings.RequireDisposition` (edited on the **Configure** screen) enforced centrally in `IActivityDispositionService` so completion is blocked until a disposition is chosen on every path; completion now also skips Subject Actions when no disposition is selected. **Reversal of the earlier wrap-up implementation:** the `WrapUp` feature / `WrapUpSession` aggregate added on 2026-06-30 was removed as redundant with disposition + subject flow (per the maintainer's "single concept" direction); after-call agent timing/capacity release is deferred to the Phase 7 agent desktop + agent presence. +3 disposition tests (47 ContactCenter + 4 disposition tests pass, clean `-warnaserror` build).
- [~] **G4 — Dialer safety (completes Phase 5, pulls forward Phase 10 essentials):** strategy-per-mode (`IDialerStrategy`), an `IDialerEligibilityService`/compliance gate (DNC, communication preferences, calling windows, retry cool-down, suppression audit), cap Power, and block Predictive until metrics + abandonment controls exist. *(P0 #4, #5)* — **Shipped (2026-06-30):** `IDialerStrategy` + `IDialerStrategyResolver` with `PowerDialerStrategy` (hard-capped `MaxCallsPerAgent`) and `ProgressiveDialerStrategy`; `DialerService` now validates the profile and delegates pacing to the resolved strategy, so Manual/Preview stay agent-driven and **Predictive is blocked** (hidden in the editor, rejected on save, and refused at runtime). The single-attempt path moved to `IDialerAttemptService`, which calls the new `IDialerEligibilityService` (`DefaultDialerEligibilityService`) before every attempt: destination present, attempt limit, retry cool-down (last interaction end + `RetryDelayMinutes`), contact `DoNotCall` communication preference, configurable calling window evaluated in the contact's time zone, and any registered `INationalDoNotCallRegistry`. Suppressed attempts release the reservation and publish an auditable `DialSuppressed` event (DNC/registry cancel the activity; window/cool-down leave it available). Added calling-window settings to `DialerProfile`/editor. +16 dialer unit tests (66 ContactCenter tests pass; clean `-warnaserror` build). **Remaining (P2/Phase 10):** abandonment caps, AMD outcomes, and predictive metrics before Predictive can be re-enabled.
- [~] **G5 — Agent desktop + supervisor real-time UX (Phase 7):** CRM-integrated agent cockpit, supervisor dashboards, queue monitor/wallboard, and scoped/audited live call-control intents. *(P1 #15, #16)* — **Started (2026-06-30):** shipped the canonical **agent state reason codes** prerequisite — a catalog-backed `AgentStateReasonCode` admin surface (Agents feature, **Interaction Center → Agent states**) following the Skills/TimeZones catalog pattern, an `AgentStateReasonCode` recipe step, a seed recipe executed at setup via `IRecipeMigrator`, and soft-phone presence-dropdown integration. **Real-time SignalR layer shipped (2026-06-30):** the new `RealTime` feature (`CrestApps.OrchardCore.ContactCenter.RealTime`, depends on `Queues` + the `SignalR` module) adds the `ContactCenterHub` (`Hub<IContactCenterHubClient>`) with per-user, per-queue, and `cc:supervisors` groups + `WatchQueue`/`UnwatchQueue`; the live `AgentSession` aggregate (split from `AgentProfile`) with `IAgentSessionService` connect/disconnect/heartbeat; the `Heartbeat`-driven `AgentSessionCleanupBackgroundTask`; the `GetSnapshot` reconnect snapshot (`AgentDesktopSnapshot`); `IContactCenterRealTimeNotifier` + the `ContactCenterRealTimeEventHandler` event projection that broadcasts presence (`PresenceChanged`), offers (`OfferReceived`/`OfferRevoked`), and queue depth (`QueueStatsChanged`); the `contact-center-realtime` client script resource; and the `MonitorContactCenter` permission + default **Supervisor** role. +13 unit tests (79 ContactCenter tests pass; clean `-warnaserror` build). **Agent desktop + supervisor dashboard shipped (2026-07-01):** a CRM-integrated **Agent Workspace** (`AgentWorkspaceController` + `Views/AgentWorkspace` + `agent-workspace.js`) at **Interaction Center → My workspace** binds to the hub and a live `State` endpoint to show presence + reason-code control, live queue depth, the ringing offer card with a countdown and one-click Accept (server accept+connect) / Decline, the active-interaction panel with a live talk timer, customer 360 link, and a **Complete activity** link into the shared Omnichannel activity-completion page, and recent history; and a **Supervisor Dashboard** (`SupervisorDashboardController` + `supervisor-dashboard.js`) at **Interaction Center → Live dashboard** (gated by `MonitorContactCenter`) with live summary metrics, per-queue SLA-health tiles, and an agent presence board. **Hardened (2026-07-01):** offer accept/decline commands are bound to the current agent, failed provider connect compensates by canceling the accepted reservation, provider call-end events move the agent into `WrapUp`, the CRM completion page accepts assigned contact-center states and runs the same source-neutral disposition flow, agent/supervisor names resolve through `IDisplayNameProvider`, and supervisor cards now expose provider-gated **Monitor/Whisper/Barge/Take over** actions backed by `IContactCenterMonitoringService`. **Multi-call controls shipped (2026-07-13):** Telephony restores all provider-authoritative active calls, permits a second dial while the selected call is held, lists active interactions by phone number/state, conferences two selected calls without exposing provider ids, and distinguishes selected-call hang-up from disconnect-all. Live Asterisk validation placed two synthetic calls into one mixing bridge. Agent-only conference departure remains unimplemented because the shared contract has no distinct agent media leg or leave-conference command. **Remaining:** reason-code deployment-plan step and browser coverage for the core agent/supervisor flows. This real-time layer also unblocks the remaining G2 item (real-time per-reservation offer timeout, P1 #14).
- [~] **G6 — Eventing/outbox + provider webhooks (hardens Phase 1, extends Phase 4):** outbox dispatch + retry/backoff, projection checkpoints, mandatory idempotency on provider events, rebuildable projections, and signed per-provider webhook adapters. *(P1 #17, #18)* — **Outbox shipped (2026-06-30):** Contact Center event dispatch is now at-least-once. `DefaultContactCenterEventPublisher` records the immutable `InteractionEvent` then delegates handler dispatch to the new `IContactCenterOutbox`/`ContactCenterOutbox`, which runs handlers inline and, on any handler failure, persists a durable `ContactCenterOutboxMessage` (`Pending`/`DeadLettered`, attempt count, next-attempt time, last error) via `IContactCenterOutboxStore`. The per-minute `OutboxDispatchBackgroundTask` calls `DispatchDueAsync`, re-running all handlers with exponential back-off (30s→30m cap) and dead-lettering after `MaxAttempts` (10); a missing referenced event is dead-lettered. Handlers must be idempotent (the shipped handlers are). +6 outbox tests + reworked 5 publisher tests (85 ContactCenter tests pass; clean `-warnaserror` build). **Remaining:** projection checkpoints + rebuildable read-model projections. **(Idempotency-key enforcement + signed per-provider webhook adapters shipped 2026-06-30 — see change log.)**
- [~] **G7 — Routing depth (completes Phase 3):** `RoutingPolicy`, `QueueMembership`, skill proficiency, business-hours/holiday calendars, overflow, sticky agent, priority/SLA-aging strategies. *(P1 #11)* — **Shipped (2026-06-30):** per-queue routing policy on `ActivityQueue` (`RoutingStrategy` = LongestIdle/RoundRobin/LeastBusy, `PreferStickyAgent`, `EnableSlaAging`, `BusinessHoursCalendarId` + `AfterHoursAction`, `OverflowQueueId` + `OverflowAfterSeconds`); `StickyAgentRoutingStrategy` (boosts the activity's last assigned user, captured on `QueueItem.StickyAgentUserId` at enqueue), `RoundRobinRoutingStrategy` (orders by new `AgentProfile.LastAssignedUtc`, stamped on reserve), and `LeastBusyRoutingStrategy` (orders by active interaction count) — each gated so only the queue's selected primary strategy scores; `QueueItemPrioritizer` SLA-aging item selection in the assignment path; a reusable `BusinessHoursCalendar` catalog (model/index/store/manager + `IBusinessHoursService` weekly-schedule + holiday + time-zone evaluation; module index provider/migration/handler/driver/controller/admin menu/views under **Interaction Center → Business hours**) that pauses assignment while closed; and `IActivityQueueService.OverflowDueAsync` (wait-time + after-hours overflow re-homing, publishing `QueueItemOverflowed`) run by the reservation/assignment background task. Queue editor extended with all new fields. +21 unit tests (106 ContactCenter tests pass; clean full-solution `-warnaserror` build). **Remaining:** skill **proficiency** levels (requires migrating agent skills from a name list to a proficiency map), a standalone `QueueMembership` aggregate (membership is still modeled via `AgentProfile.QueueIds`), and bullseye/skill-relaxation overflow expansion.
- [ ] **G8 — Inbound entry points/IVR (Phase 8), recording/monitoring (Phase 9), compliance hardening (Phase 10), analytics (Phase 12)** per the existing phase plan once G1–G7 are stable.

### Change log

- 2026-07-13: Reorganized Contact Center report module code into report-scoped Drivers, Models, Providers, Services, and ViewModels namespaces. Added raw-population semantic grand totals to every Contact Center aggregate table, campaign-group and daily-timecard subtotals alongside the existing queue-group subtotals, and targeted weighted-rate/duration tests. Detail interaction lists, exception lists, and presence audits intentionally remain total-free. The monolithic enterprise provider switch remains private and behavior-stable for this pass; the next safe architecture boundary is to extract cohesive interaction-performance, agent, usage/compliance, and detail/audit builders that receive one filtered report context and are resolved through DI, while `IReport` remains the public extension point and `AddEnterpriseReport` remains private.
- 2026-07-13: Added `ActivityQueueGroup` as a catalog-only queue classification with permission-gated admin CRUD, optional queue association, queue-editor selection, and summary display. Queue-group deletion clears current queue memberships. Reporting resolves group membership from the current queue catalog, so moving a queue changes historical group attribution without changing interactions, routing, SLA settings, entitlements, or queue behavior. Queue Usage now includes queue-group subtotal rows and a grand total, and queue-oriented interaction reports expose consistent queue-group filtering.
- 2026-07-13: Made the C001/C002 R0a capability-truth reproductions explicit: recording currently changes state and publishes success without an executable provider recording contract, while supervisor monitoring currently publishes success from a capability flag without invoking a mode-specific provider operation. R4 must invert these characterizations to NotSupported/provider-execution assertions.
- 2026-07-13: Added the first R0a in-process falsification for S001: two shell identities now publish through one Contact Center hub context and must resolve to distinct tenant-qualified user and supervisor destinations. Added a source-level group-join ordering guard that requires queue/supervisor authorization before any matching group join and aborts unauthorized connections before adding the tenant-qualified user destination. Production Redis/backplane evidence remains an R0b dependency.
- 2026-07-13: Added the R0a static feature-dependency characterization (F001/F002/T001). `ContactCenterFeatureDependencyArchitectureTests` parses the relevant manifests and every Contact Center `StartupBase` class, checks recognized generic registrations against manifest and `[RequireFeatures]` closures, pins the three known graph violations in `.github/contact-center/feature-dependency-violations.v1.json`, and validates current transitive feature closures. Factory/non-generic registrations and live tenant activation remain T001 dependencies; production feature-boundary remediation remains deferred to R2.
- 2026-07-13: Published the PR-to-test control matrix in `.github/contact-center/pr-test-control-matrix.v1.json` covering all 41 current P0/P1 gates (C001-C008 correctness, D001-D009 data, F001-F006 feature/package graph, O001-O006 operations, S001-S005 security, T001-T003 test/topology, V001-V004 voice/provider). Every gate resolves a category DRI role, approver roles, a stable test id, a CI job (implemented, partial, or explicitly planned), provider/database/topology execution context, a falsifiable invariant, and a retained-evidence location. Added `ContactCenterPrTestControlMatrixTests` so a missing/duplicate P0-P1 id, an unresolved DRI/approver, or a missing execution-context/evidence field fails the build.
- 2026-07-13: Pinned the R0 clean-tree baseline at commit `ccb1076d` in `.github/contact-center/R0-BASELINE.md`: strict Release build with zero warnings/errors, 1,472 unit tests, 24 Telephony browser tests, asset and documentation builds, toolchain versions, and SHA-256 hashes for the generated TRX evidence.
- 2026-07-13: Published `support-matrix.v1.json` as the finite candidate GA contract. Release remains blocked through R8; the initial Tier-1 target is PostgreSQL 16, one region, two to four application nodes with Redis, one Asterisk or DialPad provider profile per tenant, Manual/Preview dialing only, and a 100-agent/50-voice-interaction per-tenant envelope. Added contract tests and public production-support documentation.
- 2026-07-13: Published `service-objectives.v1.json` for the Tier-1 profile: 30-day availability/error-budget targets, p95/p99 routing/notification/webhook/dashboard objectives, database/Redis/provider limits, 1 MiB ingress ceiling, relational and rebuildable-state RPO/RTO, and accountable DRI/approver roles. Named individuals must fill those roles before release approval.
- 2026-07-13: Added manager-owned agent queue/campaign entitlements under **Interaction Center → Agent entitlements**. Agent-selected memberships are constrained centrally, soft-phone options and reconnect snapshots expose only entitled memberships, routing requires both live membership and a manager grant, and entitlement removal prunes the live agent session, removes active connections from revoked SignalR queue groups, and refreshes the connected membership snapshot while preserving presence and reservation state.
- 2026-07-13: Challenged the production-readiness plan with Claude Opus 4.8 and GPT-5.6 Terra. Incorporated tenant-qualified SignalR user targeting, a headless Availability feature, durable after-call deadlines, canonical provider identity, portable WorkState CAS, provider `OutcomeUnknown` commands, finite support/feature matrices, feature quiesce/re-enable, split R0a/R0b reproductions, earlier SLO/ownership decisions, replay-horizon retention rules, fail-closed optional media capabilities, and separate GA versus higher-scale certification.
- 2026-07-13: Completed an independent production-readiness review across Orchard feature composition, multi-tenancy, domain/persistence, provider/telephony, security, operations, scalability, testing, CI/CD, and documentation. The commercial release decision is blocked. Added evidence-backed P0/P1 gates, a normative target feature graph, canonical state/outbox/inbox decisions, a red-green-refactor remediation sequence R0-R9, required feature/database/browser/distributed/security/load/chaos/upgrade suites, capacity acceptance rules, and production release gates. Reopened historical completion claims where tenant isolation, authorization, feature dependencies, atomicity, provider capability execution, compliance, observability, data governance, or production proof remain incomplete.
- 2026-07-13: Refined multi-call soft-phone conference and transfer behavior. The conference control appears after two or more active interactions are selected, active-call number/state share one compact row, the Keypad scrolls within a bounded height, and dial input clears after submission and when held-call dialing becomes available. The shared merge contract accepts any number of call ids. Asterisk clears persisted hold markers and adds all selected channels to one mixing bridge; DialPad merges each additional selected call sequentially into the primary call. Every merged participant displays **In conference**. Conference transfer remains hidden until one interaction is explicitly selected. Added the provider-neutral `Directory` capability and `ITelephonyDirectoryProvider`; Asterisk lists ARI endpoints and DialPad lists paginated company users for transfer destination selection.
- 2026-07-13: Added a real two-party Asterisk dashboard simulation. The dashboard now originates two configurable endpoints into `crestapps-dashboard`, waits for both Stasis channels, creates an ARI mixing bridge, joins both parties, and refreshes live telemetry; partial setup failures clean up channels and bridges. Aspire adds synthetic `Local/2001@crestapps-simulation` and `Local/2002@crestapps-simulation` parties with distinct tone patterns, while the same form can ring configured PJSIP endpoints. A live local Asterisk run produced four Local legs, two connected logical calls, and one shared bridge.
- 2026-07-13: Started the AI voice implementation with an explicit provider capability seam. `ContactCenterVoiceProviderCapabilities.BidirectionalMedia` now announces true two-way live media support, while the separate `IContactCenterVoiceMediaProvider` and `IContactCenterVoiceMediaSession` contracts keep provider transport and framing outside Contact Center orchestration. Resolution requires both the capability flag and a matching media provider registration. Selected ARI External Media over RTP/UDP as the first Asterisk transport because it has broader version compatibility than newer WebSocket media transports; Asterisk now implements that transport while DialPad remains unavailable for AI media.
- 2026-07-13: Implemented the Asterisk ARI External Media RTP/UDP adapter. The provider now advertises `BidirectionalMedia`, creates or reuses the live call bridge, attaches an `ulaw` external-media channel, resolves Asterisk's RTP return endpoint, parses and emits RTP packets, and removes media resources without hanging up the provider call. Session metadata supplies the Asterisk-reachable Orchard host and optional local bind address/port; DialPad remains capability-disabled.
- 2026-07-13: Moved report export actions out of the filter panel and into a view-only toolbar on the right side of the first visible report section heading. The toolbar and heading are not serialized into report data; CSV begins with data headings, Excel uses the report title for worksheet tabs, and all active filter inputs remain part of export requests.
- 2026-07-13: Corrected report user rendering so each user table cell renders Orchard Core's cached `UserDisplayName` shape instead of eagerly flattening the entire report document. Browser rendering preserves the enclosing admin layout, while the export resolver explicitly suppresses layout rendering so CSV and Excel receive only the resolved username/display-name text rather than the surrounding admin page. Reports show the username through the default text shape and use `IDisplayNameProvider` only when the optional User Display Name feature contributes its `UserDisplayNameText.Report` alternate. Omnichannel activity reports group by stable user ids, resolve current usernames in one report-time user query, and no longer duplicate usernames in the activity index; the migration removes the temporary username columns from upgraded tenants.
- 2026-07-12: Completed an end-to-end agent workflow hardening pass against the `blog1` tenant and local Asterisk stack. Presence changes now run over SignalR without reloading or leaving the soft phone spinning; phone inputs format national and international numbers, dial normalized destinations, and support Enter-to-dial without storing phone numbers in browser storage; active connected numbers are restored from authoritative call state after reload or reconnect. Work selectors show `Select queue(s)` and `Select campaign(s)` until the agent explicitly selects memberships. Answered calls enter Wrap-up when provider truth reports termination, interaction history records wrap-up start/completion, activity disposition returns the signed-in agent to Available or a pending break, and reports include wrap-up and handle-time metrics. The shared CRM completion screen now renders read-only contact details, editable subject context, activity metadata, notes, dispositions, and subject-action scheduling. Dialer reservations now identify agent-facing dialer sources in their live offer notification, automatically open the assigned activity completion page, and carry a local-only return URL back to My workspace; inbound offers continue to wait for explicit accept or decline, and manual activity completion continues to default to Activities. Live validation completed a Power attempt through Asterisk, dispositioned it from Wrap-up, completed a Manual activity, changed presence without navigation, signed out/in to queue and campaign memberships, and placed/disconnected a normalized manual call from the keypad. Automated Power/Progressive reservations are excluded from generic inbound recovery, and outbox handler checkpoints no longer create same-scope YesSql identity-map failures.
- 2026-07-11: Repaired the live stale-capacity failure found in the `blog1` tenant. Provider reconciliation now treats a terminal `CallSession` as the invariant when the paired interaction has drifted back to a nonterminal state, restores the interaction's terminal status/timestamps, and runs ended-offer cleanup so dead queue assignments, reservations, and agent capacity are released even when the original terminal provider event's idempotency key is already recorded. Answered-call cleanup also idempotently starts wrap-up, covering partial failures that persisted terminal provider state before the normal presence transition. Follow-up reproduction showed Asterisk can mark the inbound caller leg answered before an agent accepts; ended-offer cleanup now uses accepted reservation/assigned queue state instead of that provider timestamp, removes terminal waiting/reserved items, releases the temporarily reserved agent, and lets the bounded offer loop continue to the next live call. The dashboard delay was independently traced to `/js/signalr.min.js` returning `404`: the linked output asset resolved to a nonexistent source path, so the browser used its 15-second polling fallback even though server snapshots took only milliseconds. Asterisk Web now builds the SignalR client into its own physical `wwwroot`. Assignment, sign-in/membership, and Asterisk dashboard refresh paths now emit structured trace details; the Work tab omits false boolean option attributes and shows explicit empty-selection placeholders; and single-framework builds use `TargetFramework` while multi-target overrides retain `TargetFrameworks`, so Aspire project resources start successfully without breaking cross-targeting.
- 2026-07-11: Hardened inbound queue routing and real-time operations after local Asterisk testing. New queue/activity writes are flushed before the immediate YesSql-backed assignment query, eliminating the read-after-write gap that reported no agent even when an eligible signed-in agent was available. Inbound results now distinguish durable `queued` work from immediately `routed` work, and the simulator labels waiting calls instead of presenting them as rejected. The soft-phone Work tab now validates empty sign-in attempts, displays signed-in queue/campaign names, and supports per-membership sign-out. Supervisor queue tiles now expose signed-in, available, busy/reserved/wrap-up, and other not-ready staffing counts. The Asterisk Web dashboard serves SignalR locally and polls only while the SignalR connection is unavailable.
- 2026-07-11: Inbound contact phone lookup remains scoped to published contact versions after the shared omnichannel contact index became version-aware.
- 2026-07-11: Closed the remaining "zombie inbound offer" gap where a queued call whose Asterisk channel no longer existed kept being offered and accepted, leaving the agent stuck (unable to disconnect, unable to receive new calls). Three provider-truth guards were added and unit-tested: (1) `VoiceContactCenterCallRouter.OfferNextAsync` now refreshes each queued interaction against provider truth in a bounded loop and, when the provider confirms the call is gone, removes it from the queue and releases the reservation/agent via `ReconcileEndedOfferAsync` before offering the next call — a dead call is never offered; (2) `ContactCenterCallCommandService` accept-media failures now re-check provider truth and reconcile a confirmed-gone call (removing the offer and releasing the agent) instead of leaving an accepted reservation stuck on a Cancel no-op; (3) `ProviderVoiceEventService` now seeds a freshly created call session with the interaction's pre-event state instead of the incoming terminal state, so a first-observed terminal event (e.g. a reconcile of an already-gone queued call) still records a non-terminal→terminal transition, publishes `CallEnded`, and runs ended-offer cleanup instead of silently leaving the interaction queued. +3 unit tests (1,279 total pass, clean `-warnaserror` build). Confirmed the Asterisk dashboard is already event-driven end to end (ARI `subscribeAll` forwarder → 50ms-coalesced SignalR snapshot → client `dashboardSnapshot` handler with reconciliation-poll fallback); the observed lag is provider emission/network, not a code defect.
- 2026-07-11: Corrected the recurring inbound zombie path found in live logs. Agent reset/sign-in healing now reconciles provider-backed work before touching routing state, retries stale provider aliases through the tenant's current default provider, terminalizes calls the provider no longer reports, and preserves calls the provider confirms are still active instead of resetting them to `Created` and requeueing answered work. The soft phone now performs an authoritative active-call refresh when a terminal event arrives for a different call id than its stale browser state. The Asterisk development dashboard event stream now subscribes to all visible ARI resources so channel and bridge changes request immediate SignalR snapshots.
- 2026-07-11: Eliminated zombie soft-phone calls by making provider events and provider-state lookup the only authorities for active call state. Telephony command responses no longer mutate the browser or broadcast `CallStateChanged`; active-call restoration validates the provider instead of trusting `TelephonyInteraction.Outcome`, and a new locked reconciliation service runs at tenant startup, every minute, after Asterisk listener reconnects, and during soft-phone reconnect/page restoration. Provider-confirmed missing calls delete orphaned in-progress Telephony records and disconnect connected clients, while transient lookup failures preserve the record for retry. Asterisk terminal projection now ignores duplicate terminal events and ambiguous down-state bridge-leave snapshots, and duplicate Contact Center events retain Contact Center ownership.
- Codebase analysis completed for Telephony, Omnichannel Core, Omnichannel Management, SMS automation, SignalR docs, target bundle, solution structure, docs, and tests.
- Plan reviewed and expanded to add inbound entry points/IVR, call recording, live monitoring (silent monitor/whisper/barge/take-over), outbound compliance hardening, quality management, a standard terminology/metrics glossary, scale-out/high-availability, data retention/privacy, a testing strategy, and a migration strategy. Phases renumbered to 0-14.
- Phase 0 started: promoted the session plan to this durable repo-tracked document, added the copilot-instructions pointer, and created the public Contact Center docs landing page.
- Phase 0 completed and Phase 1 (Domain foundation) implemented: added the `ContactCenter.Abstractions`, `ContactCenter.Core`, and `ContactCenter` base module projects; extended `OmnichannelActivity` with kind/source/assignment/reservation metadata so CRM activities remain the universal work item; made `Interaction` an Orchard `Entity` communication-history record linked to activities; added the durable `InteractionEvent` log with idempotency and the `DefaultContactCenterEventPublisher`; added the `IActivityDispositionService` contract; registered everything in `.slnx` and the `Cms.Core.Targets` bundle; added 13 unit tests; and documented the feature on the docs landing page and the `v2.0.0` changelog. The base feature is headless by design — all future CRUD/agent/supervisor UI must use Display Management, display drivers, shapes, placement, and AI Profile-style catalog screens. Next: Phase 2 (agents, presence, activity queues, reservations).
- Load Inventory source selection implemented: **Add Inventory Load** now opens a source modal like AI Provider Connections, sources are registered through `ActivityBatchSourceOptions`, and source cards use shape alternates. Manual inventory loads keep the selected-user assignment flow. Dialer inventory loads hide the user selector and load activities as unassigned `Available` dialer inventory for later reservation by dialer/routing services.
- Phase 2 implemented and Phases 3/5 started: added `Agents`, `Queues`, and `Dialer` features in the ContactCenter module. Core now has AgentProfile/ActivityQueue/QueueItem/ActivityReservation/DialerProfile models, indexes, stores, and managers, plus presence/queue/reservation/assignment/dialer orchestration services. Assignment pairs the highest-priority waiting item with the longest-idle available agent (Phase 3 core); reservations expire via background task. Outbound dialing initially used `IDialerProvider`/`IDialerProviderResolver`, power/progressive pacing, dialer batch sources, and a `DialPad.Dialer` provider; this was later corrected so outbound voice calls route through `IVoiceContactCenterCallRouter` and `IContactCenterVoiceProvider`. Added admin CRUD for queues/dialer profiles, agent/queue/dialer permissions, and 7 new unit tests (20 total pass). Docs + `v2.0.0` changelog updated. Next: skills/sticky/business-hours routing (Phase 3), voice integration (Phase 4), retry/callback/suppression (Phase 5), wrap-up (Phase 6).
- 2026-06-29: Phase 3 routing advanced with an extensible `IActivityRoutingStrategy` pipeline, required-skills eligibility, longest-idle scoring, and `RoutingDecisionMade` audit events that capture candidate scores and reasons. Contact Center voice-provider resolution was added through `IContactCenterVoiceProviderResolver`; outbound dialer failure paths now cancel reservations and enforce max-attempt boundaries; inbound offer failures release reservations immediately; agent sign-in clears stale reservations and serializes profile creation. Queue and dialer admin UI now uses Orchard `ocat-*` layout and exposes routing skills, inbound endpoint mapping, and retry/do-not-call settings. Added routing/dialer/reservation tests and updated docs/changelog. Next: sticky-agent and business-hours routing, then wrap-up timers and required-disposition policies before Phase 7 real-time desktop work.
- 2026-07-08: Renamed the local Asterisk-specific startup sample to `CrestApps.OrchardCore.Asterisk.Web` and moved its inbound simulator to a Stasis-managed PBX flow. The sample now originates each test call into the configured Asterisk Stasis application, listens for the matching `StasisStart` event over ARI, and only then forwards the normalized `InboundVoiceEvent` to the authenticated Contact Center ingress endpoint using the real Asterisk channel id. This keeps the dashboard, ARI event stream, and Contact Center ingress aligned on the same provider call during local routing and agent-offer testing.
- 2026-07-08: Converted the Contact Center's JSON/result-oriented voice, workspace, supervisor, and provider-webhook routes from MVC controller actions to Minimal API endpoint registrations while preserving their existing URLs, route names, permissions, and antiforgery behavior. This keeps the feature aligned with the repository's endpoint pattern and leaves the remaining MVC controllers focused on HTML surfaces only.
- 2026-07-08: Adjusted the Minimal API inbound voice ingress endpoint to explicitly disable antiforgery so the local Dialer Simulator can continue posting authenticated JSON `InboundVoiceEvent` requests without the 403 regression introduced during the endpoint conversion.
- 2026-07-09: Hardened the soft-phone widget UX and reconnect flow. The Telephony widget now remembers the selected Keypad/Recent/Work tab across reloads, waits for the real provider status before showing the unconfigured warning, and keeps a stable body height across tabs. Contact Center Voice now re-checks already-waiting inbound voice queues after the soft phone reconnects, so a refresh or reconnect no longer leaves queued calls waiting indefinitely for the newly reconnected agent.
- 2026-07-10: Added a dedicated Contact Center technical architecture guide for inbound voice routing, outbound dialer flow, provider-truth synchronization, and restart reconciliation, and removed the last explicit Telephony soft-phone feature-id compatibility alias so the WIP voice surface no longer carries that obsolete constant.
- 2026-07-11: Fixed the live Asterisk event path discovered during end-to-end testing. The detached ARI listener now creates explicit tenant shell scopes instead of relying on an absent ambient `ShellScope`, so provider-side disconnects continue through normalized server events to the soft phone without a refresh. Dial acknowledgements remain `Connecting`, pending commands are de-duplicated in the widget, and Telephony removes active records whose old provider is no longer registered or enabled instead of leaving an uncontrollable zombie interaction. The local inbound simulator concurrently drains Stasis events, bounds Orchard forwards, recognizes tenant-prefixed failed logins, and reconciles a missed `StasisStart` from an authoritative matching ARI channel snapshot before reporting a timeout. The Aspire host now passes `--framework net10.0` to all multi-target project resources so the complete local stack actually starts.
- Phase 4 inbound voice MVP + Phase 6 disposition unification implemented. **Telephony:** added an incoming-call extensibility contract (`IncomingCallCard`/`IncomingCallContext`/`IncomingCallContributionContext`, `IIncomingCallContextProvider`, `IIncomingCallDispatcher`), implemented `DefaultIncomingCallDispatcher` over `IHubContext<TelephonyHub, ITelephonyClient>` (`Clients.User`), added a capability-gated voicemail operation (`ITelephonyProvider.SendToVoicemailAsync` + `TelephonyCapabilities.Voicemail`, implemented in DialPad), a `Voicemail` hub method, and a soft-phone incoming-call modal (Answer/Voicemail/Ignore + contributed contact cards with an Answer-and-open shortcut and accept/decline lifecycle posts). **Contact Center:** added the `Voice` feature with `InboundVoiceEvent` and the `VoiceContactCenterCallRouter`/`IInboundVoiceService` compatibility seam (resolves endpoint→subject flow→queue, looks up the contact by caller number, creates the inbound `OmnichannelActivity` + Subject + voice `Interaction`, enqueues, reserves the longest-idle available agent, and offers the call via the dispatcher; re-offers on decline), `ContactCenterIncomingCallContextProvider` (lists matched customers + wires accept/decline URLs), `IInboundContactLookup`, a provider-agnostic ingress endpoint (`POST /api/contact-center/voice/inbound`), an offer-lifecycle `VoiceController`, and an optional `ActivityQueue.InboundChannelEndpointId` mapping. **Disposition unification:** implemented `DefaultActivityDispositionService` (the previously contract-only `IActivityDispositionService`) and routed `ActivitiesController.Complete` through it so inbound and outbound activities disposition through the same subject workflow. Added 5 unit tests (25 ContactCenter tests pass); full solution builds clean with `-warnaserror`. Docs (`telephony/index.md`, `contact-center/index.md`) and the `v2.0.0` changelog updated. Next: transfer/conference taxonomy + standalone call-session aggregate (Phase 4), wrap-up timers + required-disposition policies (Phase 6), agent desktop/supervisor real-time UX (Phase 7).
- 2026-06-29: Contact Center admin entries were moved under the Omnichannel Management **Interaction Center** menu, and the Contact Center base feature now depends on `CrestApps.OrchardCore.Omnichannel.Managements`. Agent sign-in campaign and skill fields now use managed catalog data; campaigns come from the Interaction Center campaign catalog. Added a managed Contact Center Skills catalog and **Interaction Center → Skills** CRUD UI; agent sign-in and queue selectors now read from that catalog. Skill, Queue, and Dialer Profile admin CRUD now follows the Omnichannel Campaigns display-driver pattern with catalog summary/editor shapes and the required root `*.Edit.cshtml` wrapper templates. The Telephony soft phone is shown on admin pages by default. The Voice feature now exposes `IVoiceContactCenterCallRouter` as the inbound and outbound voice routing boundary, the dialer routes outbound calls through that router, and DialPad registers an `IContactCenterVoiceProvider` implementation while Telephony remains the media execution layer.
- 2026-06-29: The standalone agent sign-in admin navigation item was removed from the Contact Center menu. Telephony soft-phone extensibility now uses `DisplayDriver<SoftPhoneWidget>` zones for contributed tabs/views, and Contact Center contributes a **Work** tab for queue/campaign sign-in, sign-out, and presence so agent availability controls stay inside the soft phone instead of a separate navigation page.
- 2026-06-30: Agent routing skills were clarified as administrator-owned eligibility data, not agent-selected sign-in data. The soft-phone **Work** tab should only let agents choose queues/campaigns, while presence belongs in a soft-phone header dropdown. Request break is system-approved: new routing decisions must not target agents in request-break/break states, but an already-made assignment continues and grants Break after release. Agent state/reason-code CRUD should be added as a catalog-backed admin surface with recipe and deployment steps, seeded by executing a module recipe during tenant setup. Dialer Profile remains useful as an outbound execution policy tying campaign inventory to queue, dialing mode, provider, pacing, and retry/compliance settings; it must not become the source of activity/business workflow configuration.
- 2026-06-30: Full design review of the shipped Contact Center code against industry-standard cloud contact center / dialer platforms. Added the **"Design review: closing the gap to a state-of-the-art dialer"** section (verdict, P0/P1/P2 findings with code citations, recommended voice-provider boundary redesign, and a phase-mapped execution order) and a **Gap-closure backlog (G1–G8)** in the progress checklist. Headline findings, all verified in code: (1) media is never bridged to the selected agent on inbound accept or outbound answer — the platform creates CRM/interaction records and offers work but never connects the customer to the agent; (2) the soft-phone "Answer" is two uncoordinated, best-effort actions (fire-and-forget accept POST + separate Telephony `Answer`); (3) `IContactCenterVoiceProvider` is too thin and lacks delivery models, lifecycle ops, and normalized provider events; (4) Progressive/Predictive dialer modes are exposed but unsafe; (5) configured outbound compliance (`RespectDoNotCall`/`RetryDelayMinutes`/calling windows/suppression) is not enforced; (6) `MaxConcurrentInteractions` capacity is ignored in routing; (7) assignment/dequeue is not multi-node concurrency-safe; (8) `ActivitiesController.Complete` only accepts `NotStated`, so contact-center-state activities cannot be completed. Phase 4/5/6 checklist entries were annotated with these blockers. No code changed in this pass — planning only. Next: start G1 (voice foundation hardening / media delivery).
- 2026-06-30: **G2 P0 core implemented (assignment safety).** Closed the two P0 correctness blockers in the assignment path. (P0 #7) `ActivityAssignmentService` now serializes per-queue assignment under a distributed lock: both `AssignNextAsync` and `AssignQueueAsync` acquire `ContactCenterQueueAssignment:{queueId}` (10s acquire / 30s expiration), looping a lock-free `AssignNextCoreAsync` so the single-call inbound path (`OfferNextAsync`) and the per-minute reservation-expiry drain can no longer double-assign the same item or agent across nodes; if the lock is not acquired the call returns no assignment and the holder drains the queue. (P0 #6) Added `CapacityRoutingStrategy` (Order 20, between `RequiredSkillsRoutingStrategy` and `LongestIdleRoutingStrategy`) that rejects any candidate whose active interaction count has reached `MaxConcurrentInteractions` (treating an unset/zero value as 1), backed by the new `IInteractionStore`/`IInteractionManager.CountActiveByAgentAsync` (counts interactions for the agent that are not `Ended`/`Failed`); registered in `QueuesStartup`. Added 6 unit tests — capacity strategy (at/under/unset capacity, skips already-ineligible candidates), an end-to-end routing test proving a busy longest-idle agent is skipped for a free agent, and an assignment lock-skip test — for 47 ContactCenter tests passing; ContactCenter projects build clean with `-warnaserror`. Docs (`contact-center/agents-queues-dialer.md`) and the `v2.0.0` changelog updated. **Remaining for G2:** optimistic concurrency/compare-and-set on reservation creation, and the live `AgentSession` split from `AgentProfile` with SignalR heartbeat + stale-session cleanup + capacity counters (P1 #13) and the real-time per-reservation offer timeout (P1 #14) — both depend on the not-yet-built real-time layer (G5). Next: G3 (wrap-up + completion unification) or the real-time layer (G5) that unblocks the rest of G2.
- 2026-06-30: **G3 implemented (wrap-up + completion unification — completes Phase 6).** Added the `WrapUp` feature (`CrestApps.OrchardCore.ContactCenter.WrapUp`, depends on `Queues`). New `WrapUpSession` aggregate (model + `WrapUpSessionStatus` enum + index + store + manager + index provider + migration) following the `CallSession` pattern. Queues gained a wrap-up policy (`RequireDispositionOnWrapUp` + `WrapUpTimeoutSeconds`) edited through a feature-gated `WrapUpQueuePolicyDisplayDriver` contributed to the queue editor. `IContactCenterWrapUpService`/`ContactCenterWrapUpService` provides `StartAsync` (idempotent per activity; applies queue policy; sets the agent to `WrapUp`), `CompleteAsync` (enforces required disposition, otherwise routes completion through the source-neutral `IActivityDispositionService` — which accepts contact-center activity states like `InProgress`/`AwaitingAgentResponse`, fixing **P0 #8** — and releases agent capacity), and `ExpireDueAsync` (auto-completes non-required sessions, marks required sessions `TimedOut`, and releases the agent in both cases so a missed wrap-up never strands an agent). Wrap-up starts automatically via a `StartWrapUpOnCallEndedHandler` (`IContactCenterEventHandler`, the first implementation of that seam) that reacts to `CallEnded` only for answered, agent-handled interactions, and a `WrapUpExpiryBackgroundTask` closes expired sessions every minute. Added wrap-up events (`WrapUpStarted`/`DispositionRequired`/`WrapUpCompleted`/`WrapUpTimedOut`). Added 9 unit tests (56 ContactCenter tests pass); ContactCenter projects build clean with `-warnaserror`. Docs (`contact-center/index.md`) and the `v2.0.0` changelog updated. **Deferred:** the agent-facing wrap-up UI (Phase 7) and subject-/campaign-level required-disposition overrides (queue-level shipped). Next: G4 (dialer safety — strategy-per-mode, compliance/eligibility gate, cap Power, block Predictive), then G5 (agent desktop + supervisor real-time UX), which also unblocks the rest of G2.
- 2026-06-30: **G3 simplified — wrap-up removed; Subject Flow confirmed as the single decision controller (maintainer direction).** The maintainer questioned the value of the `WrapUp`/`WrapUpSession` concept given that dispositions + subject flow already exist, and asked for a single completion concept across CRM, inbound, and outbound. Investigation confirmed the unified concept already exists: a **Subject** + its **Subject Flow** (Configure settings + disposition-driven `SubjectAction`s on Manage Flow), completed through the source-neutral `IActivityDispositionService`, which applies the disposition, marks the activity `Completed`, and runs the Subject Actions — identical for inbound and outbound, whether the disposition is applied by an agent, AI, or the system. **Decision:** the wrap-up code shipped earlier today is redundant. Removed the entire `WrapUp` feature (`WrapUpSession` model/enum/index/store/manager/index-provider/migration, `IContactCenterWrapUpService`/impl, `StartWrapUpOnCallEndedHandler`, `WrapUpExpiryBackgroundTask`, the queue-policy driver/view-model/view, `WrapUpStartup`, the Manifest feature, the `Feature.WrapUp` + wrap-up event constants, the two `ActivityQueue` wrap-up fields, and the 9 wrap-up tests). Replaced the "required disposition" need with `SubjectFlowSettings.RequireDisposition` (edited on the Configure screen) enforced centrally in `DefaultActivityDispositionService` so the rule applies to every completion path for inbound and outbound; also fixed `ApplyAsync` to skip Subject Actions (instead of throwing) when no disposition is selected. After-call agent timing/capacity release is deferred to the Phase 7 agent desktop + agent presence, not a domain aggregate. +3 disposition tests; full solution builds clean with `-warnaserror`; docs (`contact-center/index.md`, `v2.0.0` changelog) updated.
- 2026-06-30: **G4 implemented (dialer safety — completes Phase 5).** Closed P0 #4 and #5. Each automated dialing mode is now a dedicated `IDialerStrategy` resolved by `IDialerStrategyResolver`: `PowerDialerStrategy` (per-cycle pacing hard-capped at `PowerDialerStrategy.MaxCallsPerAgent`) and `ProgressiveDialerStrategy` (one call per available agent, bounded by a safety max), sharing a `DialerStrategyBase` reserve-then-dial loop. `DialerService` was refactored to validate the profile, confirm outbound routing, and delegate to the resolved strategy — Manual/Preview stay agent-driven, and **Predictive is blocked** end-to-end (hidden in the editor, rejected on save via the display driver, and refused at runtime because no strategy resolves). The single-attempt logic moved out of `DialerService` into `IDialerAttemptService`/`DialerAttemptService`, which now runs the new `IDialerEligibilityService` compliance gate before every attempt. `DefaultDialerEligibilityService` (module, so it can use `IContentManager`, `IPhoneNumberService`, and optional `INationalDoNotCallRegistry` providers) enforces: destination present, attempt limit, retry cool-down (last interaction `EndedUtc` + `RetryDelayMinutes`), the contact's `DoNotCall` communication preference, a configurable calling window evaluated in the contact's time zone (falling back to the profile's default zone), and national do-not-call registries. Suppressed attempts release the reservation and publish an auditable `DialSuppressed` event with a `DialerSuppressionEventData` payload; do-not-call/registry suppressions cancel the activity while window/cool-down suppressions leave it available for a later cycle. Added calling-window fields to `DialerProfile` + view model + editor (and removed the Predictive option). New abstractions: `DialerSuppressionReason` enum and the `DialSuppressed` event constant. The ContactCenter module now references `CrestApps.OrchardCore.DncRegistry.Abstractions`. Added 16 dialer unit tests (eligibility rules, strategy pacing/caps, attempt-service dial + suppression audit, and `DialerService` mode routing) for **66 ContactCenter tests passing**; ContactCenter module builds clean with `-warnaserror`. Docs (`contact-center/agents-queues-dialer.md`) and the `v2.0.0` changelog updated. **Remaining (Phase 10/P2):** abandonment caps, answering-machine detection, calling-window calendars, and predictive metrics before Predictive can be re-enabled; callback model + callback queues and dialer run/attempt projections for Phase 5. Next: G5 (agent desktop + supervisor real-time UX), which also unblocks the rest of G2.
- 2026-06-30: **G5 started (Phase 7) — agent state reason codes.** Implemented the catalog-backed agent state reason-code surface that the change log flagged as the next concrete step and that the agent-desktop/supervisor presence UX depends on. Added `AgentStateReasonCode` (Core: model, index, `IAgentStateReasonCodeStore`/store, `IAgentStateReasonCodeManager`/manager — stored in the isolated ContactCenter collection like the Skills catalog) with `Name`, `Description`, `AppliesTo` (the `AgentPresenceStatus` it sets), `SortOrder`, and `Enabled`. Module side mirrors the Skills/TimeZones patterns: index provider, migration (creates the table and seeds the standard reason codes via `IRecipeMigrator.ExecuteAsync("agent-state-reason-codes.recipe.json")`), `AgentStateReasonCodeHandler` (timestamps + validation + `PopulateAsync` for recipe JSON binding), display-driver CRUD (`AgentStateReasonCodeDisplayDriver` + view model + edit/summary/list views), `AgentStateReasonCodesController` (**Interaction Center → Agent states**, `ManageContactCenterAgents`), `ContactCenterAgentsAdminMenu`, the `AgentStateReasonCode` recipe step (`AgentsRecipesStartup`, `[RequireFeatures("OrchardCore.Recipes.Core")]`), and the seed recipe in `Migrations/`. The ContactCenter module now references `OrchardCore.Recipes.Abstractions`. Soft-phone integration: the presence header renders the enabled reason codes (ordered by sort order) as per-reason mini-forms that post `status = AppliesTo` + `presenceReason = Name`, falling back to the built-in not-ready states when none are configured; the soft-phone widget driver resolves the reason-code manager optionally (`IEnumerable<IAgentStateReasonCodeManager>`) so the Queues-feature widget still works when Agents is off. Added 2 recipe-step unit tests (79 ContactCenter/recipe tests pass); clean `-warnaserror` Release build; docs (`contact-center/agents-queues-dialer.md`) and the `v2.0.0` changelog updated. **Remaining for G5/Phase 7:** the real-time SignalR layer (hub + tenant/user/queue/team groups + reconnect snapshots), the CRM-integrated agent desktop, supervisor dashboards, the queue monitor/wallboard, scoped/audited live call-control intents, and a reason-code deployment-plan step. The real-time layer also unblocks the remaining G2 items (live `AgentSession` + heartbeat + stale cleanup + real-time offer timeout).
- 2026-06-30: **G5 advanced + G2 #13 closed — real-time SignalR layer (Phase 7).** Implemented the real-time foundation that the previous entry flagged as the next concrete step. Added the `CrestApps.OrchardCore.ContactCenter.RealTime` feature (depends on `Queues` + the `CrestApps.OrchardCore.SignalR` module; the module now references the SignalR project with `PrivateAssets="none"` and the `CrestApps.Core.SignalR` package). **Core:** the live `AgentSession` aggregate (model/index/`IAgentSessionStore`+store/`IAgentSessionManager`+manager, in the isolated ContactCenter collection) is split from the administrator-owned `AgentProfile` and tracks open SignalR connection ids, online flag, heartbeat, and queue/campaign snapshot; `IAgentSessionService`/`AgentSessionService` orchestrates `ConnectAsync`/`DisconnectAsync`/`HeartbeatAsync` (serialized per user with a distributed lock), builds the `AgentDesktopSnapshot` reconnect payload, and `ExpireStaleAsync` signs out + deletes sessions whose heartbeat is older than 90s (closing P0 #6/#13's "closed browser stays Available" bug, with a grace window for refresh/reconnect). **Module:** the `ContactCenterHub` (`Hub<IContactCenterHubClient>`, `[Authorize]`) registers connections, joins per-user/per-queue/`cc:supervisors` groups, exposes `Heartbeat`/`GetSnapshot`/`WatchQueue`/`UnwatchQueue`, and runs each call in a child shell scope authorized by `ContactCenterSignIntoQueues` (agents) or the new `MonitorContactCenter` (supervisors); the `AgentSession` index provider + migration; the `AgentSessionCleanupBackgroundTask` (per-minute); `IContactCenterRealTimeNotifier`/`ContactCenterRealTimeNotifier` over `IHubContext<ContactCenterHub, IContactCenterHubClient>`; and the `ContactCenterRealTimeEventHandler` (`IContactCenterEventHandler`) that projects `AgentSignedIn/Out`/`AgentPresenceChanged` → `PresenceChanged`, `AgentReserved` → `OfferReceived`, `AgentReleased`/`QueueItemAssigned` → `OfferRevoked`, and `QueueItemAdded`/`QueueItemDequeued` → `QueueStatsChanged` to the affected agent, queue watchers, and supervisors. Added a hand-written static `wwwroot/scripts/contact-center-realtime.js` client helper registered as the `contact-center-realtime` resource (depends on `signalr`) that connects, heartbeats every 30s, fetches the snapshot, and dispatches the strongly-typed callbacks. Added the `MonitorContactCenter` permission + a default **Supervisor** role stereotype. Added 13 unit tests (`AgentSessionServiceTests`, `ContactCenterRealTimeEventHandlerTests`) for **79 ContactCenter tests passing**; ContactCenter projects build clean with `-warnaserror`. Docs (`contact-center/index.md` real-time section, `agents-queues-dialer.md` feature table/recipe, `v2.0.0` changelog) updated. **Remaining for G5/Phase 7:** the CRM-integrated agent desktop UI, supervisor dashboards, queue monitor/wallboard, scoped/audited live call-control intents, and a reason-code deployment-plan step; and the remaining G2 item — the real-time per-reservation offer timeout (P1 #14) driven from the desktop (the SignalR foundation + `ServerTimeUtc`/`ExpiresUtc` on the offer notification now exist; the background reservation-expiry task remains the safety net). Next: the agent desktop UI + supervisor dashboards on top of this hub, or G6 (eventing/outbox + signed provider webhooks).
- 2026-06-30: **G6 started — reliable event dispatch (outbox) (P1 #17).** Made Contact Center domain-event dispatch at-least-once so a transient handler failure no longer silently drops an event. **Core:** added the `ContactCenterOutboxMessage` aggregate (`OutboxMessageStatus` Pending/DeadLettered, `EventId`, `EventType`, `AttemptCount`, `NextAttemptUtc`, `LastError`), its index, and `IContactCenterOutboxStore`/`ContactCenterOutboxStore` (`ListDueAsync`); and `IContactCenterOutbox`/`ContactCenterOutbox`, which owns handler execution: `DispatchAsync` runs every `IContactCenterEventHandler` inline (per-handler try/catch isolation) and, on any failure, persists a Pending retry message; `DispatchDueAsync` reloads the event, re-runs handlers, deletes the message on success, applies exponential back-off (base 30s, ×2 per attempt, capped at 30m) on failure, and dead-letters after `MaxAttempts` (10) or when the referenced event is gone. `DefaultContactCenterEventPublisher` was refactored to stamp + idempotency-dedupe + persist the immutable `InteractionEvent`, then delegate to the outbox (it no longer loops handlers itself). **Module:** registered the outbox store/service, the outbox index provider + migration, and the per-minute `OutboxDispatchBackgroundTask` in the base feature `Startup`. Reworked the 5 publisher tests (now assert persist + delegate-to-outbox + idempotency skip + stamping) and added 6 `ContactCenterOutboxTests` (success no-retry, failure schedules Pending + still runs other handlers, retry-succeeds-deletes, retry-fails-backoff, max-attempts dead-letters, missing-event dead-letters) for **85 ContactCenter tests passing**; ContactCenter projects build clean with `-warnaserror`; docs (`contact-center/index.md` "Domain events and reliable dispatch", `v2.0.0` changelog) updated. **Remaining for G6:** mandatory idempotency-key enforcement on provider-sourced events; projection checkpoints + rebuildable read-model projections; and signed per-provider webhook adapters (P1 #18) that validate provider signatures, dedupe, and normalize to `ProviderVoiceEvent` before the pipeline (`VoiceIngressController` still only accepts authenticated, pre-normalized internal posts). Next: signed provider webhook adapters (P1 #18) to finish G6, or G7 (routing depth).
- 2026-06-30: **G7 advanced — routing depth completes Phase 3 (P1 #11).** Turned single-strategy routing into a per-queue routing policy. **Core:** extended `ActivityQueue` with `RoutingStrategy` (`QueueRoutingStrategy` = LongestIdle/RoundRobin/LeastBusy), `PreferStickyAgent`, `EnableSlaAging`, `BusinessHoursCalendarId` + `AfterHoursAction` (`QueueAfterHoursAction`), and `OverflowQueueId` + `OverflowAfterSeconds`; added `QueueItem.StickyAgentUserId`/`OverflowedFromQueueId` and `AgentProfile.LastAssignedUtc`. Added three routing strategies — `StickyAgentRoutingStrategy` (Order 30, additive boost for the activity's last assigned user, captured on the queue item when enqueued), `RoundRobinRoutingStrategy` and `LeastBusyRoutingStrategy` (Order 100) — and gated `LongestIdleRoutingStrategy`/RoundRobin/LeastBusy so only the queue's selected primary strategy scores. Added `QueueItemPrioritizer` (SLA aging: effective priority rises one step per SLA interval past threshold) used by `ActivityAssignmentService` to pick the next item; the assignment service now also pauses while the queue's business-hours calendar reports closed (new `IClock` + `IBusinessHoursService` deps). `ActivityReservationService` stamps `LastAssignedUtc` on reserve. Added a reusable `BusinessHoursCalendar` catalog (`BusinessHoursCalendar`/`BusinessHoursDay` models, index, store/manager mirroring Skills) and `IBusinessHoursService`/`DefaultBusinessHoursService` (time-zone-aware weekly-schedule + holiday evaluation). `IActivityQueueService.OverflowDueAsync` re-homes waiting items to the overflow queue (wait-time threshold or after-hours), publishing the new `QueueItemOverflowed` event; the reservation-expiry background task calls it per queue before assigning. **Module:** registered the strategies + business-hours store/manager/service/index/migration/handler/driver; added the `BusinessHoursCalendarsController` + **Interaction Center → Business hours** admin menu + Index/Edit/Create/summary views (`ManageQueues`); extended the queue editor (view model + driver + options provider + `ActivityQueueFields.Edit.cshtml`) with the routing-strategy select, sticky/SLA-aging toggles, business-hours + after-hours selects, and overflow queue + seconds. Added 21 unit tests (`BusinessHoursServiceTests`, `QueueItemPrioritizerTests`, `RoutingStrategyTests`, `ActivityQueueServiceTests`) and updated `ActivityAssignmentServiceTests` for the new ctor — **106 ContactCenter tests passing**; full solution builds clean with `-warnaserror`. Docs (`contact-center/agents-queues-dialer.md` routing-policy/business-hours/overflow sections + feature table, `v2.0.0` changelog) updated. **Remaining for G7:** skill **proficiency** levels (needs an agent-skill model migration), a standalone `QueueMembership` aggregate (membership still via `AgentProfile.QueueIds`), and bullseye/skill-relaxation overflow expansion. Next: finish G6 (signed provider webhook adapters, P1 #18), or build the Phase 7 agent desktop UI on the real-time hub.
- 2026-07-01: **G6 advanced — signed provider webhook adapters + idempotency enforcement (P1 #18).** Added a provider-grade voice webhook boundary. New `IProviderVoiceWebhookAdapter` (Abstractions) validates a provider signature and normalizes a delivery into `ProviderVoiceEvent`s from an HTTP-agnostic `ProviderVoiceWebhookRequest` (headers/body/query); `HmacProviderVoiceWebhookAdapterBase` provides constant-time HMAC-SHA256 verification with configurable secret + header. `IProviderVoiceWebhookProcessor`/`ProviderVoiceWebhookProcessor` (Core) resolves the adapter by technical name, verifies the signature, **requires an idempotency key on every parsed event** (rejects the batch otherwise), and forwards accepted events to `IProviderVoiceEventService`. The anonymous `ProviderVoiceWebhookController` (`POST /api/contact-center/voice/webhook/{provider}`) reads the raw body, maps the outcome to 200/401/404/400, and is authenticated purely by the provider signature. Registered in `VoiceStartup`. +6 tests (`ProviderVoiceWebhookProcessorTests`: valid/keyed ingest, unknown provider, invalid signature, missing key, HMAC match/tamper/no-secret) for **112 ContactCenter tests passing**; clean `-warnaserror` build; docs (`v2.0.0` changelog) updated. **Remaining for G6:** projection checkpoints + rebuildable read-model projections. Next: G5 agent desktop UI, or G2 optimistic concurrency on reservation.
- 2026-07-01: **G2 hardened — compare-and-set on reservation (P0 #7).** `ActivityReservationService.ReserveAsync` now re-reads the queue item by id and aborts (returns null, creates no reservation) unless it is still `Waiting`, so even if two writers slip past the per-queue lock the second cannot reserve an already-reserved item. +1 test (`ReserveAsync_WhenItemNoLongerWaiting_AbortsWithoutReserving`); updated the two existing reserve tests to mock the re-fetch. **113 ContactCenter tests pass**; clean `-warnaserror` build. Remaining G2: real-time per-reservation offer timeout (needs the G5 desktop).
- 2026-07-01: **G1 advanced — transfer/conference taxonomy (completes Phase 4 orchestration).** Added `InteractionTransferType` (Blind/Consultative) and `InteractionTransferTargetType` (Agent/Queue/External/EntryPoint) enums, `TransferRequest`/`TransferResult` models, and `IContactCenterTransferService`/`ContactCenterTransferService` which records the transfer on `Interaction.TransferHistory`, sets the interaction `Transferring`, publishes `InteractionTransferred`, and re-enqueues the activity into the target queue for queue transfers (agent/external/entry-point record intent; media handoff stays a provider concern). Added `CallTransfer` + `Conference` capability flags to `ContactCenterVoiceProviderCapabilities`. Registered in `VoiceStartup`. +3 tests (`ContactCenterTransferServiceTests`) for **116 ContactCenter tests passing**; clean `-warnaserror` build; changelog updated. Remaining G1: soft-phone JS accept-then-answer coordination + asset rebuild (client-side, not unit-testable here).
- 2026-07-01: **Phase 8 started — inbound entry points.** Added the `ContactCenterEntryPoint` catalog (Core: model/index/store/manager) mapping dialed numbers (DIDs) to a target queue + priority, gated by a business-hours calendar, with a `ClosedAction` (HoldInQueue/Voicemail/Overflow/Reject) and welcome/closed messages. `IEntryPointResolver`/`EntryPointResolver` matches the DID and, via `IBusinessHoursService`, builds an `EntryPointRoutingPlan` (testable `EntryPointRoutingPlanner`). Wired into `VoiceContactCenterCallRouter`: open calls route to the entry point queue at its priority; closed calls hold/overflow or return not-routed for voicemail/reject; falls back to endpoint→queue mapping when no entry point matches. Module: index provider/migration/handler/display driver/`EntryPointsController`/**Interaction Center → Entry points** admin menu + views (Voice feature, `ManageQueues`). +6 tests (`EntryPointResolverTests`) for **122 ContactCenter tests passing**; clean `-warnaserror` build; changelog updated. Remaining Phase 8: multi-step IVR decision trees, DTMF/menu capture, estimated-wait announcements, ambiguous-contact disambiguation. Next: Phase 10 (compliance: callbacks + calling-window calendars + abandonment) or Phase 9 (recording).
- 2026-07-01: **Phase 10 started — callbacks (Phase 5/10 remnant).** Added the `CallbackRequest` catalog (Core: model/index/store/manager) with `CallbackRequestStatus` (Pending/Scheduled/InProgress/Completed/Canceled/Failed). `ICallbackService`/`CallbackService` schedules pending callbacks (`CallbackScheduled` event) and `PromoteDueAsync` turns each due pending callback into an outbound `ActivitySources.Callback` `OmnichannelActivity`, enqueues it when a queue is set, marks it `Scheduled`, and publishes `CallbackPromoted`. Per-minute `CallbackDispatchBackgroundTask` runs promotion; registered in the Dialer feature with index provider + migration. +3 tests (`CallbackServiceTests`) for **125 ContactCenter tests passing**; clean full-solution `-warnaserror` build; changelog updated. Remaining Phase 10: abandonment caps, AMD outcomes, calling-window calendars, predictive metrics. Next: Phase 12 (analytics projections) or Phase 9 (recording).
- 2026-07-01: **Phase 12 started — daily event-metrics projection.** Added `ContactCenterEventMetric` (Core: model/index/store) keyed by (DateKey `yyyy-MM-dd` + Date + EventType), `IContactCenterMetricsService`/`ContactCenterMetricsService` (`RecordAsync` find-or-create+increment, `GetSummaryAsync` range totals by event type), and `ContactCenterMetricsProjectionHandler` (`IContactCenterEventHandler`) that records every published domain event. Registered in the always-on base feature (store/service/handler + index provider + migration). Because it derives from the durable event log it is rebuildable. +4 tests (`ContactCenterMetricsServiceTests`) for **129 ContactCenter tests passing**; clean `-warnaserror` build; changelog updated. Remaining Phase 12: per-queue/agent/campaign breakdowns, SLA/adherence snapshots, wallboard/report UI, exports. Next: Phase 9 (recording) or Phase 11 (workflow bridge).
- 2026-07-01: **Phase 9 started — recording + live-monitoring orchestration.** Added `RecordingState` + `MonitorMode` enums, `Interaction.RecordingState`, and provider capability flags `Recording`/`Monitor`/`Whisper`/`Barge`/`TakeOver`. `IContactCenterRecordingService` drives the recording state machine (start/pause/resume/stop) with `RecordingStarted/Paused/Resumed/Stopped` events; `IContactCenterMonitoringService.EngageAsync` performs a capability-gated, audited supervisor engagement (`SupervisorMonitorStarted` with mode+supervisor) and refuses unsupported modes. Registered in the Voice feature. +4 tests for **133 ContactCenter tests passing**; clean full-solution `-warnaserror` build; changelog updated. Remaining Phase 9: provider media execution, consent/retention, recording storage/access audit, QM scorecards. Next: Phase 11 (workflow bridge) or G5 (agent desktop UI).
- 2026-07-01: **Phase 11 complete — optional Workflows bridge.** Added an `OrchardCore.Workflows.Abstractions` package reference and a feature-gated (`[RequireFeatures(\"OrchardCore.Workflows\")]`) bridge: the `ContactCenterEvent` workflow event activity (`EventActivity` with an optional `EventType` filter, Matched/Ignored outcomes, +display driver + view model + Fields.Edit/Design/Thumbnail views) and `ContactCenterWorkflowEventHandler` (`IContactCenterEventHandler`) that triggers `TriggerEventAsync(nameof(ContactCenterEvent), input, correlationId)` for every published domain event, exposing event type + interaction/aggregate/actor/source as workflow input. Registered in `ContactCenterWorkflowsStartup`. Module builds clean; **133 ContactCenter tests still pass** (workflow activities are integration-tested, not unit-tested); changelog updated. Next: Phase 13 (scale-out/resilience/data governance) or Phase 14 (AI assist), or G5 agent desktop UI.
- 2026-07-01: **Phase 13 started — data-governance retention.** Added `ContactCenterRetentionOptions` (`InteractionEventRetentionDays`, bound from the `CrestApps_ContactCenter:Retention` shell config section), `IContactCenterRetentionService`/`ContactCenterRetentionService` that batch-purges interaction events older than a cutoff (`IInteractionEventStore.ListOlderThanAsync` added, querying the indexed `OccurredUtc`), and a daily `ContactCenterRetentionBackgroundTask` that computes the cutoff and purges when retention is enabled. Registered in the base feature (the base `Startup` now takes `IShellConfiguration`). +2 tests for **135 ContactCenter tests passing**; clean full-solution `-warnaserror` build; changelog updated. Multi-node safety (distributed locks) already shipped in G2. Remaining Phase 13: SignalR backplane guidance, projection rebuild tooling, PII redaction/erasure, retention for sessions/metrics/recordings. Next: Phase 14 (AI assist) or G5 (agent desktop UI).
- 2026-07-01: **Phase 14 started — AI assist seam.** Added a pluggable, provider-agnostic assist seam: `IContactCenterAssistProvider` (`SuggestDispositionAsync`/`SummarizeAsync`, ordered) + `AssistContext`/`DispositionSuggestion` models, and `IContactCenterAssistService`/`ContactCenterAssistService` that orchestrates registered providers by order and returns the first result (`IsAvailable` reflects whether any provider is installed). Registered in the base feature; the Contact Center stays decoupled from any specific AI provider. +4 tests for **139 ContactCenter tests passing**; clean `-warnaserror` build; changelog updated. Remaining Phase 14: a concrete AI provider wired to the AI module (summaries/disposition/sentiment), virtual-agent handoff, AI routing recommendations. Remaining overall: G5 agent-desktop/supervisor UI, G7 skill proficiency, G4 abandonment/AMD.
- 2026-07-01: **DialPad completed as the Contact Center phone provider (inbound + outbound).** Outbound already routed through `DialAsync`; added the missing inbound path so agents can both make and take calls through DialPad. New `DialPadWebhookController` (`POST /api/dialpad/webhook/call`, `[AllowAnonymous]`, DialPad Contact Center Voice feature) validates the DialPad HS256-signed webhook JWT with a new protected **Webhook signing secret** setting (`DialPadJwtValidator` — no external JWT dependency; unsigned JSON accepted only when no secret is configured), parses the `DialPadCallEvent` payload, and `DialPadWebhookService` normalizes it: state changes go to `IProviderVoiceEventService.IngestAsync` (updates interaction + call session), and a new inbound live call with no matching interaction is routed via `IVoiceContactCenterCallRouter.RouteInboundAsync` (creates activity+interaction, resolves entry point/queue, offers to an agent). Advertised the `CallTransfer` capability. Added the webhook secret to DialPad settings (view model + protected driver + view). +9 DialPad tests (JWT validate/tamper/secret + webhook route/update/ignore) — clean full-solution `-warnaserror` build. Docs (`telephony/dialpad.md`, `v2.0.0` changelog) updated. This realizes the per-provider signed webhook adapter goal (P1 #18 / G1 inbound) for DialPad and gives Phase 8 entry points a real inbound provider.
- 2026-07-01: **G5 agent desktop + supervisor dashboard shipped (P1 #15, #16); G1 completed; obsolete dialer-provider code removed.** Built the CRM-integrated **Agent Workspace** in the `RealTime` feature: `AgentWorkspaceController` (`Index` page + `State` JSON snapshot + `SetPresence` + `Complete`), 8 workspace view models, `Views/AgentWorkspace/Index.cshtml`, the hand-written static `agent-workspace.js` (binds to `ContactCenterHub` via the shared `contact-center-realtime` helper + the `State` endpoint), and `styles/contact-center-workspace.css`. The desktop shows presence with reason-code control, live queue chips, the ringing **offer card** (countdown + one-click **Accept** → the existing `ContactCenterVoiceAcceptOffer` accept+connect command, or **Decline** → re-offer), the **active-interaction** panel (live talk timer + customer 360 link), an inline **disposition + notes wrap-up** that completes through the source-neutral `IActivityDispositionService`, and recent history — with change-detection rendering so a live refresh never clobbers an in-progress disposition/notes entry. Added a **Supervisor Dashboard** (`SupervisorDashboardController` + `supervisor-dashboard.js`) at **Interaction Center → Live dashboard** (gated by `MonitorContactCenter`): live summary metrics, per-queue SLA-health tiles (waiting / longest wait / SLA breaches), and an agent presence board. Added `IInteractionStore`/`IInteractionManager.FindActiveByAgentAsync` + `ListRecentByAgentAsync` (indexed), a `ContactCenterRealTimeAdminMenu` (**My workspace** + **Live dashboard**), and registered the two scripts + the stylesheet as named resources. **G1 completed:** the Telephony soft-phone `answerIncoming` now awaits the Contact Center accept and only answers the agent device when the accepted offer reports `RequiresDeviceAnswer`, closing the P0 #2 "two uncoordinated actions" race; asset rebuilt (`soft-phone.min.js`). **Obsolete code removed (maintainer-approved cleanup of this not-yet-merged module):** deleted the superseded outbound `IDialerProvider`/`IDialerProviderResolver`/`DialerProviderResolver` chain and its `DialerDialRequest`/`DialerDialResult`/`DialerProviderCapabilities` models (outbound already routes through `IContactCenterVoiceProvider`, leaving a single provider boundary), removed the DialPad `IDialerProvider` implementation + registration, and dropped the orphaned `ContactCenterConstants.Components.WrapUp` constant. All **139 ContactCenter tests pass**; clean full-solution `-warnaserror` build. Docs (`contact-center/index.md`, `contact-center/agent-desktop.md`, `agents-queues-dialer.md`, `v2.0.0` changelog) updated. **Remaining for Phase 7/G5:** scoped/audited supervisor live call-control intents (monitor/whisper/barge/take-over UI on top of `IContactCenterMonitoringService`) and a reason-code deployment-plan step.
- 2026-07-02: **Contact Center admin documentation clarified.** Expanded `src\CrestApps.Docs\docs\contact-center\index.md` with an Interaction Center admin-menu concept table explaining agent states, agents, business hours, campaigns, channel endpoints, entry points, queues, skills, dialer profiles, My workspace, and Live dashboard with examples. This was documentation-only for Contact Center; no Contact Center domain code changed.
- 2026-07-01: **Contact Center code review hardening pass.** Fixed the highest-risk orchestration gaps found during review without adding new domain aggregates: offer accept/decline now verifies the reservation belongs to the current agent before changing state; failed server-side provider connection after accept compensates by canceling the accepted reservation so the queue/agent/activity are not stranded; successful outbound dial attempts accept their reservation so the expiry task cannot later release a live call; provider terminal call events move agents into `WrapUp`; workspace completion verifies the active/wrap-up interaction belongs to the current agent and then releases presence to the requested state or availability; the Agent Workspace preserves disposition/notes drafts across status-refresh rerenders and surfaces offer failures; and the Supervisor Dashboard now exposes provider-gated **Monitor**, **Whisper**, **Barge**, and **Take over** actions backed by the existing audited monitoring service. Docs were expanded with manager runbooks for entry points, outbound/callback operations, and workflow automation. Remaining: browser coverage, reason-code deployment-plan step, projection rebuild tooling, multi-step IVR, advanced outbound compliance, and quality-management features.
- 2026-07-01: **PR review security hardening.** Addressed the CodeQL logging findings on provider voice webhooks by logging the matched adapter's registered provider technical name instead of the webhook-supplied provider route value when signature validation or idempotency-key validation fails.
- 2026-07-02: **Phase 12 Reports UI shipped — the "Reports" tab (Analytics feature).** Added the new `CrestApps.OrchardCore.ContactCenter.Analytics` feature (depends on `Queues`) that surfaces an **Interaction Center → Reports** admin area (`ContactCenterReportsAdminMenu`, nested Reports group) with six pages served by `ReportsController` (`[Feature(Analytics)]`, `[Admin]`): **Overview**, **Call insights**, **Agent productivity**, **Queue usage**, **Campaign summary**, and **Subject inventory**. Each page shares a `yyyy-MM-dd` date-range filter (default last 30 days) and a CSV export (`ReportCsvBuilder`, UTF-8 BOM). **Core:** added `IContactCenterReportingService`/`ContactCenterReportingService` (Core) that aggregates the durable interaction history and the CRM `OmnichannelActivityIndex` inventory into strongly-typed report models under `Models/Reports` (`CallInsightsReport` with per-day trend + channel/status breakdowns + answered/abandoned/failed + AHT/ASA/talk time; `AgentProductivityReport` per-agent handled/talk-time/completed-activities; `QueueUsageReport` per-queue handled/answered/abandoned/AHT/ASA + live waiting depth + SLA threshold; `CampaignSummaryReport` and `SubjectInventoryReport` bucketing each group into Completed/Pending/InProgress/Failed/Cancelled + attempts + completion rate — i.e. "what is completed vs pending" per campaign and per subject). The heavy aggregation lives in `internal static` builders so it is unit-testable without a live session. Added the `ViewContactCenterReports` permission (granted to Administrator + the built-in Supervisor role). **Tests:** +6 reporting unit tests (call insights totals/handle-time, daily grouping, agent productivity aggregation, queue usage + waiting, campaign completed-vs-pending bucketing, subject grouping) — **all ContactCenter tests pass**; clean `-warnaserror` Release build of `ContactCenter.Core` and the module. Docs (`contact-center/index.md` new "Reports and analytics" section, `agents-queues-dialer.md` feature table + recipe, `v2.0.0` changelog "Reports & Analytics UI") updated. Also verified DialPad completeness as the phone provider: outbound dial + inbound signed webhook routing + `CallTransfer` are shipped (`AgentDeviceNative` delivery); provider media execution of recording/monitoring remains a Phase 9 roadmap item, not a dialing-path gap. **Remaining Phase 12:** SLA/adherence trend snapshots and operational alerts.
- 2026-07-02: **Reports generalized into a reusable framework + industry-standard CRM reports (maintainer direction).** Per the maintainer's request for a reusable reports structure, a top-level Reports tab, extensible (display-driver) filters with a from/to range, exports, and CRM reports, the Contact Center-specific reports UI was replaced by a shared framework. **New `CrestApps.OrchardCore.Reports` module** (+ `CrestApps.OrchardCore.Reports.Abstractions`): `IReport` (name/category/permission/`RunAsync`→`ReportDocument`), a display-driver-extensible `ReportFilter` with a built-in from/to date-range driver, a uniform `ReportDocument` (metric-card / table / bar sections, with emphasized totals rows for aggregated reports), a generic `ReportsController` (Index landing + `Display(id)` + `Export(id, format)`), a top-level **Reports** admin menu (`ReportsAdminMenu`) that groups registered reports by category and gates each by its own permission, an `IReportManager` registry, and a pluggable `IReportExportFormat` with a built-in `CsvReportExportFormat`. Registered in `.slnx` and the `Cms.Core.Targets` bundle. **Contact Center migration:** deleted the CC-specific `ReportsController`, `ContactCenterReportsAdminMenu`, `ReportCsvBuilder`, `ReportFormat`, report `ViewModels`, and `Views/Reports`; kept `IContactCenterReportingService` + its DTOs (and the 6 tests) and added five thin `IReport` adapters (`*ReportProvider`) that map the DTOs to `ReportDocument`. The Analytics feature now depends on the Reports feature and registers the adapters. **Omnichannel CRM reports:** added an **Omnichannel Reports** feature (`CrestApps.OrchardCore.Omnichannel.Reports`) in the Managements module with `OmnichannelReportAggregator` (pure, tested) + `OmnichannelReportQuery` and three `IReport`s — **Activity summary**, **Campaign performance**, and **Disposition breakdown** — plus a `ViewOmnichannelReports` permission (implied by `ManageActivities`). **Validation:** clean full-solution `-warnaserror` build; 226 ContactCenter/Omnichannel/Report tests pass (+3 new Omnichannel aggregator tests); and a live end-to-end smoke test confirmed the top-level **Reports** menu, all eight report pages (5 CC + 3 CRM) rendering with the from/to filter, and CSV export, on a fresh tenant. Docs: new `modules/reports.md`, updated `modules/index.md`, `contact-center/index.md`, `omnichannel/index.md`, and the `v2.0.0` changelog. **Remaining:** per-agent self-service report scoping, additional export formats (Excel/PDF), and SLA/adherence trend snapshots.
- 2026-07-06: Fixed the default Debug solution build by re-enabling `CrestApps.OrchardCore.Reports.Abstractions` and `CrestApps.OrchardCore.Reports` in `CrestApps.OrchardCore.slnx`, which removed the 45 reporting-related errors from plain `dotnet build`. Load Inventory was also tightened back to the Contact Center design: the picker now exposes only **Manual** and **Dialer** sources, dialer inventory loads require a dialer profile, and the default batch loader applies the selected profile's dialing mode and campaign to the created activities while leaving them unassigned for later dialer reservation. Legacy hidden source registrations remain available so older inventory loads do not lose their source metadata.
- 2026-07-06: Terminology was aligned again by renaming the Omnichannel Management UI/docs label from **Contact Lists** to **Load Inventory** while keeping the internal `OmnichannelActivityBatch` model/API stable for compatibility. The Contact Center plan also records the dialer workflow decision: technical call outcomes (no answer, busy, disconnected, rejected, failed, voicemail, and similar provider states) must be classified by the dialer/voice layer and then mapped at the Subject level into business dispositions or retry/callback actions; the dialer profile remains an execution policy, not a workflow owner.
- 2026-07-13: **Multi-party conference and campaign-group reporting follow-up.** The shared Telephony merge request now accepts any number of selected call ids while retaining legacy primary/secondary compatibility. The soft phone permits unlimited selection, shows the conference action for two or more calls, labels every merged participant **In conference**, and formats Recent phone numbers. Asterisk adds all selected channels to one mixing bridge and clears their hold markers; DialPad sequentially merges each additional call into the primary call. Omnichannel Management now provides catalog-backed Campaign Groups, optional campaign membership, group filters, and campaign-group aggregates across CRM and Contact Center campaign reports. Unit and Playwright coverage includes three-call conference behavior and multi-campaign group aggregation.
- 2026-07-09: **Reliability/build-quality review pass (Softphone + Contact Center + Asterisk + DialPad).** Ran a comprehensive review against the stated objectives (code quality, real-time event sync, inbound real-time processing, self-healing, dashboard call-state visibility, unit coverage, build quality). **Shipped:** (1) Fixed 5 `CA1873` analyzer warnings in `TelephonyHub.cs` that failed CI `-warnaserror` — the five completion-log `_logger.LogInformation` sites now guard on `_logger.IsEnabled(LogLevel.Information)`, matching the file's existing `LogHubActionStart` helper pattern; the full solution now builds clean with `-warnaserror` (0 warnings, 0 errors) and all 1219 tests pass. (2) Improved Asterisk dashboard call-state granularity: `AsteriskDiagnosticsService.SummarizeCallState` now reports **"In conference"** for bridged multi-party (3+) calls instead of collapsing them to "Connected" (Ringing/Offering/Offered/On hold/Connected/In conference are now distinguished, with Muted/On hold/bridge-type badges already rendered by `dashboard.js`). (3) Added a self-healing unit test (`HealForAvailabilityAsync_WhenRingingInteractionHasNoActiveReservation_RequeuesIt`) covering the critical path where an agent stuck with a stale **Ringing** interaction and no live reservation is reclaimed so future inbound calls can route — the exact "never stay falsely on a call" objective; 245 ContactCenter+Telephony tests pass. **Findings reported for maintainer decision (not auto-fixed because correct fixes need multi-node/optimistic-concurrency design + live validation):** (a) *[High]* `ActivityReservationService.ReserveAsync` is serialized only by the per-**queue** lock in `ActivityAssignmentService`, but the over-committed resource is the **agent**; two concurrent inbound calls on two queues that share one available agent can both reserve that agent. **Partially fixed this pass:** `ReserveAsync` now re-reads the agent and aborts (compare-and-set on `ActiveReservationId`) before booking, so it no longer double-books once the prior reservation is visible (+1 unit test). **Still recommended:** a per-agent distributed lock around the reserve/accept/release transition (plus an optimistic-concurrency/version guard) to fully close the multi-node TOCTOU window, since YesSql session identity-map caching + commit-at-scope-end make an in-session re-read alone insufficient across nodes. (b) *[Medium]* `ReserveAsync`/`AcceptAsync` vs `ReleaseAsync` (expiry/cancel) are not serialized, so an accepted (live) call can race an expiry pass and be requeued to a second agent; `ReleaseAsync` should re-validate the reservation is still `Pending` under the same per-agent lock before mutating. (c) *[High]* the **Asterisk** module is command-only — it registers no ARI WebSocket/event listener and never emits `ProviderVoiceEvent`/`CallStateChanged`, so remote answer/hangup/hold are never observed and call state cannot stay in sync with the PBX (unlike DialPad's signed webhook → `IProviderVoiceEventService.IngestAsync`). Recommend an in-module ARI event subscriber (hosted service) mapping `ChannelStateChange`/`ChannelHangupRequest`/`StasisEnd`/hold events to normalized provider voice events, mirroring the DialPad path. (d) *[Medium]* DialPad webhook accepts unsigned bodies when no signing secret is configured (`[AllowAnonymous]` + no-secret bypass) — recommend requiring a secret before enabling the webhook. Self-healing (`AgentWorkStateHealingService`), `AgentSessionService` heartbeat/expiry locking, `ProviderVoiceEventService` state mapping/idempotency, OAuth/PKCE + token encryption, and the DialPad JWT signature path all reviewed clean.
- 2026-07-11: **Softphone, Contact Center, Asterisk, and DialPad reliability hardening completed.** Closed the outstanding review findings and additional race/recovery defects: provider events now reject stale/nonterminal-after-terminal transitions and publish unique semantic idempotency keys; every event is durably enqueued before handler fan-out with per-handler completion checkpoints; inbound provider calls use provider-scoped distributed locks and provider+call-id lookups; agent reservation and reservation lifecycle transitions use distributed locks with accept-versus-expiry revalidation; terminal dial failures/permanent suppressions dequeue work; overnight business hours and fail-closed equal dialer windows are covered; overflow preserves total SLA age while tracking per-queue dwell time and preventing queue cycles; Asterisk listeners are independently supervised, reconnect with backoff, perform provider-scoped serialized reconciliation, recover hold/mute variables, reject unknown channel states, and map bridge-leave transitions; DialPad webhooks fail closed without a usable signing secret and include richer state in idempotency keys. Added regression coverage for outbox restart/partial-handler behavior, provider event ordering/idempotency, inbound duplicate delivery, reservation races/agent locks, dialer terminal behavior, overnight hours/overflow chains, provider restart reconciliation, Asterisk granular state, and DialPad authentication.
- 2026-07-12: **Live routing, dialer, soft-phone, and Asterisk dashboard validation pass.** Diagnosed the reported queued-call and delayed-dashboard behavior from `blog1` database state, CMS/Asterisk logs, ARI state, and live browser reproduction before changing code. Fixed stale reservation/capacity recovery and repeated queue progression; made loaded dialer activities immediately queue-eligible; prevented duplicate same-scope reservations by flushing reservation state before selecting more work; started Preview attempts from generic offer acceptance; repaired Local DNC schema migration; added the Asterisk Contact Center voice-provider adapter and persisted its actual configured provider alias; supported local E.164 destinations; and verified provider disconnect propagation into interaction termination and agent disposition state. Fixed dashboard latency by assigning Asterisk Web the separate `crestapps-dashboard` ARI application and closing ARI HTTP connections after each request, producing event-triggered SignalR snapshots in about one second instead of waiting for periodic reconciliation. Also added live call/bridge badge updates, consistent initial-snapshot JSON naming, compact no-provider soft-phone layout, Enter-to-dial, persistent connected-number display, explicit queue/campaign placeholders, feature-gated AI batch fields, and null-safe historical activity management. The Aspire test dialplan now uses a generated tone sequence plus Echo rather than relying on absent container sound files.
- 2026-07-12: **Phase 12 enterprise reporting catalog completed and validated.** Expanded Contact Center Analytics and Omnichannel Management to 79 immediately runnable reports grouped for executives, operations, queue/routing, agents, workforce/payroll, billing usage, CRM/campaigns, compliance/audit, and technical/IT roles. Added a reusable responsive Chart.js report section model and upgraded the executive performance report with KPI cards plus daily volume, channel mix, queue service-level, and agent workload charts; chart data exports as equivalent CSV and Excel tables. Added presence-derived workforce timecards, breaks, utilization, occupancy, payroll inputs, user productivity, measured billing usage, campaign analysis, compliance exceptions, transcript/recording coverage, and call-leg diagnostics without inventing schedules, wages, prices, survey scores, or quality evaluations that are not persisted. Browser validation executed every one of the 79 catalog routes, verified all four executive charts initialize without console errors, and confirmed date, agent, and channel filters materially change browser/CSV results. Final validation passed with a strict Release solution build, all 1,376 tests, the full asset rebuild, and the Docusaurus production build.
- 2026-07-13: **R1 tenant-targeting and Asterisk simulator security started.** Added the shared `TenantSignalRGroupName` contract and moved every Contact Center, Telephony, and Asterisk soft-phone projection away from globally scoped SignalR user/group destinations. Authorized hub connections now join Orchard-shell-qualified user, queue, and supervisor groups, preventing equal identifiers in different tenants from crossing a shared backplane. Hardened Asterisk diagnostics by removing credential-bearing ARI URIs from logs, adding negative secret-redaction tests, removing committed sample credentials, and refusing to start the standalone destructive dashboard outside Development with loopback defaults. Targeted tenant-routing and Asterisk security coverage passes; R1 remains open for entitlement enforcement, centralized PII redaction, stored-XSS correction, and webhook ingress hardening.
- 2026-07-13: **R1 stored-XSS correction completed.** Replaced the Omnichannel activity-completion workflow preview's string-built `innerHTML` cards with DOM-created elements, `textContent`, and property assignment for action titles, labels, schedule values, and attributes. Added a source-level security regression that fails if the completion view reintroduces an `innerHTML` sink. R1 remains open for queue/campaign entitlement enforcement, centralized operational PII redaction, and webhook ingress hardening.
- 2026-07-14: **R1 centralized operational PII redaction completed.** Added `OperationalLogFieldKind`, `OperationalLogIdentifierCategory`, and `OperationalLogRedactor` to the shared `CrestApps.OrchardCore.Abstractions` assembly: a classification API that pseudonymizes stable identifiers (user/agent/session/call/interaction/activity/reservation/queue/event) with a process-local random-key HMAC-SHA-256 for correlation within one process lifetime and resistance to offline brute-force of low-entropy identifiers, fully redacts customer/E.164 addresses and secrets/token-shaped values, redacts free-form request descriptions, complete metadata dictionaries, and provider response/error bodies, and wraps logged exceptions so their type and a bounded, control-sanitized stack-frame summary remain available while messages, inner exceptions, data, and overridable exception text are removed. Migrated every logging path identified by the R0 characterization and the subsequent full-surface audit: the Telephony hub, OAuth, dispatch, reconciliation, and background paths; the Asterisk provider, media, tenant, and real-time listener/dispatcher paths; DialPad call, directory, OAuth, action, and secret paths; the SMS Omnichannel event handler; and Contact Center Core/module presence, provider voice-event, provider call-state synchronization, dialer attempt, outbox, queue/reservation assignment, agent-session, disposition, healing, tenant, and background-task services. Safe enums, counts, provider/action names, exception types/sanitized frames, and configuration labels are left unredacted. Replaced `ContactCenterOperationalLogPrivacyTests`' R0 "preserves raw value" characterizations with inverted negative tests that execute the Telephony hub's `Describe*` methods and the new redactor with sentinel E.164 numbers, user/agent/call ids, secrets, nested exceptions, and an attacker-controlled `StackTrace` override, plus source-scan assertions that reject raw exception arguments throughout every covered project and a CRLF-stripping regression test; added `OperationalLogRedactorTests` for the new API. All 1,597 unit tests, a strict zero-warning solution build, and the Docusaurus documentation build pass.
- 2026-07-14: **R1 provider webhook rate and concurrency limits completed.** Added a tenant-scoped `ProviderWebhookIngressLimiter` with a shared pre-buffering concurrency ceiling and authenticated per-canonical-provider token buckets, configured through `CrestApps_ContactCenter:WebhookIngress`. The generic Contact Center and built-in DialPad webhook routes now return HTTP 429 when capacity is exhausted and emit `Retry-After` when the token bucket supplies one; signature/JWT failures occur before provider rate consumption so unauthenticated traffic cannot starve valid deliveries. The existing Contact Center Voice tenant-activation event eagerly resolves the limiter so invalid limits fail feature activation rather than the first request. Added direct limiter, generic endpoint/processor, DialPad endpoint, activation, and feature-dependency tests. All 1,606 unit tests, a strict zero-warning solution build, the asset rebuild, and the Docusaurus build pass. Freshness/replay enforcement and durable inbox acknowledgement remain open.
- 2026-07-14: **R1 provider webhook freshness enforcement completed.** Extended the tenant-scoped webhook ingress policy with configurable maximum delivery age and future clock skew. Generic normalized provider events now require a signed UTC `OccurredUtc`; DialPad JWT payloads require an epoch-millisecond `event_timestamp`. Missing, malformed, non-UTC, stale, and excessively future deliveries are rejected before state-changing processing, while authenticated replay floods still consume the provider rate budget. Added boundary, invalid-option, missing/non-UTC generic timestamp, and missing/malformed/out-of-range DialPad tests. Durable inbox acknowledgement and durable replay uniqueness remain open.
- 2026-07-14: **R1 durable provider webhook acceptance completed.** Added a tenant-local YesSql provider webhook inbox shared by generic Contact Center and DialPad voice ingress. Authenticated normalized deliveries are committed under a canonical-provider plus delivery-id lock before HTTP 2xx; duplicate retries resolve to the existing durable message. Processing begins only after commit and uses server-owned cancellation, with immediate persisted dispatch for call-state latency plus a tenant background task for restart/transient recovery. Failed handlers use exponential backoff, do not block later due messages, retain only sanitized exception type diagnostics, and dead-letter after ten attempts; temporarily disabled provider-feature handlers defer without consuming their retry budget. Provider delivery ids are bounded to the 256-character durable-index contract. The production support matrix now requires `OrchardCore.Redis.Lock` in addition to the Redis SignalR backplane for every multi-node deployment; single-node development can use the local lock implementation. Added unit coverage for acceptance, duplicate/busy behavior, commit ordering, dispatch, retry, poison isolation, feature-handler absence, and dead-lettering; SQLite persistence coverage proves acceptance is visible from a new YesSql session before return; generic and DialPad handler tests prove persisted payload routing. Multi-database/provider execution and a real socket disconnect after commit remain R8/provider-contract evidence gates.
