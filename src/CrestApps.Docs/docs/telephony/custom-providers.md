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
| Provider identity | `ITelephonyProvider` | Display name and advertised `TelephonyCapabilities`. It declares no call operations |
| Soft-phone call control | `ITelephonyCallControlProvider` | Dial and hang up |
| Inbound call handling | `ITelephonyInboundCallProvider` | Answer and reject a ringing inbound call |
| Hold and mute | `ITelephonyHoldProvider`, `ITelephonyMuteProvider` | Hold/resume and mute/unmute |
| Transfer | `ITelephonyTransferProvider`, `ITelephonyAttendedTransferProvider` | Blind transfer, and attended transfer where the destination is consulted first |
| Conference and DTMF | `ITelephonyConferenceProvider`, `ITelephonyDtmfProvider` | Merge calls and send digits |
| Voicemail | `ITelephonyVoicemailProvider` | Send a ringing call to voicemail |
| Soft-phone credentials | `ITelephonySoftPhoneCredentialsProvider` | Issue the client credentials the browser soft phone registers with |
| Agent audio delivery | `ITelephonyAudioProvider` | Advertise browser and/or external-device audio, expose the configured mode, and name an executable browser media adapter |
| Live call-state lookup | `ITelephonyCallStateProvider` | Query the provider's current server truth for a specific call so Contact Center can revalidate offers and reconcile restarts |
| Contact Center identity | `IContactCenterVoiceProvider` | Stable provider identity, display name, delivery model, and capability metadata |
| Contact Center call control | `IContactCenterVoiceCallControlProvider` | Dialer dialing and server-side agent bridging |
| Contact Center queue ownership | `IContactCenterVoiceQueueAssignmentProvider` | Provider-side agent assignment and queue placement |
| Contact Center transfer and conference | `IContactCenterVoiceTransferProvider`, `IContactCenterVoiceConferenceProvider` | Live-call transfer and conference execution |
| Contact Center recording and monitoring | `IContactCenterVoiceRecordingProvider`, `IContactCenterVoiceMonitoringProvider` | Recording control and supervisor monitor, whisper, or barge execution |
| Bidirectional live media | `IContactCenterVoiceMediaProvider` | Receive caller audio and inject application-generated audio into an existing provider call |
| Provider event ingestion | `IProviderVoiceEventSink` | Submit normalized provider call-state events without referencing Contact Center persistence models |
| Inbound provider routing | `IInboundVoiceEventSink` | Route a normalized inbound call into Contact Center work |
| Provider reconciliation | `IProviderCallStateReconciler` | Reconcile active Contact Center calls against authoritative provider state |
| Durable webhook ingress | `IProviderWebhookInbox`, `IProviderWebhookInboxHandler`, `IProviderWebhookIngressLimiter` | Commit authenticated deliveries, dispatch provider-owned payload handlers, and enforce ingress limits |
| Provider event ingress | Provider-owned webhook endpoint + `IProviderWebhookInboxHandler`, or a provider-specific stream listener | Authenticate provider deliveries at the provider's own endpoint, commit them to the durable inbox, and convert them into normalized `ProviderVoiceEvent` instances |

The soft phone stays provider-agnostic because **providers never push UI updates directly to the browser**. Every provider must translate its native events into the internal Contact Center voice-event pipeline first.

```text
Provider webhook / stream / callback
                |
                v
Provider endpoint or stream listener (+ durable inbox)
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

`ITelephonyProvider` declares only the provider's display name and its advertised `TelephonyCapabilities`. Every call operation lives on a separate capability contract, so you implement exactly the operations your backend really supports and nothing else. A provider that can only place and end calls is complete after two methods:

```csharp
public sealed class MyTelephonyProvider : ITelephonyProvider, ITelephonyCallControlProvider
{
    public LocalizedString Name => S["My PBX"];

    public TelephonyCapabilities Capabilities => TelephonyCapabilities.Dial | TelephonyCapabilities.Hangup;

    public Task<TelephonyResult> DialAsync(DialRequest request, CancellationToken cancellationToken = default) { /* ... */ }

    public Task<TelephonyResult> HangupAsync(CallReference call, CancellationToken cancellationToken = default) { /* ... */ }
}
```

At minimum, your provider should:

1. Return a stable technical name and display name
2. Advertise accurate `TelephonyCapabilities`
3. Implement the capability contract behind every capability it advertises
4. Return provider-neutral `TelephonyCall` results
5. Register the provider through the Telephony provider options configuration pattern used by the built-in modules

Use `TelephonyCall.Metadata` only for contextual data that should travel with the call without polluting the shared contract with provider-specific fields.

:::note Technical names are unique and case-insensitive
Provider technical names registered with `TelephonyProviderOptions` are compared case-insensitively and trimmed of surrounding whitespace, so `"Asterisk"` and `"asterisk"` resolve to the same provider. Re-registering the identical provider type under an existing name is a harmless no-op, but registering a **different** provider type under a name another module already claimed throws at startup instead of being silently discarded — pick a distinct technical name, or use `ReplaceProvider` when an override is intentional.
:::

### Capabilities and contracts are checked together

`TelephonyCapabilityContracts` records the contract each capability flag requires, and the shared telephony service refuses an operation unless the resolved provider **both** advertises the flag **and** implements the contract. Advertising a capability you did not implement fails closed, and implementing a contract you did not advertise fails closed too, so neither half alone can make an operation reachable.

| Capability | Required contract |
| --- | --- |
| `Dial`, `Hangup` | `ITelephonyCallControlProvider` |
| `ReceiveCalls` | `ITelephonyInboundCallProvider` |
| `Hold`, `Resume` | `ITelephonyHoldProvider` |
| `Mute` | `ITelephonyMuteProvider` |
| `Transfer` | `ITelephonyTransferProvider` |
| `AttendedTransfer` | `ITelephonyAttendedTransferProvider` |
| `Merge` | `ITelephonyConferenceProvider` |
| `SendDigits` | `ITelephonyDtmfProvider` |
| `Voicemail` | `ITelephonyVoicemailProvider` |
| `Directory` | `ITelephonyDirectoryProvider` |

Blind and attended transfer are separate capabilities. A backend that can release a call to a destination but cannot consult that destination first advertises `Transfer` only; a warm transfer request then fails closed instead of reaching a method that would have to refuse it.

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
- `IContactCenterVoiceMonitoringProvider` for monitor, whisper, and barge
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

Every provider-specific callback or stream event should be translated into `ProviderVoiceEvent` and passed to `INormalizedVoiceEventIngestor.IngestAsync()`. Do not call a specific consumer such as `IProviderVoiceEventService` or `IProviderVoiceEventSink` directly: the ingestor is the single entry point that canonicalizes your provider identity, takes the ingestion lease for the call stream exactly once, and then hands the same delivery to every consumer.

The normalized event supports:

- `State` for lifecycle changes such as dialing, ringing, connected, held, transferred, ended, failed
- `IsMuted` for mute/unmute changes
- `RecordingState` and `RecordingReference` for recording lifecycle
- `IsConference` and `ParticipantCount` for multi-party/conference updates
- `Metadata` for provider-specific troubleshooting context

`ProviderVoiceEvent` is immutable. Build one with an object initializer and, if you need a variant of an event you already hold, derive it with a `with` expression rather than assigning to it. The type is both a contract you implement against and something ingestion adjusts — it canonicalizes the provider identity and scopes the idempotency key by it — and while the type was mutable those adjustments were applied to the caller's instance, so ingestion had to defend itself with a hand-written copy. That copy was one more thing to keep complete, and it was not: it dropped the provider's hangup cause, and because a session infers a cause when none is supplied, every call reported the inferred cause instead of the real one, with nothing to say the real one had been lost. A `with` expression copies every member by construction, so that class of loss cannot recur. `Metadata` is snapshotted when you assign it, so keeping your own reference to the dictionary you supplied and writing to it afterwards does not change the event you handed over. The snapshot keeps the comparer your dictionary was built with, and it keeps it through derivation too: assigning `Metadata` from another event carries that event's comparer rather than resetting to ordinal comparison, so a case-insensitive metadata set stays case-insensitive however many times the event is derived. A source that cannot report a comparer — an implementation the platform does not recognize — is keyed ordinally, which is the only honest choice when the source will not say how it compares its own keys.

### One ingest, many projections

A normalized delivery is consumed by every registered `INormalizedVoiceEventHandler`, not by the first one that recognizes it. Two ship in the box:

- `TelephonyCallHistoryVoiceEventHandler` writes the `TelephonyInteraction` call-history record and pushes the soft-phone state change. It runs whether or not Contact Center is installed.
- `ContactCenterVoiceProjection` drives the durable `CallSession` and `Interaction`, emits detailed internal events such as `CallHeld`, `CallResumed`, `CallMuted`, `CallUnmuted`, `RecordingStarted`, `RecordingPaused`, `RecordingResumed`, `RecordingStopped`, and `CallConferenceChanged`, and projects the authoritative state back to the soft phone. It is registered by the Contact Center Voice feature.

Handlers are peers. A handler must never suppress the delivery for the others, because each one is an independent view of the same call and a suppressed delivery silently desynchronizes it. Return `false` from `HandleAsync` when your handler had nothing to project — that is a report, not a veto — and let the ingestor continue.

If you add your own handler, note what the ingress already did for you: the provider name on the event is canonical, the call stream is already serialized by the ingestion lease, and asking for that lease again from inside your handler is satisfied re-entrantly rather than taken twice. Do not open your own distributed lock on the same call, and do not create a second de-duplication record for a delivery another consumer already recorded.

Only internal call-control orchestration may take an event *out* of the stream before ingestion — for example answering and parking a first-seen inbound channel, or releasing a leg the module itself originated. That is a provider-private concern and belongs in your own module, never in a normalized-event handler.

When the provider also implements `ITelephonyCallStateProvider`, Contact Center can use that same server truth to:

1. revalidate a ringing offer immediately before accept
2. reconcile persisted active interactions when the tenant activates after a restart
3. run a periodic safety reconciliation in case a live provider event was delayed or missed

## 5. Choose the provider transport model

Providers usually fall into one of these transport models:

### Webhook model

The provider sends HTTP callbacks to Orchard. Map a **provider-owned endpoint** when:

- the provider signs webhook requests
- the payload can be authenticated per request
- Orchard only needs to accept inbound HTTP events

Typical flow:

1. the provider's own minimal-API endpoint receives the webhook and validates the signature
2. the endpoint commits the raw delivery to the durable `IProviderWebhookInbox` under an idempotency key
3. the provider's `IProviderWebhookInboxHandler` deserializes the payload and normalizes one or more `ProviderVoiceEvent` records
4. `IProviderVoiceEventService` ingests them

The durable inbox (commit-then-dispatch) makes ingress idempotent and recoverable, so a retried delivery is de-duplicated and an event is never lost to an inline dispatch race. The shipping DialPad provider follows exactly this shape (`api/dialpad/webhook/call` → inbox → `DialPadWebhookInboxHandler`).

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

## 7. Keep provider-private metadata inside your own module

Provider operation results carry a metadata dictionary. It is tempting to declare every key you populate in the shared Contact Center contracts so it looks like part of the platform, but a key that only your module writes and that no provider-neutral code reads is not a shared contract — it is your implementation detail wearing the platform's name. Published there, it silently obligates every future provider to populate a concept that may not exist in its backend at all.

The rule the codebase enforces is:

- **Shared contracts** hold keys the platform itself supplies or consumes. `ContactCenterConstants.TransferMetadata.AgentUserId`, `ConferenceMetadata.AgentUserId`, and `AttendedTransferMetadata.AgentUserId` are shared because the platform passes them *into* every provider. `RecordingMetadata.ProviderRecordingId` and `RecordingMetadata.RecordingUrl` are shared because neutral recording and governance code reads them back out.
- **Provider-private keys** live in your own module. The Asterisk adapter keeps its channel, snoop, and bridge identifiers in `AsteriskVoiceResultMetadata`, an `internal` class inside `CrestApps.OrchardCore.Asterisk`. Nothing outside that module can reference them, which is exactly right: nothing outside that module knows what they mean.

Provider-neutral projects must also avoid vendor vocabulary in the names they declare. Use platform words for platform concepts — a call session's joined media is `MediaTopologyId`, not a vendor's word for its own grouping primitive. Prose in comments and docs may still name a provider as a concrete example; it is the declared identifiers and metadata key literals that must stay neutral. `ProviderNeutralContractArchitectureTests` fails the build when either rule is broken.

## 8. Registration checklist

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
- `IProviderWebhookInboxHandler`
- `IProviderVoiceEventService`
- `IIncomingCallContextProvider`
- `IIncomingCallDispatcher`

Use those seams together and the next provider can plug in without changing the soft phone itself.
