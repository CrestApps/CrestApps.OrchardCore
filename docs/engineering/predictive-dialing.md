# True predictive dialing - design and plan

Status: B1 (foundation) implemented. B2 (over-dial core) and B3 (latency and compliance hardening) open.

This is the engineering plan for placing calls without a reserved agent ("over-dialing") on Predictive dialer profiles.
It was first written against the dialer auto-disposition work and before the abandoned-call message work (PR #773)
existed; the [reconciliation](#reconciliation-with-the-abandoned-call-work-pr-773) section records what changed once
#773 landed, and the rest of the document uses #773's names.

## Facts about the code before B1

1. Predictive could not be saved: `DialerProfileHandler.ValidatingAsync` rejected `Mode == Predictive`.
   `PredictiveDialerStrategy` was registered by `DialerPacedStartup` but unreachable; the integration harness does not
   register it.
2. `DialerProfileHandler` runs on every write path (editor, recipe, deployment), so it is the one place profile rules live.
3. `DialProviderCommandTypeExecutor.IsAuthorizedFirstDialAsync` requires `AgentId` and `AgentUserId`, so over-dial needs
   an explicit, authorized "system dial" branch.
4. `ProviderVoiceEventService.StageAnsweredOutboundBridgeAsync` (`ProviderVoiceEventService.Answer.cs`) returns early
   when `session.AgentId` is empty: the hook point for picking an agent at answer.
5. Browser auto-answer is armed by the `OfferRevoked(Accepted)` SignalR push (`contact-center-soft-phone.js`). In Power
   the accept is long before the answer; in over-dial the accept happens at answer, so the push races the agent-leg
   INVITE.
6. `ContactCenterMetricsProjectionHandler` has only daily event counts, which is too coarse for pacing.
7. Useful existing behaviour: `DialerAttemptService` dispatches the provider dial after commit (a lost commit places no
   call); `ReserveAsync`/`AcceptAsync` take activity and agent locks, re-check availability and commit through
   compare-and-set (`CommitTransitionAsync`, `ConcurrencyException`); the queue item `Waiting -> Assigned` and work state
   `Available -> Assigned` transitions are allowed; `ProviderVoiceOfferSynchronizationService` already removes a live
   Assigned agentless item when the call ends.
8. Without the Redis lock feature `IDistributedLock` is process-local, so a lock alone is not a multi-node guard.

## Reconciliation with the abandoned-call work (PR #773)

#773 built, and B1 reuses rather than duplicates:

| #773 | What B1 does with it |
| --- | --- |
| `DialerLiveAnswered` and `DialerCallAbandoned` events filed under `AggregateType = DialerProfile`, `AggregateId = <profile id>`, keyed on the interaction (`IDialerAbandonmentTracker`). | They are the abandonment numerator and denominator for pacing too. The pacing statistics take them from `IDialerAbandonmentStatisticsProvider`, so pacing steers by exactly the counts the cap is enforced on. |
| `InteractionEventDialerAbandonmentStatisticsProvider` (two aggregate-key seeks per window). | Reused as-is for the rolling window and, in B2, for the 30-day compliance window (`ComplianceWindowDays`). |
| `DialerAbandonment.ConnectThreshold` (2 s) and the reasons (`agent_leg_failed`, `agent_connect_failed`, `agent_unavailable`, `agent_connected_late`, `customer_hung_up_waiting`). | The plan's own `MaxConnectLatency` option was dropped: a connect later than the threshold is already counted abandoned by the tracker. B2 adds a reason for "no agent free at answer" (for example `no_agent_available`). |
| `SafeHarborEnabled` / `SafeHarborMessage`, relabelled "Abandoned call message", played through `IQueueTreatmentProvider.EndWithMessageAsync` without `client_state`; Telnyx hangs up on `call.speak.ended` via `TelnyxHangUpAfterSpeechRegistry`, plus a safety hang-up. | Over-dial requires the message. B2 abandons an answered, unclaimed call through `IDialerAbandonmentTracker.AbandonAsync`, which already plays the message and records the event. |
| Ring time 15-120 s (`DialerAbandonment.ResolveRingTimeoutSeconds`) sent as Telnyx `timeout_secs`. | Unchanged. The measured ring-to-answer time feeds the free-up horizon. |
| `DialerCallMetadata` live-answer, agent-joined and abandoned markers. | B2 reads them for idempotency of the connector instead of adding `dialer_live_answer_utc` / `dialer_abandoned_utc` keys of its own. |

### Why no `DialerInteractionIndex` in B1

The first plan proposed a new map index over interactions for answer rate, ring-to-answer, handle time and connect
latency. B1 reads the same facts from what is already stored:

- `DialerAttemptStarted` is already filed under the profile aggregate by `DialerAttemptService`, so attempts are one
  seek on `IDX_InteractionEventIndex_Aggregate`, like #773's counts.
- From each attempt, the first `DialerLiveAnswered` and `AgentLegAnswered` are found through
  `IDX_InteractionEventIndex_Interaction (InteractionId, OccurredUtc, EventType)`, and `EndedUtc`, `WrapUpStartedUtc`,
  `WrapUpCompletedUtc` through the interaction index's unique `ItemId`. The statement (`DialerPacingQueries`) uses
  correlated sub-selects, reads at most `MaxTimingSamples` newest attempts and is cached for `StatisticsCacheDuration`.

Calls placed before #773 have no `DialerLiveAnswered`; a call an agent was connected to (`AgentLegAnswered`) is
therefore counted as answered even without one, so old history cannot make the answer rate read low. A call answered and
abandoned before #773 still reads as unanswered, which is why B2 must require the answer-rate sample to come from the
window only (it does: at most 240 minutes) and should not enable over-dial in the first window after #773 deploys.

That gives history from the first cycle (a new index starts empty, and the plan ruled out a backfill), no new write or
migration on the call path, and one definition of "answered" and "abandoned" shared with the cap. A dedicated projection
remains an option if production profiling shows the sub-selects are too costly; it would sit behind
`IDialerPacingStatisticsProvider` without changing callers. Per-campaign (queue) statistics are not in B1: #773 measures
per profile, and the docs already recommend one profile per campaign when each rate must stand alone. B2 can add a
queue-scoped variant by joining the interaction's `QueueId` if needed.

## Architecture

A second pacing model inside the Predictive strategy: dial N calls with no agent (queue item `Waiting -> Assigned` with
`AgentId = null`, interaction `AgentId = null`); on a human answer pick an agent atomically in a connector and reuse the
existing Answer command and bridge; no agent free means the abandoned-call message, counted abandoned. It falls back to
the reserve-then-dial loop (which cannot abandon) whenever statistics are missing, below their floor or near the cap.
Missing abandonment statistics keep the existing full suppression.

## B1 - foundation (implemented, no behaviour change)

- Feature `ContactCenterConstants.Feature.DialerPredictive` = `CrestApps.OrchardCore.ContactCenter.Dialer.Predictive`,
  depending on Paced Dialing; `DialerPredictiveStartup` registers `PredictiveDialerStrategy` (moved from
  `DialerPacedStartup`), `ContactCenterPredictiveDialingOptions` + validator, `DialerPacingStatisticsCache` (tenant
  singleton) and `IDialerPacingStatisticsProvider`.
- `DialerModeExtensions.RequiresPacedDialerFeature` now includes Predictive; `RequiresPredictiveDialerFeature` added.
- `DialerProfile` JSON fields (no migration), constants in `PredictiveDialingDefaults`:
  `PredictivePacingModel` (`ReservedPerCall` default, `OverDial`), `TargetAbandonmentRatePercent` 2,
  `MaxLinesPerAgent` 2 (1-5), `MaxCallsInFlight` 100 (1-1000), `AnswerRateSampleFloor` 50 (10-10000),
  `AnswerRateWindowMinutes` 15 (5-240), `CreditAgentsFreeingUp` false, `FreeUpCreditPercent` 50 (0-100),
  `ConnectWaitMilliseconds` 0 (0-1500), `AbandonedRetryRequiresAgent` true.
- `DialerProfileHandler`: the blanket Predictive rejection is replaced by "Predictive requires the Predictive Dialing
  feature"; Predictive profiles are range-checked; `OverDial` additionally requires the enforced cap, the abandoned-call
  message, a cap above 0 and target < cap. Other modes are not held to the predictive fields.
- Editor: new `DialerProfilePredictive.Edit.cshtml` card "Predictive pacing" (`data-dialer-modes="Predictive"`), the
  mode picker offers Predictive when the feature is on, Calls per agent and answering-machine screening are shown for
  Predictive. The card shows the measured answer rate, ring time and connect time of a saved profile.
- Recipe schema describes every new property; recipe import and deployment are reflection-driven
  (`ContactCenterDeploymentSerializer`) and carry them without change.
- `PredictiveOverDialCalculator` (pure), `PredictivePacingInput.ForProfile`, `PredictivePacingDecision`,
  `AgentFreeUpPredictor` (pure). Nothing calls the calculator yet.

**OverDial in B1.** The editor lists "Over-dial (available in a later release)" disabled (enabled only when the stored
value already is OverDial, so the editor never silently rewrites an imported value). A profile carrying OverDial is
validated against the over-dial safeguards and dialed by the reserve-then-dial loop, with
`PredictiveDialerStrategy` logging a warning each cycle. That was chosen over refusing OverDial on save so a recipe
written for B2 imports on B1 and behaves safely, and over hiding it so operators see what is coming.

## Pacing algorithm (`PredictiveOverDialCalculator`)

Inputs: `A` available agents now (`IAgentAvailabilityService.GetForQueueAsync` minus those with an
`ActiveReservationId`); `F` agents freeing within the horizon `H` (`AgentFreeUpPredictor`; only with
`CreditAgentsFreeingUp`, times the credit percent); `H` = measured median ring-to-answer (`DefaultRingHorizon` until
measured); `I` in flight (including answered-but-unclaimed); `r` answer rate from settled calls; `a` rolling abandonment
(#773); `a30` abandonment over `ComplianceWindowDays` (#773's provider with a 30-day window).

Decision, in order:

1. Policy not permitted: `Suppressed` (0).
2. Invalid limits; `r`, `a` or `a30` missing or not finite; a sample below its floor; `a30 >= cap` or `a >= cap`:
   `ReservedFallback` (existing loop).
3. Throttle `g` in [0,1]: 1 while `a <= 0.5 * target`, linear to 0 at the cap; `g = 0` is `ReservedFallback`;
   `a_eff = target * g`.
4. `E = A + credited F`; `E < 1` places 0.
5. `r` bounded to [0.05, 1]. Largest `N >= floor(E)` with `X ~ Binomial(N, r)` such that
   `sum_{k > E} (k - E) P(X = k) / (N r) <= a_eff`, computed exactly in log space. Treating in-flight calls as fresh
   over-estimates answers, which is the safe direction.
6. Caps: `N <= floor(MaxLinesPerAgent * A) + floor(F_credited)` with `MaxLinesPerAgent` clamped to 5;
   `N <= MaxCallsInFlight`; dials = `clamp(N - I, 0, MaxDialsPerCycle)`.

Fail closed: an exception, a lock not acquired or a concurrency conflict places 0. Every decision is logged and stored in
`PredictivePacingState.LastDecision` (B2). The queue head is consumed with the same held-back logic as
`ActivityAssignmentService.NextRoutableItemAsync` (`IQueuedDialerWorkGate`); extract a shared helper.

`AgentFreeUpPredictor`: a wrapping agent frees after the rest of the average wrap-up; a talking agent after the rest of
the average call plus a whole average wrap-up. An agent past the average, or a phase without a measured average, gets
no credit.

## B2 - over-dial core (open)

Components (ContactCenter.Core/Services unless noted):

- `IPredictiveOverDialPacer` / `PredictiveOverDialPacer`: one cycle per campaign queue: pacing lock, inputs, calculator,
  unreserved dials, save `PredictivePacingState` with a concurrency check, commit before releasing the lock.
- `IPredictiveAgentConnector` / `PredictiveAgentConnector`: at answer, claim a free agent or abandon through
  `IDialerAbandonmentTracker.AbandonAsync`.
- `IPredictivePacingScheduler` (tenant singleton): debounced per-queue pacing on `IContactCenterDeadlineScheduler` key
  `predictive-pacing:{queueId}`.
- `PredictivePacingTriggerHandler : IContactCenterEventHandler`: request pacing on AgentStateChanged to Available,
  AgentReleased, CallEnded, DialerAttemptCompleted, QueueItemAssigned, CallConnected, `DialerCallAbandoned`, agent
  sign-in. Naturally idempotent.
- `PredictiveConnectDeadlineHandler`: in-process timer for the optional connect wait plus a sweep backstop.
- `DialerAttemptService.TryDialUnreservedAsync(DialerProfile, QueueItem, ct)`: same activity lock key as `ReserveAsync`
  (`ContactCenterActivityReservation:{activityId}`), re-read Waiting, eligibility, item to Assigned (AgentId and
  ReservationId null, `DialedUtc`), work state Assigned (AssignedToId null), interaction AgentId null plus a pacing-model
  marker in `DialerCallMetadata`, Dial command with ReservationId null, caller ID without an agent line.
- `IActivityReservationService.ClaimConnectedCallAsync(QueueItem, AgentProfile, interactionId, lockWait, ct)`: activity
  and agent locks (short wait), re-check availability and empty `ActiveReservationId`, reservation created Accepted in
  memory before the first save (expiry never sees it), item gets ReservationId/AgentId (stays Assigned), agent Busy, work
  state Assigned with the user, AgentId on interaction and call session, publish QueueItemAssigned (arms auto-answer),
  `CommitTransitionAsync`.
- `PredictiveDialerStrategy`: delegate to the pacer when `PredictivePacingModel == OverDial` and the pacer is registered;
  otherwise today's loop (the B1 warning stays for the unregistered case).
- `ProviderVoiceEventService.Answer.cs`: when AgentId is empty and the call is an unreserved predictive dial: keep the
  AMD hold and machine hang-up; on a human (or AMD off) record the live answer through the tracker and
  `ScheduleAfterCommit<IPredictiveAgentConnector>(c => c.ConnectAsync(interactionId))`.
- `ContactCenterAgentLegFailureService`: unreserved predictive and no agent joined: release the agent and take the
  abandonment path (#773 already plays the message on agent-leg failure).
- `DialProviderCommandTypeExecutor.IsAuthorizedFirstDialAsync`: an empty AgentId is authorized only for a
  Predictive + OverDial profile with the feature on and a campaign dial. The dispatch validator re-runs eligibility,
  including the cap.
- `DialerAttemptFinalizer.RemoveWaitingQueueItemAsync`, `OrphanedActivityRecoveryService.DropQueueItemAsync`: also clear
  agentless Assigned items.
- `QueuedVoiceWorkOfferService`: when an agent becomes reachable, request pacing for their over-dial campaign queues;
  unclaimed answered calls are serviced first.
- `DialerPacingBackgroundTask`: for over-dial profiles call `scheduler.Ensure(queueId)` (the minute task becomes the
  backstop).
- Data: `PredictivePacingState` document + `PredictivePacingStateIndex(QueueId)` + migration (QueueId, Sequence,
  LastCycleUtc, LastDecision), saved with `checkConcurrency` every cycle: racing nodes lose one commit and its dials are
  never placed (dispatch after commit). `QueueItem.DialedUtc` in the document only;
  `IQueueItemStore.CountDialerInFlightAsync(queueId)` = Assigned with AgentId IS NULL (existing
  `QueueItemIndex(QueueId, Status, AgentId)`).
- Events: `DialerAgentConnectClaimed` (AggregateType DialerProfile), `DialerPacingModeChanged` (only on mode flips).

### Call flows

A. Answer, pick, bridge: `call.answered` -> session Connected -> `StageAnsweredOutboundBridgeAsync` sees no agent and an
unreserved predictive call. AMD requested and no verdict: return; machine: existing hang-up. Human: record the live
answer, commit, then `PredictiveAgentConnector.ConnectAsync`: lock `ContactCenterPredictiveConnect:{interactionId}`
(wait 0), idempotent through #773's markers plus a claimed marker; candidates from `GetForQueueAsync` ordered by
`IActivityRoutingService.SelectAgentAsync`, longest-idle tie-break; `ClaimConnectedCallAsync` per candidate (null or
`ConcurrencyException` moves to the next); on success register the Answer command (extract the payload builder from
`StageAnsweredOutboundBridgeAsync`) with the ReservationId, commit, push arms auto-answer -> agent leg -> bridge.

B. Answer, no agent: if `ConnectWaitMilliseconds > 0` and before the deadline, retry shortly; an agent becoming
Available services waiting answered calls first. At the deadline: `IDialerAbandonmentTracker.AbandonAsync` plays the
message and records `DialerCallAbandoned`; request pacing. No wrap-up (no AgentId); the agentless Assigned item is
removed; `DialerAttemptCompleted` dispositions through `DialerAttemptFinalizer`; `AbandonedRetryRequiresAgent` flags the
retry for the reserved path.

### Concurrency

Two answers for one agent: agent lock `ContactCenterAgentReservation:{agentId}` (shared with inbound `ReserveAsync`) plus
availability and `ActiveReservationId` re-check plus CAS commit inside the lock; the loser tries the next candidate; short
`ConnectLockWait`. Duplicate deliveries: per-interaction connect lock plus markers. Pacing: lock
`ContactCenterPredictivePacing:{queueId}` (wait 0, expiry `PacingLockExpiration`), `SaveChangesAsync` before releasing;
`PredictivePacingState` concurrency is the real guard. Dial claim versus routing: same activity lock key plus Waiting
re-check; `ReservationExpiryBackgroundTask` already skips the PredictiveDial source.

### Bookkeeping

No reservation before the claim, so expiry cannot act; the reservation is created Accepted; `OfferDeadlineEventHandler`
never arms (no AgentReserved). Orphan recovery: the unsettled interaction is already protected; the agentless item is now
dropped; interaction and queue claim are written in one transaction. `AgentWorkStateHealingService` is per agent and
ignores agentless work; verify `QueuedWorkWithdrawalService` ignores Assigned. Backstop sweep: an answered predictive call
with no claim and no abandonment marker older than `AnsweredUnconnectedSweepAfter` gets the message (or a hang-up if
gone), counted abandoned.

### Testing (B2)

`DialerModeIntegrationHarness` / `DialerHarnessDoubles`: register `PredictiveDialerStrategy`, pacer, connector, real
`ClaimConnectedCallAsync`, the Answer executor (or a recording Answer path); fakes for pacing statistics, abandonment
statistics, a recording queue treatment, a router accepting agentless dials; helpers `RunPredictiveCycleAsync`,
`RaiseHumanAnswerAsync`, `RaiseMachineAnswerAsync`, `RaiseAgentLegAnsweredAsync`, `SeedPacingStats`. Integration: over-dial
counts; longest-idle claim; answer race (2 calls, 1 agent, barrier in a fake lock, about 50 loops: exactly one claim,
one message); no agent -> message, Abandoned, no wrap-up; machine -> no claim; fail-closed fallbacks; expiry and orphan
recovery leave in-flight calls alone; two nodes -> `ConcurrencyException` keeps within the cap; inbound steals the
agent -> abandonment path; agent-leg failure after the claim -> message. The B1 harness test that Predictive without the
feature places nothing must keep passing.

## B3 - latency and compliance hardening (open)

Standby auto-answer armed while Available on an over-dial campaign plus a SIP header (for example `X-CC-Reservation`)
matched by `auto-answer.js` (Telnyx and JS), removing the push-versus-INVITE race; the answered-unconnected sweep; the
optional connect wait, validated so wait plus measured p95 connect latency stays within 2 s and refused without latency
data; abandoned-retry-requires-agent enforcement; supervisor visibility of `LastDecision`.

## Compliance

Two-second rule: immediate message by default; a connect later than `DialerAbandonment.ConnectThreshold` is counted
abandoned by #773's tracker. 30-day gate (`ComplianceWindowDays`) per profile (reserved fallback at or over the cap).
Rolling window: #773's gate plus `TargetAbandonmentRatePercent` steering. The 15-second minimum ring from #773 feeds `H`.
Calling windows, DNC and attempt limits: `IDialerEligibilityService` at claim and at dispatch. The abandoned-call message
is required for over-dial; abandoned calls are not redialed without a guaranteed agent (default on).

## Cadence

Event-driven request -> schedule `predictive-pacing:{q}` at now + `PacingDebounce`; run: take the lock (held -> retry in
1 s), one cycle, commit; next run after `PacingInterval` while there is inventory and `E >= 1`, else stop until the next
event or the minute backstop. In-process timers are accelerators only; the lock plus state concurrency give correctness.

## Rollout

Off unless the feature is enabled and the profile's pacing model is OverDial. `ReservedPerCall` is the default and
current behaviour. Power and Progressive are unaffected. Pilot: `MaxLinesPerAgent` 1.5, target 1%, crediting off; watch
`LastDecision`, connect latency and abandonment.

## Risks

Agent-leg connect time eats the 2 s budget (B3 standby auto-answer, later a nailed-up agent connection). Push versus
INVITE race (`RedialAgentLegAsync` plus B3). Agents shared with inbound queues (recommend dedicated; optionally discount
`A`). Process-local locks on multi-node (state concurrency; recommend Redis). Statistics query cost (bounded sample,
cache; a projection if profiling demands it). Activity status reports with no agent (`ActivityProgressTally`,
`OmnichannelReportAggregator`).
