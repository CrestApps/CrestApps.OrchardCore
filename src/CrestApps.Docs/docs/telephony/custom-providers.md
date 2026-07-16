---
sidebar_label: Custom Providers
sidebar_position: 4
title: Custom Telephony and Contact Center Providers
description: How to add a custom telephony provider, Contact Center voice provider, and real-time event ingress path for CrestApps Orchard Core.
---

# Custom Telephony and Contact Center Providers

Use this guide when you want to add another PBX or telephony backend to CrestApps.OrchardCore.

## Architecture at a glance

There are five separate seams, and a provider may implement any combination supported by its backend:

| Seam | Interface | Responsibility |
| --- | --- | --- |
| Soft-phone call control | `ITelephonyProvider` | Dial, hang up, hold, resume, mute, transfer, merge, answer, reject, voicemail, and provider capabilities |
| Agent audio delivery | `ITelephonyAudioProvider` | Advertise browser and/or external-device audio, expose the configured mode, and name an executable browser media adapter |
| Live call-state lookup | `ITelephonyCallStateProvider` | Query the provider's current server truth for a specific call so Contact Center can revalidate offers and reconcile restarts |
| Contact Center identity | `IContactCenterVoiceProvider` | Stable provider identity, display name, delivery model, and capability metadata |
| Contact Center call control | `IContactCenterVoiceCallControlProvider` | Dialer dialing and server-side agent bridging |
| Contact Center queue ownership | `IContactCenterVoiceQueueAssignmentProvider` | Provider-side agent assignment and queue placement |
| Contact Center transfer and conference | `IContactCenterVoiceTransferProvider`, `IContactCenterVoiceConferenceProvider` | Live-call transfer and conference execution |
| Contact Center recording and monitoring | `IContactCenterVoiceRecordingProvider`, `IContactCenterVoiceMonitoringProvider` | Recording control and supervisor monitor, whisper, barge, or take-over execution |
| Bidirectional live media | `IContactCenterVoiceMediaProvider` | Receive caller audio and inject application-generated audio into an existing provider call |
| Provider event ingestion | `IProviderVoiceEventSink` | Submit normalized provider call-state events without referencing Contact Center persistence models |
| Inbound provider routing | `IInboundVoiceEventSink` | Route a normalized inbound call into Contact Center work |
| Provider reconciliation | `IProviderCallStateReconciler` | Reconcile active Contact Center calls against authoritative provider state |
| Durable webhook ingress | `IProviderWebhookInbox`, `IProviderWebhookInboxHandler`, `IProviderWebhookIngressLimiter` | Commit authenticated deliveries, dispatch provider-owned payload handlers, and enforce ingress limits |
| Provider event ingress | `IProviderVoiceWebhookAdapter` or provider-specific stream listener | Convert provider webhooks or stream events into normalized `ProviderVoiceEvent` instances |

The soft phone stays provider-agnostic because **providers never push UI updates directly to the browser**. Every provider must translate its native events into the internal Contact Center voice-event pipeline first.

```text
Provider webhook / stream / callback
                |
                v
Provider-specific adapter
                |
                v
ProviderVoiceEvent
                |
                v
IProviderVoiceEventService
                |
                v
CallSession + Interaction + Contact Center events
                |
                v
TelephonyHub / soft phone projection
```

## 1. Implement the soft-phone provider

To appear as a telephony provider in **Settings → Communication → Telephony**, implement `ITelephonyProvider`.

At minimum, your provider should:

1. Return a stable technical name and display name
2. Advertise accurate `TelephonyCapabilities`
3. Implement the call-control methods your backend really supports
4. Return provider-neutral `TelephonyCall` results
5. Register the provider through the Telephony provider options configuration pattern used by the built-in modules

Use `TelephonyCall.Metadata` only for contextual data that should travel with the call without polluting the shared contract with provider-specific fields.

### Declare the agent audio path

Implement `ITelephonyAudioProvider` when the provider has a known, executable agent audio path:

- Advertise `Browser` only when a provider browser SDK or signaling adapter can actually send microphone audio and play live remote audio.
- Advertise `ExternalDevice` when audio stays on a hard phone, desktop/mobile provider client, or another provider-owned endpoint.
- Advertise both only when the provider supports both deployments. Add the choice to that provider's settings UI and return it from `ConfiguredAudioMode`.
- Return a stable `BrowserMediaAdapterName` only for browser audio. The shared resolver fails closed when browser capability has no adapter name.

The provider's browser script registers `window.telephonySoftPhone.mediaAdapters[adapterName]`. The factory receives provider credentials, the acquired microphone stream, the remote audio element, a remote-stream setter, and the shared error callback. It may return `handleCallState(call)` and `dispose()` methods. Call state remains provider-authoritative through the server event pipeline; the adapter is the media executor, not the orchestration authority.

## 2. Implement the Contact Center voice provider when the backend can do more than keypad calling

If the provider participates in Contact Center voice orchestration, implement `IContactCenterVoiceProvider` for identity and capability metadata. Add only the executable interfaces backed by real provider operations:

- `IContactCenterVoiceCallControlProvider` for dialer calls and server-side agent connection
- `IContactCenterVoiceQueueAssignmentProvider` for provider-owned assignment and queue placement
- `IContactCenterVoiceTransferProvider` for live-call transfer
- `IContactCenterVoiceConferenceProvider` for conference creation or participant addition
- `IContactCenterVoiceRecordingProvider` for start, stop, pause, and resume recording
- `IContactCenterVoiceMonitoringProvider` for monitor, whisper, barge, and take-over
- `IContactCenterVoiceMediaProvider` for bidirectional live media

Capability flags are discovery metadata, not executable behavior. Advertise an executable call-control, transfer, conference, recording, or monitoring capability only when the corresponding interface is implemented. Contact Center also checks the executable contract before routing or staging provider work, so a flag without an implementation fails closed. Live media is discovered only from `IContactCenterVoiceMediaProvider` registrations and has no capability flag.

## 3. Implement bidirectional media only when the provider exposes live audio

Providers that can attach an external media stream to an active call may implement `IContactCenterVoiceMediaProvider`.

Provider modules should reference `CrestApps.OrchardCore.ContactCenter.Abstractions` only. Do not reference the Contact Center Core or module assemblies to ingest events, route inbound calls, reconcile provider state, or participate in the durable webhook inbox; use the stable provider-facing contracts above.

Installing a provider package does not implicitly install the Contact Center module. Hosts that enable a provider's Contact Center adapter must also install the Contact Center module package; the adapter feature's manifest dependency then enables the required Contact Center Voice feature for that tenant.

`IContactCenterVoiceMediaProviderResolver` returns a media provider only when:

1. a base voice provider with the same technical name is registered
2. the media feature registers an `IContactCenterVoiceMediaProvider`

The executable media registration is authoritative so independently enabled Orchard features remain safe. Enabling the base provider adapter alone cannot expose media, while enabling the declared media feature adds the contract without requiring the base singleton to advertise services owned by another feature.

An opened `IContactCenterVoiceMediaSession` exposes:

- the provider call and media-session identifiers
- negotiated incoming and outgoing audio formats
- an asynchronous stream of ordered incoming audio frames
- an outgoing audio-frame writer
- an explicit stop operation that detaches media without ending the underlying call

The initial shared format vocabulary supports linear PCM, G.711 mu-law, and G.711 A-law, including sample rate, channel count, and preferred frame duration. Provider implementations remain responsible for their native transport, framing, codec negotiation, jitter handling, and bridge attachment.

Do not register `IContactCenterVoiceMediaProvider` for event-only integrations, recording downloads, post-call transcripts, or providers that can receive audio but cannot inject audio into the same live call.

## 4. Normalize provider events into `ProviderVoiceEvent`

This is the most important real-time seam.

Every provider-specific callback or stream event should be translated into `ProviderVoiceEvent` and passed to `IProviderVoiceEventService.IngestAsync()`.

The normalized event supports:

- `State` for lifecycle changes such as dialing, ringing, connected, held, transferred, ended, failed
- `IsMuted` for mute/unmute changes
- `RecordingState` and `RecordingReference` for recording lifecycle
- `IsConference` and `ParticipantCount` for multi-party/conference updates
- `Metadata` for provider-specific troubleshooting context

The Contact Center pipeline then updates the durable `CallSession` and `Interaction`, emits detailed internal events such as `CallHeld`, `CallResumed`, `CallMuted`, `CallUnmuted`, `RecordingStarted`, `RecordingPaused`, `RecordingResumed`, `RecordingStopped`, and `CallConferenceChanged`, and projects the authoritative state back to the soft phone.

When the provider also implements `ITelephonyCallStateProvider`, Contact Center can use that same server truth to:

1. revalidate a ringing offer immediately before accept
2. reconcile persisted active interactions when the tenant activates after a restart
3. run a periodic safety reconciliation in case a live provider event was delayed or missed

## 5. Choose the provider transport model

Providers usually fall into one of these transport models:

### Webhook model

The provider sends HTTP callbacks to Orchard.

Use `IProviderVoiceWebhookAdapter` when:

- the provider signs webhook requests
- the payload can be parsed synchronously per request
- Orchard only needs to accept inbound HTTP events

Typical flow:

1. controller/endpoint receives the webhook
2. adapter validates the signature
3. adapter parses one or more `ProviderVoiceEvent` records
4. `IProviderVoiceEventService` ingests them

### Live stream model

The provider exposes a long-lived WebSocket, SSE, or similar server-side event stream.

Use a provider-specific **tenant-scoped shell component** when:

- Orchard must keep a connection open to the provider
- the provider pushes state changes over a socket instead of posting webhooks
- event delivery needs reconnect, backoff, and tenant-aware configuration

Do **not** push those raw provider events directly to the browser. The stream listener should still normalize everything into `ProviderVoiceEvent` and route it through `IProviderVoiceEventService`. In Orchard Core, that listener should follow the shell lifecycle instead of an app-wide hosted service: start it from a tenant-scoped `ModularTenantEvents` component, reconnect per tenant configuration, and resolve scoped services through `ShellScope.UsingChildScopeAsync(...)` while handling each event so persistence and hub projection run inside a fresh shell scope.

### Hybrid model

Some providers use both:

- webhooks for durable lifecycle events
- WebSocket/SSE for faster live state

That is fine. Both paths should normalize into the same internal `ProviderVoiceEvent` contract.

## Transport and firewall checklist

When documenting or deploying a provider, be explicit about which protocols the environment must allow:

| Scenario | Protocol(s) to allow | Notes |
| --- | --- | --- |
| Browser soft phone ↔ Orchard | `https`, `wss` | Required for the Telephony/Contact Center SignalR experience. Keep HTTPS fallback traffic available too because SignalR may use SSE or long polling when WebSockets are blocked. |
| Provider webhook → Orchard | `https` | Recommended for all production webhook ingress, including DialPad-style signed callbacks. |
| Orchard → provider REST API | `https` | Used for call control, authentication, and call-state lookup when the provider exposes HTTP APIs. |
| Orchard → provider live socket | `wss` | Preferred for production provider event streams. |
| Orchard → provider live socket (dev/lab only) | `ws` | Acceptable only in trusted non-production environments or when TLS terminates before the provider connection. |
| Orchard → Asterisk ARI control API | `http` or `https` | Depends on the Asterisk deployment. Prefer HTTPS whenever ARI is exposed across networks you do not fully trust. |
| Orchard → Asterisk ARI events | `ws` or `wss` | Required for the tenant-scoped ARI listener to receive live channel changes. Prefer WSS in production. |

If a proxy, ingress controller, or firewall is involved, make sure it allows:

1. **WebSocket upgrade headers** for browser SignalR and provider live-stream connections.
2. **Long-lived outbound sockets** from Orchard to provider event streams such as Asterisk ARI.
3. **Inbound HTTPS webhook posts** from providers such as DialPad.
4. **Outbound HTTPS API calls** for provider lookup and control endpoints.
5. **Explicit outbound egress rules** on locked-down hosts. If Orchard runs in an environment where outbound traffic is restricted by default, you must allow the app to open outbound `https`, `ws`, or `wss` connections to the provider endpoints it depends on.

In other words, yes: the docs now distinguish **inbound to Orchard**, **outbound from Orchard**, and **bidirectional browser traffic**, because providers do not all use the same direction:

- **DialPad webhook delivery** is primarily **inbound to Orchard**
- **DialPad REST lookup/control** is **outbound from Orchard**
- **Asterisk ARI control** is **outbound from Orchard**
- **Asterisk ARI real-time events** are also **outbound from Orchard** because Orchard opens the `ws`/`wss` connection to Asterisk
- **Browser soft-phone SignalR** is **bidirectional**

## 6. Keep the soft phone authoritative from server truth

The browser should send **intents** such as dial, hold, resume, mute, hang up, or accept offer.

The browser should **not** be treated as the source of truth for the live call state.

Instead:

1. provider executes the action
2. provider sends webhook or stream event
3. Orchard normalizes that event
4. Contact Center updates the call session and interaction
5. Telephony hub pushes the resulting state back to the soft phone

This keeps hard phones, provider-native devices, and the browser soft phone synchronized from the same server-side truth.

## 7. Registration checklist

For a new provider module, the usual registration checklist is:

1. Register the telephony provider implementation and settings UI
2. Implement `ITelephonyAudioProvider` and a browser media adapter only for executable agent audio modes
3. Register `IContactCenterVoiceProvider` for Contact Center identity and capability metadata, then implement only the executable operation interfaces the backend supports
4. Register `IContactCenterVoiceMediaProvider` only in the feature that supplies bidirectional live audio
5. Register webhook endpoints or the tenant-safe live-stream listener
6. Implement `ITelephonyCallStateProvider` when the backend can query the current state of a call by id
7. Normalize every provider event into `ProviderVoiceEvent`
8. Ensure the provider's current-state lookup and live-event mapping agree on lifecycle semantics so reconciliation never "undoes" provider truth
9. Add targeted tests for:
   - state mapping
   - idempotency
   - inbound routing
   - capability-to-executable-contract parity and media resolver matching
   - media-session cancellation and cleanup when live media is supported
   - live state updates such as hold, resume, mute, unmute, recording, and multi-party changes
   - call-state lookup and restart reconciliation
10. Update the docs and changelog with the supported capabilities and ingress model

## Current built-in examples

| Provider | Transport into Orchard | Notes |
| --- | --- | --- |
| DialPad | Signed webhook + per-call REST lookup | Converts call-event webhooks into `ProviderVoiceEvent`, routes new inbound calls, and supports current-state reconciliation by call id. Telephony audio is currently external-device/provider-client only; it does not advertise embedded browser audio or bidirectional Contact Center media. |
| Asterisk | ARI HTTP control + per-call ARI lookup + tenant-scoped ARI event stream + External Media RTP/UDP | Handles call control, call-state lookup, live normalized events, and server-side bidirectional G.711 mu-law media sessions attached to call bridges. Telephony audio is currently external-device only; the External Media adapter is not browser WebRTC. |

## Related interfaces

- `ITelephonyProvider`
- `ITelephonyCallStateProvider`
- `IContactCenterVoiceProvider`
- `IContactCenterVoiceCallControlProvider`
- `IContactCenterVoiceQueueAssignmentProvider`
- `IContactCenterVoiceTransferProvider`
- `IContactCenterVoiceConferenceProvider`
- `IContactCenterVoiceRecordingProvider`
- `IContactCenterVoiceMonitoringProvider`
- `IContactCenterVoiceMediaProvider`
- `IContactCenterVoiceMediaProviderResolver`
- `IContactCenterVoiceMediaSession`
- `IProviderVoiceWebhookAdapter`
- `IProviderVoiceEventService`
- `IIncomingCallContextProvider`
- `IIncomingCallDispatcher`

Use those seams together and the next provider can plug in without changing the soft phone itself.
