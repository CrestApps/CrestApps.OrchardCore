# Capability contracts catalog

Every operation a provider can perform lives on a **separate interface**. The identity interfaces
(`ITelephonyProvider`, `IContactCenterVoiceProvider`) only declare a name and a set of capability *flags*.
For each flag you advertise, you MUST implement the matching executable contract, or the operation fails
closed. Implement only what your backend can truly do.

Namespaces:
- Telephony: `CrestApps.OrchardCore.Telephony` (+ `.Models`), project
  `src/Abstractions/CrestApps.OrchardCore.Telephony.Abstractions`.
- Contact Center: `CrestApps.OrchardCore.ContactCenter` (+ `.Models`), project
  `src/Abstractions/CrestApps.OrchardCore.ContactCenter.Abstractions`.
- SMS: `OrchardCore.Sms` (Orchard Core framework).

---

## 1. Telephony (soft-phone) contracts

### Identity — `ITelephonyProvider` (required)

```csharp
LocalizedString Name { get; }
TelephonyCapabilities Capabilities { get; }   // [Flags]
```

`TelephonyCapabilities` flags and the contract each one requires
(authoritative table: `TelephonyCapabilityContracts.ContractsByCapability`):

| Flag | Required contract | Method(s) |
| --- | --- | --- |
| `Dial` | `ITelephonyCallControlProvider` | `Task<TelephonyResult> DialAsync(DialRequest, CancellationToken)` |
| `Hangup` | `ITelephonyCallControlProvider` | `Task<TelephonyResult> HangupAsync(CallReference, CancellationToken)` |
| `Hold`, `Resume` | `ITelephonyHoldProvider` | `HoldAsync(CallReference,…)`, `ResumeAsync(CallReference,…)` |
| `Mute` | `ITelephonyMuteProvider` | `MuteAsync(CallReference,…)`, `UnmuteAsync(CallReference,…)` |
| `Transfer` | `ITelephonyTransferProvider` | `TransferAsync(TransferRequest,…)` |
| `AttendedTransfer` | `ITelephonyAttendedTransferProvider` | `StartAttendedTransferAsync(TransferRequest,…)` (+ complete/cancel via transfer/hangup) |
| `Merge` | `ITelephonyConferenceProvider` | `MergeAsync(MergeRequest,…)` |
| `SendDigits` | `ITelephonyDtmfProvider` | `SendDigitsAsync(SendDigitsRequest,…)` |
| `ReceiveCalls` | `ITelephonyInboundCallProvider` | `AnswerAsync(CallReference,…)`, `RejectAsync(CallReference,…)` |
| `Voicemail` | `ITelephonyVoicemailProvider` | `SendToVoicemailAsync(CallReference,…)` |
| `Directory` | `ITelephonyDirectoryProvider` | directory lookup for transfer targets |
| `ExtensionDial` | `ITelephonyExtensionDialProvider` | dial an internal extension (`ExtensionDialRequest`) |
| `ExtensionConference` | `ITelephonyExtensionDialProvider` | add an internal extension to a live call (`ExtensionConferenceRequest`) |

All executable methods return `Task<TelephonyResult>` (or a lookup result). `TelephonyResult` has
`Succeeded`, an error string, and an optional `TelephonyCall`. Use its factory helpers:
`TelephonyResult.Success(call)`, `TelephonyResult.Failed(msg)`, `TelephonyResult.Unknown(msg)` (ambiguous
outcome — the call may or may not have happened).

### Audio delivery — `ITelephonyAudioProvider`

```csharp
TelephonyAudioCapabilities AudioCapabilities { get; }   // None / Browser / ExternalDevice (flags)
TelephonyAudioMode ConfiguredAudioMode { get; }         // None / Browser / ExternalDevice
string BrowserMediaAdapterName { get; }                 // e.g. "telnyx-webrtc", "sipjs", or null
```

Return `null` for `BrowserMediaAdapterName` when the provider has no in-browser media (e.g. DialPad).

### Soft-phone credentials

- `ITelephonySoftPhoneCredentialsProvider` — `Task<TelephonyClientCredentials> GetClientCredentialsAsync(CancellationToken)`.
- `ISoftPhoneRegistrationConfigContributor` — `string ProviderName { get; }` +
  `Task<SoftPhoneRegistrationConfig> BuildAsync(SoftPhoneRegistrationConfigContext, CancellationToken)`.
  Returns `null` when not configured. This is what the browser adapter logs in with (signaling URL, SIP URI,
  credential, ICE, media codecs, session, `ClientOriginatesCalls`, `OutboundCallerId`, `EchoTestDestination`).
- `ISoftPhoneCredentialRegistrar` — `string ProviderName { get; }` +
  `Task<bool> ReportRegisteredAsync(...)`. The browser calls back when it has actually registered against the
  provider (so the platform knows the agent has a live media leg).
- `ISoftPhoneCredentialRevoker` — `string ProviderName { get; }` + `Task<int> RevokeForUserAsync(...)` +
  `Task<bool> RevokeCredentialAsync(...)` (default `false`). Revoke short-lived credentials on sign-out.

### Live call-state lookup — `ITelephonyCallStateProvider`

```csharp
Task<TelephonyCallLookupResult> GetCallStateAsync(string callId, CancellationToken);
```

Return `Succeeded=true, Found=false` for a not-found call, `Succeeded=false` on transient failure.

---

## 2. Contact Center voice contracts

### Identity — `IContactCenterVoiceProvider` (required for ACD participation)

```csharp
string TechnicalName { get; }
LocalizedString Name { get; }
ContactCenterVoiceProviderCapabilities Capabilities { get; }   // [Flags]
VoiceProviderDeliveryModel DeliveryModel { get; }
```

`VoiceProviderDeliveryModel`:
- `AgentDeviceNative` — the provider rings the agent's own device/soft phone; the platform reserves/offers
  but does not bridge media.
- `ServerSideAcd` — the provider parks/queues the call; the platform must explicitly ask the provider to
  connect (bridge) the call to the agent once the offer is accepted. **Requires the `AgentConnect` flag.**

`ContactCenterVoiceProviderCapabilities` flags → executable contract:

| Flag | Required contract | Method(s) |
| --- | --- | --- |
| `DialerDial` | `IContactCenterVoiceCallControlProvider` | `DialAsync(...)` |
| `AgentConnect` | `IContactCenterVoiceCallControlProvider` | `ConnectToAgentAsync(...)` |
| `AgentCallAssignment` | `IContactCenterVoiceQueueAssignmentProvider` | `AssignCallAsync(...)` |
| `ProviderQueue` | `IContactCenterVoiceQueueAssignmentProvider` | `QueueCallAsync(...)` |
| `QueueEvents` | (report queue events into the event pipeline) | — |
| `AgentPresenceSync` | (sync PBX presence into Contact Center) | — |
| `CallTransfer` | `IContactCenterVoiceTransferProvider` | `TransferAsync(...)` |
| `Conference` | `IContactCenterVoiceConferenceProvider` | `ConferenceAsync(...)` |
| `Recording` | `IContactCenterVoiceRecordingProvider` | `SetRecordingStateAsync(...)` (start/stop) |
| `RecordingPause` | `IContactCenterVoiceRecordingProvider` | `SetRecordingStateAsync(...)` (pause/resume a segment) |
| `Monitor` / `Whisper` / `Barge` | `IContactCenterVoiceMonitoringProvider` | `EngageAsync(...)`, `StopAsync(...)` |
| `SecureCapture` / `SecureCaptureMasking` | secure-capture path (`ISecureCaptureTokenSink` et al.) | — |

All executable CC methods return `Task<ContactCenterVoiceProviderResult>`.

### Bidirectional media — `IContactCenterVoiceMediaProvider` + `IContactCenterVoiceMediaSession`

```csharp
// provider
string TechnicalName { get; }
Task<IContactCenterVoiceMediaSession> OpenSessionAsync(ContactCenterVoiceMediaSessionRequest, CancellationToken);

// session
string SessionId { get; }
string ProviderCallId { get; }
ContactCenterVoiceMediaFormat IncomingFormat { get; }   // e.g. PCMU 8kHz
ContactCenterVoiceMediaFormat OutgoingFormat { get; }
IAsyncEnumerable<ContactCenterVoiceMediaFrame> ReadIncomingAsync(CancellationToken);
ValueTask WriteOutgoingAsync(ContactCenterVoiceMediaFrame frame, CancellationToken);
Task StopAsync(CancellationToken);
```

Telnyx implements this with Telnyx **Media Streaming** over a WebSocket the provider dials back to (RTP mode,
base64 codec payloads both ways). Asterisk uses ARI External Media. Only implement this if the provider can
stream call audio bidirectionally.

---

## 3. Event ingress & identity

- `IProviderIdentityProvider` — `IEnumerable<ProviderIdentity> GetIdentities()`. Contributes the canonical
  provider technical name and any runtime aliases so voice ingress can resolve provider names without
  referencing implementation assemblies.
- `IProviderWebhookInboxHandler` — `string TechnicalName { get; }`,
  `ContactCenterHandlerReplaySafety ReplaySafety { get; }`, `Task HandleAsync(string payload, CancellationToken)`.
  The durable, provider-owned handler that processes an accepted webhook delivery (dispatched immediately and
  again by the background inbox on retry — so it must be idempotent/replay-safe).
- `IProviderWebhookInbox` — commit an authenticated delivery (`AcceptAsync`) and dispatch it (`DispatchAsync`).
- `IProviderWebhookIngressLimiter` — concurrency + per-provider rate leases (`AcquireConcurrencyAsync`,
  `AcquireRateAsync`) and `IsFresh(...)` replay-window check.
- `IProviderVoiceEventSink` — submit normalized `ProviderVoiceEvent`s (state transitions) without touching
  Contact Center persistence models.
- `IInboundVoiceEventSink` — route a normalized inbound call into Contact Center work.
- `IProviderCallStateReconciler` — reconcile active Contact Center calls against authoritative provider state
  after a restart.

---

## 4. Media provisioning — `IVoiceMediaProvisioner`

```csharp
string ProviderTechnicalName { get; }
Task<string> UploadAsync(Stream audio, string contentType, string namePrefix, CancellationToken);   // returns media reference
Task DeleteAsync(string mediaReference, CancellationToken);
```

Used for hosted media the provider plays back (e.g. voicemail greetings uploaded to the provider's media
storage). Implement only if the provider has hosted media storage.

---

## 5. SMS/MMS — `OrchardCore.Sms.ISmsProvider`

```csharp
LocalizedString Name { get; }
Task<Result> SendAsync(SmsMessage message, CancellationToken);   // OrchardCore.Infrastructure.Result
```

A separate Orchard feature. Register with `services.AddSmsProvider<T>(technicalName)` (see the Telnyx SMS
startup). Inbound SMS + delivery receipts arrive through the provider's own signed messaging webhook and are
normalized like voice events.

---

## Fail-closed checklist

- Do NOT set a `TelephonyCapabilities`/`ContactCenterVoiceProviderCapabilities` flag unless the class also
  implements the mapped interface. `TelephonyCapabilityContracts` is the source of truth for the telephony
  side — read it when in doubt.
- If a capability is partially supported, either implement it fully or drop the flag. There is no
  "advertise but no-op" state; the resolver treats an advertised-but-unimplemented capability as a defect.
- `Hold`/`Mute` for a browser-audio provider may be executed by the **browser media adapter** (SIP re-INVITE
  / local-track toggling) rather than a server REST call — Telnyx reports these optimistically from the
  server method and lets the adapter do the work. That is still a real implementation of the contract.
