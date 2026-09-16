# Telnyx reference walkthrough

The canonical, most-complete provider. Read the real files; this summarizes the patterns worth copying.

## Delivery model & how a call reaches the agent

Telnyx delivers calls **server-side** and the platform bridges the live call to the agent's browser SIP
endpoint, so:

- `TelnyxContactCenterVoiceProvider.DeliveryModel => VoiceProviderDeliveryModel.ServerSideAcd`.
- It advertises `DialerDial | AgentConnect | CallTransfer | Recording` (only what it executes).
- Telnyx **rejects a server-originated call placed to a registered WebRTC credential** (403), so for
  soft-phone dialing the **browser originates its own outbound call** through the Telnyx SDK
  (`ClientOriginatesCalls = true` in the registration config; `canOriginate`/`originate(...)` in the JS
  adapter). This is a hard-won provider quirk — check whether your provider has the same constraint.

## `TelnyxTelephonyProvider` (soft-phone side) — patterns to copy

Advertises `Dial | Hangup | Hold | Resume | Mute | Transfer | AttendedTransfer | Merge | SendDigits |
Voicemail | ReceiveCalls | ExtensionDial | ExtensionConference` and implements the matching contracts plus
`ITelephonyAudioProvider`, `ITelephonySoftPhoneCredentialsProvider`, `ITelephonyCallStateProvider`.

- **Every method fails closed when unconfigured**: `if (!_options.IsConfigured) return NotConfigured();`.
- **Every method returns a typed result and never throws to the caller.** The catch ladder is deliberate:
  ```csharp
  catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }   // honor cancellation
  catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
  { _logger.LogError(...); return TelephonyResult.Unknown(...); }   // transport ambiguity → Unknown
  catch (Exception ex) { _logger.LogError(...); return TelephonyResult.Failed(...); }               // clear failure
  ```
- **Ambiguous HTTP status → `Unknown`, not `Failed`.** `TelephonyProviderResponse.IsAmbiguousStatusCode(status)`
  decides. `Unknown` means "the call may have been placed" — the orchestration reconciles rather than
  double-dialing.
- **Idempotency on non-idempotent POSTs.** An outbound dial passes the caller's idempotency key as the
  provider's `command_id` so a retried POST after a lost response is de-duplicated by Telnyx instead of
  placing a second call. Do the equivalent with your provider's idempotency mechanism.
- **Optimistic hold/mute for browser audio.** Because the audio is in the browser, `HoldAsync`/`MuteAsync`
  do no REST call — they return the target state and the browser media adapter does the SIP re-INVITE /
  track toggle. This still fully satisfies the contract.
- **`ExecuteActionAsync` helper** centralizes the POST-action-with-result-mapping pattern, with
  `succeedWhenMissing: true` for idempotent teardown (hangup/reject) so a 404 (already gone) is success.
- **`CreateClient()`** resolves the resilient named client and sets base address + bearer auth per call.
- **Never log secrets or raw payloads unsanitized** — response bodies go through `.SanitizeLogValue()`.
- **`SendToVoicemailAsync`** is a good example of a multi-step provider flow (answer → play greeting →
  beep → record) driven partly by webhooks (`client_state` correlation, greeting-ended fast path).

## `TelnyxContactCenterVoiceProvider` (Contact Center side)

- Splits recording into a partial class (`.Recording.cs`) — keep large providers organized by capability.
- `DialAsync`/`ConnectToAgentAsync` re-resolve the telephony provider and **check its capabilities before
  acting** (`provider.Capabilities.HasFlag(TelephonyCapabilities.Dial)`), so CC never calls a capability the
  soft-phone provider does not have.
- Returns `ContactCenterVoiceProviderResult` and stamps `ProviderName = TechnicalName` on results/calls.

## Credentials & soft-phone registration

- `TelnyxTelephonyCredentialIssuer` mints a **short-lived** SIP credential from the provider (with a TTL) —
  see the memory note "Extension dial needs a live credential, not presence": the credential is what makes a
  target reachable, distinct from agent presence.
- `TelnyxSoftPhoneRegistrationConfigContributor.BuildAsync` returns the `SoftPhoneRegistrationConfig` the
  browser logs in with: signaling (WSS URL + SIP URI + auth user), credential (password + expiry), ICE
  (servers + transport policy — only override iceServers when they include TURN; see the extension-call audio
  memory), media codecs, session (interaction id + expiry), `ClientOriginatesCalls`, `OutboundCallerId`,
  `EchoTestDestination`. Returns `null` when not configured.
- `TelnyxAgentCredentialStore` persists the user→live-credential mapping (indexed) so the platform can find
  an agent's registered endpoint (e.g. to bridge or extension-dial). Cap live credentials per user and
  supersede-revoke old ones (a churn/cap issue caused `LOGIN_FAILED` — see the extension-calls memory).
- `TelnyxSoftPhoneCredentialRegistrar.ReportRegisteredAsync` is called back by the browser once it has
  actually registered. `TelnyxSoftPhoneCredentialRevoker` revokes on sign-out.

## SMS (`TelnyxSmsProvider : ISmsProvider`)

- Reads resolved credentials from `IOptionsMonitor<TelnyxSmsOptions>` (merged appsettings section
  `OrchardCore_Sms_Telnyx` + UI settings), fails closed when `!options.IsValid`.
- `SendAsync` POSTs to the Messaging API, returns `Result.Success()` / `Result.Failed(localized)`, truncates
  and localizes error bodies, and catches `HttpRequestException`/`TaskCanceledException`.
- Inbound SMS + delivery receipts arrive on the provider's own signed messaging webhook and are normalized
  like voice events. Registered as its own feature.

## Constants worth mirroring (`TelnyxConstants`)

Provider technical name, browser media adapter name, data-protector names (API key + webhook key), default
API base URL, SIP WSS URL (note the non-default port quirk `:7443`), SIP domain, STUN URL, webhook signature
& timestamp header names, webhook path, media-stream path, work-partition keys, recording constants (event
types, `client_state` intents, format, ingest batch/retry/backoff), SMS section + paths + event types, and
feature ids.

## Tests

`tests/CrestApps.OrchardCore.Tests/Telnyx/` — unit tests over the `.Core` logic (e.g.
`SoftPhoneHealthMetricsTests`). Add your provider's tests here. Fake the HTTP seam and assert result types
(`Success`/`Failed`/`Unknown`), fail-closed behavior, idempotency-key propagation, and webhook
signature/replay handling. See also the CC distributed-test infra memory for Redis/Postgres-gated tests.

## Related hard-won lessons (from prior debugging — check if they apply to your provider)

- Server can't bridge a leg to a registered browser credential → browser must originate (403).
- SIP-over-WebSocket may be on a non-default port (Telnyx `:7443`; `1006` close otherwise).
- Only override `iceServers` when they include TURN, or a restrictive-NAT callee gets one-way/no audio.
- A provider webhook handler must **propagate `ConcurrencyException`** (don't swallow it) so the durable
  inbox retries in a fresh scope.
- Register idle agents with the provider on connect and keep them registered, or inbound never rings.
