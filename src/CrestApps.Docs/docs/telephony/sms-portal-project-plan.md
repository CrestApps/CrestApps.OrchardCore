---
sidebar_label: SMS Portal Project Plan
sidebar_position: 4
title: SMS Communication Portal — Project Plan
description: Design plan for a human two-way SMS portal (inbox, conversations, group SMS, department numbers) built on the existing Omnichannel, Telephony, and Contact Center foundations.
---

# SMS Communication Portal — Project Plan

This page is the design plan for a **human-operated, two-way SMS portal**: an inbox and conversation
workspace where agents send, receive, and manage SMS the way a phone's messaging app does, on top of the
provider and routing foundations we already own. It complements the automated (AI) SMS path documented under
[Omnichannel](../omnichannel/index.md) and reuses the [Contact Center](../contact-center/index.md) routing model.

> **Status: implemented — this is a historical design record.** This feature has since been built and shipped
> as the **[SMS Workspace](../omnichannel/sms-workspace)** module; use that page as the current reference. This
> plan is kept for its architectural rationale, reuse boundaries, and known limits, but it does **not** track
> the final implementation. Notably, the separate **`SmsNumberRoute`** routing entity described below was
> **not** built: all SMS routing (target, distribution mode, auto-reply) now lives on the **Omnichannel channel
> endpoint** instead. Treat any `SmsNumberRoute` references here as the retired original design.

## Goals

- A messaging portal (inbox + conversation thread + composer) for **human agents**, not just automation.
- **Two-way**: inbound provider messages route to the right agent/department; agents reply and start new threads.
- **New SMS, group SMS**, and management of conversations started from the portal or received from a provider.
- **Departments** with shared department numbers that more than one agent can text from.
- **One agent, multiple numbers.**
- **Notifications** for inbound messages and delivery state.
- Pull **customer info** from the Omnichannel contact.
- **Telnyx SMS** provider integration (alongside the existing Twilio path).
- UI built with **Orchard Core display management** (display manager + drivers) so it is extensible.

## Chosen approach

Three decisions frame the whole plan:

1. **Reuse Contact Center as the routing backbone — via its channel-neutral features, never Voice.** A new
   `Telephony.Sms` module owns the *conversation*, *number-routing*, and portal UI, and delegates agents,
   presence, queues, routing strategies, and business hours to Contact Center via the already-defined
   `InteractionChannel.Sms`. It depends only on the channel-neutral Contact Center features
   (`Agents`, `Queues`, `RealTime`), so a tenant can run the SMS portal **without enabling Contact Center Voice
   or any Telephony voice wiring**.
2. **A "department" is an existing `ActivityQueue`, not a new entity.** The only genuinely new routing concept
   is binding a phone number to a target (agent or queue); everything else — membership, routing strategy,
   business hours, presence — already exists on the queue/agent model and is shared with voice.
3. **Ship human 1:1 two-way first.** Inbox + thread + send/receive on a personal or queue-backed number, with
   the Telnyx provider, before group SMS and advanced pooled routing.

### Why we do not gate on, or abstract away from, Contact Center Voice

Contact Center is already partitioned so the reusable primitives sit below Voice, not inside it. Voice
*depends on* these; they do not depend on Voice:

| Feature | Provides | Depends on Voice? |
| --- | --- | --- |
| `ContactCenter` (Area) | interaction lifecycle + history, event log, permissions, settings, admin menu | No |
| `ContactCenter.Agents` | agent profiles, skills, presence, availability, queue sign-in | No |
| `ContactCenter.Queues` | queues, reservations, business hours, **routing strategies + activity assignment** | No |
| `ContactCenter.RealTime` | shared SignalR hub, presence/offer/queue broadcasts | No |
| `ContactCenter.Voice` | voice call router + telephony wiring | — (depends on Queues + RealTime + Telephony) |

`QueuesStartup` registers the routing strategies and `IActivityAssignmentService` with **no voice-provider
dependency**, and the router routes **activities** (the CRM activity is the universal work item), not calls. So
no new "shared services" abstraction is needed: the SMS module simply takes a dependency on
`ContactCenter.Queues` (which pulls in Agents + Area) and `ContactCenter.RealTime`. The **only** Voice-shaped,
Voice-gated primitive we deliberately do **not** reuse is `ContactCenterEntryPoint` (it carries spoken
prompts, ring timeout, and voicemail and lives in the `InboundVoice` feature); the SMS module owns its own
small number-routing table instead.

## What already exists (reuse) vs. what is missing (build)

| Capability | State | Where |
| --- | --- | --- |
| Send SMS (provider-agnostic) | Exists | `OrchardCore.Sms.ISmsService` → `ISmsProvider` |
| Inbound SMS webhook (Twilio) | Exists, AI-only | `TwilioWebhookEndpoint` (Omnichannel.Sms) |
| Inbound persisted as a message | Exists | `OmnichannelMessage` (customer/service address, inbound flag) |
| Automated (AI) two-way SMS | Exists | `SmsOmnichannelEventHandler` — routes only to an automated activity |
| Channel → number catalog | Exists | `OmnichannelChannelEndpoint` (Channel="SMS", Value=DID) |
| Contact resolution + SMS opt-out | Exists | `OmnichannelContactPart.DoNotSms`, phone-match indexes, `OmnichannelSmsComplianceHelper` |
| Entry points / queues / agents / presence | Exists, voice-shaped | Contact Center |
| `InteractionChannel.Sms`, `Interaction` (channel-generic) | Exists, unused for SMS | ContactCenter.Abstractions / Core |
| Real-time notifier + SignalR hub | Exists, voice-shaped | `IContactCenterRealTimeNotifier`, `ContactCenterHub` |
| **Human inbox / conversation thread view** | **Missing** | — |
| **Agent-composed outbound, new + group SMS** | **Missing** | — |
| **Conversation (thread) domain model** | **Missing** (only flat `OmnichannelMessage`) | — |
| **Departments + shared department numbers** | **Missing** | — |
| **Inbound routing to a human / department / queue** | **Missing** (unmatched inbound is dropped today) | — |
| **Telnyx SMS provider + inbound + delivery receipts** | **Missing** | — |
| **Per-number provider dispatch (mixed providers)** | **Missing** | — |
| **Messaging real-time notifications** | **Missing** | — |

The core gap is that there is **no conversation concept and no human inbound path**. Inbound SMS that does not
match an automated activity is logged and dropped
(`"Unable to link incoming SMS message… to an Activity"`). Everything else is a building block we already own.

## Module layout

```
Abstractions/CrestApps.OrchardCore.Telephony.Sms.Abstractions   contracts, enums, notifications
Core/CrestApps.OrchardCore.Telephony.Sms.Core                    models, stores, indexes, routing, services
Modules/CrestApps.OrchardCore.Telephony.Sms                      drivers, controllers, hub, views, admin
```

Provider integration goes into the **existing Telnyx module** as a new feature, mirroring how Telnyx voice is a
feature-gated `Startup` there:

```
Modules/CrestApps.OrchardCore.Telnyx  →  new [Feature] "Telnyx SMS"
   Services/TelnyxSmsProvider.cs            OrchardCore.Sms.ISmsProvider named "Telnyx"
   Endpoints/TelnyxSmsWebhookEndpoint.cs    inbound + delivery-status (mirrors TwilioWebhookEndpoint)
```

Feature gating follows the established pattern (`[RequireFeatures(...)]`, `TryAdd*` so the right implementation
wins regardless of startup order).

## Domain model

Each entity is a `CatalogItem` + `ICatalog<T>` + `DisplayDriver<T>` so it inherits admin CRUD, deployment steps,
and display-management extensibility, the same as `ContactCenterEntryPoint` and `OmnichannelChannelEndpoint`.

**`SmsConversation`** — the thread (the central new entity), stored via `ICatalog<SmsConversation>` so each
conversation is its own document
: `Id`, `Channel="SMS"`; `ServiceAddress` (the DID we own) + `CustomerAddress` (E.164);
  `OwnerType` (`Personal | Queue`) + `OwnerId` (agent profile id for `Personal`, `ActivityQueue` id for
  `Queue`); `ContactContentItemId` (resolved contact, nullable →
  "unknown contact"); `AssignedAgentId`, `AssignmentStatus` (`Unassigned | Assigned | Pooled`);
  `Status` (`Open | Snoozed | Closed | Spam`), `IsRead`, `UnreadCount`, `LastMessageUtc`, `LastMessagePreview`;
  `AISessionId` (set when the thread was, or still is, AI-handled — see AI↔human handoff);
  `LabelIds`, `WindowExpiresUtc` (messaging/session window), timestamps. The conversation document holds only
  the thread rollup, **not** the message bodies.

**Messages — extend `OmnichannelMessage`, do not introduce a separate entity.** `OmnichannelMessage` is already
the inbound persistence model; add the portal fields to it: `ConversationId`, `SentByAgentId` (null for
inbound/automated), `DeliveryStatus` (`Queued | Sent | Delivered | Failed | Undelivered`), `ProviderMessageId`,
`MediaReferences` (ingested MMS — see below), `ErrorCode`. Messages are stored as **individual records linked
by `ConversationId` (indexed), not embedded in the conversation document**: an SMS thread is unbounded, so
embedding months of history in one document invites large-document problems, and keeping messages as records
preserves both the existing inbound-persistence path and the automated AI transcript. The conversation loads
its bubbles by the `ConversationId` index.

**`SmsNumberRoute`** — new (the one genuinely-new routing concept)
: Binds a DID to a target for inbound SMS: `EndpointId` (the `OmnichannelChannelEndpoint` / DID),
  `TargetType` (`Agent` | `Queue`), `TargetId` (agent profile id or `ActivityQueue` id),
  `DistributionMode` (`Routed` = assign via the existing reservation/routing; `SharedPool` = visible to all
  queue members, claim-to-own), `AutoReplyMessage`, `Enabled`. The provider is **not** stored here — it is a
  property of the number (see Provider selection). This is the SMS analog of an entry point, but SMS-shaped and
  not Voice-gated. It is what makes every number scenario fall out of one table:
  - one DID → `Agent` = a personal number;
  - several DID routes → the same agent = one agent, multiple numbers;
  - several DIDs → the same `Queue` = a "department" with multiple numbers.

**There is no `SmsDepartment` entity.** A department is an existing **`ActivityQueue`**: it already owns agent
membership (agents sign in via `AgentProfile.QueueIds` / `AllowedQueueIds`), a routing strategy, business
hours, reservations, real-time queue-stats, entitlements, and supervisor authorization — all shared with
voice. Inventing a parallel department would fork the agent sign-in model and duplicate routing, hours, and
membership. For pure organizational grouping (no routing impact), `ActivityQueueGroup` already exists.

"Department" survives only as a **UI label**: an SMS-enabled queue can be presented to operators as a
"Department" (or "Team") in the portal, so the requested vocabulary is preserved without a second entity or a
second membership model behind it.

Reused (extended in place, not replaced): `OmnichannelMessage` (the message/bubble, + portal fields above) and
`OmnichannelChannelEndpoint` (the numbers, + a new `ProviderName`). Reused without change:
`OmnichannelContactPart` + contact resolution + `DoNotSms`, Contact Center `AgentProfile` (presence,
membership, capacity, skills — the SMS operator identity, no separate "SMS user"), `ActivityQueue`
(the "department"), `QueueRoutingStrategy` + the routing strategies, `BusinessHoursCalendar`,
`ActivityReservation`, `AIChatSessionPrompt` (the AI transcript, for handoff),
`Interaction` / `InteractionChannel.Sms`. Deliberately **not** reused: `ContactCenterEntryPoint`
(Voice-gated, voice-shaped).

## Inbound routing pipeline

An ordered chain of `ISmsInboundRouter` implementations (`TryAdd` so precedence is deterministic):

```
Provider webhook (Telnyx / Twilio / Azure ACS)
  → normalize → OmnichannelMessage (exists)
  → resolve contact by phone (exists)
  → find-or-create SmsConversation keyed on (ServiceAddress DID + CustomerAddress)
  → SmsInboundRouter chain:
       1. AutomatedActivityRouter    open automated activity matches → existing AI path
       2. ExistingConversationRouter append to an open human conversation, keep assignment
       3. NumberRouteRouter          SmsNumberRoute for the DID → target Agent (personal inbox) or
                                     Queue (Routed = reserve/assign via ActivityRoutingService;
                                     SharedPool = visible to queue members, claim-to-own)
       4. FallbackRouter             unassigned inbox / spam bucket (no more silent drop)
  → persist message, bump UnreadCount, raise SmsMessageReceived
  → notify (SignalR)
```

The **DID (`ServiceAddress`) is the routing key**, resolved through `SmsNumberRoute`. Queue targets reuse the
existing `IActivityRoutingService` / `IActivityAssignmentService` (channel-neutral, no Voice), so SMS is routed
by the same strategies as every other channel.

## Two-way send path

`ISmsConversationService.SendAsync(conversationId, body, mediaUrls, actingAgent)`:

1. Authorize the agent owns/serves the number (personal owner, or a signed-in member of the target queue).
2. Enforce `DoNotSms` opt-out + the target queue's business hours + messaging window.
3. Dispatch through **`ISmsDispatcher`** (see Provider selection) → the `ISmsProvider` that owns the DID.
4. Persist the outbound `OmnichannelMessage` with `DeliveryStatus=Queued`, `SentByAgentId`.
5. On the provider delivery webhook, update `DeliveryStatus` and notify (the "Delivered" tick).

**Group SMS (phase 2):** an `SmsBroadcast` entity — one composer, N recipients, fan-out via an `IBackgroundTask`
(reuse the work-partition pattern from Telnyx recording ingest). Two flavors: **broadcast** (individual 1:1
threads, no cross-visibility — the compliant default) and **group thread** (MMS group — provider-dependent,
flagged advanced).

## Provider selection & multi-provider

The provider is a property of **the number**, not the routing or the message. Concretely:

- Add a `ProviderName` field to `OmnichannelChannelEndpoint` (the number catalog; it has no provider field
  today). The DID "knows" whether it is a Telnyx, Twilio, or ACS number.
- Add a **tenant-default SMS provider** setting, surfaced in the portal, used when no specific number pins the
  provider (a brand-new provider-agnostic send, or a number left unset — back-compat for existing endpoints).
- A new **`ISmsDispatcher`** resolves the provider for every outbound send as: the `From` number's
  `ProviderName` → else the tenant default → then calls that specific `OrchardCore.Sms.ISmsProvider` directly.

This is required because `OrchardCore.Sms.ISmsService` only ever sends through the single tenant-default
provider and cannot pick by `From` number — so a portal with numbers from more than one carrier must route the
send itself. `SmsNumberRoute` therefore does not carry a provider; it reads it from the bound endpoint.

## AI ↔ human handoff

A conversation can begin automated (the existing `SmsOmnichannelEventHandler` AI path) and later need a human,
or be human from the start. They share one inbox:

- When an automated activity hands off, the customer's `SmsConversation` is created (or reused) for the same
  `ServiceAddress` + `CustomerAddress`, its `AISessionId` is set, and it becomes assignable like any other
  conversation.
- The thread view **hydrates full prior context** by merging the `OmnichannelMessage` history with the
  automated session's transcript (`AIChatSessionPrompt` records: `Assistant` = the AI, `User` = the customer),
  ordered by time — so the agent picks up seeing exactly what the AI and the customer already exchanged.
- After handoff, inbound routing's `AutomatedActivityRouter` yields to `ExistingConversationRouter`, so replies
  land in the human thread rather than re-triggering automation (an explicit "return to automation" action can
  hand it back).

## Telnyx SMS provider

- Implement `OrchardCore.Sms.ISmsProvider` named `"Telnyx"` in the Telnyx module (same shape as the built-in
  `TwilioSmsProvider`). `SendAsync` → Telnyx Messaging API (`/v2/messages`, `messaging_profile_id` +
  `from`/`to`/`text`/`media_urls`), auth via the API key already stored/protected in Telnyx settings.
- Inbound + delivery-status webhook mirroring `TwilioWebhookEndpoint`: verify the Telnyx **Ed25519**
  signature (`telnyx-signature-ed25519` / `telnyx-timestamp` — note this differs from the Telnyx *voice*
  webhook validator we already have; worth a shared helper), map `message.received` → `OmnichannelMessage`,
  and `message.sent` / `message.finalized` → delivery status.

## UI — Orchard Core display management

A three-pane messaging workspace, assembled from drivers/shapes so other modules can extend every surface:

- **Conversation list** — `SmsConversation` in a `SummaryAdmin`-style display type (filters: All / Unread /
  Assigned to me / Queue (Department) / Unknown). Each row is a `DisplayDriver<SmsConversation>` shape, so badges
  (contact tags, CRM links) can be injected.
- **Thread** — message bubbles via `DisplayDriver<OmnichannelMessage>` (SMS display type) so MMS/media, delivery ticks, and system events
  are placement-driven shapes; a driver renders the composer (attachments, emoji, templates).
- **Contact profile** — `OmnichannelContactPart` display plus a placement zone so CRM/notes drivers slot into
  the right rail.

Delivery: a new `SmsPortalController` + admin menu entry (mirror `AgentWorkspaceController`), rendered through
`IDisplayManager<T>` so zones (`Content`, `Actions`, `Meta`, `Aside`) are placement-configurable. A docked
soft-phone-style widget option reuses the `SoftPhoneWidget` filter/driver pattern so agents can text without
leaving the current page. `SmsNumberRoute` admin is standard catalog controllers + drivers
(copy `EntryPointsController` + `ContactCenterEntryPointDisplayDriver`); the queues that back "departments" are
administered by the existing Contact Center Work Distribution screens — nothing new to build there.

## Real-time notifications

Add a parallel `ISmsRealTimeNotifier` + an `SmsPortalHub` (SignalR), mirroring
`ContactCenterHub` / `IContactCenterHubClient`:

- `NewInboundMessage(summary)` → toast + unread badge + list reorder.
- `MessageDelivered/Failed(messageId, status)` → live tick update.
- `ConversationAssigned/Claimed(...)` → a pooled queue message disappears from other inboxes when claimed.
- `TypingIndicator` (optional), `ConversationRead`.
- Groups: per-agent, per-queue, per-conversation (reuse the group-membership approach from
  `ContactCenterHub`).
- "Available to text" reuses `AgentProfile.PresenceStatus`, the same switch as voice. Optional bridge into
  Orchard `INotifier` / push for out-of-app alerts (phase 2).

## Permissions, compliance, security

- New `TelephonySmsPermissions` (copy `ContactCenterPermissions`): `ManageSmsNumberRoutes`, `UseSmsPortal`,
  `SendGroupSms`, `ViewAllConversations` (supervisor) vs. own/queue only. Queue membership itself is governed by
  the existing Contact Center agent entitlements — no parallel permission set.
- Enforce `DoNotSms` on every send; honor STOP/opt-out keywords
  (`OmnichannelSmsComplianceHelper.IsOptOutRequest` already exists) → set `DoNotSms` + auto-close.
- Redact addresses/PII in logs with the existing redactor pattern (`IRedactorProvider`,
  `LogDataClassifications.AddressSet`).
- Per-provider webhook auth (Telnyx Ed25519, Twilio HMAC via the existing `TwilioWebhookEndpoint.IsRequestValid`,
  Azure ACS Event Grid validation).
- 10DLC / messaging-window awareness on the conversation (`WindowExpiresUtc`) so the UI can warn before a
  session-expired send.

---

## Limits of this plan

These are the real constraints to weigh before building.

### Provider two-way support (Twilio, Azure Communication Services, Telnyx)

**All three can do two-way SMS, but two-way is not free from the current `ISmsService` abstraction — it needs
per-provider work on both ends.**

- **Outbound** today goes through `OrchardCore.Sms.ISmsService`, which sends via the **single tenant-default
  provider** (`SmsSettings.DefaultProviderName`). It does **not** pick a provider based on the `From` number.
  So a portal whose numbers span multiple providers cannot rely on the built-in resolver — every message would
  go out the one default provider regardless of which number it is "from." **We must add our own
  `ISmsDispatcher` that maps DID → `ProviderName` → the specific `ISmsProvider` and calls it directly.** This is
  a required, non-trivial piece.
- **Inbound is send-only in the abstraction.** Orchard's SMS stack has no inbound concept; each provider needs
  its own receiver:
  - **Twilio** — inbound webhook + Event Grid receiver **exist** in `Omnichannel.Sms`, but are wired only to
    the automated-activity path; they need to feed the new conversation pipeline.
  - **Azure Communication Services** — the `OrchardCore.Sms.Azure` provider exists for **outbound**; inbound +
    delivery reports arrive via **Event Grid** and need a new receiver. ACS also has **regional and
    number-type constraints** (toll-free/short-code/alphanumeric differ by country) and **limited/uneven MMS
    support** — treat ACS MMS as not guaranteed.
  - **Telnyx** — nothing exists yet; provider + inbound/delivery webhook are new (this plan).
- **Delivery receipts** differ per provider and must be normalized into `OmnichannelMessage.DeliveryStatus`.
- **Net:** two-way works for all three, but the honest scope is "one shared portal + one dispatcher +
  three provider adapters (one exists partially, two are new)," not "flip a provider setting." Phase 1 targets
  **Telnyx + Twilio**; **Azure ACS is a fast follow** once the Event Grid inbound receiver is added.

### One agent, multiple numbers — **supported by design**

Several `SmsNumberRoute`s can target the same agent, and the composer lets the agent choose the sending number.
No architectural limit. The only constraints are operational: each DID must exist as an
`OmnichannelChannelEndpoint` and be provisioned/routed at the provider.

### Department phone numbers — **supported by reusing `ActivityQueue`**

A department is an existing `ActivityQueue`: multiple `SmsNumberRoute`s point their DIDs at the same queue, and
the queue's members (via agent sign-in) share them. `DistributionMode` picks the behavior: `SharedPool`
(shared inbox, claim-to-own — the phase-1 model) or `Routed` (reservation/assignment via the existing routing
strategies with business-hours gating — phase 2). No new department entity, and the agent sign-in model is
shared with voice.

### Contact Center features this depends on — **none of them are Voice**

The dependency is on the channel-neutral features only; Voice and Telephony can stay disabled.

| CC building block | Feature that owns it | Used for | Phase |
| --- | --- | --- | --- |
| `AgentProfile` (presence, capacity, skills, membership) | `Agents` | who agents are, availability, capacity | 1 |
| `AgentPresenceStatus` | `Agents` | "available to text" | 1 |
| `ActivityQueue` (the "department") | `Queues` | shared-number team + membership | 1 (pool) / 2 (routed) |
| `IActivityRoutingService` + `QueueRoutingStrategy` | `Queues` | assign a conversation to an agent | 2 |
| `ActivityReservation` | `Queues` | exclusive assignment in `Routed` mode | 2 |
| `BusinessHoursCalendar` | `Queues` | open/closed gating, auto-reply | 2 |
| `Interaction` / `InteractionChannel.Sms` | `Area` | channel-generic communication record | 1 |
| `IContactCenterRealTimeNotifier` / `ContactCenterHub` pattern | `RealTime` | *copied* into `SmsPortalHub` | 1 |
| `ContactCenterPermissions` pattern | `Area` | *copied* into `TelephonySmsPermissions` | 1 |
| `ContactCenterEntryPoint` | `InboundVoice` (Voice) | **not reused** — SMS uses `SmsNumberRoute` | — |

**Resolved:** the earlier open question — "are these reachable without Voice?" — is answered yes. `Agents`,
`Queues`, and `RealTime` are all independent of, and depended upon by, `Voice`
(`Voice → Queues + RealTime + Telephony`), and `QueuesStartup` registers routing/assignment with no
voice-provider dependency. The SMS portal declares its module dependency on `ContactCenter.Queues` (which pulls
Agents + Area) and `ContactCenter.RealTime`. The one remaining nuance is `AgentProfile.MaxConcurrentInteractions`,
documented as *voice* capacity; we read it as generic concurrent-interaction capacity for now and can add
per-channel capacity later.

### Other limits / non-goals for phase 1

- **MMS/group threads** are provider-dependent (Twilio/Telnyx yes; ACS limited) — group SMS is phase 2/3.
- **MMS media storage** (decided): ingest inbound media into the encrypted store
  (`LocalEncryptedRecordingMediaStore`) via a background task, mirroring Telnyx recording ingest, and store an
  internal `MediaReferences` value on the message plus provider metadata — provider-hosted URLs expire or
  require account auth, so they are unfit for durable history. Outbound media is uploaded to our store and
  served to the provider by a fetchable URL. Lands with the MMS-enabled phase; text-only 1:1 is the MVP.
- **Number provisioning/porting** is out of scope; numbers are assumed to already exist at the provider and be
  registered as channel endpoints.
- **10DLC / A2P registration and carrier throughput limits** are operational concerns the portal surfaces
  (window warnings) but does not manage.

## Phased roadmap

**Phase 1 — Human 1:1 two-way (MVP)**
: `Telephony.Sms` module skeleton · `SmsConversation` catalog + extended `OmnichannelMessage` + `ConversationId`
  index · `SmsNumberRoute` (Agent target) · `OmnichannelChannelEndpoint.ProviderName` + default-provider setting ·
  `ISmsDispatcher` (per-number provider) · Telnyx SMS provider + inbound/delivery webhook · Twilio
  inbound rewired to the conversation pipeline · inbound router (existing-conversation + number-route +
  fallback) · send service · three-pane portal via display drivers · `SmsPortalHub` inbound/delivery
  notifications · contact resolution + `DoNotSms`. **Outcome: an agent picks a number, sees threaded history,
  sends/receives, and no inbound is dropped.**

**Phase 2 — Departments & routing**
: Queue-backed numbers (`SmsNumberRoute` → `ActivityQueue`) + shared pool + claim/assign · `Routed` mode via
  the existing routing strategies and business hours (`InteractionChannel.Sms`) · supervisor view of all
  conversations · presence gating · Azure ACS inbound (Event Grid) receiver.

**Phase 3 — Group & scale**
: Broadcast (1:1 fan-out) + group MMS threads · templates / canned responses · labels / spam · assignment
  transfer · out-of-app push · analytics (reuse Contact Center reports infra) · encrypted MMS media ingest.

## Resolved decisions

The five previously-open questions are now settled:

1. **Message store** — extend `OmnichannelMessage` (not a separate entity); the `SmsConversation` is an
   `ICatalog<>` document holding the rollup, and messages are individual records linked by `ConversationId`
   (indexed), not embedded.
2. **AI ↔ human handoff** — yes, one inbox; the human thread hydrates the full prior AI + customer transcript
   (see AI ↔ human handoff).
3. **Agent identity** — reuse the Contact Center `AgentProfile` (from the channel-neutral `Agents` feature); no
   separate "SMS user." An SMS-only operator is an `AgentProfile` with the voice-only fields left empty.
4. **MMS media** — ingest into the encrypted store (see Other limits); provider URLs are reference metadata
   only.
5. **Multi-provider** — provider is a property of the number (`OmnichannelChannelEndpoint.ProviderName`) plus a
   tenant-default provider setting, resolved by `ISmsDispatcher` (see Provider selection).
