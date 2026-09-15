---
name: crestapps-phone-provider
description: >
  Skill for adding a new phone/telephony provider (for example Twilio, Vonage, Plivo, Bandwidth, Amazon
  Connect, or a self-hosted PBX) to the CrestApps.OrchardCore platform, modeled on the reference Telnyx
  implementation. Covers the full capability surface a provider can implement — the Telephony soft-phone
  contracts, the Contact Center voice orchestration contracts, SMS/MMS, browser WebRTC media (native
  provider SDK preferred, SIP.js fallback), signed and durable webhooks, short-lived credential issuance,
  call recording ingest, DI registration, settings/options, and the module/feature layout. Use this skill
  whenever the request is to "add a phone provider", "integrate <telephony vendor>", "create a new
  CrestApps.OrchardCore.<Provider> module", implement ITelephonyProvider / IContactCenterVoiceProvider /
  ISmsProvider, wire a provider webhook, add a browser media adapter, or extend telephony/Contact Center
  provider infrastructure. Every capability is optional: a provider must implement only the executable
  contracts for the operations it can actually deliver, and must fail closed on the rest.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Adding a Phone Provider to CrestApps.OrchardCore

This skill teaches you to add a new telephony/phone provider by following the **Telnyx** reference
implementation, which is the most complete provider in the repo. It exists so a new provider (e.g.
`CrestApps.OrchardCore.Twilio`) can be built accurately, resiliently, and without silently missing a
capability.

> The authoritative, always-current architecture reference already lives in the docs site at
> `src/CrestApps.Docs/docs/telephony/custom-providers.md`. **Read it first**, keep it in sync, and treat
> this skill as the practical, Telnyx-derived build workflow that complements it.

## Golden rules

1. **Capabilities are opt-in and must fail closed.** A provider advertises a set of capability flags AND
   implements the matching executable contract for each. Advertising a flag without its contract is a bug:
   the operation must be impossible, not half-wired. Implement **only what the backend can truly deliver**.
2. **Prefer the provider's own native browser SDK** for in-browser WebRTC media (as Telnyx uses
   `@telnyx/webrtc`). Fall back to SIP.js only when the provider has no first-party browser SDK or when a
   plain SIP registrar is the most reliable path. Native SDKs handle the provider's tuned media gateway,
   ICE/TURN defaults, and reconnection far better than a generic SIP stack.
3. **The browser is provider-agnostic.** Providers never push UI to the browser. Native provider events
   (webhooks/streams/callbacks) are authenticated, made durable, normalized into the internal Contact
   Center voice-event pipeline, and only then projected to the soft phone.
4. **Be resilient.** Outbound REST goes through a named, resilient `HttpClient` (retry with jitter + circuit
   breaker, **never auto-replay unsafe methods**), carries an idempotency/command id on non-idempotent
   POSTs, treats ambiguous HTTP status codes as `Unknown` (not `Failed`), and never throws out of a
   provider method — it returns a typed result.
5. **A new provider is a new module.** Put shared, testable logic in a `*.Core` project and the
   Orchard module (manifest, startup, endpoints, drivers, views) in `src/Modules/CrestApps.OrchardCore.<Provider>`.
6. **Test-driven.** Write the failing test first (see `AGENTS.md` → *Test-Driven Development*), then make it
   pass. Providers are highly testable because every method returns a typed result over a fakeable HTTP seam.
7. **Update the docs in the same change.** Add `src/CrestApps.Docs/docs/telephony/<provider>.md`, extend
   `custom-providers.md` if you introduce a new seam, and update the changelog. Docs must describe what the
   code does today — never aspirational behavior.

## The reference implementation (study these)

| Concern | Reference files |
| --- | --- |
| Soft-phone telephony provider | `src/Core/CrestApps.OrchardCore.Telnyx.Core/Services/TelnyxTelephonyProvider.cs` (+ `.Extensions.cs`) |
| Contact Center voice provider | `.../Services/TelnyxContactCenterVoiceProvider.cs` (+ `.Recording.cs`) |
| Bidirectional media | `.../Services/TelnyxContactCenterVoiceMediaProvider.cs`, `TelnyxContactCenterVoiceMediaSession.cs` |
| SMS/MMS | `src/Modules/CrestApps.OrchardCore.Telnyx/Services/TelnyxSmsProvider.cs` |
| Browser media (native SDK) | `src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone.js` → `createTelnyxBrowserMediaAdapter` |
| Browser media (SIP.js) | same file → `createSipJsBrowserMediaAdapter` (and `CrestApps.OrchardCore.Asterisk`) |
| Vendored SDK registration | `src/Modules/CrestApps.OrchardCore.Resources/ResourceManagementOptionsConfiguration.cs` |
| DI + features | `src/Modules/CrestApps.OrchardCore.Telnyx/Startup.cs`, `Manifest.cs`, `TelnyxConstants.cs` |
| Webhook (signed + durable + backpressure) | `src/Modules/CrestApps.OrchardCore.Telnyx/Endpoints/TelnyxWebhookEndpoint.cs`, `.../Services/TelnyxWebhookInboxHandler.cs`, `TelnyxWebhookSignatureValidator.cs` |
| Credentials + soft-phone registration | `TelnyxTelephonyCredentialIssuer.cs`, `TelnyxSoftPhoneRegistrationConfigContributor.cs`, `TelnyxAgentCredentialStore.cs`, `TelnyxSoftPhoneCredentialRegistrar.cs`, `TelnyxSoftPhoneCredentialRevoker.cs` |
| Recording ingest | `TelnyxRecordingIngestService.cs`, `TelnyxRecordingIngestEnqueuer.cs`, `TelnyxRecordingIngestJobStore.cs`, `BackgroundTasks/TelnyxRecordingIngestBackgroundTask.cs` |
| Settings/options | `Models/TelnyxSettings.cs`, `Services/TelnyxOptions.cs` + `TelnyxOptionsConfigurations.cs`, `Drivers/TelnyxSettingsDisplayDriver.cs` |

## The capability map (the heart of the design)

A provider is a small identity object plus a **set of separate capability interfaces**. Each advertised
flag has exactly one executable contract; if you cannot implement the contract, do not advertise the flag.

- **Soft-phone / Telephony** (`ITelephonyProvider.Capabilities`, `TelephonyCapabilities` flags):
  `Dial`/`Hangup`→`ITelephonyCallControlProvider`, `Hold`/`Resume`→`ITelephonyHoldProvider`,
  `Mute`→`ITelephonyMuteProvider`, `Transfer`→`ITelephonyTransferProvider`,
  `AttendedTransfer`→`ITelephonyAttendedTransferProvider`, `Merge`→`ITelephonyConferenceProvider`,
  `SendDigits`→`ITelephonyDtmfProvider`, `ReceiveCalls`→`ITelephonyInboundCallProvider`,
  `Voicemail`→`ITelephonyVoicemailProvider`, `Directory`→`ITelephonyDirectoryProvider`,
  `ExtensionDial`/`ExtensionConference`→`ITelephonyExtensionDialProvider`. The authoritative mapping is
  `TelephonyCapabilityContracts.ContractsByCapability`.
- **Audio delivery** (`ITelephonyAudioProvider`): advertises browser vs external-device audio, the
  configured mode, and the `BrowserMediaAdapterName` (the JS adapter the soft phone loads).
- **Soft-phone credentials** (`ITelephonySoftPhoneCredentialsProvider`, `ISoftPhoneRegistrationConfigContributor`,
  `ISoftPhoneCredentialRegistrar`, `ISoftPhoneCredentialRevoker`).
- **Live call-state lookup** (`ITelephonyCallStateProvider`).
- **Contact Center voice** (`IContactCenterVoiceProvider.Capabilities`, `ContactCenterVoiceProviderCapabilities`
  flags + `VoiceProviderDeliveryModel`): `DialerDial`/`AgentConnect`→`IContactCenterVoiceCallControlProvider`,
  `AgentCallAssignment`/`ProviderQueue`→`IContactCenterVoiceQueueAssignmentProvider`,
  `CallTransfer`→`IContactCenterVoiceTransferProvider`, `Conference`→`IContactCenterVoiceConferenceProvider`,
  `Recording`/`RecordingPause`→`IContactCenterVoiceRecordingProvider`,
  `Monitor`/`Whisper`/`Barge`→`IContactCenterVoiceMonitoringProvider`,
  `SecureCapture`→ the secure-capture path.
- **Bidirectional media** (`IContactCenterVoiceMediaProvider` + `IContactCenterVoiceMediaSession`) for
  streaming caller audio and injecting audio (Telnyx Media Streaming / Asterisk external media).
- **Event ingress** (`IProviderWebhookInbox`, `IProviderWebhookInboxHandler`, `IProviderWebhookIngressLimiter`,
  `IProviderVoiceEventSink`, `IInboundVoiceEventSink`, `IProviderCallStateReconciler`, `IProviderIdentityProvider`).
- **Media provisioning** (`IVoiceMediaProvisioner`) for uploading/deleting hosted media (e.g. greetings).
- **SMS/MMS** (`OrchardCore.Sms.ISmsProvider`) — a separate feature.

See `references/capability-contracts.md` for every interface with its method signatures and the exact flag
that gates it.

## Build workflow

Work through these steps. Each has a dedicated reference; do not skip the failing-test-first step.

1. **Scaffold the module and core project.** Two projects, manifest, features, constants, csproj wiring, and
   the Targets reference. → `references/module-scaffold.md`
2. **Model settings + options.** `*Settings` (persisted, secrets data-protected), `*Options` + a
   `IConfigureOptions<>` that merges appsettings and UI settings, `IOptionsMonitor<>`-driven refresh, a
   settings display driver + edit view. → `references/module-scaffold.md`
3. **Write a failing test** for the first operation you will implement (e.g. `DialAsync` returns
   `Unknown` on an ambiguous HTTP status). → `AGENTS.md` TDD section + `tests/CrestApps.OrchardCore.Tests/Telnyx`.
4. **Implement the telephony provider** — `ITelephonyProvider` + only the capability contracts you can
   deliver; advertise exactly those flags. Route all REST through a resilient named `HttpClient`.
   → `references/telnyx-reference.md`, `references/webhooks-and-resilience.md`
5. **Implement the browser media adapter** if audio is in-browser: prefer the native SDK, vendor it into the
   Resources module, register the script, add per-provider loading, and implement the `IBrowserMediaAdapter`
   JS contract + the server `ISoftPhoneRegistrationConfigContributor`. → `references/browser-media-adapter.md`
6. **Implement the webhook / event ingress** — signed, replay-protected, backpressured, durable inbox,
   normalized into `ProviderVoiceEvent`. → `references/webhooks-and-resilience.md`
7. **Implement the Contact Center voice provider** (if the provider participates in ACD) with the correct
   `VoiceProviderDeliveryModel` and only the CC capability flags it can execute. Register under
   `[RequireFeatures(ContactCenterConstants.Feature.Voice)]`. → `references/telnyx-reference.md`
8. **Add SMS** (optional, its own feature) implementing `ISmsProvider`. → `references/telnyx-reference.md`
9. **Register everything** in `Startup` (and feature-gated sub-startups) exactly as Telnyx does.
   → `references/module-scaffold.md`
10. **Run tests**, then **write the docs** and build the docs site.

## Registration shape (must-match)

- Register the provider type with the telephony framework via
  `IConfigureOptions<TelephonyProviderOptions>` → `options.TryAddProvider(TechnicalName, new TelephonyProviderTypeOptions(typeof(<Provider>)) { IsEnabled = ... })`.
- Register the resilient client with `services.AddHttpClient(<TechnicalName>).AddStandardResilienceHandler(...)`
  and `options.Retry.DisableForUnsafeHttpMethods()`.
- Contact Center glue is **not** a separate operator-facing feature; register it in a
  `[RequireFeatures(ContactCenterConstants.Feature.Voice)]` startup so it activates automatically when both
  the provider and Contact Center Voice are enabled. Use `TryAddScoped` for the inbound router fallback so
  the CC router always wins regardless of startup ordering.
- Media glue lives under `[RequireFeatures(ContactCenterConstants.Feature.VoiceMedia)]`.

## Definition of done

- [ ] Advertised capability flags exactly match implemented executable contracts (no over/under advertising).
- [ ] Every provider method returns a typed result and never throws; ambiguous HTTP → `Unknown`.
- [ ] Non-idempotent POSTs carry an idempotency/command id; the transport does not replay unsafe methods.
- [ ] Secrets (API keys, webhook keys) are data-protected at rest and never logged (`SanitizeLogValue`).
- [ ] Webhook is signature-verified, timestamp/replay-checked, size-limited, backpressured, and durable.
- [ ] Browser media uses the native SDK when available; the SDK is vendored and loaded only for this provider.
- [ ] Provider registered in `Startup`, feature-gated CC/Media startups added, module added to the Targets project.
- [ ] Tests written first and passing (`dotnet test`), including a regression test per fixed bug.
- [ ] Docs added/updated under `src/CrestApps.Docs/docs/telephony/` and the changelog; docs site builds.
- [ ] Consider whether this module needs its own dev skill under `.agents/skills/` (per `AGENTS.md`).
