# Contact Center Module Architecture and Implementation Plan

> **Status:** Active. This is the durable, repository-tracked design and progress document for the Contact Center module set. It is referenced from `.github/copilot-instructions.md` so every AI session reviews it before doing Contact Center work.
>
> **How to use this document:**
>
> - Read the **Progress status** section (bottom) first to see what is done and what is next.
> - Treat the **Phased delivery plan** as the source of truth for scope and ordering. Start at the lowest incomplete phase.
> - Keep the **Progress status** section current after each meaningful change (what shipped, what is in progress, decisions made).
> - Never write competitor product names in code, comments, public docs, or identifiers. Adopt only the industry-standard concepts and terminology captured in the **Standard contact center terminology and metrics** section.
> - Respect the layer boundary: **CRM (Omnichannel) owns business work data, Contact Center owns orchestration, Telephony owns media execution.** `OmnichannelActivity` remains the universal work item. `Interaction` is communication history for one attempt and never owns workflow or disposition.

## Problem statement

Design an enterprise-grade Contact Center orchestration layer for the existing Orchard Core communications platform. The Contact Center must extend Omnichannel Management instead of introducing a separate work model, sit between CRM and Telephony, own routing and communication orchestration, and allow agents and supervisors to operate directly inside the CRM UI without depending on an external contact center system.

The design is intentionally domain- and architecture-focused. It does not include code or low-level implementation details.

## Current codebase baseline

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
  - activity batches via `OmnichannelActivityBatch`
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
- Activity batches select an activity source before showing the editor. Manual batches require user assignment while loading; dialer batches hide user selection and load unassigned activities with an available assignment status so the dialer can reserve and assign them later.
- Dispositions belong to activities, not interactions. Provider, agent, AI, and workflow outcomes must converge through a single activity disposition service before subject actions/workflows run.
- Contact Center must provide CRM-integrated agent surfaces where agents receive work. Queue/campaign sign-in, sign-out, and presence belong in the Telephony soft phone through display-driver tabs; broader desktop surfaces handle offers, active activity context, interaction history, wrap-up, required disposition, and supervisor/AI assistance from the CRM UI.
- PBX/voice providers that can do more than soft-phone call control may implement Contact Center voice-provider abstractions for dialer dialing, call assignment, provider-side queues, queue events, and PBX presence synchronization.

### Existing real-time boundary

- `src\Modules\CrestApps.OrchardCore.SignalR` provides the shared SignalR feature and hub registration pattern.
- Telephony already uses SignalR for soft-phone call-control requests and current-user call state updates.

Design implication: Contact Center should add its own real-time event stream for agent desktop, supervisor dashboard, and queue monitors, and it should consume or normalize Telephony events instead of overloading TelephonyHub with routing responsibilities.

### Current gaps to solve

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
3. Source-driven activity loading: Activity batches are the common activity-loading surface and support source-specific UI/behavior for manual, dialer, callback, inbound, API, and future sources through Orchard display drivers and source options.
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
   - Base Contact Center module and dependency root.
   - Core feature: interaction management, event log, tenant settings, baseline permissions, and admin navigation.

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

7. `CrestApps.OrchardCore.ContactCenter.WrapUp`
   - Wrap-up timers, required disposition rules, disposition validation, post-interaction completion, and CRM activity updates.

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

### 7. Disposition & Wrap-Up Management

| Area | Design |
| --- | --- |
| Purpose | Govern post-communication activity completion, required outcomes, timers, notes, CRM activity updates, and follow-up automation. |
| Responsibilities | Start wrap-up when interaction work ends; enforce required activity disposition rules by queue/subject/campaign; track wrap-up duration; save notes and outcome; update CRM activity through `IActivityDispositionService`; trigger subject actions and optional workflows; release agent capacity when wrap-up completes or times out. |
| Data owned | Wrap-up session, wrap-up start/end, required disposition policy, disposition source, notes, completion state, timer policy, auto-close policy, and validation results. |
| Events consumed | CallEnded, ChannelSessionEnded, InteractionWorkCompleted, DispositionSelected, WrapUpTimerExpired, AgentSubmittedWrapUp. |
| Events emitted | WrapUpStarted, DispositionRequired, DispositionSelected, WrapUpCompleted, ActivityCompleted, SubjectActionsRequested, PostInteractionWorkflowRequested, AgentReleased. |
| Interactions | Updates CRM Activity and Subject data; executes existing Subject Actions; optionally emits OrchardCore workflow events; informs Agent Presence and Analytics. It never dispositions an Interaction. |
| Why it exists | Contact center work is not complete when the call ends. Wrap-up ensures business outcomes are captured consistently through the Activity and agents are released at the correct time. |

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
| WrapUpSession | Post-work completion period | References OmnichannelActivity, latest Interaction, Agent, Disposition |
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
       -> QueueItem / AgentReservation / WrapUpSession
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
12. When the call ends, Wrap-Up starts.
13. Agent selects disposition and completes required fields.
14. CRM activity is updated and Subject Actions or optional workflows run.
15. Analytics projections and supervisor dashboards update.
```

## Outbound dialing sequence

```text
1. CRM campaign/activity batch produces eligible phone activities.
2. Campaign Dialer selects eligible activities using schedule, timezone, DNC/compliance, retry, and priority rules.
3. Dialer mode determines whether an agent previews first or the system reserves an agent before dialing.
4. Routing Engine reserves an eligible agent and capacity.
5. Interaction Management creates the outbound Interaction and links it to the CRM Activity.
6. Contact Center Voice requests Telephony to dial using the configured channel endpoint/caller id.
7. Telephony executes the provider dial action and returns provider call state.
8. Call Session Management maps provider call id to Interaction.
9. Agent desktop receives real-time dial/call state updates.
10. Outcome is classified: connected, no answer, busy, failed, canceled, voicemail, callback, or completed.
11. Wrap-Up enforces disposition and notes when agent-handled.
12. CRM Activity, retry policy, callback schedule, and campaign metrics update.
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
12. Wrap-up timer, required activity disposition, notes, CRM activity completion, and Subject Action execution through `IActivityDispositionService`.
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
- Define a provider inbound-event normalization boundary so inbound calls and provider webhooks enter Contact Center reliably.
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

- Dialer profiles as execution policies over CRM campaign/activity inventory, not as a replacement for campaigns, subjects, activity batches, or activity configuration.
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
- Release agent capacity after wrap-up completion or timeout.

Deliverables:

- Wrap-up feature.
- Required disposition policies.
- Activity completion integration.
- Subject Action integration.
- Timer and timeout behavior.
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
- Add advanced supervisor controls and quality workflows.

Deliverables:

- Progressive dialer.
- Predictive dialer safety design.
- Chat/SMS/email routing adapters.
- AI assistance feature.
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
4. The Telephony abstraction may need a provider event normalization boundary so inbound calls and provider webhooks can enter Contact Center reliably.
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

> Added 2026-06-30 after a full review of the shipped Contact Center code against industry-standard cloud contact center / dialer platforms. This section is the authoritative gap list and execution order for turning the current foundation into a state-of-the-art inbound + outbound voice contact center. Treat every unchecked item here as in-scope work, and keep the linked phase checklist below in sync as items ship.

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
| 10 | No **wrap-up** lifecycle: no timer, required-disposition policy, auto-close, structured notes, or capacity release after wrap-up. | `DefaultActivityDispositionService.cs` | Add `WrapUpSession`, per-queue/subject/campaign required-disposition rules, timeout, and capacity release on completion/timeout. |
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
- **Lifecycle operations (capability-gated, provider-neutral intents):** `Dial`, `Bridge/Connect`, `Answer`, `Hangup`, `Hold`/`Resume`, `Transfer` (blind/consultative, to agent/queue/external), `Conference`, `SendDigits`, `Park`, `Recording` (start/stop/pause/resume), `Monitor`/`Whisper`/`Barge`/`TakeOver`.
- **Inbound + state events:** a normalized `ProviderVoiceEvent` (provider event id, idempotency key, call id, leg id, normalized state, from/to, queue/agent hints, timestamps, sanitized raw metadata) delivered through an `IProviderVoiceEventHandler`, replacing the current assumption that inbound always arrives as a fully-formed `InboundVoiceEvent` posted to an authorized endpoint.
- **Capabilities enum** must grow to cover the above so the agent desktop and supervisor UI hide/disable unsupported actions exactly like `TelephonyCapabilities` does today.

### Execution order (maps onto existing phases)

1. **Voice foundation hardening (completes Phase 4):** provider boundary redesign + delivery models, `CallSession` aggregate, `ProviderVoiceEvent` normalization, and the unified Contact Center call-command service that delivers media to the agent on inbound accept and outbound answer. *(P0 #1, #2, #3; P1 #9)*
2. **Assignment safety (hardens Phases 2/3):** distributed-lock/single-writer assignment, capacity enforcement, live `AgentSession` + heartbeat + stale cleanup, real-time offer timeout. *(P0 #6, #7; P1 #13, #14)*
3. **Wrap-up + completion unification (completes Phase 6):** wrap-up session, required-disposition policies, capacity release, contact-center-aware completion path through `IActivityDispositionService`. *(P0 #8; P1 #10)*
4. **Dialer safety (completes Phase 5, pulls forward Phase 10 essentials):** strategy-per-mode, eligibility/compliance gate (DNC, preferences, calling windows, retry cool-down, suppression audit), cap Power, block Predictive. *(P0 #4, #5)*
5. **Agent desktop + supervisor real-time UX (Phase 7):** CRM-integrated cockpit, supervisor dashboards, queue monitor, live call-control intents. *(P1 #15, #16)*
6. **Eventing/outbox + provider webhooks (hardens Phase 1, extends Phase 4):** outbox, projections, idempotency, signed webhook adapters. *(P1 #17, #18)*
7. **Inbound entry points/IVR (Phase 8), recording/monitoring (Phase 9), compliance hardening (Phase 10), and analytics (Phase 12)** proceed per the existing phase plan once the above is stable.

## Progress status

Keep this section current. Use the checklist below to track phase-level progress; add dated notes under "Change log" for meaningful decisions.

### Phase checklist

- [x] **Phase 0 — Project governance and durable planning**
  - [x] Durable repo-tracked plan at `.github/contact-center/PLAN.md`
  - [x] Pointer added to `.github/copilot-instructions.md`
  - [x] Public docs landing page under `src/CrestApps.Docs/docs/contact-center`
  - [x] Module/feature map confirmed against the solution and target bundle
- [x] **Phase 1 — Domain foundation** (CRM activity extension, interaction history, event log, base module)
  - [x] `CrestApps.OrchardCore.ContactCenter.Abstractions` (constants, channel/direction/status/priority/role enums, event vocabulary)
  - [x] `OmnichannelActivity` extended with activity kind/source, assignment status, and reservation metadata so CRM activities remain the universal work item
  - [x] Activity Batch UI changed to source-first creation with Manual and Dialer sources; Dialer batches load unassigned activities for later reservation
  - [x] `IActivityDispositionService` contract added as the source-neutral path for activity dispositions
  - [x] `CrestApps.OrchardCore.ContactCenter.Core` (Interaction + InteractionParticipant + InteractionEvent models, indexes, stores, `IInteractionManager`, event publisher, permissions)
  - [x] `CrestApps.OrchardCore.ContactCenter` base module (Startup, index providers, migrations, permission provider)
  - [x] Registered in `.slnx` and the `Cms.Core.Targets` bundle
  - [x] 13 unit tests (event envelope, event publisher dispatch/idempotency/resilience, interaction manager lifecycle, entity metadata extensibility)
  - [x] Docs landing page + `v2.0.0` changelog entry
- [x] **Phase 2 — Agent, presence, queue, and reservation foundation**
  - [x] `Agents` feature: AgentProfile (presence, capacity, skills, queue/campaign membership), store/manager/index, presence manager, soft-phone queue/campaign sign-in/out
  - [x] `Queues` feature: ActivityQueue, QueueItem, ActivityReservation models/stores/managers/indexes; queue + reservation lifecycle (reserve/accept/reject/expire); reservation-expiry background task
  - [x] Availability-based assignment service (longest-idle agent ↔ highest-priority item); agent/queue/dialer permissions; admin menu + CRUD UI; unit tests
  - [x] Assignment safety (G2 P0 core): per-queue distributed-lock single-writer assignment (P0 #7) and `MaxConcurrentInteractions` capacity enforcement via `CapacityRoutingStrategy` (P0 #6)
- [x] **Phase 3 — Routing MVP** (policy-based routing shipped: per-queue routing strategy — longest-idle / round-robin / least-busy — plus required-skills + capacity filtering, additive sticky-agent preference, SLA-aging item selection, business-hours gating, queue overflow, and auditable routing-decision events; tests cover strategy selection, tie-breaking, business hours, SLA aging, and overflow)
- [~] Phase 4 — Voice integration with Telephony (`Voice` feature: Voice Contact Center Call Router (`IVoiceContactCenterCallRouter`) for inbound and outbound voice routing, inbound voice ingress + normalization boundary (`InboundVoiceEvent`/`IInboundVoiceService` compatibility), inbound activity+subject+interaction creation, queue→endpoint routing, agent offer routing, outbound provider dispatch through `IContactCenterVoiceProvider`, and the Telephony soft-phone incoming-call modal with `IIncomingCallContextProvider`/`IIncomingCallDispatcher` extensibility + voicemail capability; **G1 backend shipped: provider delivery models (`AgentDeviceNative`/`ServerSideAcd`) + `ConnectToAgentAsync`, `CallSession` aggregate, normalized `ProviderVoiceEvent`/`IProviderVoiceEventService` ingestion, and the authoritative `IContactCenterCallCommandService` that accepts the reservation, bridges media, and advances interaction+call-session together. Remaining: soft-phone JS coordination (await accept, answer device only when `RequiresDeviceAnswer`) + asset rebuild, per-provider signed webhook adapters, and transfer/conference taxonomy** — see "Design review" P0 #1, #2, #3 and P1 #9)
- [~] Phase 5 — Outbound dialer MVP (`Dialer` feature: profiles, modes, power/progressive pacing, dialer batch sources, outbound calls routed through the Voice Contact Center Call Router, DialPad Contact Center Voice provider. **G4 dialer safety shipped (2026-06-30):** each mode is now a dedicated `IDialerStrategy` (Predictive disabled in editor + rejected server-side + refused at runtime; Power hard-capped via `PowerDialerStrategy.MaxCallsPerAgent`); the new `IDialerEligibilityService` compliance gate runs before every attempt and audits `DialSuppressed` (destination, max-attempts, retry cool-down, contact do-not-call, calling window in the contact's time zone, and national DNC registries); single-attempt logic moved to `IDialerAttemptService`. Remaining: callback scheduling (`CallbackRequest`) + callback queues, and dialer run/attempt projections.)
- [x] Phase 6 — Disposition lifecycle (the **Subject Flow is the single decision controller** for CRM, inbound, and outbound: every activity carries a Subject + Subject Flow, and completion routes through the source-neutral `IActivityDispositionService`, which applies the disposition, marks the activity `Completed` regardless of its prior contact-center state — resolving P0 #8 — and runs the disposition-driven Subject Actions. A subject flow can require a disposition (`SubjectFlowSettings.RequireDisposition`), enforced centrally so completion is blocked until a disposition is chosen. **Design decision (2026-06-30):** the separate `WrapUp`/`WrapUpSession` concept added earlier was removed as redundant with disposition + subject flow; after-call agent timing/capacity release is deferred to the Phase 7 agent desktop and agent presence, not a domain aggregate)
- [~] Phase 7 — Agent desktop and supervisor real-time UX (**Agent state reason codes shipped (2026-06-30)** + **real-time SignalR foundation shipped (2026-06-30):** the new `RealTime` feature adds the `ContactCenterHub` (user/queue/`cc:supervisors` groups + `WatchQueue`/`UnwatchQueue`), a live `AgentSession` aggregate split from `AgentProfile`, a `Heartbeat`-driven stale-session cleanup background task that signs out dead clients, a `GetSnapshot` reconnect snapshot (`AgentDesktopSnapshot`), the `ContactCenterRealTimeEventHandler` that broadcasts presence/offer/queue events, a `contact-center-realtime` client script, and the `MonitorContactCenter` permission + Supervisor role. Remaining: the CRM-integrated agent desktop UI, supervisor dashboards, the queue monitor/wallboard, and scoped/audited live call-control intents — see G5)
- [ ] Phase 8 — Inbound entry points, IVR and self-service
- [ ] Phase 9 — Recording and live monitoring
- [ ] Phase 10 — Outbound compliance hardening
- [ ] Phase 11 — Optional Workflow bridge
- [ ] Phase 12 — Analytics and operations
- [ ] Phase 13 — Scale-out, resilience and data governance
- [ ] Phase 14 — Advanced capabilities

### Gap-closure backlog (from the 2026-06-30 design review)

Ordered by the "Design review" execution order. Each item is a hard requirement to reach a state-of-the-art dialer; numbers reference the P0/P1 findings.

- [~] **G1 — Voice foundation hardening (completes Phase 4):** redesign the voice-provider boundary with delivery models (`AgentDeviceNative` vs `ServerSideAcd`), capability-gated lifecycle ops, and a normalized `ProviderVoiceEvent`/`IProviderVoiceEventHandler`; add a `CallSession` aggregate; add a unified Contact Center call-command service that delivers media to the agent on inbound accept and outbound answer as one atomic, audited transition. *(P0 #1, #2, #3; P1 #9)* — **Backend shipped (2026-06-30):** `VoiceProviderDeliveryModel` + `ContactCenterConnectRequest` + `ConnectToAgentAsync` + `AgentConnect` capability on `IContactCenterVoiceProvider` (DialPad declares `AgentDeviceNative`); `CallSession` model/index/store/manager/migration registered in the base feature; `ProviderVoiceEvent` + `IProviderVoiceEventService` idempotent ingestion that advances interaction+call-session and bridges answered outbound calls on server-side ACD; `IContactCenterCallCommandService` accept/decline wired into `VoiceController`; +5 unit tests (41 ContactCenter tests pass, clean `-warnaserror` build). **Remaining:** soft-phone JS coordination (await accept, answer device only when `RequiresDeviceAnswer`) + asset rebuild; per-provider signed webhook adapters that emit `ProviderVoiceEvent`; transfer/conference taxonomy.
- [~] **G2 — Assignment safety (hardens Phases 2/3):** distributed-lock/single-writer assignment + optimistic concurrency on reservation; enforce `MaxConcurrentInteractions`; split a live `AgentSession` from `AgentProfile` with SignalR heartbeat + stale cleanup; drive offer timeout from the real-time layer. *(P0 #6, #7; P1 #13, #14)* — **P0 core shipped (2026-06-30):** per-queue distributed-lock single-writer assignment in `ActivityAssignmentService` (both `AssignNextAsync` and `AssignQueueAsync` acquire the lock; inbound `OfferNextAsync` routes through the same path), and `MaxConcurrentInteractions` enforcement via the new `CapacityRoutingStrategy` (Order 20, between required-skills and longest-idle) backed by `IInteractionManager.CountActiveByAgentAsync`. +6 unit tests (47 ContactCenter tests pass, clean `-warnaserror` build). **P1 #13 shipped (2026-06-30):** the live `AgentSession` aggregate (model/index/store/manager + `IAgentSessionService`) is now split from `AgentProfile`, the `ContactCenterHub` registers each SignalR connection on the session with a per-user distributed lock, the client sends a `Heartbeat` every 30s, and the `AgentSessionCleanupBackgroundTask` signs out + deletes sessions whose heartbeat is older than 90s so routing stops targeting a dead client (a brief reconnect is tolerated by the grace window). **Remaining:** optimistic concurrency / compare-and-set on reservation creation (P0 #7 hardening); and the real-time per-reservation offer timeout (P1 #14) driven from the desktop (the SignalR foundation + `ServerTimeUtc`/`ExpiresUtc` on the offer notification now exist; the background reservation-expiry task remains the safety net).
- [x] **G3 — Completion unification via the Subject Flow (completes Phase 6):** make the Subject Flow the single decision controller and route every completion through `IActivityDispositionService`. *(P0 #8; P1 #10)* — **Shipped (2026-06-30):** completion already flows through the source-neutral `IActivityDispositionService` for CRM, inbound, and outbound, which marks the activity `Completed` regardless of its prior contact-center state (P0 #8) and runs the disposition-driven Subject Actions. Added `SubjectFlowSettings.RequireDisposition` (edited on the **Configure** screen) enforced centrally in `IActivityDispositionService` so completion is blocked until a disposition is chosen on every path; completion now also skips Subject Actions when no disposition is selected. **Reversal of the earlier wrap-up implementation:** the `WrapUp` feature / `WrapUpSession` aggregate added on 2026-06-30 was removed as redundant with disposition + subject flow (per the maintainer's "single concept" direction); after-call agent timing/capacity release is deferred to the Phase 7 agent desktop + agent presence. +3 disposition tests (47 ContactCenter + 4 disposition tests pass, clean `-warnaserror` build).
- [~] **G4 — Dialer safety (completes Phase 5, pulls forward Phase 10 essentials):** strategy-per-mode (`IDialerStrategy`), an `IDialerEligibilityService`/compliance gate (DNC, communication preferences, calling windows, retry cool-down, suppression audit), cap Power, and block Predictive until metrics + abandonment controls exist. *(P0 #4, #5)* — **Shipped (2026-06-30):** `IDialerStrategy` + `IDialerStrategyResolver` with `PowerDialerStrategy` (hard-capped `MaxCallsPerAgent`) and `ProgressiveDialerStrategy`; `DialerService` now validates the profile and delegates pacing to the resolved strategy, so Manual/Preview stay agent-driven and **Predictive is blocked** (hidden in the editor, rejected on save, and refused at runtime). The single-attempt path moved to `IDialerAttemptService`, which calls the new `IDialerEligibilityService` (`DefaultDialerEligibilityService`) before every attempt: destination present, attempt limit, retry cool-down (last interaction end + `RetryDelayMinutes`), contact `DoNotCall` communication preference, configurable calling window evaluated in the contact's time zone, and any registered `INationalDoNotCallRegistry`. Suppressed attempts release the reservation and publish an auditable `DialSuppressed` event (DNC/registry cancel the activity; window/cool-down leave it available). Added calling-window settings to `DialerProfile`/editor. +16 dialer unit tests (66 ContactCenter tests pass; clean `-warnaserror` build). **Remaining (P2/Phase 10):** abandonment caps, AMD outcomes, and predictive metrics before Predictive can be re-enabled.
- [~] **G5 — Agent desktop + supervisor real-time UX (Phase 7):** CRM-integrated agent cockpit, supervisor dashboards, queue monitor/wallboard, and scoped/audited live call-control intents. *(P1 #15, #16)* — **Started (2026-06-30):** shipped the canonical **agent state reason codes** prerequisite — a catalog-backed `AgentStateReasonCode` admin surface (Agents feature, **Interaction Center → Agent states**) following the Skills/TimeZones catalog pattern, an `AgentStateReasonCode` recipe step, a seed recipe executed at setup via `IRecipeMigrator`, and soft-phone presence-dropdown integration. **Real-time SignalR layer shipped (2026-06-30):** the new `RealTime` feature (`CrestApps.OrchardCore.ContactCenter.RealTime`, depends on `Queues` + the `SignalR` module) adds the `ContactCenterHub` (`Hub<IContactCenterHubClient>`) with per-user, per-queue, and `cc:supervisors` groups + `WatchQueue`/`UnwatchQueue`; the live `AgentSession` aggregate (split from `AgentProfile`) with `IAgentSessionService` connect/disconnect/heartbeat; the `Heartbeat`-driven `AgentSessionCleanupBackgroundTask`; the `GetSnapshot` reconnect snapshot (`AgentDesktopSnapshot`); `IContactCenterRealTimeNotifier` + the `ContactCenterRealTimeEventHandler` event projection that broadcasts presence (`PresenceChanged`), offers (`OfferReceived`/`OfferRevoked`), and queue depth (`QueueStatsChanged`); the `contact-center-realtime` client script resource; and the `MonitorContactCenter` permission + default **Supervisor** role. +13 unit tests (79 ContactCenter tests pass; clean `-warnaserror` build). **Remaining:** the CRM-integrated agent desktop (offer → accept+connect → customer 360 → script/subject → call controls), supervisor dashboards, queue monitor/wallboard, scoped/audited live call-control intents, and a reason-code deployment-plan step. This real-time layer also unblocks the remaining G2 item (real-time per-reservation offer timeout, P1 #14).
- [~] **G6 — Eventing/outbox + provider webhooks (hardens Phase 1, extends Phase 4):** outbox dispatch + retry/backoff, projection checkpoints, mandatory idempotency on provider events, rebuildable projections, and signed per-provider webhook adapters. *(P1 #17, #18)* — **Outbox shipped (2026-06-30):** Contact Center event dispatch is now at-least-once. `DefaultContactCenterEventPublisher` records the immutable `InteractionEvent` then delegates handler dispatch to the new `IContactCenterOutbox`/`ContactCenterOutbox`, which runs handlers inline and, on any handler failure, persists a durable `ContactCenterOutboxMessage` (`Pending`/`DeadLettered`, attempt count, next-attempt time, last error) via `IContactCenterOutboxStore`. The per-minute `OutboxDispatchBackgroundTask` calls `DispatchDueAsync`, re-running all handlers with exponential back-off (30s→30m cap) and dead-lettering after `MaxAttempts` (10); a missing referenced event is dead-lettered. Handlers must be idempotent (the shipped handlers are). +6 outbox tests + reworked 5 publisher tests (85 ContactCenter tests pass; clean `-warnaserror` build). **Remaining:** mandatory idempotency-key enforcement on provider-sourced events (currently deduped but not required); projection checkpoints + rebuildable read-model projections; and signed per-provider webhook adapters that validate signatures, dedupe, and normalize to `ProviderVoiceEvent` before entering the pipeline (P1 #18 — `VoiceIngressController` is still `[Authorize]`+`ManageInteractions`, accepting pre-normalized internal posts only).
- [~] **G7 — Routing depth (completes Phase 3):** `RoutingPolicy`, `QueueMembership`, skill proficiency, business-hours/holiday calendars, overflow, sticky agent, priority/SLA-aging strategies. *(P1 #11)* — **Shipped (2026-06-30):** per-queue routing policy on `ActivityQueue` (`RoutingStrategy` = LongestIdle/RoundRobin/LeastBusy, `PreferStickyAgent`, `EnableSlaAging`, `BusinessHoursCalendarId` + `AfterHoursAction`, `OverflowQueueId` + `OverflowAfterSeconds`); `StickyAgentRoutingStrategy` (boosts the activity's last assigned user, captured on `QueueItem.StickyAgentUserId` at enqueue), `RoundRobinRoutingStrategy` (orders by new `AgentProfile.LastAssignedUtc`, stamped on reserve), and `LeastBusyRoutingStrategy` (orders by active interaction count) — each gated so only the queue's selected primary strategy scores; `QueueItemPrioritizer` SLA-aging item selection in the assignment path; a reusable `BusinessHoursCalendar` catalog (model/index/store/manager + `IBusinessHoursService` weekly-schedule + holiday + time-zone evaluation; module index provider/migration/handler/driver/controller/admin menu/views under **Interaction Center → Business hours**) that pauses assignment while closed; and `IActivityQueueService.OverflowDueAsync` (wait-time + after-hours overflow re-homing, publishing `QueueItemOverflowed`) run by the reservation/assignment background task. Queue editor extended with all new fields. +21 unit tests (106 ContactCenter tests pass; clean full-solution `-warnaserror` build). **Remaining:** skill **proficiency** levels (requires migrating agent skills from a name list to a proficiency map), a standalone `QueueMembership` aggregate (membership is still modeled via `AgentProfile.QueueIds`), and bullseye/skill-relaxation overflow expansion.
- [ ] **G8 — Inbound entry points/IVR (Phase 8), recording/monitoring (Phase 9), compliance hardening (Phase 10), analytics (Phase 12)** per the existing phase plan once G1–G7 are stable.

### Change log

- Codebase analysis completed for Telephony, Omnichannel Core, Omnichannel Management, SMS automation, SignalR docs, target bundle, solution structure, docs, and tests.
- Plan reviewed and expanded to add inbound entry points/IVR, call recording, live monitoring (silent monitor/whisper/barge/take-over), outbound compliance hardening, quality management, a standard terminology/metrics glossary, scale-out/high-availability, data retention/privacy, a testing strategy, and a migration strategy. Phases renumbered to 0-14.
- Phase 0 started: promoted the session plan to this durable repo-tracked document, added the copilot-instructions pointer, and created the public Contact Center docs landing page.
- Phase 0 completed and Phase 1 (Domain foundation) implemented: added the `ContactCenter.Abstractions`, `ContactCenter.Core`, and `ContactCenter` base module projects; extended `OmnichannelActivity` with kind/source/assignment/reservation metadata so CRM activities remain the universal work item; made `Interaction` an Orchard `Entity` communication-history record linked to activities; added the durable `InteractionEvent` log with idempotency and the `DefaultContactCenterEventPublisher`; added the `IActivityDispositionService` contract; registered everything in `.slnx` and the `Cms.Core.Targets` bundle; added 13 unit tests; and documented the feature on the docs landing page and the `v2.0.0` changelog. The base feature is headless by design — all future CRUD/agent/supervisor UI must use Display Management, display drivers, shapes, placement, and AI Profile-style catalog screens. Next: Phase 2 (agents, presence, activity queues, reservations).
- Activity Batch source selection implemented: **Add Activity Batch** now opens a source modal like AI Provider Connections, sources are registered through `ActivityBatchSourceOptions`, and source cards use shape alternates. Manual batches keep the selected-user assignment flow. Dialer batches hide the user selector and load activities as unassigned `Available` dialer inventory for later reservation by dialer/routing services.
- Phase 2 implemented and Phases 3/5 started: added `Agents`, `Queues`, and `Dialer` features in the ContactCenter module. Core now has AgentProfile/ActivityQueue/QueueItem/ActivityReservation/DialerProfile models, indexes, stores, and managers, plus presence/queue/reservation/assignment/dialer orchestration services. Assignment pairs the highest-priority waiting item with the longest-idle available agent (Phase 3 core); reservations expire via background task. Outbound dialing initially used `IDialerProvider`/`IDialerProviderResolver`, power/progressive pacing, dialer batch sources, and a `DialPad.Dialer` provider; this was later corrected so outbound voice calls route through `IVoiceContactCenterCallRouter` and `IContactCenterVoiceProvider`. Added admin CRUD for queues/dialer profiles, agent/queue/dialer permissions, and 7 new unit tests (20 total pass). Docs + `v2.0.0` changelog updated. Next: skills/sticky/business-hours routing (Phase 3), voice integration (Phase 4), retry/callback/suppression (Phase 5), wrap-up (Phase 6).
- 2026-06-29: Phase 3 routing advanced with an extensible `IActivityRoutingStrategy` pipeline, required-skills eligibility, longest-idle scoring, and `RoutingDecisionMade` audit events that capture candidate scores and reasons. Contact Center voice-provider resolution was added through `IContactCenterVoiceProviderResolver`; outbound dialer failure paths now cancel reservations and enforce max-attempt boundaries; inbound offer failures release reservations immediately; agent sign-in clears stale reservations and serializes profile creation. Queue and dialer admin UI now uses Orchard `ocat-*` layout and exposes routing skills, inbound endpoint mapping, and retry/do-not-call settings. Added routing/dialer/reservation tests and updated docs/changelog. Next: sticky-agent and business-hours routing, then wrap-up timers and required-disposition policies before Phase 7 real-time desktop work.
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
