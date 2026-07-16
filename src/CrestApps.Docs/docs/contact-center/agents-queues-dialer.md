---
sidebar_label: Agents, Queues & Dialer
sidebar_position: 1
title: Agents, Queues, Routing, and Dialer
description: Contact Center agent presence, queues, skill-aware routing, reservations, availability-based assignment, and voice-routed outbound dialing.
---

This phase adds the operational core of the Contact Center: agent presence, work queues, reservations, skill-aware routing, availability-based assignment, and an outbound dialer that routes voice calls through Contact Center Voice providers. Each capability is a separate, feature-gated module so tenants enable only what they need.

## Features

| Feature | Feature ID | Purpose |
| --- | --- | --- |
| Contact Center Agents | `CrestApps.OrchardCore.ContactCenter.Agents` | Agent profiles, reason codes, skills, and administrator-owned queue/campaign entitlements. |
| Contact Center Availability | `CrestApps.OrchardCore.ContactCenter.Availability` | Canonical routing availability, durable sessions, heartbeat tracking, capacity projection, after-call recovery, and logout synchronization without requiring SignalR. |
| Contact Center Queues | `CrestApps.OrchardCore.ContactCenter.Queues` | Managed skills, business-hours calendars, work queues, queue items, and reservations. |
| Contact Center Routing | `CrestApps.OrchardCore.ContactCenter.Routing` | Policy-based routing strategies and availability-based activity assignment over Contact Center queues. |
| Contact Center Dialer | `CrestApps.OrchardCore.ContactCenter.Dialer` | Outbound profiles, callbacks, and Manual or Preview inventory loads routed through Contact Center Voice. |
| Contact Center Outbound Compliance | `CrestApps.OrchardCore.ContactCenter.Compliance` | Mandatory eligibility, suppression auditing, retry, do-not-call, and calling-window enforcement before an outbound attempt executes. |
| Contact Center Automated Dialer | `CrestApps.OrchardCore.ContactCenter.Dialer.Automated` | Compliance-gated Power and Progressive strategies, automated batch source, and scheduled pacing. |
| Contact Center Entry Points | `CrestApps.OrchardCore.ContactCenter.EntryPoints` | Inbound voice entry-point administration, business-hours qualification, closed actions, and queue ingress. |
| Contact Center Recording | `CrestApps.OrchardCore.ContactCenter.Recording` | Optional recording orchestration and recording-state events over Contact Center Voice. |
| Contact Center Voice Media | `CrestApps.OrchardCore.ContactCenter.Voice.Media` | Dependency-only, non-GA executable media resolution foundation; transport certification is deferred to R9. |
| Contact Center Real-Time | `CrestApps.OrchardCore.ContactCenter.RealTime` | Shared SignalR hub and real-time presence, offer, and queue projections over the Availability state. |
| Contact Center Agent Desktop | `CrestApps.OrchardCore.ContactCenter.AgentDesktop` | CRM-integrated workspace, navigation, presence controls, offers, active interaction context, and recent work for agents. |
| Contact Center Supervision | `CrestApps.OrchardCore.ContactCenter.Supervision` | Live supervisor dashboard, queue and agent monitoring state, and provider-capability-gated monitoring actions. |
| Contact Center Reports & Analytics | `CrestApps.OrchardCore.ContactCenter.Analytics` | Enterprise report catalog under the shared Reports area, including executive, interaction, queue/SLA, agent, transfer, recording, campaign, and subject reports plus CSV exports. |
| Asterisk Contact Center Voice | `CrestApps.OrchardCore.Asterisk.ContactCenterVoice` | Asterisk implementation of the Contact Center voice provider boundary. |
| Asterisk Contact Center Media | `CrestApps.OrchardCore.Asterisk.ContactCenterMedia` | Dependency-only Asterisk RTP development foundation; unavailable in approved GA-Core profiles. |
| DialPad Contact Center Voice | `CrestApps.OrchardCore.DialPad.ContactCenterVoice` | DialPad implementation of the Contact Center voice provider boundary. |

## Agents and presence

An **agent profile** links an Orchard user to Contact Center configuration: display name, capacity, administrator-assigned skills, queue membership, campaign membership, and live presence. Presence states include `Offline`, `Available`, `Break`, `Away`, `DoNotDisturb`, `Meeting`, `Training`, `AfterHoursUnavailable`, and system-managed states such as `Reserved`, `Busy`, and `WrapUp`.

Agents sign in from the floating Telephony soft phone. When the Contact Center queues feature is enabled, Contact Center contributes a **Work** tab where agents select the queues and campaigns they want to receive work from and sign out. Signing in sets presence to `Available`; signing out sets it to `Offline`, clears the current queue/campaign membership, and Orchard logout now runs the same sign-out path after the Orchard logout request completes successfully so the browser is not left spinning on logout. When the Voice feature is enabled, signing in or returning to `Available` immediately offers any already-waiting inbound voice work from the selected queues instead of waiting for a new inbound call. The `SignIntoQueues` permission grants self-service sign-in.

Presence is a dropdown in the soft-phone header so agents can change availability without switching tabs. **Request break** is system-approved: if no assignment is in progress, the request is granted immediately and the agent enters `Break`; if a route/reservation is already in progress, the request is kept pending while the call continues, and the system grants `Break` automatically when that in-flight work is released. Agents in `RequestBreak` or `Break` are not eligible for new routing decisions.

Routing never treats the profile presence value by itself as proof that an agent can receive work. The Availability feature computes a canonical projection by joining an `Available` profile and queue entitlement with the agent's selected session queue, online connection state, fresh heartbeat, and remaining interaction capacity. The last connection disconnect therefore removes the agent from routing immediately without destroying the profile's requested presence during a transient reconnect; stale-session cleanup later performs the durable sign-out. Reservation creation repeats the canonical check while it holds the activity and agent transition locks, closing the race where a client disconnects after candidate selection.

Availability policy is tenant configuration:

```json
{
  "CrestApps_ContactCenter": {
    "Availability": {
      "HeartbeatTimeout": "00:01:30",
      "MaximumWrapUpDuration": "00:15:00"
    }
  }
}
```

`HeartbeatTimeout` controls how old a session heartbeat may be before routing treats the session as disconnected. `MaximumWrapUpDuration` is the server-owned after-call deadline. A tenant background task runs each minute and releases agents whose wrap-up interaction exceeded that deadline or whose `WrapUp` presence has no matching pending wrap-up interaction. Deadline recovery records the interaction's wrap-up completion time and restores the pending/default presence; it does not complete the CRM activity or invent a disposition.

### Presence state reference

| State | Set by | Meaning and routing behavior |
| --- | --- | --- |
| `Offline` | Agent/system | Signed out and ineligible for all work. |
| `Available` | Agent/system | Ready for work. A transition to this state publishes an event that triggers queued voice recovery in a separate service scope. |
| `Reserved` | System | An offer is assigned but not yet accepted. The agent cannot receive another offer beyond configured capacity. |
| `Busy` | System | The agent accepted and is actively handling an interaction. |
| `WrapUp` | System | The answered interaction ended and after-call work is pending. The agent remains ineligible until the CRM activity is completed or the server-owned recovery deadline releases orphaned capacity. |
| `RequestBreak` | Agent | A request to enter `Break`; when work is already reserved or active, the request is stored and granted after the work is released or completed. |
| `Break` | Agent/system | A granted break. The agent is signed in but ineligible for work. |
| `Away` | Agent | Not ready because the agent is away from the desk. |
| `DoNotDisturb` | Agent | Not ready and should not receive work. |
| `Meeting` | Agent | Not ready because the agent is in a meeting. |
| `Training` | Agent | Not ready because the agent is in training. |
| `AfterHoursUnavailable` | Agent/system | Not ready outside staffed hours. |

`Reserved`, `Busy`, and `WrapUp` are system-managed. Agents should not manually force those states. A completed wrap-up returns the agent to the pending requested state, when one exists, or otherwise to the default ready state (`Available` for a signed-in agent and `Offline` for a signed-out agent).

Every sign-in, sign-out, and presence transition is stored as a durable Contact Center event with the previous state, current state, requested state, reason code, queue memberships, campaign memberships, and transition time. This event history supports calculations such as available time, break time, not-ready time, queue/campaign staffing time, and state-transition audits. Voice interactions separately record creation, answer, end, wrap-up-start, and wrap-up-completion timestamps; reports include talk time, wrap-up time, and average handle time.

## Agent state reason codes

Administrators define **reason codes** from **Interaction Center → Management → Agent states** so agents pick an auditable, standardized reason when they go not ready. A reason code has a unique name, an optional description, the presence state it places the agent in (`AppliesTo` — `Break`, `Away`, `DoNotDisturb`, `Meeting`, `Training`, or `AfterHoursUnavailable`), a sort order, and an enabled flag. The catalog is managed with the same display-driver CRUD pattern as Skills and queues, and the `ManageContactCenterAgents` permission gates it.

When reason codes are configured, the soft-phone presence dropdown lists them (ordered by sort order) in place of the fixed not-ready states; selecting one sets the agent's presence to the reason's `AppliesTo` state and records the reason on the agent profile and the `AgentPresenceChanged` event. If no reason codes exist, the dropdown falls back to the built-in not-ready states.

The Agents feature seeds a standard set of reason codes at setup (short break, lunch, away from desk, team meeting, training, coaching, and system issue) by running the `agent-state-reason-codes` module recipe. Reason codes are also importable through the `AgentStateReasonCode` recipe step so they can be seeded or moved between tenants in deployment recipes.

## Skills

Administrators manage routeable capabilities from **Interaction Center → Management → Skills**. A skill has a unique name, description, and enabled state. Enabled skills appear in admin assignment surfaces and queue editor selectors; disabled skills remain on existing agents and queues but are hidden from new selections. Agents do not self-select skills from the soft phone because skills are routing eligibility data owned by supervisors/administrators.

Queues can require one or more skills. Agents must have every required skill assigned on their agent profile to be eligible for that queue, and the default routing strategy filters out agents missing any required skill before longest-idle scoring runs.

## Queues, reservations, and assignment

A **queue** holds activities waiting for an agent, with a default priority, an SLA threshold, required skills, an optional inbound channel endpoint mapping, a reservation timeout, a routing policy, an optional business-hours calendar, and optional overflow settings. Activities enter a queue as **queue items**; the system pairs the highest-priority, oldest waiting item with an eligible available agent signed in to that queue and creates a short-lived **reservation**.

Administrators can organize queues under **Interaction Center → Management → Queue groups** and select an optional group in each queue editor. Queue groups are catalog and reporting metadata only: they do not provide routing defaults, SLA inheritance, agent entitlements, capacity, overflow, or any other queue behavior. The dedicated **Manage Contact Center queue groups** permission controls the group catalog, while queue configuration remains controlled by **Manage Contact Center queues**.

Queue-group reports use **current-membership semantics**. An interaction keeps its queue identifier, while the report resolves that queue's group from the current queue catalog when the report runs. Moving a queue to another group therefore changes the group attribution of its historical interactions; it does not rewrite or reroute those interactions. Deleting a queue group makes its assigned queues ungrouped. Queue Usage includes per-queue rows, queue-group aggregate rows, and a recalculated grand total.

If no eligible agent is available when an activity enters the queue, the activity remains durable waiting work; it is not rejected merely because no agent is immediately available. Signing in, returning to **Available**, the assignment background task, or another routing trigger can offer it later. Business-hours, overflow, reservation-timeout, voicemail, and rejection policies determine when waiting work should move or end.

Routing is strategy-based. The strategy chain first rejects agents that do not have every required queue skill, then rejects agents that are already handling their maximum number of concurrent interactions, then applies the queue's selected scoring strategy. Each assignment publishes an auditable routing-decision event that records the queue item, selected agent, candidate scores, and reasons, so later supervisor and analytics features can explain why work was offered to an agent.

### Routing policy

Each queue selects a primary **routing strategy** that decides which available, eligible agent receives the next item:

- **Longest idle** (default) — offers work to the agent who has been available the longest.
- **Round robin** — distributes work fairly by offering to the agent who least recently received an assignment (tracked on the agent's `LastAssignedUtc`, stamped when a reservation is created).
- **Least busy** — offers work to the agent currently handling the fewest active interactions.

Only the selected strategy scores candidates; the other primary strategies stay inert for that queue.

When a queue enables **prefer sticky agent**, routing boosts the eligible candidate who most recently owned the activity (captured from the activity's assigned user when it is enqueued), so returning work prefers the agent the customer already worked with. The sticky preference is additive and never overrides skill or capacity eligibility.

When a queue enables **SLA aging**, a waiting item's effective priority increases by one step for every SLA-threshold interval it waits beyond the threshold, so aging work is routed ahead of newer higher-priority work instead of starving.

Agent capacity is enforced during candidate selection. Each agent profile defines `MaxConcurrentInteractions` (default `1`), and the capacity routing strategy counts the agent's active (not ended and not failed) interactions before they can be offered new work, so an agent is never offered more concurrent interactions than they are configured to handle.

### Business hours and overflow

A queue can reference a reusable **business-hours calendar** (managed from **Interaction Center → Management → Business hours**). A calendar defines a time zone, a weekly open window per day, and all-day holiday dates. Weekly windows can cross midnight, and equal opening/closing times represent an enabled 24-hour day. While the calendar reports the queue closed, assignment pauses. The queue's **after-hours action** decides what happens to waiting items: *Hold in queue* keeps them until the queue reopens, and *Overflow* moves them to the configured overflow queue.

Independently of business hours, a queue may set an **overflow queue** and an **overflow-after** threshold. Waiting items that exceed the threshold are moved to the overflow queue so long-waiting work can be picked up by a broader team. Contact Center preserves the original enqueue time for SLA aging while separately tracking when the item entered its current queue, so every overflow hop receives its configured dwell time and visited queues cannot form a routing cycle. Overflow moves run each minute alongside reservation expiry and assignment.

A reservation locks the activity for one agent and can be accepted, rejected, canceled, or expired. The CRM activity moves through `Available → Reserved → Assigned`, mirrored on the queue item and agent presence. Canceled reservations always return the item to the queue. The **Reservation timeout (seconds)** setting controls how long an unanswered offer stays reserved, and **Unanswered offer action** controls what happens when that timeout expires: requeue the work, send the live voice call to voicemail, or reject the live voice call. Voicemail and reject are voice-only actions; the terminal reservation transition and provider-command intent commit together before provider execution, while the live interaction remains nonterminal until the provider confirms the action. A definitive provider rejection re-enqueues and reoffers the still-live call; an uncertain outcome remains isolated for reconciliation so the platform does not risk both redirecting and reoffering the same call. When there is no live provider call or command infrastructure to act on, the system safely falls back to requeueing the work instead of dropping it. A background task expires stale reservations and assigns waiting work every minute.

Declining an inbound offer rejects that reservation and immediately makes the agent eligible according to their pending/default presence while routing tries the next eligible agent. If the agent does not respond before the reservation timeout, the queue's unanswered-offer action is applied. Preview outbound work remains agent-controlled; power and progressive work is owned by the dialer pacing cycle rather than the generic inbound assignment loop. Automated dialer reservations without a valid interaction are released so pacing can retry safely instead of leaving the agent stuck in `Reserved`.

Assignment uses distributed locks for contention control and database invariants for correctness. Each queue's assignment runs under a per-queue lock; reservation creation acquires an activity lock and then an agent lock in a consistent order; and accept/reject/cancel/expiry transitions share a per-reservation lock. Before reserving, the service revalidates canonical session liveness and queue opt-in, the agent's current presence, pending reservation ownership, and active-interaction capacity. YesSql document-version checks make the queue, reservation, agent, and CRM activity updates one compare-and-set commit. Portable unique claim keys allow only one active queue item per activity, one pending or accepted reservation per activity, and one pending reservation per agent while retaining terminal history. A writer that loses the database race aborts the operation scope instead of reusing YesSql's canceled session, so lock expiry or overlapping holders cannot publish a second successful reservation.

Inbound offers are local atomic transitions. Assignment does not synchronously query the provider or dispatch a transport notification before commit; provider webhooks and reconciliation own provider truth, while the durable `AgentReserved` outbox projection delivers the offer after the reservation commit.

## Dialer

A **dialer profile** is an execution policy, not the source of CRM work. Activities, campaigns, subjects, inventory definitions, dispositions, and contact context still come from Omnichannel. The profile tells the Contact Center how a specific outbound campaign should be dialed: which queue supplies agents, which dialing mode is used, which Contact Center voice provider places calls, how pacing works, and how attempts/retries and compliance are bounded. Enable **Contact Center Outbound Compliance** before executing outbound attempts. Enable **Contact Center Automated Dialer** for Power and Progressive profiles; it cannot activate without Compliance and runs its pacing cycle each minute. Manual and Preview profiles remain agent-driven. Dialer inventory loads create **unassigned** activities and enqueue automated work immediately so the selected profile can reserve it without a separate operator enqueue step.

### Dialing modes and safety

Each automated mode is implemented as a dedicated `IDialerStrategy`, so unsupported modes are withheld rather than falling through to an unsafe default:

| Mode | Behavior |
| --- | --- |
| `Manual` | The agent chooses and places the call. No automated cycle runs. |
| `Preview` | The agent reviews the activity, then accepts or skips. Accepting the offer starts the outbound attempt through the configured Contact Center voice provider; no automated cycle runs. |
| `Power` | Reserves agents and places a capped number of calls per cycle. **Calls per agent is hard-capped** (`PowerDialerStrategy.MaxCallsPerAgent`) until predictive pacing exists. Requires the **Contact Center Automated Dialer** feature. |
| `Progressive` | Places one call per available agent as agents become available. Requires the **Contact Center Automated Dialer** feature. |
| `Predictive` | **Disabled.** The editor hides it, saving it is rejected, and the dialer refuses to run it until answer-rate forecasting exists. |

The Power and Progressive automated pacing modes, their strategies, scheduled pacing task, and automated batch source live in the **Contact Center Automated Dialer** feature, which hard-depends on **Contact Center Outbound Compliance**. When that feature is disabled, the dialer-profile editor only offers Manual and Preview, and saving a Power or Progressive profile is rejected so a profile can never silently fail to pace. Manual and Preview remain on the base **Contact Center Dialer** feature.

### Outbound compliance gate

Before every attempt, `IDialerEligibilityService` runs and records an auditable `DialSuppressed` event when an attempt must be blocked. The default gate enforces, in order:

- **Destination present** and the **maximum attempt count** has not been reached.
- **Retry cool-down** - a previous attempt must be older than `RetryDelayMinutes`.
- **Do-not-call / communication preferences** - the contact's `DoNotCall` opt-out (when *Respect do-not-call and communication preferences* is enabled).
- **Calling window** - when *Enforce a calling window* is enabled, the destination is only dialed while its business-hours calendar reports open. The profile selects a default **calling calendar** and optional per-region calendar overrides keyed by the destination's ISO 3166-1 alpha-2 region code; the calendar is evaluated in the contact's own time zone. A missing or disabled required calendar fails closed rather than silently allowing calls.
- **Abandonment cap** - when *Enforce an abandonment cap* is enabled for an automated pacing mode (Power/Progressive), the profile's rolling live-answer/abandon statistics must stay at or below `MaxAbandonmentRatePercent`. The cap is only evaluated once the rolling window has accumulated at least `AbandonmentSampleFloor` live answers, and it **fails closed**: an automated profile that enforces the cap but cannot prove its current rate is suppressed. Manual and Preview modes bind an agent per call and are always permitted.
- **National do-not-call registries** - any registered `INationalDoNotCallRegistry` (for example the USA FTC or Canada DNCL registries) is scrubbed when *Respect do-not-call* is enabled.

When an automated profile enforces the abandonment cap, **safe-harbor messaging** must be enabled with an announcement so a live party that no agent reaches hears a caller-identifying message instead of a silent drop. `CrestApps_ContactCenter:Compliance:AbandonmentRollingWindowMinutes` (default 30, range 1-1440) sets the rolling measurement window and is validated on start.

Answering-machine detection (AMD) outcomes reported by the provider are mapped to a provider-neutral `AnswerClassification` (`Human`, `Machine`, `Fax`, `Unknown`) and stored on the call session and interaction technical metadata under the stable `amd_answer_classification` key for downstream pacing and analytics.

Do-not-call, registry, missing-destination, and maximum-attempt suppressions are terminal and remove the queue item so the dialer cannot retry them forever. Calling-window, abandonment, and cool-down suppressions release the reservation and leave the activity available for a later cycle. Terminal provider dial failures also remove the queue item, while retryable failures remain available according to the configured retry policy.

Before an eligible automated attempt reaches the provider, the dialer commits the reservation as accepted, then stages the activity attempt count, interaction, stable provider command, domain event, and event-outbox record in one tenant database commit. The command identifier is sent on the provider request and in its metadata, allowing provider adapters and later reconciliation to correlate retries without creating a second logical command; the DialPad adapter forwards it as the HTTP `Idempotency-Key` header. The request path never sends the provider operation itself: it only schedules a best-effort post-commit wake-up, while durable command recovery remains the authoritative executor after a process failure. If reservation acceptance fails, no interaction or provider request is created. If the atomic command-intent commit fails, compensation runs in a fresh Orchard child scope so a canceled YesSql session is never reused.

The durable provider-command state machine orchestrates these actions using explicit `Pending`, `Claimed/Fenced`, `Sent`, `OutcomeUnknown`, `Confirmed`, `Compensating`, and terminal states. Command registration is serialized per idempotency key; dispatch, reconciliation, and compensation use fenced leases; and a superseded caller treats a lost claim as a handoff to the authoritative recovery owner instead of reporting a safe-to-redial failure. Type-specific executors keep Dial compliance/projections separate from inbound Answer/Connect and timeout voicemail/reject behavior while sharing the same recovery and fencing rules. Server-side inbound offer acceptance, answered-outbound bridging, and unanswered-offer actions persist command intent before provider operations; agent-device-native acceptance remains local because the device owns its answer action. Unknown-outcome and terminal confirmation settlement commit with the related interaction, call-session, activity, domain-event, and outbox projections in one tenant database transaction. Provider transport failures and ambiguous HTTP 408, 429, and 5xx responses transition to `OutcomeUnknown`; deterministic client rejections remain definitive failures. `ProviderCommandRecoveryBackgroundTask` runs each minute and processes bounded batches selected through due-time and expired-lease indexes. Pending Dial commands are not blindly dispatched after a crash: recovery reloads the governing dialer profile and activity and re-evaluates current policy. Missing policy infrastructure, profiles, activities, or ineligible decisions fail closed and compensate the accepted work without contacting the provider. Every CAS-sensitive recovery transition runs in a fresh Orchard child scope, so a normal optimistic-concurrency ownership loss cannot reuse a canceled YesSql session or abort unrelated commands in the batch. Recovery attempts reconciliation by command key before any retry; providers that cannot prove the outcome transition safely to `Paused` instead of risking duplicate execution.

### Callback operations

Callbacks use the same Activity, queue, routing, and disposition path as outbound campaign calls. A `CallbackRequest` records the contact, destination, optional campaign and queue, requested/due window, attempt count, status, and notes. The callback dispatcher runs every minute and promotes each due pending callback into an outbound `Callback` activity. When the request has a queue, that activity is enqueued so the next eligible signed-in agent receives it through the Agent Workspace.

Use callbacks when an agent schedules a later follow-up, an inbound entry point offers a callback instead of waiting in queue, or workflow automation decides the next best action is a phone callback. Managers should configure a dedicated callback queue when callbacks need different SLA, skills, or priority from live inbound calls. Agents handle the promoted callback like any other outbound call: answer the offer, complete the conversation, select a disposition, and finish wrap-up through the Subject Flow.

## Voice Contact Center Call Router

The dialer never talks to a telephony platform directly. It calls `IVoiceContactCenterCallRouter`, which resolves the configured `IContactCenterVoiceProvider`, so the Contact Center keeps assignment, queue, pacing, and compliance logic while the provider executes call operations. The `CrestApps.OrchardCore.DialPad.ContactCenterVoice` feature implements `IContactCenterVoiceProvider` over the DialPad telephony provider. The `CrestApps.OrchardCore.Asterisk.ContactCenterVoice` feature provides the equivalent Asterisk adapter and uses the tenant Asterisk provider when enabled, otherwise resolving the configured **Default Asterisk** provider.

Voice providers that support contact-center orchestration beyond soft-phone call control can also register `IContactCenterVoiceProvider`. The `IContactCenterVoiceProviderResolver` resolves those providers by technical name so future PBX integrations can participate in provider-side queueing, call assignment, and voice-specific orchestration without coupling Contact Center to one provider. Dial results include the actual executing provider identity, which is persisted on the interaction so provider events and reconciliation use the same configured alias.

## Admin UX and extensibility

Contact Center management entries live under **Interaction Center**. Queue groups, skills, queues, business-hours calendars, and dialer profile CRUD screens match the Omnichannel Campaigns UI: searchable list pages render summary shapes, and create/edit screens render display-driver editor shapes with the required root edit wrapper templates. Agent sign-in and presence are injected into the Telephony soft phone through `DisplayDriver<SoftPhoneWidget>`, so the operational controls stay with the phone while management screens remain catalog-focused.

Agent state reason codes are a catalog-backed admin surface (**Interaction Center → Management → Agent states**), not a provider-specific dialer setting. The Agents feature seeds standard reason codes during tenant setup by executing the `agent-state-reason-codes` module recipe, and the `AgentStateReasonCode` recipe step lets reason codes be imported or moved between tenants. A dedicated deployment-plan step is a planned follow-up.

## Enable via recipe

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.ContactCenter",
        "CrestApps.OrchardCore.ContactCenter.Agents",
        "CrestApps.OrchardCore.ContactCenter.Queues",
        "CrestApps.OrchardCore.ContactCenter.Dialer",
        "CrestApps.OrchardCore.ContactCenter.RealTime",
        "CrestApps.OrchardCore.ContactCenter.Analytics",
        "CrestApps.OrchardCore.DialPad.ContactCenterVoice"
      ]
    }
  ]
}
```
