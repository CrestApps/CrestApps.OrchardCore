# SMS Communication Portal — Implementation Progress

Authoritative spec: `src/CrestApps.Docs/docs/telephony/sms-portal-project-plan.md`. Re-read the plan and this
file at the start of every work cycle. Every decision in the plan is FINAL — implement, do not redesign.

Legend: `[ ]` not started · `[~]` in progress · `[x]` done & green (builds + tests pass) · `[!]` needs live validation

## New projects (module layout)

- [x] `src/Abstractions/CrestApps.OrchardCore.Telephony.Sms.Abstractions` — contracts, enums, notifications
- [x] `src/Core/CrestApps.OrchardCore.Telephony.Sms.Core` — models, stores, indexes, routing, services
- [x] `src/Modules/CrestApps.OrchardCore.Telephony.Sms` — drivers, controllers, hub, views, admin
- [x] Add all three to `CrestApps.OrchardCore.slnx`
- [ ] New test project `tests/CrestApps.OrchardCore.Telephony.Sms.Tests` (or reuse existing per convention)

---

## Phase 1 — Human 1:1 two-way (MVP)

### Workstream: Domain model & storage
- [x] Enums (Abstractions): OwnerType, AssignmentStatus, ConversationStatus, NumberRoute TargetType,
      DistributionMode, DeliveryStatus
- [x] `SmsConversation` (Core, CatalogItem + ICatalog<> document) + index + index provider + migration
- [x] Extend `OmnichannelMessage` in place: ConversationId, SentByAgentId, DeliveryStatus, ProviderMessageId,
      MediaReferences, ErrorCode
- [x] Extend `OmnichannelMessageIndex` + provider + migration with `ConversationId` (indexed)
- [x] `SmsNumberRoute` (Core, Agent target for phase 1) + index + provider + migration
- [x] Stores + managers for SmsConversation and SmsNumberRoute (copy EntryPoint store/manager pattern)

### Workstream: Provider dispatch (multi-provider)
- [x] Extend `OmnichannelChannelEndpoint` with `ProviderName`
- [x] Tenant-default SMS provider setting (portal setting) + display driver
- [x] `ISmsDispatcher` (resolve From number ProviderName → tenant default → ISmsProvider) + impl

### Workstream: Telnyx SMS provider (in existing Telnyx module, new feature)
- [x] `TelnyxSmsProvider : OrchardCore.Sms.ISmsProvider` named "Telnyx" (SendAsync → /v2/messages)
- [x] `TelnyxSmsWebhookEndpoint` — inbound (message.received) + delivery (message.sent/finalized)
- [x] Telnyx Ed25519 signature verification helper (differs from voice webhook validator)
- [x] `[Feature] "Telnyx SMS"` manifest + Startup (TryAdd pattern)

### Workstream: Inbound routing pipeline
- [x] `ISmsInboundRouter` chain contract + ordered registration (TryAdd precedence)
- [x] `ExistingConversationRouter` (append to open human conversation, keep assignment)
- [x] `NumberRouteRouter` (SmsNumberRoute → Agent personal inbox; Queue phase 2)
- [x] `FallbackRouter` (unassigned inbox / spam bucket — no silent drop)
- [x] `ISmsInboundProcessor` orchestration: normalize → OmnichannelMessage → resolve contact →
      find-or-create SmsConversation → router chain → persist + bump unread → raise event → notify
- [x] Twilio inbound rewired to the conversation pipeline (feed new pipeline, keep AI path)

### Workstream: Send path
- [x] `ISmsConversationService.SendAsync(...)` — authorize, DoNotSms + window checks, dispatch, persist Queued
- [x] Delivery-webhook update of DeliveryStatus + notify

### Workstream: Portal UI (Orchard Core display management)
- [x] `SmsPortalController` + admin menu (mirror AgentWorkspaceController) — conversation list scaffolding
- [x] Conversation list (SummaryAdmin display type, filters) via DisplayDriver<SmsConversation>
- [x] Thread view — message bubbles via DisplayDriver<OmnichannelMessage> (SMS display type) + composer
- [ ] Contact profile pane (OmnichannelContactPart display + placement zone)
- [x] `SmsNumberRoute` admin catalog controllers + drivers (copy EntryPointsController + driver)

### Workstream: Real-time notifications
- [x] `ISmsRealTimeNotifier` + `SmsPortalHub` (mirror ContactCenterHub / IContactCenterHubClient)
- [x] NewInboundMessage, MessageDelivered/Failed wiring (Assigned/Claimed groundwork)

### Workstream: Permissions / compliance / security
- [x] `TelephonySmsPermissions` (copy ContactCenterPermissions): ManageSmsNumberRoutes, UseSmsPortal,
      SendGroupSms, ViewAllConversations
- [x] Enforce DoNotSms on every send; honor STOP/opt-out (OmnichannelSmsComplianceHelper) → DoNotSms + auto-close
- [x] Redact addresses/PII in logs (IRedactorProvider, LogDataClassifications.AddressSet)
- [x] Per-provider webhook auth (Telnyx Ed25519)

---

## Phase 2 — Departments & routing
- [x] Queue-backed numbers (SmsNumberRoute → ActivityQueue) + shared pool + claim/assign
- [ ] `Routed` mode via existing routing strategies + business hours (InteractionChannel.Sms)
- [x] Supervisor view of all conversations
- [ ] Presence gating ("available to text" via AgentProfile.PresenceStatus)
- [ ] Azure ACS inbound (Event Grid) receiver

## Phase 3 — Group & scale
- [ ] `SmsBroadcast` broadcast (1:1 fan-out via IBackgroundTask) + group MMS threads
- [ ] Templates / canned responses
- [ ] Labels / spam
- [ ] Assignment transfer
- [ ] Out-of-app push (INotifier bridge)  — deferred, optional
- [ ] Analytics (reuse Contact Center reports infra)  — basic conversation metrics
- [ ] Encrypted MMS media ingest (LocalEncryptedRecordingMediaStore, background task)

---

## Live validation needed (carrier accounts + public HTTPS webhooks)
_Appended as provider paths are completed. These are covered by mocked-provider unit tests; the human must
verify the real round trip._

### Telnyx SMS two-way (Phase 1)
The full Telnyx code path (send via `POST /v2/messages`, inbound + delivery webhook with Ed25519
verification) is implemented and covered by mocked-provider unit tests (`TelnyxSmsWebhookParserTests`,
`SmsDispatcherTests`, `SmsConversationServiceTests`, `SmsInboundProcessorTests`). A human must verify the real
round trip:
1. Enable features: **Telnyx**, **Telnyx SMS**, **SMS Communication Portal** (pulls in Omnichannel Managements
   + Contact Center Agents/Work Distribution/Real-Time + OrchardCore.Sms/SignalR).
2. In Telnyx settings, set the account **API key** and **webhook public key** (both are reused from the Telnyx
   provider settings). Optionally set `TelnyxSmsSettings.MessagingProfileId` (no editor yet — via recipe) if
   the number is not directly bound to a messaging profile.
3. Register a Telnyx **messaging profile webhook** pointing at `https://<public-host>/api/telnyx/webhook/sms`.
4. Add the Telnyx DID as an `OmnichannelChannelEndpoint` (Channel=SMS, ProviderName=`Telnyx`) and create an
   `SmsNumberRoute` binding that DID to an agent.
5. Text the DID from a mobile → expect a new conversation in the agent inbox; reply from the portal → expect
   delivery on the handset and the delivery tick to advance to Delivered.
   Expected result: inbound creates/append a conversation (no silent drop), outbound persists with
   DeliveryStatus and reconciles on the `message.finalized` receipt.
</content>
