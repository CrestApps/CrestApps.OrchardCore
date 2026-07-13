---
sidebar_label: Asterisk
sidebar_position: 2
title: Asterisk Telephony Provider
description: Integrate Asterisk as a telephony provider for the Orchard Core soft phone.
---

| | |
| --- | --- |
| **Feature Name** | Asterisk |
| **Feature ID** | `CrestApps.OrchardCore.Asterisk` |

The **Asterisk** module integrates the [Asterisk](https://www.asterisk.org/) platform as a provider for the [Telephony](./) soft phone. It uses the **Asterisk REST Interface (ARI)** over HTTP basic authentication and performs call control server-side, so the browser never needs direct access to Asterisk credentials. Here, **provider** means the Asterisk backend adapter that the soft phone talks to, not a separate telecom billing provider.

## Two Asterisk providers

When the **Asterisk** feature is enabled, the telephony settings can expose up to two selectable providers:

| Provider | How it is configured | When it appears |
| --- | --- | --- |
| **Asterisk** | Site settings under **Settings → Communication → Telephony → Asterisk** | When an administrator enables it for the tenant |
| **Default Asterisk** | Shell configuration (`appsettings.json`, user secrets, or environment variables) | Automatically, when the required configuration exists |

This mirrors the Orchard Core default-provider pattern used by modules such as SMTP: the tenant-specific provider is managed in the UI, while the configuration-backed provider is automatically available across tenants whenever the host supplies its configuration.

## Capabilities

The Asterisk provider currently advertises support for:

- **Dial**
- **Hang up**
- **Answer / reject inbound calls**
- **Send to voicemail** when a voicemail context and extension template are configured
- **Hold / resume**
- **Mute / unmute**
- **Blind transfer** when the endpoint template resolves to a `Local/...@context` dialplan route
- **Merge**
- **Send DTMF digits**

Voicemail routing still depends on your dialplan design, but the provider can now expose the soft-phone
voicemail button when you configure a voicemail dialplan target for the integration.

## Tenant-configured Asterisk settings

Configure the tenant-specific **Asterisk** provider on the **Asterisk** tab under **Settings → Communication → Telephony**. The Telephony settings UI creates that tab from the site-settings display driver, and the Asterisk editor itself only renders the provider fields inside the tab. You need the `Manage telephony settings` permission.

| Setting | Description |
| --- | --- |
| **Enable Asterisk provider** | Turns the tenant-configured provider on, makes it selectable as the default provider, and reveals the rest of the tenant-specific Asterisk fields in the settings UI. |
| **ARI base URL** | The base ARI endpoint, for example `http://localhost:8088/ari/`. If you omit `/ari`, it is added automatically. |
| **ARI user name** | The Asterisk ARI user name used for HTTP basic authentication. |
| **ARI password** | The ARI password. Stored encrypted with the data protection provider. |
| **Stasis application name** | The ARI application name that receives originated channels. |
| **Endpoint template** | Optional. Use `{number}` to convert the dialed destination into an Asterisk endpoint, for example `PJSIP/{number}@phones` or `Local/{number}@default`. The admin hint now renders that token literally, so the settings screen remains stable while showing the exact placeholder to enter. When empty, the dialed destination is sent to Asterisk as-is. |
| **Outbound caller id** | Optional caller identifier presented on outbound calls. |
| **Dial timeout (seconds)** | How long Asterisk keeps trying to originate the call before timing out. |
| **Voicemail context** | Optional. The dialplan context Orchard continues a ringing call into when the agent chooses **Send to voicemail**. |
| **Voicemail extension template** | Optional. Resolves the dialplan extension used for voicemail routing. It can reference provider-neutral call metadata such as `{voicemailRecipientUserName}`, `{voicemailRecipientUserId}`, `{calledAddress}`, or `{queueName}`. |
| **Voicemail priority** | Optional. The dialplan priority to start at when the provider continues the call to voicemail. |

When you enable **Asterisk** and no default telephony provider is set yet, **Asterisk** becomes the default automatically. When you disable **Asterisk** while it is the default, the default provider is cleared and the soft phone is disabled until another provider is selected.

## Configuration-backed Default Asterisk provider

The **Default Asterisk** provider is not managed through site settings. Instead, the host configures it through shell configuration. When all required values are present, the provider appears automatically in the **Default telephony provider** selector for every tenant where the module is enabled.

### Required configuration

Use the `OrchardCore:CrestApps:Asterisk:Default` section:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Asterisk": {
        "Default": {
          "BaseUrl": "http://localhost:8088/ari/",
          "UserName": "crestapps",
          "Password": "crestapps-dev",
          "ApplicationName": "crestapps-telephony",
          "EndpointTemplate": "Local/{number}@default",
          "TimeoutSeconds": 30,
          "VoicemailContext": "crestapps-voicemail",
          "VoicemailExtensionTemplate": "{voicemailRecipientUserName}",
          "VoicemailPriority": 1
        }
      }
    }
  }
}
```

Equivalent environment variables use the standard double-underscore path, for example:

```text
OrchardCore__CrestApps__Asterisk__Default__BaseUrl=http://localhost:8088/ari/
OrchardCore__CrestApps__Asterisk__Default__UserName=crestapps
OrchardCore__CrestApps__Asterisk__Default__Password=crestapps-dev
OrchardCore__CrestApps__Asterisk__Default__ApplicationName=crestapps-telephony
OrchardCore__CrestApps__Asterisk__Default__EndpointTemplate=Local/{number}@default
OrchardCore__CrestApps__Asterisk__Default__TimeoutSeconds=30
OrchardCore__CrestApps__Asterisk__Default__VoicemailContext=crestapps-voicemail
OrchardCore__CrestApps__Asterisk__Default__VoicemailExtensionTemplate={voicemailRecipientUserName}
OrchardCore__CrestApps__Asterisk__Default__VoicemailPriority=1
```

The provider becomes available only when `BaseUrl`, `UserName`, `Password`, and `ApplicationName` are all configured.

## How call control works

The provider uses ARI endpoints such as:

- `POST /channels` to originate a call
- `DELETE /channels/{id}` to hang up a call
- `POST /channels/{id}/answer` to answer an inbound Stasis-managed channel
- `POST` / `DELETE /channels/{id}/hold` to hold and resume
- `POST` / `DELETE /channels/{id}/mute?direction=both` to mute and unmute
- `POST /channels/{id}/continue` to blind-transfer a Stasis-managed Local channel back into the dialplan
- `POST /channels/{id}/variable` plus `POST /channels/{id}/continue` to push provider-neutral metadata into the channel and route it to the configured voicemail dialplan target
- `POST /channels/{id}/dtmf` to send digits
- `POST /bridges` plus `POST /bridges/{id}/addChannel` to merge two calls; the provider stamps the owned bridge id on both channels and deletes the mixing bridge after the last tracked participant hangs up

Because all requests are issued server-side, the ARI password never reaches the browser.

## Bidirectional RTP media

The Asterisk Contact Center voice provider advertises `BidirectionalMedia` and registers `AsteriskContactCenterVoiceMediaProvider`. It uses ARI External Media over RTP/UDP with G.711 mu-law, 8 kHz, mono audio.

Opening a media session:

1. binds an Orchard UDP socket
2. finds the bridge containing the provider call or creates a mixing bridge
3. creates an ARI `/channels/externalMedia` channel using RTP/UDP and `ulaw`
4. adds the external-media channel to the call bridge
5. reads `UNICASTRTP_LOCAL_ADDRESS` and `UNICASTRTP_LOCAL_PORT` from Asterisk for outbound RTP
6. exposes incoming and outgoing frames through `IContactCenterVoiceMediaSession`

The session request must include `AsteriskConstants.ExternalMediaHostMetadataKey` (`externalHost`), containing the host or IP address Asterisk can reach for the Orchard RTP socket. This is often different from the address Orchard binds locally when containers, NAT, or reverse proxies are involved.

Optional metadata:

| Key | Description |
| --- | --- |
| `bindAddress` | Local IP address on which Orchard binds the UDP socket. Defaults to all local interfaces. |
| `bindPort` | Local UDP port. Defaults to an operating-system-assigned ephemeral port. Production deployments should select and allow an explicit UDP range. |

The current adapter accepts and emits RTP payload type `0` (G.711 mu-law). It parses RTP header extensions, contributing-source entries, and padding before exposing the audio payload. Stopping the media session removes the external-media channel without hanging up the customer call. A bridge created exclusively for the media session is also removed.

The automated test suite exercises the ARI bridge and External Media lifecycle with a scripted HTTP transport and exchanges real RTP datagrams over loopback UDP sockets. It validates sender filtering, malformed-packet rejection, mu-law payload enforcement, sequence/timestamp continuity, and cleanup behavior. These tests validate the application-side adapter without requiring a live PBX; production deployment still requires network, NAT/firewall, codec, and Asterisk configuration validation.

Production networking must allow Asterisk to send UDP RTP to the advertised `externalHost:bindPort`. If Orchard is scaled across nodes, the media session must remain pinned to the node that owns the UDP socket unless a dedicated media relay is introduced.

## Real-time call state and recovery

The module keeps a tenant-scoped ARI WebSocket listener for every configured Asterisk provider. Long-running listeners create an explicit scope through `IShellHost` and the tenant's `ShellSettings` for every reconciliation and event dispatch; they do not depend on an ambient request scope that disappears after tenant activation. Each listener is supervised independently, reconnects with exponential jitter after failure, and runs immediate provider-scoped reconciliation for both Contact Center interactions and plain Telephony interactions after connecting. A failed endpoint therefore does not stop healthy provider listeners, and reconnect recovery does not trigger overlapping full-provider sweeps.

ARI events are normalized into provider-authoritative call states and projected through Orchard to connected soft-phone clients without continuous browser polling. Command acknowledgements do not update or re-query the browser call state; the corresponding ARI event drives the transition. A hangup request that receives ARI `404 Channel not found` is treated as idempotent success because Asterisk has already reached the requested disconnected state. Provider lookup also verifies that the channel still exists after reading hold and mute variables, preventing a channel destroyed during the multi-request lookup from being reported as connected. Supported dashboard states include **Offered**, **Offering**, **Ringing**, **Connected**, **In conference**, and **On hold**, with hold and mute badges where Asterisk exposes those facts. Bridge-leave events only project a lifecycle state when the channel snapshot is authoritative, while hold/unhold, mute-variable, state-change, hangup, and Stasis lifecycle events update the projection in real time. Once an interaction is terminal, later `ChannelHangupRequest`, `StasisEnd`, or `ChannelDestroyed` events are ignored so one physical hangup cannot republish the terminal transition multiple times.

Periodic and startup reconciliation query the ARI channel directly. The provider reads the `CRESTAPPS_STATE_ONHOLD` and `CRESTAPPS_STATE_MUTED` channel variables so an `Up` channel is not incorrectly collapsed to a plain connected state after a restart. Unknown ARI channel states fail the lookup instead of being guessed as connected, leaving the prior projection intact until authoritative state is available. An ARI `404` is authoritative evidence that the channel no longer exists: the reconciler removes the orphaned in-progress Telephony record and sends a disconnected state to the soft phone, preventing a restart or page reload from restoring a call that Asterisk has already ended.

An accepted ARI originate response begins in **Connecting** even for the bundled Local-channel endpoint. The soft phone does not assume that accepting the Dial command means the call is connected; it waits for an ARI event or authoritative channel lookup to report the actual state.

## Voicemail routing

When an agent clicks **Send to voicemail**, Orchard now sends a provider-neutral metadata bag along
with the call action. For Contact Center offers that bag includes values such as:

- `voicemailRecipientUserId`
- `voicemailRecipientUserName`
- `voicemailRecipientDisplayName`
- `calledAddress`
- `callerAddress`
- `queueId`
- `queueName`

The Asterisk provider copies those values into channel variables with a `CRESTAPPS_METADATA_`
prefix and then continues the call into the configured voicemail dialplan target. For example,
`voicemailRecipientUserName = mike` becomes the channel variable
`CRESTAPPS_METADATA_VOICEMAILRECIPIENTUSERNAME`.

That lets your dialplan decide how to route voicemail without introducing Asterisk-specific
properties into the shared telephony contracts. A common pattern is to make the extension template
match the intended mailbox key:

```text
VoicemailContext = crestapps-voicemail
VoicemailExtensionTemplate = {voicemailRecipientUserName}
VoicemailPriority = 1
```

Then configure Asterisk to map the extension or the channel variables to the actual mailbox, with a
fallback when the user-specific mailbox does not exist.

## Aspire local development

`src\Startup\CrestApps.Aspire.AppHost` now provisions an **Asterisk** container for local development using the `andrius/asterisk:latest` image, mounts minimal `http.conf`, `ari.conf`, and `extensions.conf` files, and injects the **Default Asterisk** environment variables into the Orchard Core web project automatically. Repository projects use `TargetFramework` for the default single `net10.0` target while preserving `TargetFrameworks` for multi-target overrides, so Aspire can launch Orchard Core, Asterisk Web, and the sample clients without `dotnet run` stopping for an ambiguous framework selection.

This makes the configuration-backed provider available immediately for local tenants as soon as:

1. The **Asterisk** module is enabled.
2. The tenant selects **Default Asterisk** as its default telephony provider.

The bundled local development credentials are:

- **ARI user name**: `crestapps`
- **ARI password**: `crestapps-dev`
- **ARI base URL**: `http://localhost:8088/ari/`

Visiting `http://localhost:8088/` returns **Not Found** by design because the container exposes the ARI HTTP service, not a browser landing page. `http://localhost:8088/ari/` prompts for the credentials above and can be used to verify that ARI is reachable.

The default Aspire endpoint template uses `Local/{number}@default`, which loops the originated call back into the bundled demo dialplan. Numeric and E.164 destinations beginning with `+` are supported. The dialplan answers, plays a short generated tone sequence, and enters `Echo()` so the simulation does not depend on sound files that are absent from the container image. That local development path still **originates through the configured Stasis application**, so the same live channel remains under ARI control for hold, resume, mute, merge, inbound answer/reject, and Local-route blind transfer while the simulated media stays inside Asterisk.

### Two-party dashboard simulation

The **Asterisk Dashboard** now includes a **Two-party call simulation** form. It originates both selected endpoints into the dashboard's Stasis application, waits until both channels are under ARI control, creates a `mixing` bridge, and adds both channels to that bridge. Partial failures remove any channels or bridge that were already created.

The bundled defaults are:

| Party | Endpoint | Behavior |
| --- | --- | --- |
| A | `Local/2001@crestapps-simulation` | Answers and emits a repeating 440 Hz synthetic tone pattern. |
| B | `Local/2002@crestapps-simulation` | Answers and emits a repeating 659 Hz synthetic tone pattern. |

These endpoints create a real Asterisk media bridge and remain active for up to five minutes or until disconnected from the dashboard. Disconnect the channels to end either simulated party, and disconnect the bridge when the simulation is finished so the ARI bridge is removed. The live dashboard shows four Local channel legs, the two logical calls, and the shared mixing bridge. To test with two actual people or devices, replace the defaults with endpoints configured by your PBX, such as `PJSIP/1001` and `PJSIP/1002`. Both endpoints must answer before the bridge can be completed.

This simulation validates Asterisk channel origination, Stasis control, media bridging, live ARI events, and dashboard diagnostics. It does not run the automated AI conversation pipeline or create a Contact Center activity; use the separate **Inbound Simulator** for Contact Center routing and activity creation.

For inbound routing tests, use the **Asterisk Web** startup sample (`src\Startup\CrestApps.OrchardCore.Asterisk.Web`). It signs in to Orchard, originates one or more Asterisk channels directly into the configured Stasis application, waits for the matching `StasisStart` events, and then forwards the normalized `InboundVoiceEvent` requests to `POST /api/contact-center/voice/inbound` using the live Asterisk channel ids. The WebSocket reader queues events to concurrent dispatch workers, so one slow Orchard ingress request does not block later calls in a burst, and each forward has a bounded timeout. If the sample listener misses the matching event, the simulator briefly reconciles the originated channel through ARI and forwards it only when the authoritative channel snapshot confirms the configured Stasis application and exact simulation key. This prevents a transient listener gap from turning a successfully originated inbound call into a false HTTP 504 result. The sign-in check also recognizes tenant-prefixed login redirects and fails explicitly instead of continuing with an unauthenticated client. The sample exposes two pages: **Asterisk Dashboard** for live ARI telemetry and two-party bridge testing, and **Inbound Simulator** for Contact Center burst testing. The dashboard uses a dedicated `crestapps-dashboard` ARI application so it does not compete with the CMS `crestapps-telephony` event listener. It treats the Asterisk event stream as the primary update path: channel, bridge, state, dialplan, and variable events request an immediate snapshot, the server coalesces only a short event burst, reads independent ARI diagnostics endpoints concurrently, enriches active channels concurrently, and pushes the snapshot to connected browsers over SignalR. Dashboard ARI HTTP requests close their connections after each response to avoid stale pooled sockets after container restarts. The sample serves the SignalR client from the application instead of depending on an external CDN. While SignalR is connected, browser polling is stopped; a 15-second reconciliation poll starts only while SignalR is unavailable or reconnecting, then stops again after reconnection. Call and bridge count badges update from every snapshot, and the initial page snapshot uses the same web JSON naming policy as live hub messages. The dashboard groups raw local channel legs into logical calls so one Local call is easier to read, shows inferred call direction, surfaces provider-tracked hold and mute state, estimates party counts from bridge membership, and adds a disconnect action so you can simulate caller hangup from the PBX side. Its two-party form can use the bundled synthetic Local endpoints or real PBX endpoints and reports the created bridge and channel ids. The inbound simulator distinguishes calls that were immediately **Offered**, are **Waiting in queue**, or were **Not queued**, so `routed: false` is no longer presented as a rejection when the durable queue accepted the call. Live notifications now sit beside the raw ARI payload drill-down so the active call and bridge tables have more room. In the simulator, configured defaults populate the initial form, the configured provider identity is authoritative and read-only so ingress records match the live ARI listener, **To address** controls which Contact Center entry point or queue mapping the inbound call targets, and **Caller number seed** only changes the generated caller identities. The sample and Aspire host use the root Orchard URL and **Default Asterisk** provider by default; set **Orchard base URL** to the tenant URL, such as `https://localhost:5001/blog1`, when testing a named tenant.

The bundled local configuration is intended for development and connectivity testing. Production deployments should supply their own ARI credentials, dialplan, endpoints, and media/network configuration.

## Verifying local Asterisk activity

The bundled image does not expose a separate web dashboard for live calls. For local development, the easiest inspection points are the ARI endpoints and the container logs.

### Quick ARI checks

With the default Aspire credentials, these endpoints are useful:

- `GET http://localhost:8088/ari/asterisk/info` confirms that ARI is reachable and authenticated.
- `GET http://localhost:8088/ari/channels` lists the active channels that Asterisk currently knows about.
- `GET http://localhost:8088/ari/bridges` lists any active bridges, including merged calls.

If the soft phone dials successfully but `channels` stays empty while the call is active, the originate request is not reaching or being accepted by Asterisk. If the call appears in `channels` but a later action such as hold fails, inspect the Orchard application logs and the Asterisk container logs together to see the ARI response body and the PBX-side reason.

The Telephony SignalR hub now logs the start and completion of each soft-phone action with the authenticated user id, SignalR connection id, the provider call id, and any Contact Center correlation metadata that travelled with the call reference. When an Asterisk call-control action fails after a queued inbound offer is accepted, those hub entries make it easier to confirm whether Orchard is still acting on the original offered channel id or on the latest provider-side call identity.

The Asterisk module also registers a Contact Center voice-provider adapter. It returns the actual tenant or configuration-backed provider name used for the call, allowing outbound interactions and later ARI lifecycle events to correlate on the same provider identity.

The Asterisk development dashboard also logs the refresh source, refresh-lock wait, ARI snapshot duration, SignalR broadcast duration, and resulting channel/bridge/logical-call counts. Compare the timestamp of the incoming ARI event with these entries to distinguish delayed provider event delivery from slow snapshot acquisition, lock contention, or SignalR broadcast delay. The standalone sample builds its own local SignalR browser asset through `Assets.json`; if `/js/signalr.min.js` is missing, the dashboard cannot receive event pushes and intentionally falls back to its slower reconciliation poll.

### What to expect from the bundled local path

The local `Local/{number}@default` endpoint remains useful for keeping the media loop inside Asterisk during development, and the provider now originates those calls directly into the configured Stasis application so ARI events, dashboard telemetry, and the forwarded Contact Center ingress event all describe the same provider call id. Because the same Local loopback call leg stays inside Stasis, the Orchard soft phone can now expose advanced ARI-backed controls there instead of hiding them.
