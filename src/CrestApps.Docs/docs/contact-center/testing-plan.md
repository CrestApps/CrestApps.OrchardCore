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
| Live session teardown | `RealtimeVoiceConversationRunnerTests` | A transferred call and a caller who hangs up both let the session return, so the work after it runs; the closing line is never clipped, a customer who speaks after goodbye is not hung up on, and the model ending the call again after that still hangs up |
| Voicemail on a turn-based call | `VoiceAgentConversationLoopTests`, `VoicemailGreetingTests` | A recorded greeting is recognised and never replied to as the customer; a greeting still playing is let finish; one message is left, never a question, and the call hangs up; a line nobody has said a word on is treated as voicemail rather than asked whether anybody is there |
| Session tooling | `RealtimeVoiceConversationRunnerTests` | The live session is given the end-call tool, and the transfer tool only when the call has a queue behind it, and is started inside the invocation scope those tools need |
| Who speaks, and when | `RealtimeVoiceConversationRunnerTests` | The assistant opens the call even on a session the provider answers for itself; it speaks up when nobody has spoken for too long, and stops after two attempts; the closing line is played, counted and recorded once however many times the model produces it |
| Which way a call runs | `VoiceAgentConversationLoopTests` | A profile whose model declares the realtime capability holds the call as a live session. The harness resolves it the way the framework does — the chat slot refuses realtime deployments — so a call cannot pass here and fall back to turn-based live |
| Disposition guidance | `Modules/Omnichannel/SubjectDispositionGuidanceTests` | The model is given the subject's own wording for a disposition when there is one, the disposition's general description when there is not, and never another subject's wording |
| Contact preferences | `Modules/Omnichannel/Managements/DefaultSubjectActionExecutorTests` | A disposition that sets do-not-call actually writes the contact, and publishes it, because the lists that decide who gets dialled query published contacts |
| Trying again | `DefaultSubjectActionExecutorTests` | A retry is placed the same way as the attempt it follows: an automated call's retry keeps its kind, source, AI profile, voice and pacing, and starts a new conversation |
| Editing an activity | `OmnichannelActivityEditRulesTests` | Saving an existing activity leaves its channel, endpoint, interaction type and campaign alone; only a new activity, or one moved to another subject, takes them from the subject's flow |
| What a turn-based call hears | `Telephony/VoiceAgentMediaProviderTests` | Listening starts on a model made for phone audio, on the far end's track only, and falls back to the default engine's phone-call model when the account refuses it |
| Call outcome | `VoiceCallConclusionPolicyTests` | Dispositions come from the subject's own actions; a disposition the model invented is refused; a silent call is never given an invented summary; an escalated call is left for the agent |
| Voicemail access | `VoicemailMediaEndpointTests` | An agent can play and delete a voicemail addressed by the soft phone's own row id, and cannot reach one belonging to somebody else |
| Permission to make contact | `Modules/Telephony/OutboundCallScreeningTests`, `Modules/ContactCenter/ManualCallScreenerTests` | The question is asked immediately before the call goes out, not when the batch was loaded: a denial never reaches the provider, the first refusal wins, and a screener that cannot answer — an unreachable registry, a number it cannot canonicalize — refuses rather than allows |
| The same question on every path | `AutomatedActivitiesProcessorBackgroundTaskTests`, `Modules/Omnichannel/Sms/SmsOmnichannelProcessorTests`, `SmsReEngagementBackgroundTaskTests` | Somebody who opted out after their activity was created is neither called nor messaged — held separately at each of the three paths that used to skip the question: the automated voice call, the SMS processor that is the last code before the carrier, and the cadence that would otherwise nudge them on a schedule for days |
| Who a batch admits | `Managements/Services/DefaultContactActivityBatchLoaderTests` | The per-channel "include do-not-calls" flags decide what they claim to: unticked excludes the people who asked to be left alone, on manual sheets as well as automated ones, and ticking one is an operator overriding that on purpose. A request to stop is honoured at every number: a contact who shares a phone number with somebody who opted out is not called or texted on it |
| Business hours on a nudge | `SmsReEngagementBackgroundTaskTests` | The cadence is judged in the contact's time zone rather than against the server clock, and a calendar it cannot evaluate closes the window rather than opening it |
| The SMS outcome | `Modules/Omnichannel/Sms/SmsConclusionDispositionTests` | A conversation with no disposition chosen is never handed to the executor that rejects it, so it cannot be left open with no outcome, no notes and no follow-up |
| The voice outcome, wired up | `Modules/Omnichannel/Voice/VoiceCallConclusionWiringTests` | End to end rather than by policy alone: the chosen outcome reaches the subject's actions, an invented one does not, a silent call gets the policy's note, and a customer's email is written back only where the activity allows it. A call nobody answered, and a call voicemail answered, take the outcome that tries again and are never reviewed |
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
| Stopping at every number | Verified | a batch aimed at a contact who shares a number with somebody who opted out loaded nobody, where the same batch had loaded seven before the fix |
| A call nobody answers | Verified | a call that rang out was concluded as No Answer and a retry was scheduled a day later with the AI profile it needs to be placed; before the fix the retry was a manual task with no profile, which nothing could dial |
| Voicemail on a turn-based call | Verified | the greeting was recognised, one message was left, the call hung up and was concluded as No Answer. Before the fix the greeting was answered as the customer and the review concluded the call as do-not-call, opting out somebody who never picked up |
| A voicemail heard under the greeting | Verified | a short greeting played entirely under the opening line and was never transcribed; the silence after it now leaves the message instead of asking a recording whether it is still there |
| Ringing long enough for voicemail | Verified | a call abandoned at the provider's 30-second default just before voicemail answered; at 45 seconds the voicemail answers |
| A second goodbye | Verified | the customer answered the goodbye, the model ended the call again, and the platform hung up about four seconds after the last line finished. Before the fix the line stayed open until the customer hung up |
| A retry, placed | Verified | a call rang out, its retry was created with its AI profile, rescheduled from the activity editor, dialled, answered, and held a full conversation |
| Hearing a turn-based caller | Verified | on the default engine a caller's answers came back as fragments and an email address was read back wrong four times; on the phone-audio model every answer and the address were heard right the first time |
| The turn-based call ending a conversation | Verified | the model ended the call through the end-call tool once the customer had what they needed, and the platform hung up after the goodbye |
| Outcomes the guidance separates | Verified | a buyer ready to sign was concluded as the won-lead disposition, and a buyer only wanting options as the ordinary one |
| Telnyx answering machine detection | Verified | premium detection reported a machine about five seconds after answer and the greeting's end after it; one message was left, the call hung up and was concluded as No Answer. The first run left two messages, because a late greeting transcript arrived while the first was being composed; the message is now claimed before it is composed |
| Opting out on a live call | Verified | the caller asked to be taken off the list; the call was concluded as do-not-call and the contact flagged together, the next load for the number took nobody -- eight records shared it -- and a retry already due for the contact was cancelled rather than dialled |
| Voicemail on a live session, with detection | Verified | the provider reported the machine and the greeting's end; the model stayed silent through the greeting, left one message, and the call was concluded as No Answer |
| AI-to-agent handoff with detection on | Verified | re-run after the dial began asking for answering machine detection: the provider classified the caller as a person, the caller asked for an agent, the call was queued, offered, accepted on the soft phone and bridged with audio both ways |

## Still to be proven on a live call

- **Opting out by text.** The call path is proven end to end. A text opt-out, and a cadence step already due for
  somebody who opted out, still wait on a working SMS account: the Twilio account the tenant uses was reported by
  Twilio as not active during this round, which refused every message before any of it was tested.
- **Answering-machine detection on a live session.** Telnyx detection is proven on a turn-based call. A realtime
  call records the provider's verdict for its outcome but does not yet use the greeting's end as its cue to leave
  the message; the model still leaves it on what it hears.
- **What the model believes it heard.** Transcription on a phone line is noisier than the conversation feels, and
  nothing questions an implausible reading before it is acted on or written down. Observed live: an email address
  read back with a company domain the caller never said, confirmed with a "sure"; and a budget recorded as a
  figure that did not match the car being discussed. Both were written to the record as fact. This is the one
  open behaviour that produces wrong data rather than an awkward call. Seen twice more on 2026-09-22: the same
  spoken address came back as two different wrong domains, each read back to the caller and confirmed.
  On 2026-09-23 the same address was heard right the first time on both paths: by the realtime model, and by a
  turn-based call once it listened on the phone-audio model. Two calls are not a proof, so this stays open.
- **An ambiguous phrase read as an opt-out.** On the default transcription engine a reply came back as "no I don't
  want anyone", and the review concluded the call as do-not-call. Leaning towards stopping is the rule on purpose,
  and the engine that produced the fragment has been replaced, so the open question is only whether a clear
  transcript ever still does this.
- **A stream reported as failed after it ends.** Telnyx sends `streaming.failed` just after every live session's
  stream stops, including on calls that worked end to end. It looks like the order the socket is closed and the
  stream is stopped in, and it changes nothing about the call; the reason in the event has not been read yet.
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
