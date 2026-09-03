---
sidebar_label: "AI-to-Agent Handoff Project Plan"
sidebar_position: 6
title: AI-to-Agent Handoff — Project Plan
description: Design plan for escalating an automated (AI) SMS or phone conversation to a live human agent — the bot decides to hand off, and the interaction moves from the automated lane into the SMS Workspace inbox (SMS) or the Contact Center inbound queue (phone) with full context.
---

# AI-to-Agent Handoff — Project Plan

This page is the design plan for **escalating an automated (AI) conversation to a live human agent**. When the
bot decides — or the customer asks — the interaction leaves the automated lane and is picked up by a person, on
the **same channel and the same thread/call**, with the conversation history and an AI-written summary carried
across so the customer never repeats themselves.

It complements the automated paths documented under [SMS Automation](sms) and the Telnyx AI voice handler, and
reuses the human destinations already built: the [SMS Workspace](sms-workspace) inbox for SMS, and the
[Contact Center](../contact-center/index.md) inbound-queue + offer pipeline for phone.

> **Status: implemented (SMS end-to-end; phone pending live media verification).** The shared spine, the SMS
> handoff, and the phone handoff orchestration are built and unit-tested (`OmnichannelHandoffHelperTests`,
> `SmsAgentHandoffServiceTests`, `VoiceAgentHandoffServiceTests`). The phone path reuses the Contact Center
> enqueue-and-offer + connect pipeline; the live Telnyx media bridge from an outbound AI call still needs
> verification on a real call. The escalation **signal is the `transfer_to_agent` AI tool** the model invokes
> (no text marker): a non-selectable `AIFunction` records the decision on an AsyncLocal turn context the handlers
> read back after the completion, and system-prompt guidance tells the model when to call it. Voice carries the
> decision across webhooks with a durable `PendingVoiceHandoff` flag on the activity (the `[[HANGUP]]` marker
> still ends a call). The **handoff queue** is a free-text queue-id field on the subject AI settings.

## Goals

- A **bot-initiated (or customer-requested) handoff** from an automated conversation to a human agent.
- **Channel continuity**: SMS continues on the same number in the Workspace inbox; a phone call continues as
  the same live call, seated into a queue and bridged to an agent.
- **Warm handoff**: the agent inherits the full transcript plus an AI summary and any captured fields.
- **Graceful failure**: after-hours, all-agents-busy, and opt-out are handled without dropping the customer.
- **Measurable containment**: a distinct terminal reason and disposition so reporting can separate
  bot-contained conversations from escalated ones.

## The core insight

Both channels **already have a fully built human destination**. What is missing is the *bridge* — a signal from
the AI and a service that moves the interaction from the automated lane into the human lane.

| Channel | Automated origin (exists) | Human destination (exists) | Missing bridge |
| --- | --- | --- | --- |
| SMS | `SmsOmnichannelEventHandler` (AI reply loop, `[[HANGUP]]` conclusion) | SMS Workspace: `SmsInboundProcessor` + `ExistingConversationRouter`, `SmsConversation` with Queue/Agent owners | Nothing **creates** the human `SmsConversation` at handoff |
| Phone | `TelnyxAiVoiceConversationHandler` (webhook state machine, `[[HANGUP]]`) | Contact Center: `InboundVoiceCallProcessor` enqueue + `IVoiceQueueOfferService` offer/bridge | The AI call **hangs up** instead of being enqueued+offered |

The SMS Workspace inbound pipeline was, in fact, written in anticipation of this. Its guard already
"yields to the automated (AI) path while it owns the number, and takes over after a handoff" — see
`SmsInboundProcessor` and `ExistingConversationRouter` (which runs at `Order = 200`, before ownership
resolution, specifically so replies land in the human thread once it exists).

## Design principles (industry-standard bot → agent escalation)

1. **Deterministic signal, not text-scraping.** The model escalates via a **tool/function call**
   `transfer_to_agent(reason, summary, skill?)`, not a parsed control token. It is deterministic, carries
   structured context, and cannot be emitted by accident mid-sentence. A `[[HANDOFF]]` marker is retained as a
   fallback for parity with the existing `[[HANGUP]]` plumbing and for models/turns where the tool is not used.
2. **Warm handoff.** The agent inherits a summary + captured fields. Reuse the existing conclusion-analysis
   summarizer (already present in `SmsOmnichannelEventHandler`'s deferred conclusion pass) to produce it.
3. **Triggers — all three enabled by policy:**
   - **User asks for a human** (explicit request) — the safe baseline, always on.
   - **Qualified/interested lead** — matches the existing `sms-lead-qualification` prompt, which already ends
     with "hand off to a human" once the lead is a fit.
   - **Frustration / repeated failure** — sentiment or repeated unresolved turns.
   Each is independently toggleable per campaign so a tenant can dial aggressiveness against agent capacity.
4. **Graceful when no agent is available** — after-hours (reuse `IBusinessHoursGate`), all-busy → queue-wait /
   callback (`CallbackService`) / voicemail / hand back to bot.
5. **Containment is a measured KPI.** A distinct terminal reason (`handed_off`) + disposition so reporting can
   separate bot-contained vs. escalated conversations (containment rate is the headline bot metric).

## The unifying contract

A single shared abstraction, resolved by channel — mirroring how `IBusinessHoursGate` is an optional,
channel-resolved service:

```csharp
// CrestApps.OrchardCore.Omnichannel.Core
public interface IOmnichannelHandoffService
{
    Task<HandoffResult> RequestHandoffAsync(
        OmnichannelActivity activity,
        HandoffContext context,          // reason, AI summary, optional targetQueueId override
        CancellationToken cancellationToken = default);
}
```

- The **AI tool** `transfer_to_agent` is registered for automated omnichannel profiles. Both conversation
  handlers already own the AI turn, so they intercept the tool call (or the `[[HANDOFF]]` marker) and invoke
  the service.
- **Channel-specific implementations** are selected by `activity.Channel`:
  - `SmsAgentHandoffService` (in the SMS Workspace module — it owns `SmsConversation`).
  - `VoiceAgentHandoffService` (in Contact Center — it owns queues + offers).
- The service is resolved optionally; when the relevant feature is not enabled, the bot simply continues (no
  handoff available), exactly like the business-hours gate.

### Configuration surface

Extend [`SubjectFlowSettings`](../../../src/Core/CrestApps.OrchardCore.Omnichannel.Core/Models/SubjectFlowSettings.cs)
(composed from the subject content-type AI settings part) with:

| Field | Meaning |
| --- | --- |
| `EnableAgentHandoff` | Master switch for this campaign/subject. |
| `HandoffQueueId` | Target queue. For phone, defaults to the endpoint's existing inbound queue. |
| `HandoffOnUserRequest` / `HandoffOnQualified` / `HandoffOnFrustration` | Trigger policy toggles. |

This is the "configure the queue that handles AI→Agent handoff during campaign loading" surface: the choice is
made on the subject flow / campaign editor and snapshotted onto the activity when the inventory is loaded
(the same way `CadenceId`, `BusinessHoursCalendarId`, and the response-delay fields already are).

## SMS handoff flow

1. The model calls `transfer_to_agent` (or emits `[[HANDOFF]]`). The handler sends **one bridge SMS**:
   *"I'm connecting you with a specialist — they'll reply here shortly."*
2. **Conclude the automated activity** with terminal reason `handed_off` (a real terminal status). This
   permanently stops the AI from replying: the existing terminal-status guard in `SmsOmnichannelEventHandler`
   already refuses to reopen `Completed / Cancelled / Failed / Purged` activities, so a later customer text
   will not resurrect the bot.
3. `SmsAgentHandoffService` **creates the human `SmsConversation` immediately** (not lazily), owner = the
   configured **queue**, seeded with:
   - the prior AI turns **imported** as `OmnichannelMessage` records (so the agent sees the actual chat), and
   - the AI summary + captured fields attached as an **internal note**.
   `UnreadCount` is set and `ISmsRealTimeNotifier.NewInboundMessageAsync` fires so the inbox lights up.
4. An agent opens the thread in the **SMS Workspace inbox**, sees the full prior chat, and replies on the same
   number. `ExistingConversationRouter` keeps every subsequent inbound in that human thread.

**Design decision — transcript bridging.** The AI transcript lives in `AIChatSessionPrompt` (keyed by
`AISessionId`); the human thread stores `OmnichannelMessage`. The cleanest approach is to **import** the prior
turns into the human conversation at creation and attach the AI summary as a note, rather than build a merged
cross-store view. This keeps the Workspace inbox as the single source of truth for the human thread.

## Phone handoff flow

1. The model calls `transfer_to_agent` / emits `[[HANDOFF]]`. The handler **speaks a bridge line**:
   *"Let me connect you with a specialist — one moment."* (reusing the existing speak → `speak.ended`
   sequencing that today precedes a graceful hangup).
2. On `speak.ended`, instead of hanging up, **seat the live call into the configured inbound queue and offer
   it** — reusing the exact enqueue + `IVoiceQueueOfferService.OfferNextAsync` path that
   `InboundVoiceCallProcessor` already uses for inbound calls. The AI call already owns an `OmnichannelActivity`
   and an `Interaction`; the handoff:
   - flips the activity from the **Automated** lane to the **Manual/queued** lane,
   - enqueues it to `HandoffQueueId` (default: the endpoint's inbound queue),
   - offers to the next eligible agent; on accept, the existing offer → connect pipeline bridges caller ↔ agent.
   The AI state machine simply stops owning the call.
3. **No agent / after-hours fallbacks** reuse existing machinery:
   - `IBusinessHoursGate` for after-hours,
   - the existing **voicemail** path,
   - `CallbackService` for "we'll call you back," or
   - hand back to the bot with an apology.

**Recommended over provider blind-transfer.** `IContactCenterTransferService` + `InteractionTransferTargetType.Queue`
already implements agent-initiated "re-enqueue the activity to a target queue." We *could* drive the phone
handoff through it, but the **enqueue-and-offer path is how inbound already seats callers** and gives us
presence, reservation, voicemail, and no-agent handling for free. Use that; keep the transfer service as the
reference for the re-enqueue semantics.

## Edge cases

| Case | Handling |
| --- | --- |
| Customer opts out mid-handoff (STOP) | Existing opt-out paths win: flag Do-not-SMS, close the thread. |
| No agent available (SMS) | Conversation sits unassigned in the queue inbox; standard Workspace routing/notifications apply. |
| No agent available (phone) | Queue-wait → voicemail / callback / bot fallback (above). |
| After-hours | `IBusinessHoursGate` gates the handoff; bot offers callback or takes a message instead. |
| Re-handoff / bounce-back | The automated activity is terminal (`handed_off`); it does not re-arm. A returning SMS lands in the existing human thread, not a new bot session. |
| Handoff requested but feature disabled | `IOmnichannelHandoffService` unresolved → bot continues; no dead-end. |

## Reporting / KPIs

- New terminal reason `handed_off` + a **Handoff** disposition (or disposition category).
- **Containment rate** = concluded-by-bot ÷ total automated conversations.
- **Escalation rate**, **time-to-handoff**, and **post-handoff outcome** (agent disposition on the resulting
  human interaction) become reportable through the existing interaction/activity reporting.

## Phasing

- **Phase 0 — shared spine. ✅ Done.** `transfer_to_agent` AI tool (no marker — see status note above);
  `IOmnichannelHandoffService` contract; `handed_off_to_agent` terminal reason; `SubjectFlowSettings` config
  fields + editor (inbound **and** outbound). Two deliberate simplifications: escalations are recorded via the
  **terminal reason** rather than a dedicated `OmnichannelDisposition`, and the handlers **re-derive** the flow
  settings from the subject at handoff time rather than snapshotting them onto the activity at load.
- **Phase 1 — SMS. ✅ Done.** `SmsAgentHandoffService`: bridge SMS → conclude the automated activity
  (`handed_off_to_agent`, which the terminal-status guard makes permanent) → create the queue-owned human
  `SmsConversation`, **import the prior transcript** as `OmnichannelMessage` records → notify. Subsequent
  inbound stays in the human thread via `ExistingConversationRouter`. *The separate AI-generated summary note is
  deferred to Phase 3 (the imported transcript already gives the agent the full context).*
- **Phase 2 — Phone. ✅ Done.** `VoiceAgentHandoffService`: bridge line → create the CC interaction → flip the
  activity to the manual/queued lane → enqueue + `OfferNextAsync`. No-agent → the call waits in the queue;
  after-hours (via `IBusinessHoursGate`) → a callback is scheduled (`ICallbackService`) and the caller hears a
  closing line. *Voicemail-on-no-agent is not available — the Telnyx voice client exposes no record command.*
- **Phase 3 — polish. ✅ Mostly done.** Containment reporting (`HandoffContainmentReportProvider`); **warm
  context** — an AI-written summary is generated at SMS handoff, stored on the conversation, and shown as a
  panel at the top of the inbox thread; and the report now **counts voice *routed* handoffs** via a durable
  `OmnichannelActivity.AiEscalated` flag (indexed) set at every handoff, so an escalation is counted even after
  a routed voice call leaves the automated lane. *Remaining:* the voice-side AI summary on the **agent-desktop
  call screen** (the SMS thread has a natural home for it; the live voice call needs the agent-desktop surface).

### Live-verification gates (before production)

Everything above builds and is unit-tested at every seam. Two things need a real tenant to confirm end-to-end,
and neither can be exercised without AI credentials + a Telnyx line: (1) the model actually **invoking** the
`transfer_to_agent` tool inside the automated `CompleteAsync` path, and (2) the **media bridge** connecting an
agent onto the live outbound Telnyx call on accept.

## Open decisions (recommendations)

1. **Signal mechanism** — AI tool `transfer_to_agent` (primary) + `[[HANDOFF]]` marker (fallback). *Recommended.*
2. **Phone media path** — reuse inbound enqueue + offer, not provider blind-transfer. *Recommended.*
3. **SMS history** — import the AI transcript into the human thread + attach a summary note. *Recommended.*

## Reuse boundaries (do **not** rebuild)

- Human SMS inbox, routing, and notifications: **SMS Workspace** (`SmsInboundProcessor`,
  `ExistingConversationRouter`, `ISmsRealTimeNotifier`, `SmsConversation`).
- Phone queue seating, offer, reservation, bridge, voicemail: **Contact Center** (`InboundVoiceCallProcessor`,
  `IVoiceQueueOfferService`, `IActivityQueueService`).
- After-hours gating: `IBusinessHoursGate`. Callback: `CallbackService`. Conversation summary: the existing
  conclusion-analysis pass.
- Campaign/subject configuration + activity snapshotting: `SubjectFlowSettings` and the subject AI settings part.
