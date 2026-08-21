# SMS Communication Portal — Implementation Progress

Authoritative spec: `src/CrestApps.Docs/docs/telephony/sms-portal-project-plan.md`. Re-read the plan and this
file at the start of every work cycle. Every decision in the plan is FINAL — implement, do not redesign.

Legend: `[ ]` not started · `[~]` in progress · `[x]` done & green (builds + tests pass) · `[!]` needs live validation

## New projects (module layout)

- [ ] `src/Abstractions/CrestApps.OrchardCore.Telephony.Sms.Abstractions` — contracts, enums, notifications
- [ ] `src/Core/CrestApps.OrchardCore.Telephony.Sms.Core` — models, stores, indexes, routing, services
- [ ] `src/Modules/CrestApps.OrchardCore.Telephony.Sms` — drivers, controllers, hub, views, admin
- [ ] Add all three to `CrestApps.OrchardCore.slnx`
- [ ] New test project `tests/CrestApps.OrchardCore.Telephony.Sms.Tests` (or reuse existing per convention)

---

## Phase 1 — Human 1:1 two-way (MVP)

### Workstream: Domain model & storage
- [ ] Enums (Abstractions): OwnerType, AssignmentStatus, ConversationStatus, NumberRoute TargetType,
      DistributionMode, DeliveryStatus
- [ ] `SmsConversation` (Core, CatalogItem + ICatalog<> document) + index + index provider + migration
- [ ] Extend `OmnichannelMessage` in place: ConversationId, SentByAgentId, DeliveryStatus, ProviderMessageId,
      MediaReferences, ErrorCode
- [ ] Extend `OmnichannelMessageIndex` + provider + migration with `ConversationId` (indexed)
- [ ] `SmsNumberRoute` (Core, Agent target for phase 1) + index + provider + migration
- [ ] Stores + managers for SmsConversation and SmsNumberRoute (copy EntryPoint store/manager pattern)

### Workstream: Provider dispatch (multi-provider)
- [ ] Extend `OmnichannelChannelEndpoint` with `ProviderName`
- [ ] Tenant-default SMS provider setting (portal setting) + display driver
- [ ] `ISmsDispatcher` (resolve From number ProviderName → tenant default → ISmsProvider) + impl

### Workstream: Telnyx SMS provider (in existing Telnyx module, new feature)
- [ ] `TelnyxSmsProvider : OrchardCore.Sms.ISmsProvider` named "Telnyx" (SendAsync → /v2/messages)
- [ ] `TelnyxSmsWebhookEndpoint` — inbound (message.received) + delivery (message.sent/finalized)
- [ ] Telnyx Ed25519 signature verification helper (differs from voice webhook validator)
- [ ] `[Feature] "Telnyx SMS"` manifest + Startup (TryAdd pattern)

### Workstream: Inbound routing pipeline
- [ ] `ISmsInboundRouter` chain contract + ordered registration (TryAdd precedence)
- [ ] `ExistingConversationRouter` (append to open human conversation, keep assignment)
- [ ] `NumberRouteRouter` (SmsNumberRoute → Agent personal inbox; Queue phase 2)
- [ ] `FallbackRouter` (unassigned inbox / spam bucket — no silent drop)
- [ ] `ISmsInboundProcessor` orchestration: normalize → OmnichannelMessage → resolve contact →
      find-or-create SmsConversation → router chain → persist + bump unread → raise event → notify
- [ ] Twilio inbound rewired to the conversation pipeline (feed new pipeline, keep AI path)

### Workstream: Send path
- [ ] `ISmsConversationService.SendAsync(...)` — authorize, DoNotSms + window checks, dispatch, persist Queued
- [ ] Delivery-webhook update of DeliveryStatus + notify

### Workstream: Portal UI (Orchard Core display management)
- [ ] `SmsPortalController` + admin menu (mirror AgentWorkspaceController) — conversation list scaffolding
- [ ] Conversation list (SummaryAdmin display type, filters) via DisplayDriver<SmsConversation>
- [ ] Thread view — message bubbles via DisplayDriver<OmnichannelMessage> (SMS display type) + composer
- [ ] Contact profile pane (OmnichannelContactPart display + placement zone)
- [ ] `SmsNumberRoute` admin catalog controllers + drivers (copy EntryPointsController + driver)

### Workstream: Real-time notifications
- [ ] `ISmsRealTimeNotifier` + `SmsPortalHub` (mirror ContactCenterHub / IContactCenterHubClient)
- [ ] NewInboundMessage, MessageDelivered/Failed wiring (Assigned/Claimed groundwork)

### Workstream: Permissions / compliance / security
- [ ] `TelephonySmsPermissions` (copy ContactCenterPermissions): ManageSmsNumberRoutes, UseSmsPortal,
      SendGroupSms, ViewAllConversations
- [ ] Enforce DoNotSms on every send; honor STOP/opt-out (OmnichannelSmsComplianceHelper) → DoNotSms + auto-close
- [ ] Redact addresses/PII in logs (IRedactorProvider, LogDataClassifications.AddressSet)
- [ ] Per-provider webhook auth (Telnyx Ed25519)

---

## Phase 2 — Departments & routing
- [ ] Queue-backed numbers (SmsNumberRoute → ActivityQueue) + shared pool + claim/assign
- [ ] `Routed` mode via existing routing strategies + business hours (InteractionChannel.Sms)
- [ ] Supervisor view of all conversations
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

(none yet)
</content>
