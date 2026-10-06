---
sidebar_label: Agents, Queues & Dialer
sidebar_position: 1
title: Agents, Queues, Routing, and Dialer
description: Contact Center agent presence, queues, skill-aware routing, reservations, availability-based assignment, and voice-routed outbound dialing.
user_manual:
  - user-manual/queues
  - user-manual/skills-and-entitlements
  - user-manual/agent-states
  - user-manual/dialer-profiles
  - user-manual/business-hours
  - user-manual/agent-workspace
---

This phase adds the operational core of the Contact Center: agent presence, work queues, reservations, skill-aware routing, availability-based assignment, and an outbound dialer that routes voice calls through Contact Center Voice providers. Each capability is a separate, feature-gated module so tenants enable only what they need.

## Features

| Feature | Feature ID | Purpose |
| --- | --- | --- |
| Contact Center Agents | `CrestApps.OrchardCore.ContactCenter.Agents` | Agent profiles, reason codes, and queue/campaign sign-in, plus canonical routing availability, durable sessions, heartbeat tracking, capacity projection, after-call recovery, and logout synchronization without requiring SignalR. |
| Contact Center Agent Entitlements | `CrestApps.OrchardCore.ContactCenter.AgentEntitlements` | Optional. Restricts which queues and campaigns each agent may sign in to, with the **Agent entitlements** administration screen. When it is disabled, any agent may sign in to any queue or campaign. |
| Contact Center Business Hours | `CrestApps.OrchardCore.ContactCenter.BusinessHours` | Business-hours calendars, their administration screen, and the evaluation service used by queues, entry points, and automated sends. Enabled on its own or pulled in by Work Distribution. |
| Contact Center Work Distribution | `CrestApps.OrchardCore.ContactCenter.Queues` | Managed skills, work queues, queue items, and reservations, plus policy-based routing strategies and availability-based activity assignment over Contact Center queues. |
| Contact Center Outbound Dialer | `CrestApps.OrchardCore.ContactCenter.Dialer` | Outbound profiles, callbacks, Preview activity loads routed through Contact Center Voice, and mandatory eligibility, suppression, retry, do-not-call, and calling-window enforcement. |
| Contact Center Paced Dialing | `CrestApps.OrchardCore.ContactCenter.Dialer.Paced` | Compliance-gated Power, Progressive and Predictive strategies, the Predictive pacing statistics and tenant options, paced batch source, and scheduled pacing. Its base Dialer dependency includes the Dialer Profiles UI. |
| Contact Center Inbound Voice | `CrestApps.OrchardCore.ContactCenter.InboundVoice` | Inbound voice entry-point administration, business-hours qualification, closed actions, and queue ingress. |
| Contact Center Call Recording | `CrestApps.OrchardCore.ContactCenter.Recording` | Optional recording orchestration and recording-state events over Contact Center Voice. |
| Contact Center Voice Media | `CrestApps.OrchardCore.ContactCenter.Voice.Media` | Dependency-only, non-GA executable media resolution foundation; transport certification is deferred to R9. |
| Contact Center Real-Time | `CrestApps.OrchardCore.ContactCenter.RealTime` | Shared SignalR hub and real-time presence, offer, and queue projections over the workforce availability state. Enabled by dependency only (auto-enabled by Contact Center Voice and Supervision). |
| Contact Center Supervision & Live Dashboard | `CrestApps.OrchardCore.ContactCenter.Supervision` | Live supervisor dashboard, queue and agent monitoring state, and provider-capability-gated monitoring actions. |

> The server-side voice orchestration (`CrestApps.OrchardCore.ContactCenter.Voice`) is enabled automatically as a dependency of Inbound Voice, the Dialer, Recording, and Supervision, so it is not a separately selectable feature. It in turn pulls in Contact Center Real-Time and the phone-number administration, which is where [outbound lines](#outbound-lines) are set up.

> The **enterprise report catalog** (executive, interaction, queue/SLA, agent, transfer, recording, campaign, and subject reports plus CSV exports) is not a separately selectable feature. It activates automatically under the shared Reports area whenever both Contact Center Work Distribution (`CrestApps.OrchardCore.ContactCenter.Queues`) and the Reports framework (`CrestApps.OrchardCore.Reports`) are enabled.

> The **CRM-integrated Agent Workspace** and each **provider contact center adapter** (for example Asterisk voice and Asterisk media) are integration glue rather than selectable features. The Agent Workspace activates whenever Contact Center Agents, Contact Center Voice, Contact Center Real-Time, and the Telephony soft phone are all enabled. A provider's voice adapter activates whenever that provider module and Contact Center Voice are both enabled (the Asterisk media adapter, whenever the Asterisk module and Contact Center Voice Media are both enabled), so an operator never enables a per-provider toggle that has to match the provider they already configured.

## Agents and presence

An **agent profile** links an Orchard user to Contact Center configuration: display name, capacity, administrator-assigned skills, queue membership, campaign membership, and live presence. Presence states include `Offline`, `Available`, `Break`, `Away`, `DoNotDisturb`, `Meeting`, `Training`, `AfterHoursUnavailable`, and system-managed states such as `Reserved`, `Busy`, and `WrapUp`.

Agents sign in from the floating Telephony soft phone. When the Contact Center queues feature is enabled, Contact Center contributes a **Work** tab where agents select the queues and campaigns they want to receive work from and sign out. Signing in sets presence to `Available`; signing out sets it to `Offline`, clears the current queue/campaign membership, and Orchard logout runs the same sign-out path after the Orchard logout request completes successfully so the browser is not left spinning on logout. When the Voice feature is enabled, signing in or returning to `Available` immediately offers any already-waiting inbound voice work from the selected queues instead of waiting for a new inbound call. The `ContactCenterSignIntoQueues` permission grants self-service sign-in.

Presence is a dropdown in the soft-phone header so agents can change availability without switching tabs. **Request break** is system-approved: if no assignment is in progress, the request is granted immediately and the agent enters `Break`; if a route/reservation is already in progress, the request is kept pending while the call continues, and the system grants `Break` automatically when that in-flight work is released. Agents in `RequestBreak` or `Break` are not eligible for new routing decisions.

Routing never treats the profile presence value by itself as proof that an agent can receive work. The Agents feature computes a canonical projection by joining an `Available` profile and queue entitlement with the agent's selected session queue, online connection state, fresh heartbeat, and remaining interaction capacity. The last connection disconnect therefore removes the agent from routing immediately without destroying the profile's requested presence during a transient reconnect; stale-session cleanup later performs the durable sign-out. Reservation creation repeats the canonical check while it holds the activity and agent transition locks, closing the race where a client disconnects after candidate selection.

Queue membership is normalized into a query-aligned index. Each agent profile projects one membership row per queue it is both entitled to and signed in to (the intersection of live sign-in and the administrator-owned allow-list), so selecting the candidate agents for a queue is a single indexed lookup by queue and `Available` presence rather than a tenant-wide scan of every available agent. The index stores queue identifiers lower-cased for portable, case-insensitive matching and preserves the fail-closed entitlement rule: an agent with no matching allow-list entry is never a member.

Who fills that allow-list depends on the optional **Contact Center Agent Entitlements** feature. When it is enabled, an administrator grants each agent's queues and campaigns on the **Agent entitlements** screen, and the agent can sign in only to those. When it is disabled, entitlements are permissive: any agent may sign in to any queue or campaign, and the queues and campaigns they pick are granted onto their profile as they sign in.

Availability policy is tenant configuration:

```json
{
  "CrestApps": {
    "ContactCenter": {
      "Availability": {
        "HeartbeatTimeout": "00:01:30",
        "MaximumWrapUpDuration": "00:15:00",
        "OrphanedBusyGracePeriod": "00:01:00"
      }
    }
  }
}
```

`HeartbeatTimeout` controls how old a session heartbeat may be before routing treats the session as disconnected. `MaximumWrapUpDuration` is the server-owned after-call deadline. A tenant background task runs each minute and releases agents whose wrap-up interaction exceeded that deadline or whose `WrapUp` presence has no matching pending wrap-up interaction. Deadline recovery records the interaction's wrap-up completion time and restores the pending/default presence; it does not complete the CRM activity or invent a disposition. `OrphanedBusyGracePeriod` (default one minute) is how long an agent may stay `Busy` after the call they accepted has ended; once it passes and the agent has no other active interaction, the same task returns them to work. Normally the call's own end releases the agent within moments, so this only catches an end the system missed.

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

When reason codes are configured, the soft-phone presence dropdown lists them (ordered by sort order) in place of the fixed not-ready states. A reason that applies to `Break` submits a break request with that reason, so the break starts at once when the agent is idle, or when the current work ends if work is holding them. Any other reason sets the agent's presence to the reason's `AppliesTo` state directly. Either way the reason is recorded on the agent profile and the `AgentPresenceChanged` event. If no reason codes exist, the dropdown falls back to the built-in not-ready states. **Available** and **Offline** are always listed.

The Agents feature seeds a standard set of reason codes at setup (short break, lunch, away from desk, team meeting, training, coaching, and system issue) by running the `agent-state-reason-codes` module recipe. Reason codes are also importable through the `AgentStateReasonCode` recipe step so they can be seeded or moved between tenants in deployment recipes.

## Skills

Administrators manage routeable capabilities from **Interaction Center → Management → Skills**. A skill has a unique name, description, and enabled state. Enabled skills appear in admin assignment surfaces and queue editor selectors; disabled skills remain on existing agents and queues but are hidden from new selections. Agents do not self-select skills from the soft phone because skills are routing eligibility data owned by supervisors/administrators.

Queues can require one or more skills. Skills are held at a proficiency from 1 to 5. A skill in the queue's **Required skills** list needs proficiency 3 or higher; the **Skill requirements** table can set a different minimum proficiency per skill, mark a skill as preferred rather than required, and relax a requirement after a wait. Agents must meet every enforced requirement to be eligible for that queue, and the routing chain filters out agents who miss one before the queue's scoring strategy runs.

Skill names are compared by the same rule wherever they are read: surrounding whitespace is not part of the name, and casing does not distinguish one skill from another, while interior spacing does — `Tier 2` and `Tier2` are two different skills. Because a queue's required skills and an agent's assigned skills are read through that one rule, an agent whose skills arrived through a recipe, a deployment, or an import is matched the same way as one edited in the administration screens.

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

A reservation locks the activity for one agent and can be accepted, rejected, canceled, or expired. The CRM activity moves through `Available → Reserved → Assigned`, mirrored on the queue item and agent presence. Canceled reservations always return the item to the queue. The **Reservation timeout (seconds)** setting controls how long an unanswered offer stays reserved, and **Unanswered offer action** controls what happens when that timeout expires: requeue the work, send the live voice call to voicemail, or reject the live voice call. Voicemail and reject are voice-only actions; the terminal reservation transition and provider-command intent commit together before provider execution, while the live interaction remains nonterminal until the provider confirms the action. A definitive provider rejection re-enqueues and reoffers the still-live call; an uncertain outcome remains isolated for reconciliation so the platform does not risk both redirecting and reoffering the same call. When there is no live provider call or command infrastructure to act on, the system safely falls back to requeueing the work instead of dropping it. An offer expires at its deadline: when the reservation is made its deadline is held in process, and within about a second of it the offer is expired and the caller moved on, under the same reservation lock and compare-and-set commit an accept takes, so an accept racing the deadline and the expiry never both win. A waiting caller's next overflow hop and maximum wait are held the same way, from when they enter a queue, and so is each queue's next treatment step (the welcome, the callback offer, the next periodic announcement); the queue-treatment background task makes one pass a minute as their backstop rather than ticking inside its own run. These in-process deadlines are an accelerator, not the record: when the tenant starts it re-arms them for every offer still ringing and every caller still waiting, and a background task also expires stale reservations and assigns waiting work every minute as the durable backstop for a restart or an offer made on another node, and — on a queue that keeps taking offers — each new or re-offered voice call also opportunistically reclaims a small bounded batch of due reservations before an agent is selected, so a lapsed offer's held capacity is freed on the next offer rather than waiting for the minute tick; this pass uses only a short, bounded lock wait so it adds at most a small, bounded delay to admitting the call, and the minute background task remains the authoritative backstop for an offer ignored on an otherwise idle queue.

Declining an inbound offer rejects that reservation and immediately makes the agent eligible according to their pending/default presence while routing tries the next eligible agent. If the agent does not respond before the reservation timeout, the queue's unanswered-offer action is applied. A call that goes back to the queue after an agent declined it, let it ring out, or transferred it there is not rung straight back to that agent while somebody else could take it: declining does not change how long an agent has been idle, and a direct call leaves the transferring agent no after-call work, so without this the same agent would be picked again at once while the next agent in line was never rung. The queue item records who declined it, in order, and who transferred it in, and routing offers it, among the agents who can take it now, to: an agent who has neither declined it nor transferred it away (chosen by the queue's routing strategy); then an agent who declined it before the latest decline, earliest first; then the agent who transferred it in; and only then the agent who declined it last. No routing strategy or sticky-agent preference overrides that order, but nobody is held back either: when the only agent free is the one who just declined (or the one who transferred the call in), the call is offered to them again straight away rather than the caller being held with an agent available. For example, an agent transfers a caller to a queue and the only other agent declines the offer: the call is offered back to the transferring agent at once. The declined offer's phone leg is hung up before the re-offer is routed, so the new offer never rings over the old one, and the queue's maximum wait, voicemail and overflow still move on a caller who keeps going unanswered. The decline order applies to queue offers only: a direct line rings the one agent it belongs to, and campaign inventory is paced by the dialer. Declining is bound to the reservation it names, so a second decline for an offer that has already been declined (from another window, or a second click) does nothing. Preview outbound work remains agent-controlled; power and progressive work is owned by the dialer pacing cycle rather than the generic inbound assignment loop. Automated dialer reservations without a valid interaction are released so pacing can retry safely instead of leaving the agent stuck in `Reserved`.

Assignment uses distributed locks for contention control and database invariants for correctness. Each queue's assignment runs under a per-queue lock; reservation creation acquires an activity lock and then an agent lock in a consistent order; and accept/reject/cancel/expiry transitions share a per-reservation lock. Before reserving, the service revalidates canonical session liveness and queue opt-in, the agent's current presence, pending reservation ownership, and active-interaction capacity. YesSql document-version checks make the queue, reservation, agent, and CRM activity updates one compare-and-set commit. Portable unique claim keys allow only one active queue item per activity, one pending or accepted reservation per activity, and one pending reservation per agent while retaining terminal history. A writer that loses the database race aborts the operation scope instead of reusing YesSql's canceled session, so lock expiry or overlapping holders cannot publish a second successful reservation.

Inbound offers are local atomic transitions. Assignment does not synchronously query the provider or dispatch a transport notification before commit; provider webhooks and reconciliation own provider truth, while the durable `AgentReserved` outbox projection delivers the offer after the reservation commit.

## Dialer

A **dialer profile** is an execution policy, not the source of CRM work. Activities, campaigns, subjects, activity load definitions, dispositions, and contact context still come from Omnichannel. The profile is a reusable set of dialing settings: which dialing mode is used, which Contact Center voice provider places calls, how pacing works, and how attempts/retries and compliance are bounded. It does not name a queue or a campaign. The campaign is chosen when activities are loaded: a **Load Activities** batch with the **Dialer** source picks both the campaign and the dialer profile, and for outbound work the campaign itself acts as the routing queue. Mandatory outbound compliance is part of the base **Contact Center Outbound Dialer**, so Preview, Power, and Progressive attempts cannot run without the eligibility and suppression gates. Enable **Contact Center Paced Dialing** for Power and Progressive profiles; it inherits the **Dialer Profiles** screen and compliance services from the base Dialer feature and runs its pacing cycle each minute. Preview profiles remain agent-driven. Dialer activity loads create **unassigned** activities and enqueue automated work immediately so the selected profile can reserve it without a separate operator enqueue step.

### Dialing modes and safety

Each automated mode is implemented as a dedicated `IDialerStrategy`, so unsupported modes are withheld rather than falling through to an unsafe default:

| Mode | Behavior |
| --- | --- |
| `Preview` | The agent reviews the activity, then accepts or skips. Accepting the offer starts the outbound attempt through the configured Contact Center voice provider; no automated cycle runs. |
| `Power` | Each pacing cycle starts up to **Calls per agent** calls for the campaign, each reserving its own available agent. **Calls per agent** is capped at 3 (`PowerDialerStrategy.MaxCallsPerAgent`). Requires the **Contact Center Paced Dialing** feature. |
| `Progressive` | Places one call per available agent as agents become available, bounded at 100 calls per pacing cycle. Requires the **Contact Center Paced Dialing** feature. |
| `Predictive` | Requires the **Contact Center Paced Dialing** feature. Runs the Power loop (one reserved agent per call, up to **Calls per agent** per cycle), with the over-dial ratio throttled by the measured abandonment rate; with `PredictivePacingModel.OverDial`, places calls without a reserved agent and claims an agent when a person answers. See [Predictive dialing](#predictive-dialing). |

Manual is not a dialer-profile mode: agents dial freely from the soft phone under the separate manual-dialing screening described below. A profile saved as Manual by an earlier version opens and re-saves as Preview.

The Power, Progressive and Predictive automated pacing modes, their strategies, scheduled pacing task, and automated batch source live in the **Contact Center Paced Dialing** feature, which depends on the base **Contact Center Outbound Dialer**. The base Dialer owns the dialer-profile editor and mandatory compliance services together with its runtime services, so enabling Paced Dialing exposes a complete configuration and execution surface while Orchard resolves the rest of the dependency graph. When that feature is disabled, the dialer-profile editor only offers Preview, and saving a Power, Progressive or Predictive profile is rejected so a profile can never silently fail to pace; a stored one resolves to no strategy and the pacing cycle skips it with a warning. Preview remains on the base **Contact Center Outbound Dialer** feature.

### Predictive dialing

**Contact Center Paced Dialing** (`DialerPacedStartup`) registers `PredictiveDialerStrategy` with the Power and Progressive strategies, binds `ContactCenterPredictiveDialingOptions` (see [Predictive dialing options](../configuration.md#predictive-dialing)) and registers `IDialerPacingStatisticsProvider`. There is no separate Predictive feature: what a Predictive profile may do beyond one call per reserved agent is decided by its pacing model, whose safeguards are validated on every save, import and deployment.

**Pacing models.** `DialerProfile.PredictivePacingModel` selects how a Predictive profile paces:

| Model | Behavior |
| --- | --- |
| `ReservedPerCall` (default) | The Power reserve-then-dial loop: each call reserves its own available agent before it is placed, so a live answer always has an agent waiting. `PredictiveDialerPacing` scales the per-cycle count between **Calls per agent** and one as the measured abandonment rate approaches the cap. |
| `OverDial` | Places more calls than there are free agents without reserving any, sized by `PredictiveOverDialCalculator`, and claims a free agent when a person answers; with nobody free the person hears the abandoned-call message and the call counts abandoned. Every cycle the calculator cannot prove safe falls back to the `ReservedPerCall` loop. See [Over-dialing](#over-dialing). |

**Profile settings.** Stored on the profile document, so no migration is needed, and validated by `DialerProfileHandler` for Predictive profiles only (a Power profile is never refused for a setting it does not use):

| Property | Default | Range | Purpose |
| --- | --- | --- | --- |
| `TargetAbandonmentRatePercent` | 2 | > 0 to 100; below `MaxAbandonmentRatePercent` for `OverDial` | The rate over-dialing steers toward; the cap stays the hard limit. |
| `MaxLinesPerAgent` | 2 | 1 to 5 | Most calls in flight per free agent. |
| `MaxCallsInFlight` | 100 | 1 to 1000 | Most calls in flight per campaign. |
| `AnswerRateSampleFloor` | 50 | 10 to 10000 | Settled calls the answer rate needs before over-dialing trusts it. |
| `AnswerRateWindowMinutes` | 15 | 5 to 240 | Rolling window of the answer rate. |
| `CreditAgentsFreeingUp` | `false` | | Count agents predicted to free up within the ring horizon. |
| `FreeUpCreditPercent` | 50 | 0 to 100 | Share of those agents counted. |
| `ConnectWaitMilliseconds` | 0 | 0 to 1500 | How long an answered over-dialed call may wait for an agent before the abandoned-call message. |
| `AbandonedRetryRequiresAgent` | `true` | | Retry an abandoned call only with a reserved agent. |

`OverDial` additionally requires **Enforce an abandonment-rate cap**, the abandoned-call message, a cap above 0 and a target below the cap. The constants live in `PredictiveDialingDefaults`. The recipe step and deployment carry every property.

**Statistics.** `InteractionEventDialerPacingStatisticsProvider` measures a profile over a rolling window from the event log the platform already writes, with no new index and no new writes on the call path:

- **Calls placed** count `DialerAttemptStarted` events, filed by `DialerAttemptService` under `AggregateType = DialerProfile`, with one seek on the aggregate index.
- **Answered by a person** and **abandoned** come from `IDialerAbandonmentStatisticsProvider`, so pacing steers by exactly the counts the abandonment cap enforces.
- **Answer rate and timings** come from `DialerPacingQueries.BuildCallTimingsSql`: the newest `MaxTimingSamples` attempts in the window, each with correlated sub-selects for its first `DialerLiveAnswered` and `AgentLegAnswered` events (event index `InteractionId` key) and the interaction's `EndedUtc`, `WrapUpStartedUtc` and `WrapUpCompletedUtc` (interaction index `ItemId` key). A call that has neither a live answer, an agent nor an end is still ringing and is left out of the answer rate, which would otherwise read low and over-dial; a call an agent was connected to counts as answered even without a recorded live answer (calls placed before live answers were recorded). Ring-to-answer (median and 75th percentile), connect latency (median and 95th percentile), average talk time and average wrap-up are derived in memory.
- A measurement is cached for `StatisticsCacheDuration` in the tenant-wide `DialerPacingStatisticsCache`. When the abandonment counts cannot be read the provider returns `null`, which callers treat as a reason not to over-dial.

The editor shows the measured answer rate, ring time and connect time on the **Predictive pacing** card of a saved Predictive profile.

**Calculation.** `PredictiveOverDialCalculator.Calculate(PredictivePacingInput)` is a pure function that returns a `PredictivePacingDecision` (`Mode`, `Reason`, `DialCount`, `TargetCalls` and the figures behind them). `PredictiveOverDialPacer` calls it every over-dial cycle. In order:

1. The abandonment policy does not permit dialing: `Suppressed`.
2. Invalid limits (no cap, target not below the cap, lines per agent below 1), a missing or out-of-range answer rate, rolling rate or long-run (`ComplianceWindowDays`) rate, a sample below its floor, or either rate at or above the cap: `ReservedFallback`, so the campaign keeps the reserve-then-dial loop that cannot abandon.
3. Throttle `g`: 1 while the rolling rate is at most half the target, falling in a straight line to 0 at the cap; the over-dial is sized against `target × g`.
4. Agents `E` = available agents plus `FreeUpCreditPercent` of the agents `AgentFreeUpPredictor` expects to free up (only with `CreditAgentsFreeingUp`). Fewer than one: no calls.
5. The answer rate `r` is bounded to 0.05 to 1. With `X ~ Binomial(N, r)`, the expected abandoned calls are the sum over `k > E` of `(k − E)·P(X = k)`, computed exactly in log space. The calculator picks the largest `N` from `⌊E⌋` whose expected abandoned calls over `N·r` stay within the throttled target. Calls already ringing are treated as freshly placed, which overstates the answers to come and so errs toward fewer calls.
6. `N` is capped at `⌊MaxLinesPerAgent (at most 5) × available agents⌋ + ⌊credited agents⌋` and at `MaxCallsInFlight`; the calls to place are `N` minus the calls in flight, between 0 and `MaxDialsPerCycle`.

`AgentFreeUpPredictor` counts busy agents expected to be free within a horizon: a wrapping agent after the rest of the average wrap-up, a talking agent after the rest of the average call plus a whole average wrap-up. An agent already past the average, or a phase with no measured average, gets no credit.

### Over-dialing

**Placing calls.** `PredictiveDialerStrategy` hands an `OverDial` profile's cycle to `IPredictiveOverDialPacer`. `PredictiveOverDialPacer` takes the queue's pacing lock `ContactCenterPredictivePacing:{queueId}` without waiting (expiry `PacingLockExpiration`) and measures the inputs: the policy (`IDialerAbandonmentPolicyService`), the answer rate and timings over `AnswerRateWindowMinutes` (`IDialerPacingStatisticsProvider`), the rolling abandonment rate over `AbandonmentRollingWindowMinutes` and the long-run rate over `ComplianceWindowDays` (`IDialerAbandonmentStatisticsProvider`), the available agents of the campaign queue without an active reservation (`IAgentAvailabilityService.GetForQueueAsync`), the agents freeing up within the median ring-to-answer time (`DefaultRingHorizon` until measured), and the calls in flight (`IQueueItemStore.CountDialerInFlightAsync`: Assigned items with no agent). `ReservedFallback` runs the reserve-then-dial loop; `Suppressed` and a cycle that did not run dial nothing. Otherwise the head of the queue is read the way routing reads it (`RoutableQueueHead`, shared with `ActivityAssignmentService`: unroutable items withdrawn, records `IQueuedDialerWorkGate` holds back skipped) and each item is dialed by `IDialerAttemptService.TryDialUnreservedAsync`: under the same activity lock routing reserves with, the item must still be Waiting and pass the compliance gate; it becomes Assigned with no agent (`QueueItem.DialedUtc`), the work state Assigned with nobody, and the interaction is created without an agent and marked `dialer_pacing_model = overdial`. The Dial command carries no agent and no reservation and presents the profile's caller ID (or the load's number), never an agent line. `DialProviderCommandTypeExecutor` refuses a dial without an agent unless an `IPredictiveSystemDialAuthorizer` vouches for it; `PredictiveSystemDialAuthorizer` (registered by Paced Dialing) accepts only an enabled Predictive `OverDial` profile, a campaign queue, no reservation, and an unsettled interaction of the same activity marked over-dialed with no agent.

**The pacing record.** Every cycle rewrites the queue's `PredictivePacingState` (`QueueId`, `Sequence`, `LastCycleUtc`, `LastDecision` with the inputs and the decision; `PredictivePacingStateIndex` with a unique `QueueId`) and commits before the lock is released. The store checks concurrency, so of two nodes pacing the same queue (process-local locks without Redis) only one commits; the loser's transaction, with the calls it staged, is rolled back, and a call is only dispatched after its transaction commits, so the calls in flight never exceed what one cycle calculated. A change of mode publishes `DialerPacingModeChanged`. An exception cancels the session and places nothing.

**At the answer.** `ProviderVoiceEventService` keeps the answering-machine hold (no agent before the verdict) and the machine hang-up for an over-dialed call. On a person (or with screening off) it records the live answer through `IDialerAbandonmentTracker`, commits, and runs `IPredictiveAgentConnector.ConnectAsync` after the commit. `PredictiveAgentConnector` takes `ContactCenterPredictiveConnect:{interactionId}` without waiting, returns at once when the call is settled, already claimed (`dialer_agent_claimed_utc`) or abandoned, orders the free agents with `IActivityRoutingService.SelectAgentAsync` (its pick first, then eligible candidates by score, longest idle first), and tries `IActivityReservationService.ClaimConnectedCallAsync` for each with the `ConnectLockWait` lock wait. The claim takes the activity and agent locks, re-checks the item is still Assigned with no agent and the agent still available with no reservation, creates the reservation already Accepted (the expiry sweep only reads Pending ones), names the agent on the item, the work state and the interaction, moves the agent to Busy, sets the call session's agent and registers the Answer command with the reservation in the same transaction, publishes `QueueItemAssigned` (which arms the soft phone's auto-answer) and `DialerAgentConnectClaimed`, and commits through the routing compare-and-set. A claim that loses (null) moves to the next agent; a `ConcurrencyException` retries the whole connect in a fresh scope (up to three attempts). The Answer command then connects the agent's leg exactly as for a reserved call.

**Nobody free.** With `ConnectWaitMilliseconds` 0 (the default) the connector abandons at once through `IDialerAbandonmentTracker.AbandonAsync` with reason `no_agent_available`: the abandoned-call message starts, `DialerCallAbandoned` is recorded, and the call is hung up when no message can be played. With a wait, the connect is retried every 200 ms on `IContactCenterDeadlineScheduler` (`predictive-connect:{interactionId}`) until the wait runs out. The call ends without an agent: no wrap-up, the agentless item is removed (`ProviderVoiceOfferSynchronizationService` at the call's end, and `DialerAttemptFinalizer` also removes an agentless Assigned item), and the attempt is dispositioned like any call that ended before an agent joined. With `AbandonedRetryRequiresAgent`, a later call of the same activity is placed by the pacer with the free agent idle longest reserved for it; a cycle that has already staged a call without an agent leaves such a retry for the next cycle, because the reservation commits on its own.

**Agent leg failure.** A claimed agent whose leg fails is handled as for a reserved call: `ContactCenterAgentLegFailureService` plays the abandoned-call message (`agent_leg_failed`), settles the call and returns the agent to work.

**Cadence.** `PredictivePacingTriggerHandler` asks `IPredictivePacingScheduler` (a tenant singleton) to pace a campaign queue when an agent becomes Available, an agent is released or claimed, a call connects, ends or is abandoned, a person answers, or an attempt completes. Requests are debounced by `PacingDebounce` and merged per queue (`predictive-pacing:{queueId}`); each run, on a scope of its own, first connects answered calls still waiting for an agent (`ServiceWaitingAsync`), then runs the queue's cycle through `IDialerService`, and runs again after `PacingInterval` while the cycle places calls. `QueuedVoiceWorkOfferService` requests pacing for the over-dialing campaigns of an agent who becomes reachable. The minute `DialerPacingBackgroundTask` requests every over-dialing queue instead of pacing it inline, and every queue with agentless calls in flight: that is the safety net for a node that stopped between an answer and its connect, whose call is then connected or abandoned on the next run.

**Bookkeeping.** No reservation exists before the claim, so the expiry sweep and the offer deadline never act on an over-dialed call. `OrphanedActivityRecoveryService` leaves an activity with an unsettled interaction alone and, once settled, drops its item, agentless or not.

### Outbound compliance gate

Before every attempt, `IDialerEligibilityService` runs and records an auditable `DialSuppressed` event when an attempt must be blocked. The default gate enforces, in order:

- **Destination present and canonical** - the destination is resolved once to E.164 form before any other rule runs, using the profile's **default region** for numbers written in national form. A destination that cannot be resolved suppresses the attempt with `NoDestination`: a number the platform cannot identify cannot be screened against a do-not-call registry, and an unanswerable compliance question must not be answered by placing the call.
- The **maximum attempt count** has not been reached.
- **Retry cool-down** - a previous attempt must be older than `RetryDelayMinutes`.
- **Do-not-call / communication preferences** - the contact's `DoNotCall` opt-out (when *Respect do-not-call and communication preferences* is enabled).
- **Calling window** - when *Enforce a calling window* is enabled, the destination is only dialed while its business-hours calendar reports open. The profile selects a default **calling calendar** and optional per-region calendar overrides keyed by the destination's ISO 3166-1 alpha-2 region code; the calendar is evaluated in the contact's own time zone. A missing or disabled required calendar fails closed rather than silently allowing calls.
- **Abandonment cap** - when *Enforce an abandonment cap* is enabled for an automated pacing mode (Power/Progressive/Predictive), the profile's rolling live-answer/abandon statistics must stay at or below `MaxAbandonmentRatePercent`. The cap is only evaluated once the rolling window has accumulated at least `AbandonmentSampleFloor` live answers, and it **fails closed**: an automated profile that enforces the cap but cannot prove its current rate is suppressed. Preview binds an agent per call and is always permitted.
- **National do-not-call registries** - any registered `INationalDoNotCallRegistry` (for example the USA FTC or Canada DNCL registries) is scrubbed when *Respect do-not-call* is enabled. Registries receive the canonical number, never a raw or partially normalized one, so a registry cannot be asked about a different number than the one being dialed. Registry screening **fails closed**: a registry that is unreachable or rejecting requests has reported nothing rather than reported the number as unlisted, and the attempt is suppressed with `ComplianceScreeningUnavailable`. That suppression is deliberately **not terminal** - the destination was never shown to be off limits, only unverified, so the activity stays available for a later cycle instead of being cancelled because a registry had a bad minute. A registry that has no credentials configured is simply not participating and does not suppress anything.

When an automated profile enforces the abandonment cap, the **abandoned-call message** (`SafeHarborEnabled` / `SafeHarborMessage`) must be enabled so a live party that no agent reaches hears a caller-identifying message instead of a silent drop. `CrestApps:ContactCenter:Compliance:AbandonmentRollingWindowMinutes` (default 30, range 1-1440) sets the rolling measurement window and is validated on start.

### Abandoned calls

These mechanisms support the common abandoned-call rules for automated dialing (a live answer connected to an agent within two seconds, a short recorded message when it is not, and a minimum ring time). They do not by themselves make a campaign compliant; operators confirm their own obligations.

**Measuring.** `IDialerAbandonmentTracker` (registered with Contact Center Voice) files two facts in the durable event log for Power, Progressive and Predictive profiles, each under `AggregateType = DialerProfile` and `AggregateId = <profile id>` and keyed on the interaction so a call is counted once:

| Event | When |
| --- | --- |
| `DialerLiveAnswered` | A person answered: at the answer, or at the answering-machine verdict when screening is on. Machine, fax, busy, unanswered, failed and not-in-service calls are never recorded. The instant is stamped on the interaction as `dialer_live_answered_utc`. |
| `DialerCallAbandoned` | No agent was connected within `DialerAbandonment.ConnectThreshold` (2 seconds) of that instant: the agent leg failed (`agent_leg_failed`), the connect command failed (`agent_connect_failed`), the reserved agent could not be resolved (`agent_unavailable`), the agent leg answered later than two seconds (`agent_connected_late`), or the person hung up after waiting more than two seconds (`customer_hung_up_waiting`). The reason is stamped as `dialer_abandoned_reason`. |

`InteractionEventDialerAbandonmentStatisticsProvider` (registered with the Outbound Dialer) implements `IDialerAbandonmentStatisticsProvider` by counting both event types for the profile over the rolling window: two count queries on the event index's aggregate key, cached for the rest of the scope so a pacing cycle counts once. The rate is grouped **per dialer profile**, because that is what the pacing policy evaluates; a profile that dials several campaigns pools them, so use one profile per campaign when each campaign's rate must stand alone. The two-second threshold is measured from the answer (or the verdict), which is earlier than the end of the person's greeting, so it errs strict. The window is a short rolling window for pacing; the editor also shows the last 30 days.

**Playing the message.** When an answered automated call cannot be connected, the tracker starts the message on the customer's leg through `IQueueTreatmentProvider.EndWithMessageAsync` before anything else is written, so it starts as soon as the failure is known: on the agent leg's failed `call.hangup` webhook (`ContactCenterAgentLegFailureService.FailAsync`), when the Answer provider command fails (`AnswerProviderCommandTypeExecutor.ProjectFailureAsync`), or when the reserved agent cannot be resolved. On Telnyx the message is a `speak` command sent without a `client_state`, so the leg keeps the state it already carries (such as a recording's link to its interaction); the leg is remembered in the tenant's distributed cache (`TelnyxHangUpAfterSpeechRegistry`) and hung up on its `call.speak.ended`. A safety hang-up in a shell scope of its own ends the call after the message's estimated reading time plus 15 seconds (20 seconds to 2 minutes) in case the end is never reported. `{company}` is replaced with the site name and `{number}` with the call's caller ID (or the profile's), read digit by digit. With no message, or a provider that plays nothing, the call is hung up as before and a warning is logged.

**Ring time.** Automated dials carry `TelephonyConstants.RequestMetadata.RingTimeoutSeconds`, resolved by `DialerAbandonment.ResolveRingTimeoutSeconds`: the profile's `RingTimeoutSeconds` (default 30) clamped to 15-120 seconds. Telnyx sends it as `timeout_secs`. The profile editor and the recipe validation refuse an automated profile below 15 seconds.

Answering-machine detection (AMD) outcomes reported by the provider are mapped to a provider-neutral `AnswerClassification` (`Human`, `Machine`, `Fax`, `Unknown`) and stored on the call session and interaction technical metadata under the stable `amd_answer_classification` key for downstream pacing and analytics.

Do-not-call, registry, missing-destination, and maximum-attempt suppressions are terminal and remove the queue item so the dialer cannot retry them forever. Calling-window, abandonment, cool-down, and unavailable-screening suppressions release the reservation and leave the activity available for a later cycle. Terminal provider dial failures also remove the queue item, while retryable failures remain available according to the configured retry policy.

### Manual soft-phone screening

The campaign gate above governs the automated and preview dialer paths. Agent-initiated soft-phone dials placed directly through the telephony hub do not flow through a dialer profile, so they are screened by a separate, provider-agnostic extension point. The Telephony module exposes `IOutboundCallScreener`; `DefaultTelephonyService` runs every registered screener before dispatching any origination and fails the dial closed if any screener denies it. Standalone Telephony with no screener registered dials as before.

The base **Contact Center Outbound Dialer** registers a manual-call screener that applies the same do-not-call and calling-window rules to soft-phone dials. The screener resolves the destination to E.164, looks up the matching contact, and suppresses the dial when the contact has opted out, when the destination is on a national do-not-call registry, or when *Enforce a calling window* is enabled and the destination's calendar reports closed. A destination that cannot be parsed fails closed while do-not-call enforcement is on. Every suppression publishes a `ManualDialSuppressed` audit event so manual originations carry the same suppression trail as campaign attempts.

Manual screening is configured under `CrestApps:ContactCenter:Compliance:ManualDialing`:

| Setting | Default | Description |
| --- | --- | --- |
| `RespectDoNotCall` | `true` | Screens soft-phone dials against contact opt-out and national do-not-call registries, and fails closed on an unparseable destination. |
| `EnforceCallingWindow` | `false` | Suppresses soft-phone dials whose destination calendar reports closed. |
| `CallingCalendarId` | _(none)_ | Business-hours calendar evaluated for the calling window in the contact's time zone. |
| `DefaultRegionCode` | _(none)_ | ISO 3166-1 alpha-2 region used to canonicalize destinations written in national form. |

Before an eligible automated attempt reaches the provider, the dialer commits the reservation as accepted, then stages the activity attempt count, interaction, stable provider command, domain event, and event-outbox record in one tenant database commit. The command identifier is sent on the provider request and in its metadata, allowing provider adapters and later reconciliation to correlate retries without creating a second logical command; an adapter for a provider API that accepts an idempotency key can forward it there. The request path never sends the provider operation itself: it only schedules a best-effort post-commit wake-up, while durable command recovery remains the authoritative executor after a process failure. If reservation acceptance fails, no interaction or provider request is created. If the atomic command-intent commit fails, compensation runs in a fresh Orchard child scope so a canceled YesSql session is never reused.

The durable provider-command state machine orchestrates these actions using explicit `Pending`, `Claimed/Fenced`, `Sent`, `OutcomeUnknown`, `Confirmed`, `Compensating`, and terminal states. Command registration is serialized per idempotency key; dispatch, reconciliation, and compensation use fenced leases; and a superseded caller treats a lost claim as a handoff to the authoritative recovery owner instead of reporting a safe-to-redial failure. Type-specific executors keep Dial compliance/projections separate from inbound Answer/Connect and timeout voicemail/reject behavior while sharing the same recovery and fencing rules. Server-side inbound offer acceptance, answered-outbound bridging, and unanswered-offer actions persist command intent before provider operations; agent-device-native acceptance remains local because the device owns its answer action. Unknown-outcome and terminal confirmation settlement commit with the related interaction, call-session, activity, domain-event, and outbox projections in one tenant database transaction. Provider transport failures and ambiguous HTTP 408, 429, and 5xx responses transition to `OutcomeUnknown`; deterministic client rejections remain definitive failures. `ProviderCommandRecoveryBackgroundTask` runs each minute and processes bounded batches selected through due-time and expired-lease indexes. Pending Dial commands are not blindly dispatched after a crash: recovery reloads the governing dialer profile and activity and re-evaluates current policy. Missing policy infrastructure, profiles, activities, or ineligible decisions fail closed and compensate the accepted work without contacting the provider. Every CAS-sensitive recovery transition runs in a fresh Orchard child scope, so a normal optimistic-concurrency ownership loss cannot reuse a canceled YesSql session or abort unrelated commands in the batch. Recovery attempts reconciliation by command key before any retry; providers that cannot prove the outcome transition safely to `Paused` instead of risking duplicate execution.

### Callback operations

Callbacks use the same Activity, queue, routing, and disposition path as outbound campaign calls. A `CallbackRequest` records the contact, destination, optional campaign and queue, requested/due window, attempt count, status, and notes. The callback dispatcher runs every minute and promotes each due pending callback into an outbound `Callback` activity. When the request has a queue, that activity is enqueued so the next eligible signed-in agent receives it through the Agent Workspace.

A queued callback is dialed with a built-in Preview-style profile (the agent accepts, then the platform places the call from the agent's [outbound line](#outbound-lines), or the provider's default caller ID when they have none) that skips do-not-call and calling-window screening: the customer asked to be called back, so neither rule may refuse that call.

Use callbacks when an agent schedules a later follow-up, an inbound entry point offers a callback instead of waiting in queue, or workflow automation decides the next best action is a phone callback. Managers should configure a dedicated callback queue when callbacks need different SLA, skills, or priority from live inbound calls. Agents handle the promoted callback like any other outbound call: answer the offer, complete the conversation, select a disposition, and finish wrap-up through the Subject Flow.

## Outbound lines

A tenant with more than one phone number often wants different agents to call from different numbers: the sales team from the sales number and support from the support number, so a customer who calls back reaches the right people. Outbound lines do this with the phone numbers you already manage. There is no feature to enable: they are part of Contact Center Voice, which Inbound Voice, the Dialer, Recording and Supervision turn on for you.

1. Open **Channel endpoints** and add each number the tenant dials from as a **Phone** endpoint.
2. On each number, open **Outbound line** and pick the **Agents who dial from this number**.

Each agent dials from one number. Saving a number that lists an agent who is already on another number's line is refused with a message naming that line, and a recipe import is held to the same rule. Agents who are not on any line keep showing the provider's default caller ID, so you only need to assign the agents who should call from a different number.

The assigned number is used wherever an agent places a call:

| Call | Number shown |
| --- | --- |
| Soft phone keypad dial, including a call the browser places itself | The agent's line, otherwise the provider default. |
| Extension call to a colleague | The agent's line, otherwise the provider default. |
| Dialer attempt (Preview, Power, Progressive) | The **Dial from** number picked when the activities were loaded, then the agent's line, then the dialer profile's **Caller ID**, then the provider default. |
| Queued callback | The agent's line, then the dialer profile's **Caller ID**, then the provider default. |

The server always decides the number. A caller ID the browser sends with a dial is ignored, so an agent cannot show a number that was not assigned to them.

A dialer profile that must always show its own number, whoever makes the call and whatever number the load picked, can tick **Always show this caller ID** under **Caller ID**. Transfers, consult calls and supervisor legs still show the provider default.

The number has to be one your provider lets you show, which for Telnyx means a number on the account or a verified number. To send callbacks to the agent who called, point an inbound entry point for that number at the agent or their queue.

## Voice Contact Center Call Router

The dialer never talks to a telephony platform directly. It calls `IVoiceContactCenterCallRouter`, which resolves the configured `IContactCenterVoiceProvider`, so the Contact Center keeps assignment, queue, pacing, and compliance logic while the provider executes call operations. Each provider ships an adapter that implements `IContactCenterVoiceProvider` over its telephony provider. For example, the Asterisk adapter activates whenever the **Asterisk** module and **Contact Center Voice** are both enabled, and uses the tenant Asterisk provider when configured, otherwise resolving the configured **Default Asterisk** provider. Adapters are not separately selectable features.

Voice providers that support contact-center orchestration beyond soft-phone call control can also register `IContactCenterVoiceProvider`. The `IContactCenterVoiceProviderResolver` resolves those providers by technical name so future PBX integrations can participate in provider-side queueing, call assignment, and voice-specific orchestration without coupling Contact Center to one provider. Dial results include the actual executing provider identity, which is persisted on the interaction so provider events and reconciliation use the same configured alias.

## Admin UX and extensibility

Contact Center management entries live under **Interaction Center**. Queue groups, skills, queues, business-hours calendars, and dialer profile CRUD screens match the Omnichannel Campaigns UI: searchable list pages render summary shapes, and create/edit screens render display-driver editor shapes with the required root edit wrapper templates. Agent sign-in and presence are injected into the Telephony soft phone through `DisplayDriver<SoftPhoneWidget>`, so the operational controls stay with the phone while management screens remain catalog-focused.

Agent state reason codes are a catalog-backed admin surface (**Interaction Center → Management → Agent states**), not a provider-specific dialer setting. The Agents feature seeds standard reason codes during tenant setup by executing the `agent-state-reason-codes` module recipe, and the `AgentStateReasonCode` recipe step lets reason codes be imported or moved between tenants. A deployment plan exports them with the matching **Agent State Reason Codes** deployment step.

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
        "CrestApps.OrchardCore.Reports",
        "CrestApps.OrchardCore.Telnyx"
      ]
    }
  ]
}
```
