---
sidebar_label: Custom Providers
sidebar_position: 4
title: Custom Telephony and Contact Center Providers
description: How to add a custom telephony provider, Contact Center voice provider, and real-time event ingress path for CrestApps Orchard Core.
---

# Custom Telephony and Contact Center Providers

Use this guide when you want to add another PBX or telephony backend to CrestApps.OrchardCore.

## Architecture at a glance

There are three separate seams, and a provider may implement one, two, or all three:

| Seam | Interface | Responsibility |
| --- | --- | --- |
| Soft-phone call control | `ITelephonyProvider` | Dial, hang up, hold, resume, mute, transfer, merge, answer, reject, voicemail, and provider capabilities |
| Live call-state lookup | `ITelephonyCallStateProvider` | Query the provider's current server truth for a specific call so Contact Center can revalidate offers and reconcile restarts |
| Contact Center orchestration | `IContactCenterVoiceProvider` | Dialer dialing, server-side agent bridging, provider-side queue ownership, and other Contact Center voice operations |
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

## 2. Implement the Contact Center voice provider when the backend can do more than keypad calling

If the provider also participates in queue delivery, dialer placement, or server-side bridging, implement `IContactCenterVoiceProvider`.

Use this interface when the provider can:

- place dialer calls for reserved activities
- bridge an already-live provider call to the assigned agent
- assign an existing provider call to an agent
- place or move a call into a provider-owned queue

This layer is optional for browser-only or device-native flows, but it is required when Contact Center needs the provider to own more than basic soft-phone actions.

## 3. Normalize provider events into `ProviderVoiceEvent`

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

## 4. Choose the provider transport model

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

## 5. Keep the soft phone authoritative from server truth

The browser should send **intents** such as dial, hold, resume, mute, hang up, or accept offer.

The browser should **not** be treated as the source of truth for the live call state.

Instead:

1. provider executes the action
2. provider sends webhook or stream event
3. Orchard normalizes that event
4. Contact Center updates the call session and interaction
5. Telephony hub pushes the resulting state back to the soft phone

This keeps hard phones, provider-native devices, and the browser soft phone synchronized from the same server-side truth.

## 6. Registration checklist

For a new provider module, the usual registration checklist is:

1. Register the telephony provider implementation and settings UI
2. Register `IContactCenterVoiceProvider` if the backend supports Contact Center orchestration features
3. Register webhook endpoints or the tenant-safe live-stream listener
4. Implement `ITelephonyCallStateProvider` when the backend can query the current state of a call by id
5. Normalize every provider event into `ProviderVoiceEvent`
6. Ensure the provider's current-state lookup and live-event mapping agree on lifecycle semantics so reconciliation never "undoes" provider truth
7. Add targeted tests for:
   - state mapping
   - idempotency
   - inbound routing
   - live state updates such as hold, resume, mute, unmute, recording, and multi-party changes
   - call-state lookup and restart reconciliation
8. Update the docs and changelog with the supported capabilities and ingress model

## Current built-in examples

| Provider | Transport into Orchard | Notes |
| --- | --- | --- |
| DialPad | Signed webhook + per-call REST lookup | Converts call-event webhooks into `ProviderVoiceEvent`, routes new inbound calls, and supports current-state reconciliation by call id |
| Asterisk | ARI HTTP control + per-call ARI lookup + tenant-scoped ARI event stream | Handles call control, call-state lookup by channel id, and a live ARI stream that maps server-side channel events back into the normalized voice-event pipeline and the persisted soft-phone interaction store |

## Related interfaces

- `ITelephonyProvider`
- `ITelephonyCallStateProvider`
- `IContactCenterVoiceProvider`
- `IProviderVoiceWebhookAdapter`
- `IProviderVoiceEventService`
- `IIncomingCallContextProvider`
- `IIncomingCallDispatcher`

Use those seams together and the next provider can plug in without changing the soft phone itself.
