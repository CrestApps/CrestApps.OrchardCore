# AI agent parity plan: Retell AI

Status: planned, 2026-10-26. Nothing built yet.

The goal is to do everything Retell AI (retellai.com) offers for voice and chat AI agents, on top of what we already
have: AI profiles, the Telnyx AI voice agent, SMS agents, the contact center, and Orchard Core workflows. The request
named retailai.com, but that domain is a placeholder page with no product; Retell AI matches the description (AI
agents that make calls and run workflows), so it is the target here. Confirm this before phase 1 (decision AD1).

The comparison comes from Retell's documentation index (docs.retellai.com) and feature pages as of October 2026, and
from a code survey of this repository. Check each row against Retell's current docs before its slice starts; they
ship often.

## The approach: workflows first

Most of what Retell sells as separate features is "run something before, during or after a conversation". We already
have a workflow engine with about a hundred Contact Center event types, AI session events, HTTP requests, JavaScript
(the Script task), Liquid, timers, loops, SMS, callbacks and reports. What is missing is a way for workflows to take
part in a conversation while it is happening. Four integration points close most of the gap:

1. **Before the conversation:** a synchronous **AI conversation starting** event. A workflow fetches data, sets
   conversation variables, picks the AI profile, or declines the conversation. This is Retell's pre-call workflow,
   dynamic variables, inbound call webhook and, with a random split in a Script task, a basic A/B test.
2. **During the conversation, when the AI asks:** a **Workflow** tool-instance source. The model calls a tool; the
   tool runs a chosen workflow with typed arguments and returns the workflow's output. This is Retell's custom
   functions, code tool, CRM and calendar tools, and most of its integration tools.
3. **During the conversation, from outside:** tasks that act on a live AI conversation: add instructions, transfer,
   end, press keys. A tool workflow, an event workflow or a supervisor can use them.
4. **After the conversation:** the existing asynchronous events (Contact Center events through the outbox, AI session
   Closed, Post-Processed, Field Extracted, All Fields Extracted), plus a few new ones. This is Retell's post-call
   workflow, contact sync and outbound webhooks.

Things a workflow can't do well (audio, telephony, supervision, latency-sensitive speech, versioning, testing) are
built into the runtime. Each phase below says which route each slice takes.

### What the code tells us

These facts shape the plan; each was checked in the source.

- Contact Center events reach workflows through the outbox after the change commits
  (`Modules/CrestApps.OrchardCore.ContactCenter/Handlers/ContactCenterWorkflowEventHandler.cs`). They can react to a
  call, but never change a live one. Anything that must affect a live conversation needs a synchronous hook.
- The turn-based phone engine gives the model only `endCall` and `transferToLiveAgent`
  (`Core/CrestApps.OrchardCore.Omnichannel.Voice.Core/Services/VoiceAgentConversationLoop.Tools.cs`). Profile tools,
  tool instances and MCP tools only reach the realtime (speech-to-speech) engine, through the orchestrator
  (`RealtimeVoiceConversationRunner.Session.cs`). Every "workflow as a tool" feature depends on fixing this first.
- Tool instances already have typed parameters with **Model**, **Fixed** and **Context** fills
  (`IAIToolInstanceSource`, registered with `AddSource`; admin in
  `Modules/CrestApps.OrchardCore.AI/Controllers/AIToolInstancesController.cs`). A new source is the natural home for
  "run a workflow".
- `IWorkflowManager.StartWorkflowAsync` returns the `WorkflowExecutionContext`, whose `Output` a workflow fills with
  **Set Output**, so a tool can run a workflow synchronously and read its result.
- AI phone calls run on an AI chat session (`activity.AISessionId`), so the AI session events should fire for calls
  too. Confirm end to end in slice W5.
- The Post-Processed event passes `Session`, `Profile` and `Results`, but no transcript. Memory, QA and CRM recipes
  need one (slice W5).
- Liquid is supported in the system message and the opening message. The SMS opening message gets `Activity`,
  `Contact`, `FlowSettings`, `Profile`, `Session` and `Campaign`
  (`Modules/CrestApps.OrchardCore.Omnichannel.Sms/Services/SmsOmnichannelProcessor.cs`). What the system message sees
  on each channel is decided in CrestApps.Core and needs checking (slice W2).
- An AI call has no Interaction, CallSession, agent leg or queue while the AI is talking: only an Activity
  (`InteractionType=Automated`) and one caller leg (`InboundVoiceCallProcessor.AIAgent.cs`). Supervision needs all
  four, which is why AI calls don't show on the live dashboard (phase 2).
- The AI-to-human handoff already stops the AI audio before a human is bridged, and the contact center already has a
  synthetic direct-routing queue that rings one user (`ContactCenterConstants.DirectRouting`). Together they make
  "take over" a small change (slice S4).
- The realtime session lives in memory inside the `call.answered` webhook request, on one node. Anything that reaches
  a live call from outside (whisper, transfer, end) must find that node (slice S3).
- AI voice runs only on Telnyx: `IVoiceAgentMediaProvider` has one implementation
  (`TelnyxVoiceAgentMediaProvider.cs`). Asterisk has no AI voice.

## Where we already lead

These stay as they are; new work must not weaken them.

| Capability | Us | Retell |
| --- | --- | --- |
| A full human contact center on the same platform: queues, Preview, Power, Progressive and Predictive dialing, supervision, wrap-up, recordings, dispositions | Core | No: hands off to a CCaaS |
| CRM built in: contacts, leads, accounts, opportunities, AI lead conversion, activity history across channels | Core | Contacts only, CRM through sync |
| A general workflow engine with about a hundred contact center events, HTTP, JavaScript, Liquid, timers and loops | Core | Pre- and post-call function chains only |
| Any AI provider and model, including local models through Ollama and any OpenAI-compatible API | Core | Their model list |
| An MCP server and A2A host exposing our tools and agents | Core | MCP client only |
| A report builder with custom data sources, formulas, scheduled emails and exports | Core | Fixed dashboard charts |
| A DNC registry with FTC and Canada DNCL lists that fails closed, calling windows and opt-out checks | Core | DNC requests only |
| Recording consent and disclosure, secure data capture, recording erasure | Core | Partial |
| Commerce, bookings, payments and multi-tenant hosting with tenant hierarchy | Core | No |
| Self-hosted, on-prem or any cloud | Core | Hosted, on-prem for enterprise |

## Gaps

Every Retell feature we don't fully have. **Route** says how it closes: **Recipe** means existing workflow parts
plus a starting point and docs; **Workflow** means new workflow events or tasks; **Runtime** means work in the
conversation, telephony or admin code. The slice column refers to the phases below.

### Building agents

| Retell feature | Us today | Route | Slice |
| --- | --- | --- | --- |
| Pre-call functions: fetch data before the agent speaks or before dialing | No synchronous hook | Workflow | W3 |
| Dynamic variables in the prompt from the contact, a CRM, the API or a webhook | Liquid in the system and opening messages; contact name on calls | Workflow + Runtime | W2, W3 |
| Custom functions (HTTP to your endpoint) | HTTP API tool instances, realtime engine only | Runtime | W1 |
| Code tool (sandboxed JavaScript) | Script task exists in workflows, not as a tool | Workflow | W4 |
| Integration tools (CRM, Zendesk, calendars) | No | Workflow, then native | W4, M2, M3 |
| Extract dynamic variables during the conversation | Data extraction to session fields, with events | Runtime (small) | W2 |
| Agent transfer: another AI agent takes over the call or chat | Sub-agents answer a tool call and hand back | Runtime + Workflow | T4 |
| Conversation flow builder: nodes, transition conditions, subflows, global nodes, flex mode | No | Workflow + Runtime | F1 to F3 |
| Agent Handbook toggles: echo verification, NATO spelling, speech normalization, fuzzy name match, AI disclosure, scope limits, filler words, empathy | No | Runtime (prompt presets) | V3 |
| Multilingual agents with language detection and switching | Fixed language per persona | Runtime | V4 |
| Guardrails: moderation categories on input and output with placeholder replies | Prompt Security: injection detection, output filtering | Runtime (Core) | V5 |

### The phone AI at runtime

| Retell feature | Us today | Route | Slice |
| --- | --- | --- | --- |
| Tools on every call | Only on the realtime engine | Runtime | W1 |
| Send SMS during a call | Realtime engine only; after the call by workflow | Runtime | W1 |
| Press digits to navigate a phone menu | No | Runtime + Workflow | T1 |
| Capture the caller's keypad input during an AI call | No: `GatherAsync` is never called | Runtime + Workflow | T2 |
| Hang up on an IVR; recognize iPhone and Google call screening | Voicemail detection only | Runtime | T3 |
| Cold transfer to a phone number | No: queue handoff only | Runtime + Workflow | T4 |
| Warm transfer: private briefing, three-way introduction, wait for a person, screening side call, hold music, caller ID choice | Queue handoff with a saved AI summary | Runtime | T4 |
| Live call list, live transcript, silent listen, take over, end call | Human agents' calls only | Runtime | S1 to S5 |
| Update a live call: add context or prompt a reply | No | Workflow + Runtime | S3 |
| Inbound call webhook choosing the agent and variables per call | Entry points map to one profile | Workflow | W3 |
| Concurrency and pacing for AI campaigns | A background pass every 5 minutes | Runtime | T5 |

### Numbers and voice

| Retell feature | Us today | Route | Slice |
| --- | --- | --- | --- |
| Buy, import and release numbers from the admin | Assign numbers already in the Telnyx account | Runtime | V6 |
| Branded caller ID, verified numbers, business profile, spam-likely monitoring | No | Runtime | V7 |
| AI voice on Twilio, Vonage, generic SIP trunks; Genesys, Five9, Amazon Connect, Avaya | Telnyx only | Runtime | V8 |
| Custom SIP headers into variables | Internal `X-CC-*` headers only | Runtime | V8 |
| ElevenLabs, Cartesia and other voices; voice library search; voice cloning | OpenAI-style, Azure and Telnyx voices | Runtime (Core) | V1 |
| Pronunciation dictionary, speed, expressive speech, spell and pause markup, TTS fallback | No | Runtime (Core) | V1 |
| Interruption sensitivity, backchanneling, boosted keywords, denoising and background-speech removal on AI calls | Turn detection type and eagerness, barge-in, filler while looking up, office sound | Runtime | V2 |

### Testing and quality

| Retell feature | Us today | Route | Slice |
| --- | --- | --- | --- |
| Agent versions: draft, publish, tags, compare, restore | Profile Builder revisions of instructions only | Runtime (Core) | Q1 |
| A/B traffic split with analytics per version | No | Workflow + Runtime | W3, Q2 |
| Simulation tests: simulated caller, success criteria, variable values, mocked functions, batch runs, CI API, tests from real calls | Profile Builder replays rated messages with an LLM judge | Runtime | Q3 |
| AI QA: cohorts, sampling, criteria, hallucination and knowledge-base accuracy, tool use, top caller questions, drill-down | Conversion goals and resolution detection | Workflow + Runtime | Q4 |
| Typed post-call extraction fields, rerun analysis, write to contact fields (overwrite, fill if empty, accumulate) | Semantic and predefined-option tasks, no types, no rerun | Recipe now, Runtime later | W5, Q5 |
| Alert rules: absolute and relative thresholds, email and signed webhook | No AI alerts | Recipe now, Runtime later | W5, Q6 |
| Per-call latency breakdown, cost per call, disconnection reasons | Time to first audio, barge-ins, tokens, hangup causes | Runtime | Q7 |

### Data, memory and integrations

| Retell feature | Us today | Route | Slice |
| --- | --- | --- | --- |
| Contact memory: per phone number, across channels and agents, editable, backfill | AI Memory per signed-in user | Recipe now, Runtime later | W5, M1 |
| Calendar booking: Cal.com, Calendly, Google, Outlook | No | Workflow, then native | W4, M2 |
| Two-way CRM sync: HubSpot, Salesforce, Dynamics, Zoho, GoHighLevel, Zendesk | No | Workflow, then native | W5, M3 |
| Knowledge sources: Google Drive, OneDrive, Notion | Documents, web crawlers, local, FTP and SFTP file sources | Runtime | M4 |
| Make, Zapier, n8n, Slack; start calls from a CRM or Slack | HTTP Request task and event can do it, undocumented | Recipe | W5 |
| Signed outbound webhooks for call and chat events | Commerce events only | Runtime | M5 |

### Developer platform and enterprise

| Retell feature | Us today | Route | Slice |
| --- | --- | --- | --- |
| REST API for agents, calls, chats, numbers, knowledge bases, contacts, tests | Completion and session reads only | Runtime | D1 |
| API keys with scopes; public keys for browser calls | MCP and A2A keys, no scopes | Runtime | D1 |
| TypeScript and Python SDKs, a CLI | No | Runtime | D2 |
| Custom LLM over WebSocket | Custom providers in code; any OpenAI-compatible API | Runtime | D3 |
| PII redaction of transcripts and recordings, data storage modes, retention for AI data | Retention for recordings only | Runtime | D4 |
| An AI copilot across agents, calls, dashboards, QA and contacts (Conductor) | Profile Builder for instructions; admin AI agent tools | Runtime | D5 |

Not in scope: HIPAA, SOC 2, ISO 27001 and GDPR certifications (organizational, not code), partner programs, training
sites, KYC, and Retell's per-minute billing (decision AD11).

## Phase 1: Workflows in the conversation

The foundation every later phase builds on. Route: Workflow + Runtime.

### W1. Tools on every phone engine

- Give the turn-based engine the profile's tools, tool instances, MCP connections and A2A agents, not just end-call and
  transfer. Today `AttachCallTools` replaces the scoped tool entries for each turn; merge the profile's tools into
  them instead, so every engine offers the same tools.
- Speak a short filler ("one moment") when a tool takes longer than a threshold, as the realtime engine already does
  with `GroundingAcknowledgementDelayMs`.
- Offer **Send SMS** (`Modules/CrestApps.OrchardCore.AI.Agent/Communications/SendSmsTool.cs`) on calls, sending to
  the caller's number by default.
- Tool timeouts on calls: a per-tool limit (default 8 seconds) so a slow endpoint can't leave a caller in silence.
- **Done when:** a profile with an HTTP API tool instance answers a question from that endpoint on a turn-based call
  and on a realtime call, and texts the caller a link mid-call.

### W2. Conversation variables

- A variables bag on the AI session (string keys, JSON values), with the same keys on every channel: web chat, SMS,
  inbound and outbound calls.
- Seeded automatically: `Contact` (the content item), `Activity`, `Campaign`, `Subject`, `Profile`, `Session`, and a
  `Call` object (from, to, direction, entry point, caller ID name) or `Message` object for SMS.
- Available as `{{ Variables.name }}` in the system message, the opening message, persona instructions and prompt
  templates. Check what the CrestApps.Core system-message renderer exposes today; if it can't take extra values, add
  a template-context seam in Core (decision AD10).
- New tool-parameter **Context** keys: `contact.*`, `activity.*`, `call.*` and `variables.*`, through
  `IAIToolParameterContextResolver`, so a tool can be given the caller's number without the model typing it.
- Data extraction writes extracted fields into the variables bag too, so later tools and workflows can read what was
  captured mid-conversation.
- Shown on the session review page and passed in every AI workflow event's input as `Variables`.
- **Done when:** an outbound call's system message renders `{{ Variables.balance_due }}` from a value the load put
  on the activity, and an extracted `callback_time` is available to the next tool call.

### W3. AI conversation starting event

- A synchronous workflow event, **AI conversation starting**, fired before the first message on every channel:
  - inbound calls in `InboundVoiceCallProcessor.AIAgent.cs`, before answering;
  - outbound calls in `Modules/CrestApps.OrchardCore.Telnyx/Services/VoiceOmnichannelProcessor.cs`, before dialing;
  - SMS in `SmsOmnichannelProcessor.StartAsync`;
  - web chat when a session is created.
- Input: channel, direction, from, to, contact, activity, campaign, subject, profile, variables.
- New tasks for it:
  - **Set conversation variables**: name and Liquid value pairs.
  - **Use AI profile**: switches the profile, for routing and A/B splits.
  - **Decline conversation**: an outbound call isn't placed; an inbound call goes to the entry point's fallback, and
    a chat gets a message.
- A time budget per channel (default 3 seconds inbound, 30 seconds outbound and SMS). If a workflow runs over or
  fails, the conversation starts with what it has and the failure is logged (fail open).
- Runs in the request's shell scope with the workflow's changes committed before the conversation starts.
- Recipe schemas for the event and tasks under `Core/CrestApps.OrchardCore.Recipes.Core/Schemas/Workflows/Activities`.
- **Done when:** an inbound call's starting workflow looks the caller up with an HTTP Request, sets `tier` and
  `first_name`, and switches to a VIP profile when `tier` is `gold`, all before the greeting, within the time budget.

### W4. Workflow tool source

- A **Workflow** tool-instance source, registered with `AddSource` in the AI Workflows feature. The instance picks a
  workflow type and declares parameters with the existing editor (Model, Fixed and Context fills).
- A new start event, **AI tool called**, whose input is the tool's arguments plus `SessionId`, `ActivityId`,
  `ContactId`, `ProfileId` and `Variables`.
- The source runs the workflow with `IWorkflowManager.StartWorkflowAsync`, waits for it, and returns its `Output` (set
  with **Set Output**) to the model as JSON. A workflow that blocks (waits for a signal or timer) returns
  "still working" with its id, so the model can tell the caller it will follow up.
- Per-instance timeout, the filler from W1 while it runs, and mocks for tests (slice Q3).
- A tool workflow can use the live-call tasks from S3 (transfer, end, add instructions, press keys) on its own
  conversation.
- Authorization: creating an instance needs Manage Workflows and Manage AI Tools.
- Starting points under **Add Tool Instance**: "Look up a record over HTTP", "Run JavaScript", "Book a time with
  Cal.com", "Create a HubSpot contact", "Add to Do Not Call".
- **Done when:** during a call, the AI calls a workflow tool that asks Cal.com for free slots (HTTP Request), reads
  two options back, books the caller's choice and texts a confirmation, on both phone engines.

### W5. Recipes for what workflows already do

No new runtime code beyond small event changes; each is a documented starting point (a recipe or a workflow
template) in the user manual.

- Add `Transcript` (role, text and time per turn) and `Variables` to the AI session Closed, Post-Processed and Field
  Extracted event input, and a Liquid filter that renders a session transcript. Confirm the events fire for phone
  calls.
- **Save what the AI learned to the contact:** Field Extracted → Update Content Item.
- **Contact memory, the workflow way:** Post-Processed → AI Completion ("merge the old notes with this
  conversation", 2,000 characters) → save to a contact field → the system message reads it with Liquid. Replaced by
  the built-in version in M1.
- **CRM write-back and logging:** Post-Processed → HTTP Request to HubSpot or Salesforce.
- **Start an AI call from another system:** HTTP Request event → Start Omnichannel Activity; in a For Each for a list.
- **Follow up after the call:** a disposition → Send Text Message, Schedule Callback or Schedule Dialer Retry.
- **Slack, Teams, Zapier, Make, n8n:** HTTP Request to their incoming webhooks; HTTP Request event to receive.
- **Alerts:** Timer → Run Report (a report whose rows are the breach, so "No rows" means healthy) → Email, Notify or
  a Slack webhook. Replaced by built-in rules in Q6.
- **Basic QA:** Post-Processed → AI Completion with a QA profile → save the score. Replaced by Q4.
- **Done when:** each starting point is installable from the admin and runs on a test tenant against a real call.

### Phase 1 exit gate

An inbound call's starting workflow looks the caller up in an outside CRM and sets variables; the AI greets them by
name; mid-call it books a slot through a workflow tool and texts the confirmation; after the call the extracted
fields are on the contact, the CRM has a logged call, and a Slack channel has the summary.

## Phase 2: Supervising AI calls

Make AI calls visible and controllable, reusing the human-agent supervision where possible. Route: Runtime + Workflow.
Telnyx only.

### S1. AI calls on the live dashboard

- A section of live AI calls: activities that are Automated, In progress, on the phone channel. Shows the contact,
  number, profile, direction, campaign or entry point, duration, and whether the AI is speaking, listening or running
  a tool.
- Authorization: a new **Monitor AI calls** permission, scoped like queues by the entry point or subject's handoff
  queue when one is set.
- `HandleStateAsync` in `SupervisorDashboardEndpoints.cs` builds the board from agent rows only; add an AI-call
  query beside it.

### S2. Live transcript

- Publish each turn as it's stored (`RealtimeVoiceConversationRunner.Pumps.cs` `StorePromptAsync` and
  `VoiceAgentConversationLoop.Transcript.cs`) through a notifier to a hub group per activity.
- Shows caller and AI turns, tool calls with arguments and results, keypad digits (after T2), SMS sent, and variables
  as they change. Follows the latest turn, with "Jump to latest".
- Also usable for live chats and SMS conversations, from the same notifier.

### S3. Live conversation control

- A per-conversation control channel: a registry keyed by activity id (like `AnsweringMachineSignals.cs`) that the
  running conversation listens on. With more than one node, relay commands through Redis pub/sub when the Redis
  feature is on, so a command reaches the node holding the call (decision AD3).
- Commands, each also a workflow task:
  - **Add instructions to AI conversation:** context the model reads but doesn't speak (whisper to the AI). The
    realtime session needs a way to add context without forcing a reply; check CrestApps.Core and add it if missing
    (AD10). The turn-based engine adds a system message.
  - **Prompt the AI to respond.**
  - **End AI call:** with an optional closing line.
  - **Transfer AI call:** the targets in T4.
- Supervisor UI: a "Message the AI" box beside the live transcript, and End call.

### S4. Take over

- **Take over** is "hand off to me": reuse `VoiceAgentHandoffService`, which already stops the AI audio before
  bridging, with the synthetic direct-routing queue targeting the supervisor instead of the subject's queue. The
  supervisor must be a signed-in agent; the call arrives as an offer they have already accepted.
- The activity becomes Manual and AI escalated, with the AI's summary, as a normal handoff does, so wrap-up,
  presence and reporting apply.
- Raise the existing **Supervisor took a call over** event with the AI as the previous handler.
- The same path gives a **Transfer AI call to an agent** workflow task.
- Guard the races the handoff already handles: the turn-based `speak.ended` and `call.hangup` handlers, the
  after-hours branch, and recording continuity.

### S5. Listen

- **Spike first:** a Telnyx supervisor leg with `supervise_call_control_id` set to the caller leg and the monitor
  role. Check that the supervisor hears both sides on the turn-based engine (Telnyx `speak`) and on the realtime
  engine (audio injected through bidirectional streaming).
- If the realtime AI audio isn't carried, use the fallback: the server already has both directions, so mix them and
  stream to the supervisor's browser over WebRTC or a WebSocket, independent of Telnyx.
- Engagement records keyed by activity, modeled on `ContactCenterPhoneCallSupervisionService.cs` and its engagement
  store, rather than relaxing the CallSession guards in `ContactCenterMonitoringService.cs`.
- Barge (the supervisor speaking to the caller and the AI together) is out of scope; Take over covers it.
- Long term (decision AD2): create the Interaction and CallSession when an AI call starts, with the AI as a
  participant, so every supervision, dashboard and report path works unchanged.

### S6. New events

Added to the Contact Center event catalog (`ContactCenterWorkflowEventTypeProvider.cs`) and to the AI session events:

- AI handoff completed (a person is connected), AI transfer failed.
- AI voicemail left, AI IVR reached (after T3).
- Caller pressed a key during an AI call (after T2).
- AI tool called and AI tool failed (audit, with duration).
- AI conversation turn (each turn, asynchronous, off by default per profile because of volume). Lets a workflow flag
  "cancel my account" or negative sentiment while the call is live and notify a supervisor.
- Supervisor engaged an AI call, messaged the AI, took over.

### Phase 2 exit gate

A supervisor sees a live AI call on the dashboard, reads its transcript as it happens, listens in, tells the AI "offer
the 10% retention discount" and hears it offered, then takes the call over and talks to the caller. A workflow on the
AI conversation turn event notifies the supervisor when the caller says "cancel".

## Phase 3: Telephony actions for the AI

Route: Runtime + Workflow. Telnyx only.

### T1. Press digits

- A **Press keys** tool on calls and a matching workflow task. Sends DTMF on the caller leg through Telnyx
  `send_dtmf`, as the soft phone already does through `ITelephonyDtmfProvider`.
- Prompt guidance for phone menus: listen to the whole menu, press, wait, enter extensions.
- **Done when:** the AI calls a business, picks the right menu option and enters an extension.

### T2. Caller keypad input

- Feed Telnyx `call.dtmf.received` into the conversation loop as a caller turn ("pressed 1 2 3 #"), using
  `IVoiceAgentMediaProvider.GatherAsync` for "enter your account number" prompts with a terminator and timeout.
- Hand sensitive digits to secure data capture instead of the model when the profile says so.
- Raise the "Caller pressed a key" event (S6).

### T3. IVR and call screening

- On outbound calls, detect a phone menu (Telnyx machine detection result plus a classifier on the first utterance)
  and either hang up or navigate (T1), as a profile setting.
- Recognize iPhone and Google call screening ("state your name and reason") as a live pickup, not voicemail, and
  answer it in one sentence.
- New disconnection reasons: IVR reached, screened.

### T4. Transfers from the AI

- Targets: a queue (today), a specific agent (S4), a phone number, or another AI profile.
- Modes:
  - **Cold:** connect the caller to the destination and drop the AI.
  - **Warm:** the AI dials the destination on a side leg, waits for a person (not a phone menu or hold system), gives
    a private briefing written from the conversation, then bridges; optional three-way introduction.
  - **Screened:** a short side call asks the destination whether they can take it; on no or timeout, return to the AI
    with a reason, or go to a fallback.
- Settings per transfer: ring timeout, hold music (upload), caller ID shown (our number or the caller's, where the
  carrier allows), fallback.
- Reuse the contact center's consult and warm-transfer machinery: the handoff already creates the Interaction, so the
  AI side leg is a consult leg.
- **AI to AI:** switch the session's profile, keep the transcript and variables, announce or not, and raise an
  event. Both the transfer tool and the Transfer AI call task accept a profile.
- **Done when:** the AI warm-transfers a caller to a mobile number, briefs the person privately, introduces the caller,
  and drops off; a screened transfer that's declined returns to the AI, which offers a callback.

### T5. AI campaign pacing

- Per campaign and tenant: maximum concurrent AI calls, calls per minute, and a start interval measured in seconds,
  replacing the 5-minute background pass for AI loads.
- Calling windows, retries and DNC stay as they are.

### Phase 3 exit gate

An AI campaign of 200 contacts runs at 10 concurrent calls; on one call the AI gets through a phone menu to a
person, takes a payment reference typed on the keypad, and warm-transfers to a mobile number with a briefing.

## Phase 4: Quality

Route: Runtime, with workflow events for each result.

### Q1. Profile versions

- Draft and published versions of an AI profile: edits go to the draft; **Publish** snapshots it as version N with
  an optional tag and note. Conversations record the version they ran on.
- Version history, a side-by-side compare of the system message, tools, model settings, voice and data sources, and
  **Restore**.
- Profiles live in the CrestApps.Core catalog. Decide whether versions are a Core feature or an Orchard snapshot store
  keyed by profile id (AD10).

### Q2. A/B tests

- On an entry point, an activity load, an SMS number or a chat widget: two or more profiles or versions with weights
  that add up to 100.
- Report Builder gets profile version as a dimension, and the AI usage and session data sources can compare variants:
  success rate, duration, handoff rate, latency, cost.
- A workflow can still route by context with **Use AI profile** (W3).

### Q3. Simulation tests

- A **Test case**: a simulated caller prompt (identity, goal, personality), success criteria, variable values, mocked
  tool results, and the simulator's model.
- A run plays the agent against the simulated caller in text through the same orchestration, with tools mocked
  (workflow tools return the mock instead of running), and an LLM judge marks Pass, Fail or Error with an
  explanation.
- Batch runs over selected cases, history, pass rates across repeats, and a recipe step and API to run a batch from
  CI.
- **Create a test from this conversation** on the session review page.
- Builds on the Profile Builder's judge (`ai/profile-builder.md`).

### Q4. AI QA

- A **QA cohort**: profiles, filters (date, channel, disposition, variables) and a sample percentage.
- **Criteria**: the conditions that make a conversation successful, plus thresholds.
- Each sampled conversation is scored after post-processing: resolution, score, hallucination against the knowledge
  base and data sources used, tool failures, transfer outcome and wait, sentiment, talk-over (barge-ins are already
  recorded) and latency. Each flagged issue keeps the transcript lines that show it.
- A QA dashboard with trends, the top caller questions with resolution rates, and drill-down into a conversation.
- A QA data source in Report Builder, a **QA evaluated** workflow event and an **Evaluate conversation** task.

### Q5. Extraction upgrades

- Typed extraction fields: text, number, yes/no, date, one of a list.
- **Rerun analysis** on a session and for a filtered set, as a button, a workflow task and a recipe step.
- Map extraction fields to contact fields with a mode: overwrite, fill only if empty, or accumulate and summarize.

### Q6. Alert rules

- An **Alert rule**: a metric (conversation count, success rate, negative sentiment rate, tool failures, transfer
  failures, handoff rate, latency, cost, concurrent AI calls, QA failures), filters, a window, and a threshold that's
  absolute or relative to the previous window.
- Evaluated on a schedule against the same data Report Builder uses. Opens and resolves incidents, and notifies by
  email, admin notification, a signed webhook (M5), and **Alert triggered** and **Alert resolved** workflow events.

### Q7. Per-call metrics

- Latency per turn split into speech recognition, model first token and first audio, with percentiles on the call
  and in analytics.
- Cost per call: tokens by the deployment's prices, plus telephony minutes and SMS, shown on the call and reportable.
- A disconnection-reason taxonomy shared by both engines: caller hung up, AI ended, transferred, voicemail, IVR,
  screened, no answer, error.

### Phase 4 exit gate

A profile change is drafted, tested by a batch of 20 simulated callers, published as version 4, and split 50/50
against version 3; a week later the QA dashboard and a report compare the two, and an alert fired by email when
version 4's tool failures crossed 5% in an hour.

## Phase 5: Memory, calendars, CRM, knowledge and webhooks

Route: Runtime, with workflow tasks for each.

### M1. Contact memory

- A memory note on each contact (2,000 characters), rewritten after each conversation by a memory prompt the tenant
  can edit, shared across channels and profiles.
- Per profile: **Save to contact memory** and **Use contact memory**. When on, the note is added to the system
  message without any Liquid.
- Viewed, edited and cleared on the contact; a backfill builds it from past conversations, oldest first.
- Decide whether it is a field on the omnichannel contact or the Core AI memory store with the contact as owner (AD5).

### M2. Calendars

- An **Appointments** feature with calendar connections: Google Calendar and Microsoft 365 first, then Cal.com and
  Calendly (AD7).
- Tool-instance sources: **Find available times**, **Book appointment**, **Reschedule**, **Cancel**, each with
  working hours, buffer, duration and time zone. Matching workflow tasks.
- Optionally book Bookings module products (AD7).

### M3. CRM connectors

- A connector framework: OAuth connection, incremental import every few minutes matched by phone number, field
  mappings both ways, pushing changes after conversations, logging calls and chats as CRM activities, and an option
  to create CRM contacts.
- Tool-instance sources for look up and update record, and workflow tasks for the same.
- HubSpot first, then Salesforce (AD6); Dynamics 365, Zoho, GoHighLevel and Zendesk after.
- Starting AI calls from a CRM's own workflows uses the HTTP Request event recipe (W5) until each connector adds a
  native trigger.

### M4. Knowledge connectors

- Google Drive, OneDrive and SharePoint, and Notion as file source providers beside local, FTP and SFTP
  (`Modules/CrestApps.OrchardCore.AI.FileSources*`), with scheduled sync and per-source status showing which profiles
  use them.

### M5. Signed outbound webhooks

- Generalize the commerce webhooks (`Modules/CrestApps.OrchardCore.Commerce.Webhooks`) into webhook subscriptions for
  AI session and Contact Center events: conversation started, ended, analyzed, transferred, QA evaluated, alert
  triggered. HMAC signatures, retries and a delivery log.
- A **Send signed webhook** workflow task.

### Phase 5 exit gate

A caller who texted last week phones in; the AI remembers the open issue without being told, books a follow-up on a
Google calendar, the HubSpot contact shows the call with the outcome fields, and a signed webhook reaches an outside
endpoint.

## Phase 6: Voice and telephony reach

Route: Runtime. Several slices need CrestApps.Core work (AD10).

### V1. Voices

- ElevenLabs and Cartesia as TTS providers for the cascaded pipeline and the turn-based engine; voice library search
  and preview in the persona and profile editors.
- Voice cloning through the provider's API, stored as a tenant voice.
- A pronunciation dictionary per profile (word to phonetic or alias), speed, expressive or emotional speech where the
  provider supports it, spell and pause markup, and a fallback TTS provider when the first fails.

### V2. Turn-taking and audio

- Interruption sensitivity (how much caller speech stops the AI), backchanneling ("mm-hm") as a profile setting,
  boosted keywords for speech recognition, and choice of speech recognition provider for the cascaded pipeline.
- Noise suppression and background-speech removal on AI call legs (Telnyx noise suppression is documented for
  agents' calls today).

### V3. Speaking style presets

- Toggles on the profile that add tested prompt sections: echo verification, NATO spelling, speech normalization of
  numbers, dates and money, fuzzy name matching, AI disclosure when asked, scope limits, filler words, high empathy.
  Implemented as prompt templates, so they compose with personas.

### V4. Languages

- Detect the caller's language and switch voice, recognition language and replies; a list of allowed languages per
  profile; the starting language from a variable.

### V5. Guardrails

- Input and output moderation categories (harassment, self-harm, violence, regulated advice, jailbreak attempts) with
  a placeholder reply, on top of Prompt Security; an event when one triggers.

### V6. Numbers

- Search, buy and release Telnyx numbers from the admin (area code, features), import numbers, and track port-in
  requests. Assign to entry points and AI profiles as today.

### V7. Caller trust

- Business profile, caller name (CNAM) and branded calling through Telnyx where offered, number reputation checks, and
  counting carrier spam rejections (SIP 608) per number with an alert (Q6). Verify what Telnyx offers before building.

### V8. Other carriers

- AI voice on Twilio through Media Streams as a second `IVoiceAgentMediaProvider`, then Asterisk through
  ExternalMedia or AudioSocket, which also gives generic SIP trunks (AD8).
- Custom SIP headers read into conversation variables on inbound calls and set on transfers.
- CCaaS hand-ins (Genesys, Five9, Amazon Connect, Avaya) through SIP trunking to an entry point, documented per
  platform.

### Phase 6 exit gate

An AI agent with a cloned ElevenLabs voice answers on a newly bought number shown with the business name, switches to
Spanish when the caller does, and pronounces the product names from the dictionary; the same profile answers on a
Twilio number.

## Phase 7: Conversation flows on workflows

Retell's flow builder, built on the workflow designer instead of a new canvas. Route: Workflow + Runtime. Starts
with a spike (AD9).

### F1. Spike

- A blocking **AI conversation stage** activity: its own instructions, tools and knowledge, and outcomes that are the
  transition conditions. The conversation runtime asks the workflow which stage is active, offers the model a
  "move to stage" tool listing the outcomes, and resumes the workflow on the chosen one.
- Existing activities are the other node types: If/Else (logic split), Script (code), HTTP Request (function), Send
  Text Message (SMS), Transfer AI call (transfer), End AI call (end), Set conversation variables (extract).
- Measure the delay of persisting and resuming the workflow at each transition on both engines. Go or no-go.

### F2. Build

- The stage activity, a conversation-flow workflow type that profiles can select, global stages (reachable from any
  stage on a condition), and sub-workflows as reusable subflows.
- A flexible mode that compiles all stages into one prompt for callers who jump between tasks.
- The stage name on every transcript turn, in the live transcript and in reports.

### F3. Tooling

- A debug view replaying a conversation through the flow, simulation tests (Q3) per flow, and starting-point flows
  for receptionist, appointment setting, lead qualification and collections.

### Phase 7 exit gate

A collections flow built in the workflow designer verifies identity, branches on balance, takes a promise to pay or
transfers to an agent, with no transition adding more than 300 ms.

## Phase 8: Developer platform and compliance

Route: Runtime.

### D1. REST API

- On Orchard Core's OpenAPI module, following the commerce API pattern: AI profiles (CRUD, publish, versions),
  conversations (list, get, transcript, rerun analysis), start an AI call or SMS conversation, batch calls, stop a
  call, update a live call (S3), knowledge bases (data sources, documents, file sources), contacts, test cases and
  batch tests, phone numbers.
- API keys as OpenId client credentials with scopes per area, and restricted browser keys for web calls.

### D2. SDKs and CLI

- TypeScript and Python clients generated from the OpenAPI document with NSwag or a similar generator, published as
  packages. A CLI only if customers ask (AD12).

### D3. Custom LLM endpoint

- A provider that streams turns to a customer's WebSocket server and speaks its replies, for customers who can't
  expose an OpenAI-compatible API (AD12).

### D4. Privacy

- PII redaction of transcripts, extracted fields and summaries (names, numbers, cards, addresses), and bleeping
  recordings using the transcript timestamps.
- A data storage mode per profile: everything, everything except PII, or metadata only.
- Retention for AI sessions, prompts and usage records, aged out by a background task, beside the recording retention
  that exists.
- Signed, expiring URLs for recordings.

### D5. AI copilot

- Extend the Profile Builder and the admin AI agent with tools over conversations, QA results, reports, test cases and
  contacts, so an admin can ask "why did calls fail yesterday?" and get proposed fixes to review. Most of the tools
  exist through the MCP server; this adds the missing ones.

## Delivery

- One branch and one PR per phase, a commit per slice, with a checklist in the PR body.
- Every new workflow activity gets a recipe schema in `Core/CrestApps.OrchardCore.Recipes.Core/Schemas/Workflows`,
  shows up in the admin AI agent's workflow tools, and has a user manual section.
- Public API baselines updated before each push.
- Each phase is verified on a test tenant with real Telnyx calls and SMS before its PR is opened; admin pages are
  checked at every Bootstrap breakpoint.
- Slices that need CrestApps.Core changes (AD10) start with the Core PR, built against a local package feed until
  Core publishes.

| Phase | Slices | Depends on | Size |
| --- | --- | --- | --- |
| 1. Workflows in the conversation | W1 to W5 | Core seam for template values (W2) | L |
| 2. Supervising AI calls | S1 to S6 | W1 for tool audit events; Core realtime context (S3) | L |
| 3. Telephony actions | T1 to T5 | S3 (transfer and end commands) | L |
| 4. Quality | Q1 to Q7 | W2 (variables in tests); Core versions (Q1) | XL |
| 5. Memory, calendars, CRM, knowledge, webhooks | M1 to M5 | W4 (tool sources) | XL |
| 6. Voice and telephony reach | V1 to V8 | Core TTS and recognition seams | XL |
| 7. Conversation flows | F1 to F3 | W3, W4, S3, T4 | L |
| 8. Developer platform and compliance | D1 to D5 | Q1 (versions in the API) | L |

Phases 2 and 3 can run in parallel with phase 4 once phase 1 is in; phases 5 and 6 are independent of each other.

## Decisions needed

| Id | Question | Default taken |
| --- | --- | --- |
| AD1 | Is Retell AI the intended comparison (retailai.com is a placeholder)? | Yes |
| AD2 | AI call supervision: a parallel path keyed by activity, or an Interaction and CallSession from the start of every AI call? | Parallel path in phase 2; revisit after S5 |
| AD3 | Live-call commands across nodes: single-node registry, or a Redis relay? | Registry, plus a Redis relay when Redis is on |
| AD4 | Alerts: built-in rules, or workflow recipes only? | Built-in rules that also raise workflow events |
| AD5 | Contact memory: a field on the contact, or Core AI memory owned by the contact? | A field on the contact |
| AD6 | First CRM connectors | HubSpot, then Salesforce |
| AD7 | First calendars, and whether to book Bookings products | Google and Microsoft 365; Cal.com and Calendly by workflow recipe; Bookings later |
| AD8 | Second carrier for AI voice | Twilio Media Streams, then Asterisk |
| AD9 | Conversation flows on the workflow designer, or a dedicated canvas | Workflow designer, if the F1 spike passes |
| AD10 | Core changes: system-message template values, realtime context injection, profile versions, TTS and recognition seams, guardrail categories | Do each in CrestApps.Core first |
| AD11 | Usage billing per tenant (credits, per-minute pricing) | Out of scope; metering already exists |
| AD12 | CLI and custom LLM WebSocket | Build only on customer demand |

## Progress

Built on branch `claude/retailai-feature-gap-analysis-43fdce`, one commit per phase.

### Phase 1: built

Conversation scope and variables, Liquid in the system message, the AI Conversation Starting event with Set
Conversation Variables, Use AI Profile and Decline Conversation, the Workflow tool source, tools on turn-based calls
and SMS, the AI Conversation Turn event, Create AI Activity, and the transcript and variables on the session events.
Verified live on a test tenant with a website chat. Open: a spoken filler and a per-tool timeout on turn-based calls.

### Phase 2: built

Live AI calls on the supervisor dashboard with Follow, Listen, Message the AI, Take over and End call; the AI Call
Action task. Open: a Redis relay for commands on multi-node hosts (AD3); Listen verified only without a real call.

### Phase 3: built

Press keys (T1), the caller's keys (T2), phone-menu and screening guidance (T3), transfers to an agent, a number or
another profile (T4) and a cap on concurrent AI calls (T5). Open: warm, briefed and screened transfers; secure
keypad capture; switching profiles on a realtime session; live Telnyx tests of every action.

### Phase 4: built, with these differences from the plan

- Q1: every save is a version (no draft and publish step). Versions page with Compare and Restore; conversations and
  evaluations record the version. Built in Orchard as a snapshot store rather than in CrestApps.Core (AD10).
- Q2: the **Split Traffic** task with a sticky key, used with **Use AI Profile**. Versions are compared by copying
  the profile; a live split between two versions of one profile is not built.
- Q3: the **Simulate AI Conversation** task, batched with For Each. Tool workflows read `Workflow.Input.Simulation`
  instead of a mock table. Not built: a test case catalog, a page to run them and "create a test from this
  conversation".
- Q4: the **Evaluate AI Conversation** task and **AI Conversation Evaluated** event, sampled with Split Traffic, and
  the **Conversation evaluations** report data set. Not built: a QA dashboard page and cohorts as records; the
  hallucination check reads the transcript only, not the knowledge base.
- Q5: the **Rerun AI Conversation Analysis** task. Not built: typed extraction fields (a Core change) and mapping
  fields to the contact with a mode.
- Q6: the **Check AI Conversation Metric** task with open and resolved alerts, notifying through the workflow (AD4
  taken as workflow-first). Not built: alert rules as records and signed webhooks (M5).
- Q7: the voice session summary already records the outcome, time to first audio, barge-ins and tokens per call;
  `Transferred` was added to the outcomes. Not built: per-turn latency percentiles and cost per call in currency.
