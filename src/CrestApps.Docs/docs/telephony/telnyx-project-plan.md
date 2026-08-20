---
sidebar_label: Telnyx Project Plan
sidebar_position: 3
title: Telnyx Integration — Project Plan & Status
description: What the Telnyx integration implements today, what remains, and what each remaining item needs to be completed.
---

# Telnyx Integration — Project Plan & Status

This page tracks the state of the Telnyx voice integration: what is implemented, what remains, and what
each remaining item needs to be completed. It complements the user-facing [Telnyx](./telnyx.md)
documentation.

## Why Telnyx

Dialpad's public API only offers an "initiate‑via‑ring" model (it rings the agent's own Dialpad app,
carries no audio in the browser, and exposes no server‑side bridging), which makes a browser‑only soft
phone impossible and breaks true power dialing. Telnyx exposes a SIP‑over‑WebSocket registrar
(`wss://sip.telnyx.com:7443`) with per‑user telephony credentials **and** a REST Call Control API, so the soft
phone can be the only endpoint (browser audio) and the dialer can bridge a live answer to a free agent
(`ServerSideAcd`).

## Architecture

| Assembly | Kind | Contents |
| --- | --- | --- |
| `CrestApps.OrchardCore.Telnyx.Core` | Library | Shareable contracts and provider‑neutral building blocks: `TelnyxConstants`, settings/options/models, the normalized call‑event model + parser, the Ed25519 webhook signature validator, JSON options, the durable browser‑credential index, and the service interfaces. Other modules depend on this, never on the module. |
| `CrestApps.OrchardCore.Telnyx` | Module | OrchardCore wiring and concrete implementations: the telephony provider, Contact Center voice provider, credential issuer/store/registration/revoker, webhook endpoint + service + inbox handler, settings driver + views, migrations, and DI startups. |

Two features:

- **Telnyx** (`CrestApps.OrchardCore.Telnyx`) — provider, browser WebRTC soft phone, signed webhooks. Depends on Telephony.
- **Telnyx Contact Center Voice** (`CrestApps.OrchardCore.Telnyx.ContactCenterVoice`) — `ServerSideAcd` dial, agent bridge, transfer. Depends on Telnyx + Contact Center Voice.

## Implemented

### Base telephony feature

- ✅ `TelnyxTelephonyProvider` over Telnyx Call Control (`POST /v2/calls`, `.../actions/*`): dial, hangup, answer, reject, blind transfer, attended transfer, merge (conference), send DTMF, call‑state lookup. Hold/mute are handled by the browser media adapter (SIP re‑INVITE / local track toggle) and reported optimistically.
- ✅ Browser **WebRTC** soft phone: `TelnyxTelephonyCredentialIssuer` mints short‑lived Telnyx telephony credentials (`POST /v2/telephony_credentials`), `TelnyxSoftPhoneRegistrationConfigContributor` returns the SIP‑over‑WSS registration config for the shared `sipjs` adapter, `TelnyxSoftPhoneCredentialRevoker` deletes them on sign‑out, and `TelnyxAgentCredentialStore` (index + migration) persists user → SIP username with a per‑user cap.
- ✅ **Ed25519‑signed webhooks** at `/api/telnyx/webhook/call`: `TelnyxWebhookSignatureValidator` (BouncyCastle), `TelnyxCallEventParser`, `TelnyxWebhookEndpoint` (anonymous, antiforgery‑disabled, 1 MiB cap, freshness + rate/concurrency limits, durable provider inbox), `TelnyxWebhookService` normalizing to `ProviderVoiceEvent`, and `TelnyxWebhookInboxHandler`.
- ✅ Settings screen (API key, Call Control connection id, SIP connection id, default caller id, webhook public key, WebRTC advanced) with encrypted secrets and default‑provider handling.
- ✅ Single tenant **API‑key** auth (no per‑user OAuth), idempotent outbound dials via Telnyx `command_id`, and non‑replay resilience for unsafe HTTP methods.

### Contact Center Voice feature

- ✅ `TelnyxContactCenterVoiceProvider` with `VoiceProviderDeliveryModel.ServerSideAcd`.
- ✅ `DialerDial` — routes outbound lead calls through the telephony provider.
- ✅ `AgentConnect` — `ConnectToAgentAsync` originates the agent's browser SIP leg (`sip:{user}@sip.telnyx.com`, auto‑answered by the soft phone) and bridges it to the caller (`POST /v2/calls/{caller}/actions/bridge`).
- ✅ `CallTransfer` — live‑call transfer via `.../actions/transfer`.
- ✅ Inbound router into the Contact Center front door, provider identity, and feature lifecycle participant.

### DID → agent routing (Contact Center)

- ✅ Entry points gained a **Route to: Queue | Specific agent** target (`EntryPointTargetType`, `ContactCenterEntryPoint.TargetType`/`TargetAgentId`, planner, admin UI).
- ✅ `IActivityAssignmentService.AssignSpecificAsync` reserves the named agent directly under the per‑queue lock; `IVoiceQueueOfferService.OfferToAgentAsync` offers to that agent; `InboundVoiceCallProcessor` prefers the agent and falls back to normal queue routing when unavailable.
- ✅ `AgentProfile.OutboundCallerId` field (for presenting the agent's assigned number on outbound calls).

### Documentation

- ✅ User‑facing [Telnyx](./telnyx.md) page (the module has no `README.md`; documentation lives on the docs site) and `3.0.0` changelog entries (Telnyx provider + entry‑point agent routing).

## Remaining

### 1. Supervisor & advanced voice features (monitor / whisper / barge, conference, recording)

**Status:** not implemented. The provider advertises only the capabilities it implements
(`DialerDial | AgentConnect | CallTransfer`), so nothing fails closed — but the chosen scope
("Core + supervisor/advanced") is not yet fully met.

**Needed to complete:**

- Implement `IContactCenterVoiceMonitoringProvider` (monitor/whisper/barge) on the Telnyx CC provider using the Telnyx **Conference API**: move the live call into a conference, then join the supervisor leg muted (monitor), with `whisper_call_control_ids` (whisper), or unmuted (barge).
- Implement `IContactCenterVoiceConferenceProvider` (add participants) and `IContactCenterVoiceRecordingProvider` (`record_start` / `record_stop` / `record_pause` / `record_resume`, and recording‑saved webhook ingestion into the recording media store).
- Add the matching flags to `TelnyxContactCenterVoiceProvider.Capabilities` (`Monitor | Whisper | Barge | Conference | Recording`).
- Extend `TelnyxWebhookService` to normalize conference and recording events (`conference.*`, `call.recording.saved`) into `ProviderVoiceEvent` recording/conference fields.

### 2. Per‑agent outbound caller id — editor UI + dial‑path wiring

**Status:** the `AgentProfile.OutboundCallerId` model field exists, but there is no editor UI and the dialer/manual‑dial path does not yet resolve it into the dial request.

**Needed to complete:**

- Add the field to the agent‑profile editor (view model + display driver + view) under **Contact Center → Agents**.
- In the outbound dial path (the Voice Contact Center Call Router / manual‑dial), resolve the reserved agent's `OutboundCallerId` and set it as `ContactCenterDialRequest.CallerId` (falling back to the tenant default). No Telnyx change is required — the provider already honors `from` per call.

### 3. Automated tests

**Status:** not written.

**Needed to complete:**

- Ed25519 signature acceptance/rejection tests for `TelnyxWebhookSignatureValidator` (valid signature, wrong key, tampered body, bad timestamp).
- `TelnyxCallEventParser` + `TelnyxWebhookService` normalization/state‑mapping tests (recorded Telnyx deliveries under `tests/CrestApps.OrchardCore.Tests/Telephony/`, mirroring the Dialpad cassette approach).
- DID → agent routing tests: `EntryPointRoutingPlanner` agent target, and `InboundVoiceCallProcessor` offering the named agent then falling back to the queue when unavailable.

### 4. WebRTC end‑to‑end verification & TURN

**Status:** code complete; not verified against a live Telnyx account.

**Needed to complete:**

- Manual verification: configure a Telnyx account (API key, Call Control application, Credential Connection), register the webhook, and confirm browser registration to `wss://sip.telnyx.com:7443`, an outbound call with browser audio, and an inbound call bridged to the agent.
- Confirm whether the target networks need TURN; if so, populate the ICE/TURN settings.

### 5. Optional: recipe & setup ergonomics

**Status:** optional / nice‑to‑have. The settings screen now includes an inline step‑by‑step setup guide with portal deep links, and the docs have a [Getting your Telnyx credentials](./telnyx.md#getting-your-telnyx-credentials) walkthrough.

**Needed to complete:**

- A `contact-center-telnyx-ga-core` recipe mirroring the Dialpad/Asterisk GA recipes to enable and pre‑configure the features for a new tenant.

### 6. "Connect Telnyx" guided auto‑provisioning (API‑key based)

**Status:** ✅ implemented. The admin enters **only the API key**, saves, and clicks **Connect Telnyx**; the app provisions and configures the rest. Implemented with `ITelnyxProvisioningApiService` (find‑or‑create Call Control application + Credential connection + outbound voice profile, number discovery + assignment), `TelnyxConnectController` (`Connect` / `Status` / `Disconnect` admin endpoints), and a **stateful settings screen** that shows only Enable + API key + Connect before connecting, and the connected status + read‑only ids + editable caller id / webhook public key / advanced + Disconnect after. A plain Save never overwrites the provisioned ids.

**Verified against a live account:** the Call Control application and Credential connection creation bodies work with an **Owner/Admin** API key. Two fixes came out of live testing: (1) Telnyx enforces **unique connection names across all connection types**, so the Credential connection now uses a distinct name (`… SIP`) from the Call Control application to avoid a `10015` name collision; (2) the failure message only appends the "create the key as an Owner/Admin" permission hint when the error is actually an authorization error (`10006`/not authorized), and the UI/docs now state the key must be created by an Owner/Admin (a restricted member's key can read but not create).

**Remaining verification / polish:**

- The outbound voice profile body (`traffic_type`/`service_plan`) is still best‑effort; if Telnyx rejects it, Connect still succeeds (the two critical connection ids are required) and surfaces a warning.
- Caller id is auto‑suggested from the first discovered number; a dropdown **picker** of all discovered numbers is not yet surfaced in the UI (the list is returned by the connect endpoint).
- The webhook **public key** remains a one‑time paste (no fetch endpoint), as expected.

**Original design (for reference):**

**Design (reuses the Dialpad register‑webhook pattern):**

- `ITelnyxProvisioningApiService` (mirrors `IDialpadWebhookApiService`) uses the API key to **find‑or‑create** (idempotently, by a stable name such as `CrestApps <tenant>`):
  - a **Call Control application** (`POST /v2/call_control_applications`) with `webhook_event_url` set to `https://<tenant-host>/api/telnyx/webhook/call` → resolves `ConnectionId`;
  - a **Credential SIP connection** (`POST /v2/credential_connections`) → resolves `SipConnectionId`;
  - an **outbound voice profile** (`POST /v2/outbound_voice_profiles`) bound to both connections → resolves `OutboundVoiceProfileId`.
- It **lists existing numbers** (`GET /v2/phone_numbers`) and sets `DefaultOutboundCallerId` when one is unambiguous, or surfaces a picker (never auto‑buys).
- It attempts to **fetch the account webhook public key**; if Telnyx exposes no such endpoint, the admin pastes it once (the one remaining manual field) — everything else is filled in automatically.
- A controller/endpoint (mirrors `DialpadWebhookRegistrationController`) runs the provisioning, writes the resolved ids into `TelnyxSettings`, and the settings page polls a **status** action and refreshes — exactly like Dialpad's **Register webhook** UX. A **Disconnect** action can delete the created resources.

**Why this over OAuth:** no CrestApps‑registered OAuth application, no token storage/refresh, no OAuth scopes to reconcile. The runtime provider keeps using the static API key it already uses.

**Caveats:**

- Re‑connect must be idempotent (find‑or‑create by name) so it never duplicates applications/connections.
- Number provisioning stays "select/confirm an existing number," never auto‑buy.
- The webhook **public key** likely remains a one‑time paste unless a fetch endpoint is confirmed.

## Verification checklist

1. `dotnet build` the solution (Core + module + Contact Center changes) — currently green.
2. Run the test suite once tests (item 3) are added.
3. Manual end‑to‑end pass (item 4): outbound browser call, inbound → queue, inbound → agent, dialer bridge, and a rejected bad‑signature webhook.
