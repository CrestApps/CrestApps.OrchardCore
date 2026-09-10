# Browser media adapter (the JavaScript side)

When a provider delivers audio **in the browser** (WebRTC), it needs a browser media adapter. The soft phone
(`src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone.js`) is provider-agnostic and selects the
adapter by name at runtime.

## Prefer the native provider SDK

Use the provider's own first-party browser SDK when it has one (Telnyx ships `@telnyx/webrtc`, exposing
`window.TelnyxWebRTC` with `TelnyxRTC`). Native SDKs speak the provider's tuned media protocol (Telnyx uses
Verto, not raw SIP), ship sensible TURN/ICE defaults, and handle reconnection. Only fall back to **SIP.js**
(`window.SIP`, used by the Asterisk provider) when the provider has no browser SDK or is a plain SIP registrar.

## The `IBrowserMediaAdapter` contract

From the contract comment in `soft-phone.js`:

```
adapter(context) -> Promise<session>
context: { config, credentials, localStream, remoteAudioElement, setRemoteStream, showError,
           onProviderWarning, ... }
session: { handleCallState(call), dispose(), providerConfig, ... }
```

- `context.localStream` — the microphone stream already acquired by the soft phone; **reuse it** for outbound
  calls so the SDK does not open a second capture.
- `context.setRemoteStream(stream)` / `remoteAudioElement` — where you pipe the remote party's audio.
- `context.showError(message)` / `context.onProviderWarning(warning)` — surface errors and media-quality
  advisories to the UI.
- The adapter **fetches its registration config from the server** (the `SoftPhoneRegistrationConfig` your
  `ISoftPhoneRegistrationConfigContributor.BuildAsync` returns) and logs in with it.

The returned `session` must expose:
- `handleCallState(call)` — react to soft-phone call-state changes (answer/hold/mute/hangup).
- `dispose()` — tear down (deregister, close the peer connection, stop tracks).
- For **client-originated** providers, also expose:
  `providerConfig`, `mediaCodecs`, `canOriginate: true`, `outboundCallerId`, `echoTestDestination`,
  `getDiagnostics()` (SDP + `getStats()` for the diagnostics panel/echo test), and
  `originate(destination, callerId, onState) -> controller` where `onState` receives soft-phone state names
  `'Ringing' | 'Connected' | 'Disconnected'` and the returned controller can answer/decline/hangup that leg.

## Registering a new adapter

1. In `soft-phone.js`, `createBrowserMediaAdapterRegistry` builds the per-instance registry:
   ```js
   adapters.sipjs = createSipJsBrowserMediaAdapter(rootElement, config);
   adapters['telnyx-webrtc'] = createTelnyxBrowserMediaAdapter(rootElement, config);
   // add: adapters['twilio-voice'] = createTwilioBrowserMediaAdapter(rootElement, config);
   ```
   Registry is intentionally **per-instance** (not a global window registry) so pages can host adapters from
   different providers without one script clobbering another. Write a `createTwilioBrowserMediaAdapter(...)`
   factory returning `function(context){ return fetchRegistrationConfig(config).then(cfg => createTwilioSession(cfg, context)); }`.
2. The adapter name must equal your provider's `BrowserMediaAdapterName`
   (`ITelephonyAudioProvider.BrowserMediaAdapterName`, and the constant in `<Provider>Constants`).
3. Edit the assets, then run `npm run rebuild` so `Assets/js/soft-phone.js` compiles to
   `wwwroot/scripts/soft-phone.{js,min.js}` (see `AGENTS.md` → *Frontend Development*). Never hand-edit the
   built `wwwroot` files.

## Vendoring the SDK and per-provider loading

The provider SDK is **vendored locally** (no public CDN serves the exact browser bundle), and only loaded for
the active provider.

1. Build a browser IIFE bundle of the SDK (esbuild `--global-name=<Global>`), e.g. `window.TwilioVoice`, and
   drop it under `src/Modules/CrestApps.OrchardCore.Resources/wwwroot/vendors/<sdk>/` (min + non-min).
2. Register it as a named script in
   `src/Modules/CrestApps.OrchardCore.Resources/ResourceManagementOptionsConfiguration.cs`:
   ```csharp
   _manifest.DefineScript("twilio-voice")
       .SetUrl("~/CrestApps.OrchardCore.Resources/vendors/twilio-voice/twilio-voice.min.js",
               "~/CrestApps.OrchardCore.Resources/vendors/twilio-voice/twilio-voice.js")
       .SetVersion("<sdk-version>");
   ```
3. Load **only the current provider's** library in
   `src/Modules/CrestApps.OrchardCore.Telephony/Services/SoftPhoneWidgetPresenter.cs` →
   `RegisterResources`, which branches on `widget.BrowserMediaAdapterName` when
   `widget.AudioMode == TelephonyAudioMode.Browser`:
   ```csharp
   else if (string.Equals(adapterName, "twilio-voice", StringComparison.OrdinalIgnoreCase))
       _resourceManager.RegisterResource("script", "twilio-voice").AtFoot();
   ```
   A SIP.js provider never downloads the Telnyx SDK, and vice versa. Add your branch here.

## ICE / TURN gotcha

Only override the SDK's default `iceServers` when your configured servers include a **TURN** server. STUN-only
overrides can break media for a callee behind a restrictive NAT (they receive no audio) because they replace
the SDK's own TURN defaults. This was a real Telnyx extension-call bug — see the memory notes.
