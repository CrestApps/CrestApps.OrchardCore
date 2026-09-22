---
sidebar_label: Testing plan
sidebar_position: 14
title: Contact Center testing plan — what is covered, and what still needs a phone
description: Which parts of the dialer, the automated voice agent and the agent handoff are held by automated tests, which have been proven on a live call, and what each live test is for.
---

# Testing plan

Most of this system can be tested without a telephone. The parts that cannot are listed here explicitly, with
what each live test is meant to establish — so a live call is spent on a question that only a live call can
answer, rather than on something a test should have caught.

## The rule this plan exists to enforce

Every defect found on a live call gets a test that fails against the code that produced it, before the fix is
committed. Four separate live calls were spent on one agent handoff because the tests of the day each stopped at
a seam: the model had no tool to escalate with, the work died with the webhook request it ran inside, and the
session never returned so nothing after it ran at all. Each of those passed a test suite that looked healthy.

A test that asserts "when the flag is set, the caller is enqueued" proves the branch and not the journey. Where a
defect crossed a seam, the test has to cross it too.

## Held by automated tests

| Area | Where | What it holds |
| --- | --- | --- |
| Dialer modes | `Modules/ContactCenter/Integration/DialerModeIntegrationTests` | Power, Progressive, Predictive-refused, Manual and Preview against a real SQLite store, asserting the agent-state lifecycle `Available → Reserved → Busy → WrapUp → Available` |
| Dialer under volume | `DialerVolumeWorkflowTests` | 100 calls across 10 agents: fair spread, no double-booking, queue drains, everyone ends Available |
| Automated voice conversation | `Modules/Omnichannel/Voice/VoiceAgentConversationLoopTests` | Greeting once, listening, replying, escalating, ending; realtime and turn-based both write the same transcript |
| The handoff journey | `VoiceAgentConversationLoopTests` (journey cases) | A caller asking for a person reaches the queue **with the request token already cancelled** — which is how an abandoned provider webhook actually arrives — and is not hung up on instead |
| Live session teardown | `RealtimeVoiceConversationRunnerTests` | A transferred call and a caller who hangs up both let the session return, so the work after it runs; the closing line is never clipped, and a customer who speaks after goodbye is not hung up on |
| Session tooling | `RealtimeVoiceConversationRunnerTests` | The live session is given the end-call tool, and the transfer tool only when the call has a queue behind it, and is started inside the invocation scope those tools need |
| Who speaks, and when | `RealtimeVoiceConversationRunnerTests` | The assistant opens the call even on a session the provider answers for itself; it speaks up when nobody has spoken for too long, and stops after two attempts; the closing line is played, counted and recorded once however many times the model produces it |
| Which way a call runs | `VoiceAgentConversationLoopTests` | A profile whose model declares the realtime capability holds the call as a live session. The harness resolves it the way the framework does — the chat slot refuses realtime deployments — so a call cannot pass here and fall back to turn-based live |
| Disposition guidance | `Modules/Omnichannel/SubjectDispositionGuidanceTests` | The model is given the subject's own wording for a disposition when there is one, the disposition's general description when there is not, and never another subject's wording |
| Contact preferences | `Modules/Omnichannel/Managements/DefaultSubjectActionExecutorTests` | A disposition that sets do-not-call actually writes the contact, and publishes it, because the lists that decide who gets dialled query published contacts |
| Call outcome | `VoiceCallConclusionPolicyTests` | Dispositions come from the subject's own actions; a disposition the model invented is refused; a silent call is never given an invented summary; an escalated call is left for the agent |
| Voicemail access | `VoicemailMediaEndpointTests` | An agent can play and delete a voicemail addressed by the soft phone's own row id, and cannot reach one belonging to somebody else |
| Permission to make contact | `Modules/Telephony/OutboundCallScreeningTests`, `Modules/ContactCenter/ManualCallScreenerTests` | The question is asked immediately before the call goes out, not when the batch was loaded: a denial never reaches the provider, the first refusal wins, and a screener that cannot answer — an unreachable registry, a number it cannot canonicalize — refuses rather than allows |
| The same question on every path | `AutomatedActivitiesProcessorBackgroundTaskTests`, `Modules/Omnichannel/Sms/SmsOmnichannelProcessorTests`, `SmsReEngagementBackgroundTaskTests` | Somebody who opted out after their activity was created is neither called nor messaged — held separately at each of the three paths that used to skip the question: the automated voice call, the SMS processor that is the last code before the carrier, and the cadence that would otherwise nudge them on a schedule for days |
| Who a batch admits | `Managements/Services/DefaultContactActivityBatchLoaderTests` | The per-channel "include do-not-calls" flags decide what they claim to: unticked excludes the people who asked to be left alone, on manual sheets as well as automated ones, and ticking one is an operator overriding that on purpose |
| Business hours on a nudge | `SmsReEngagementBackgroundTaskTests` | The cadence is judged in the contact's time zone rather than against the server clock, and a calendar it cannot evaluate closes the window rather than opening it |
| The SMS outcome | `Modules/Omnichannel/Sms/SmsConclusionDispositionTests` | A conversation with no disposition chosen is never handed to the executor that rejects it, so it cannot be left open with no outcome, no notes and no follow-up |
| The voice outcome, wired up | `Modules/Omnichannel/Voice/VoiceCallConclusionWiringTests` | End to end rather than by policy alone: the chosen outcome reaches the subject's actions, an invented one does not, a silent call gets the policy's note, and a customer's email is written back only where the activity allows it |
| What a bulk action says it will touch | `Managements/Handlers/BulkManageActivityFilterHandlerSqlTests` | The count shown before a bulk complete or purge is the real one — every filter value is bound rather than inlined, the do-not-call flag is compared as a boolean so PostgreSQL does not refuse the query outright, and a contact behind several index rows is counted once |

## Proven on a live call

| Test | Status | Proven on |
| --- | --- | --- |
| Manual dial | Verified | agent keypad, client-originated |
| Preview dial | Verified | offer → accept → dial → audio |
| Power dial | Verified | pacing, cap, two defects found and fixed |
| Progressive dial | Verified | one per agent, call topology confirmed |
| Predictive dial | Out of scope | deliberately refused |
| Automated voice call | Verified | dial → greeting → conversation → summary and disposition written |
| AI-to-agent handoff | Verified | transfer → enqueue → hold music → offer accepted → both legs bridged |
| Realtime (speech-to-speech) voice call | Verified | the call leg carried `streaming_start`/`streaming_stop` and no `speak` at all, the model interrupted and was interrupted, and the platform hung up when the model finished |
| The assistant ending its own call | Verified | `endCall` invoked from the live session, closing line spoken in full, platform-issued hangup |
| The assistant opening the call | Verified | the greeting is the first thing on the line, with no caller turn before it — twice running |
| Breaking a silence | Verified | after twelve quiet seconds the assistant asks whether the caller is still there, twice, and then stops asking |
| One goodbye, not two | Verified | the model produced the closing line twice on two calls; the caller heard it once, and the transcript records it once |

## Still to be proven on a live call

- **Compliance request, end to end.** Half of this is proven: on three live refusals the model chose the
  do-not-call disposition every time, including one where the refusal was brief. The other half never worked —
  the contact's Do Not Call preference was applied in memory and never saved, so the customer stayed dialable.
  That is fixed and unit-tested; what is still owed is one live call showing the disposition and the account flag
  land together, and the contact then missing from the next inventory load.

  The scope of that call has since grown. The preference is now asked for again immediately before each contact
  rather than only when the batch was loaded, and three paths that never asked at all — the SMS processor, the
  re-engagement cadence and the automated voice call — now go through the same screening an agent's call does.
  Each of those is held by tests, and none has been watched refuse a real person. The single most useful live
  call is therefore the one that opts out and then waits: the disposition and the flag land together, the next
  inventory load no longer contains them, and a cadence step that was already due for them passes in silence.
  Note that there are three channels to prove, not four — the chat opt-out was withdrawn, because there is no
  chat channel on this platform and the preference was read by nothing.
- **Answering-machine detection.** Nothing tells a call whether a person or a machine picked up. A call that
  reaches voicemail is answered by the assistant introducing itself to the recording, and it will keep talking to
  it. Seen live; the assistant behaved sensibly on what it could hear, which is the point — it is the platform
  that has no notion of who answered.
- **What the model believes it heard.** Transcription on a phone line is noisier than the conversation feels, and
  nothing questions an implausible reading before it is acted on or written down. Observed live: an email address
  read back with a company domain the caller never said, confirmed with a "sure"; and a budget recorded as a
  figure that did not match the car being discussed. Both were written to the record as fact. This is the one
  open behaviour that produces wrong data rather than an awkward call.
- **The disposition descriptions.** Every disposition now describes when to choose it, and a subject workflow can
  override that wording for its own work. The rule that decides which description the model sees is unit-tested,
  and the descriptions themselves have not yet been judged by a model on a live call. The useful test is not
  another refusal — it is a call that should land on a *different* disposition, to show the guidance separates
  outcomes rather than pulling everything towards the one it describes most forcefully.
- **The turn-based fallback's hangup.** A call that cannot run as a live session is now told that hanging up is
  its job, and is given the tool to do it. Both are unit-tested. No live call has taken that path since, because
  the realtime path stopped falling back — so it is proven by test and unproven by phone.
- **Signaling region.** Whether moving the signaling edge moves the media edge with it. Only the round-trip figure
  on a call can answer that; the provider does not document it.

## Running the tests

```bash
dotnet build tests/CrestApps.OrchardCore.Tests/CrestApps.OrchardCore.Tests.csproj -c Debug
```

Then, from `tests/CrestApps.OrchardCore.Tests/bin/Debug/net10.0`:

```bash
dotnet CrestApps.OrchardCore.Tests.dll -filterVSTest "FullyQualifiedName~Omnichannel.Voice"
```

The front-end suite is `npm test` at the repository root. The distributed suite needs Redis and Postgres and
throws rather than skips without them — see [Production support](production-support).

## Setting up a live call

The automated voice path needs the provider's webhooks to reach the running site, so a tunnel to the local host
must be up before dialing; a call placed without one dials out and then goes silent, because every event that
drives the conversation arrives by webhook. An agent taking a handoff needs three things true at once: the soft
phone registered, presence Available, and signed into the destination queue. The queue chip on the workspace
shows the number of callers **waiting**, not the number of agents — it reads `0` on a correctly staffed queue.
