---
sidebar_label: Readiness — soft phone and Telnyx
sidebar_position: 12
title: Production readiness — soft phone and Telnyx integration
description: Findings and the test-first remediation plan for the Telephony soft phone (hub, browser client, extension), the Telnyx provider (webhooks, credentials, outbound bridge, media streams, AI voice agent) and their integration with Contact Center.
---

# Production readiness — soft phone and Telnyx

Part of the [Production Readiness Plan](../contact-center/production-readiness-plan.md). Workstream **D**.

## What exists today (verified)

- **Telephony**: `TelephonyHub` (1,376 lines) exposes dial, hangup, hold, mute, transfer, merge, DTMF, answer,
  reject, voicemail, extension dialing, credentials and status; every method opens a child shell scope and
  re-authorizes. `soft-phone.js` (6,251 lines, 253 functions) hosts the UI, the state machine, the SIP.js and
  Telnyx WebRTC adapters, quality sampling (MOS), reconnect, and the extension window mode. Recording media is
  stored with chunked AEAD encryption.
- **Telnyx**: signed (Ed25519), freshness-checked, size-limited, rate-limited webhooks feeding the durable inbox;
  browser credentials issued per user with a 60-minute TTL, an 8-credential cap and supersede-revoke; the outbound
  bridge orchestrator (704 lines) bridges the agent leg and the destination leg through `client_state`; a
  media-stream WebSocket endpoint claimed by a one-time token from an in-memory registry; recording ingest to the
  encrypted store; an AI voice agent (`TelnyxAiVoiceConversationHandler`, 978 lines) driving TTS/transcription.
- **Contact Center integration**: `TelnyxContactCenterVoiceProvider` (dial, connect-to-agent by originating to the
  agent's registered SIP endpoint, transfer, recording), `ContactCenterTelnyxInboundCallRouter` feeding the inbound
  sink, `TelnyxSoftPhoneRegistrationConfigContributor`.

## D1. Emergency and premium destination policy must apply to the soft phone (Critical)

**Evidence.** `voice-routing.md` documents that the soft-phone keypad and transfer field "pass the dialed digits
straight to the provider unfiltered" and that `ExternalDestinationPolicy` is consulted only on Contact Center
server-side paths. `TelephonyHub.Dial` → `DefaultTelephonyService.DialAsync` → provider with no destination policy.
`DefaultOutboundCallScreeningService` exists but screens for compliance, not destination classes.

**Target design.** Move `ExternalDestinationPolicy` (or an `IDialDestinationPolicy` abstraction) into Telephony
Abstractions with a default implementation in Telephony that rejects emergency and premium ranges for all
jurisdictions in a maintained table (not a suffix match on three codes), applied in `DefaultTelephonyService`
for Dial, Transfer and DialExtension before any provider call. Contact Center's policy composes on top. Provide an
explicit per-tenant allow-list for legitimate short codes. Keep the operator warning that the platform is not an
emergency service, but remove the "bypass" limitation.

**Tests first.** `TelephonyCallControlBoundaryTests` extended: dialing `911`, `112`, `000`, a premium prefix, and a
transfer to those numbers are refused at the service with a typed result; `OutboundCallScreeningTests` for the
allow-list; a Playwright test that the keypad shows the refusal.

## D2. Agent-initiated transfers go through the transfer authority (Critical)

See A9 in the routing page: the soft phone transfer field must resolve through `IContactCenterTransferService` when
Contact Center Voice is enabled, and through the Telephony destination policy otherwise. Deliverable here is the
soft phone UI (agents, queues, approved destinations) and the hub method that takes an opaque target id rather than
a raw number when the catalog is enforced.

## D3. Telnyx provider test coverage (High)

**Evidence.** `tests/.../Telnyx` and `Modules/Telnyx` contain 12 tests (AI voice contact email and subject fields);
`TelnyxSmsWebhookParserTests` sits under `Telephony/Sms`. Nothing covers `TelnyxWebhookSignatureValidator`,
`TelnyxCallEventParser`, `TelnyxWebhookService` state mapping, `TelnyxOutboundBridgeOrchestrator`,
`TelnyxTelephonyCredentialIssuer` (cap, supersede, revoke), `TelnyxContactCenterVoiceProvider.ConnectToAgentAsync`,
`TelnyxRecordingIngestService`, or `TelnyxVoicemailRecordingStarter`. The Asterisk provider has more than 40 test
files for comparable scope.

**Target design.** A `Telnyx` test folder with a recorded-HTTP `TelnyxApiHandler` double (request assertions and
canned responses) and fixture webhooks (real payload shapes for `call.initiated`, `call.answered`, `call.hangup`
with each `hangup_cause`, `call.recording.saved`, `call.gather.ended`, `streaming.*`). Tests:

- Signature validator: valid, tampered body, wrong key length, stale timestamp handled by the endpoint.
- Event parser and `TryMapState`: every documented event type and state token; unknown → Ignored.
- Bridge orchestrator: agent leg answered → bridge; destination no-answer → voicemail; agent connect failure causes;
  duplicate delivery is idempotent.
- Credential issuer: cap eviction order, supersede-revoke, revoke-for-user on sign-out, Telnyx 404 tolerated.
- Contact Center provider: connect answers the caller leg then originates with the expected `client_state`;
  missing live credential → `agent_endpoint_missing`.
- Recording ingest: saved event → job → download → encrypted store → delete-after-store; retry on transient failure.
- Webhook endpoint: 401 without key, 413 over size, 429 on limiter, 503 when inbox busy, fast path for greeting
  ended.

**Acceptance.** Telnyx.Core line coverage comparable to Asterisk (target above 70 percent on `Services`).

## D4. Credential selection and endpoint resolution consistency (Medium)

**Evidence.** `TelnyxAgentCredentialSelection.OrderByDeliveryPreference` exists, but
`TelnyxContactCenterVoiceProvider.ResolveAgentEndpointAsync` takes `live[0]` from `ListLiveByUserAsync` and
`TelnyxTelephonyProvider.Extensions.ResolveUserSipEndpointAsync` has its own resolution. Both must select the
**registered** credential (memory of a past incident: dialing the newest-issued credential produced SIP 486).

**Target design.** One `ITelnyxAgentEndpointResolver.ResolveAsync(userId)` used by the Contact Center provider,
extension dialing, and the outbound browser bridge; it applies `OrderByDeliveryPreference`, requires
`RegisteredUtc`, and logs the chosen credential id.

**Tests first.** Resolver tests: registered beats newer unregistered; expired excluded; none → null with a
structured log.

## D5. Telnyx HTTP client consolidation (Medium)

**Evidence.** `CreateClient`, `SafeReadContentAsync`, `ReadDataStringAsync` are duplicated across
`TelnyxTelephonyProvider`, `TelnyxContactCenterVoiceProvider`, `TelnyxOutboundBridgeOrchestrator`,
`TelnyxTelephonyCredentialIssuer` (and partially in the provisioning and voice-agent clients). Each sets
`BaseAddress` and the bearer header per call.

**Target design.** A typed `TelnyxApiClient` registered with `AddHttpClient<TelnyxApiClient>` configured from
`IOptionsMonitor<TelnyxOptions>` (base address, bearer, `Retry-After`-aware resilience handler with a bounded retry
for idempotent calls only), exposing `AnswerAsync`, `OriginateAsync`, `BridgeAsync`, `TransferAsync`, `SpeakAsync`,
`PlaybackAsync`, `GatherAsync`, `RecordStartAsync`, credentials CRUD, with typed results. Providers depend on it.
This also gives one place for the IVR/treatment commands added in A6/A7.

**Tests first.** `TelnyxApiClientTests` against the recorded handler; provider tests updated to assert on the client
calls rather than raw HTTP.

## D6. Soft phone JavaScript architecture (High for maintainability)

**Evidence.** `soft-phone.js` is one 6,251-line IIFE mixing DOM rendering, state, adapters, telemetry, and
extension-window logic. `contact-center-soft-phone.js` (664), `contact-center-agent-bar.js` (731) and
`agent-workspace.js` (751) re-implement timers and offer rendering. There are no JavaScript unit tests; only
Playwright end-to-end tests exercise the UI, and they run only in release CI.

**Target design.** Split into ES modules under `Assets/js/soft-phone/`: `state.js` (pure reducer for the seven-state
call model and offer overlay), `adapters/telnyx.js`, `adapters/sipjs.js`, `quality.js` (stats, MOS), `format.js`
(number formatting), `ui/*.js`, `extension.js`; bundle with the existing module asset pipeline to the same
`soft-phone.js` output so resource registration does not change. Add Vitest unit tests for the pure modules and keep
Playwright for integration. Share a `call-timer.js` and `offer-panel.js` with the Contact Center scripts.

**Tests first.** Characterization tests (Vitest) for `mapTelnyxOutboundState`, `estimateMos`, `formatNanpNumber`,
`parseWebRtcStats`, the state reducer, and the reconnect policy before moving code.

**Acceptance.** Same Playwright suite green; Vitest suite in `pr_ci.yml`; no file above 800 lines.

## D7. Multi-node media stream registry and provider reconciliation gaps (Medium)

**Evidence.** The Telnyx media-stream token registry is per node (documented); reconciliation "does not yet
bootstrap a completely unknown live provider call" after a restart (documented).

**Target design.** Redis-backed `IWebSocketConnectionRegistry` when `OrchardCore.Redis` is enabled (claim token,
node id, TTL) so a callback that lands on another node can proxy or reject deterministically; a startup
reconciliation step that lists active Telnyx calls for the connection id and creates interactions for unknown live
calls (or hangs them up with a spoken apology, per option).

**Tests first.** Distributed test in `ContactCenter.DistributedTests` for the Redis registry; reconciliation tests
using recorded `GET calls` responses.

## D8. AI voice agent placement and provider neutrality (Medium)

**Evidence.** `VoiceOmnichannelProcessor`, `TelnyxAiVoiceConversationHandler`, `PendingVoiceHandoff` and the
`TelnyxConstants.Feature.AiVoice` feature live in the Telnyx module, while the equivalent SMS automation lives in a
provider-neutral `Omnichannel.Sms` module. The conversation loop (prompt rendering, completion, handoff decision,
conclusion) is provider-agnostic; only speak/transcribe/hangup are Telnyx.

**Target design.** Create `CrestApps.OrchardCore.Omnichannel.Voice` (automated voice conversations) that owns the
loop and depends on a small `IVoiceAgentMediaProvider` (speak, start transcription, hangup, gather) in Telephony
Abstractions; Telnyx implements it. This mirrors the SMS split and lets a second provider add AI voice without
copying the handler. Deduplicate the subject/contact helpers with the SMS handler (see C11).

**Tests first.** Move the existing 12 AI voice tests to the new module; add loop tests with a fake media provider
(answered → greeting spoken; transcription → completion → speak; handoff decision → `IOmnichannelHandoffService`
called once; hangup → conclusion with summary).

## D9. Hub and endpoint hardening review (Low)

**Evidence.** Reviewed `TelephonyHub`, `SoftPhoneController`, `SoftPhoneExtensionEndpoints`,
`SoftPhoneDialerEndpoints`, `TelnyxConnectController`, the Telnyx webhook endpoints and the media-stream endpoint.
Authorization is consistently checked; antiforgery is validated on the dial endpoint; the extension config endpoint
returns only URLs and identity. No defects found. Items to keep in the release checklist:

- `TelephonyHub` per-call child scopes are correct but heavy; consider a scoped hub filter that opens one scope per
  invocation (Orchard `HubFilter`) to reduce the boilerplate and centralize authorization logging.
- Add a Playwright test for the extension `answerCallId` path and one for credential renewal near expiry.
