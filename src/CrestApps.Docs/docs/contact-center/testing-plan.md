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
| Session tooling | `RealtimeVoiceConversationRunnerTests` | The live session is given the end-call tool, and the transfer tool only when the call has a queue behind it |
| Call outcome | `VoiceCallConclusionPolicyTests` | Dispositions come from the subject's own actions; a disposition the model invented is refused; a silent call is never given an invented summary; an escalated call is left for the agent |
| Voicemail access | `VoicemailMediaEndpointTests` | An agent can play and delete a voicemail addressed by the soft phone's own row id, and cannot reach one belonging to somebody else |

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

## Still to be proven on a live call

- **Compliance request.** The caller asks not to be called again. What it is meant to establish: the model picks
  the disposition wired to the subject's do-not-call action, the contact's Do Not Call preference is actually
  set, and the contact is then excluded from the next inventory load. The wiring is configured and the disposition
  rules are unit-tested; what a live call adds is the model's judgement on a real refusal.
- **Automated voice after the realtime-capability migration.** Realtime is now a capability of the chat deployment
  rather than a deployment of its own. The decision "does this call run live" changed shape, and although it is
  covered by tests, it is the same path four live calls were spent on — worth one call before it is trusted.
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
