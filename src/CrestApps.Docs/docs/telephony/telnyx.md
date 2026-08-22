---
sidebar_label: Telnyx
sidebar_position: 2
title: Telnyx Voice Provider
description: Integrate the Telnyx platform as a browser WebRTC telephony provider and Contact Center voice provider.
---

| | |
| --- | --- |
| **Feature Name** | Telnyx |
| **Feature ID** | `CrestApps.OrchardCore.Telnyx` |

The **Telnyx** module integrates the [Telnyx](https://telnyx.com/) voice platform as a provider for the
[Telephony](./) soft phone and the [Contact Center](../contact-center/index.md). Telnyx exposes a
SIP-over-WebSocket registrar and a server-side Call Control API, so — unlike a click-to-call provider —
the browser soft phone **carries the call audio itself** (WebRTC) and the Contact Center can **bridge live
calls to agents server-side** (`ServerSideAcd`), which is what makes true power dialing possible.

## Features

| Feature | Feature ID | Purpose |
| --- | --- | --- |
| **Telnyx** | `CrestApps.OrchardCore.Telnyx` | Provides the Telnyx telephony provider, the browser WebRTC soft phone, and signed call-event webhooks. Depends on Telephony. When Contact Center Voice is also enabled, the Telnyx contact center voice adapter (outbound contact center calls, bridging live calls to agents via `ServerSideAcd`, and their real-time call events) activates automatically — it is integration glue, not a separately selectable feature. |
| **Telnyx SMS** | `CrestApps.OrchardCore.Telnyx.Sms` | Adds the Telnyx SMS/MMS provider and its signed inbound and delivery-receipt messaging webhook, so Telnyx numbers can send and receive text through the [SMS Workspace](../omnichannel/sms-workspace) or [SMS Automation](../omnichannel/sms). Category **Communication**. Depends on `OrchardCore.Sms`. See [Telnyx SMS](#telnyx-sms). |

## Dependencies

Enabling **Telnyx** automatically enables the **Telephony** feature it depends on. Install the Contact
Center module before enabling **Telnyx Contact Center Voice**; its manifest dependency then enables
Contact Center Voice for that tenant. Webhook signature verification uses `BouncyCastle.Cryptography`
(Ed25519), referenced by the Telnyx Core library.

## Authentication

Telnyx authenticates every REST call with a single tenant **API key** (v2). There is no per-user OAuth:
one account credential places and controls calls for the tenant, and each agent's browser registers with a
short-lived SIP credential minted on demand.

## Getting your Telnyx credentials

You only need an **API key**. The **Connect Telnyx** button then uses it to create and wire up everything
else automatically — a Telnyx API key carries full account access.

1. **Create an API key** — in the [Telnyx Mission Control portal](https://portal.telnyx.com/), go to
   **Account → Keys & Credentials → API Keys** and click **Create API key** (a V2 key). Paste it into
   **API key** and **Save**. ([API Keys](https://portal.telnyx.com/#/app/api-keys))

   :::warning
   Create the key while signed in as an account **Owner or Admin**. A Telnyx API key inherits the
   permissions of the member who creates it, so a **restricted member's key can read your account but
   cannot create** the Call Control application, SIP connection, and outbound voice profile that Connect
   needs — the create call fails with `Not authorized` (Telnyx code 10006). If you hit that, have an
   account owner grant your member the Voice/Connections permissions (Account → Member Permissions) or
   create the key from an owner account.
   :::
2. **Click Connect Telnyx** — the app uses the API key to **find-or-create** (idempotently):
   - a **Call Control application** with the webhook URL set to `https://<tenant-host>/api/telnyx/webhook/call`;
   - a **Credential SIP connection** (what mints the browser soft phone's SIP credentials);
   - an **outbound voice profile** bound to both connections;
   - and it discovers your numbers, suggests a **caller id**, and assigns an unassigned number to the Call
     Control application so inbound calls reach the webhook.

   The resolved ids are written into the settings automatically and shown read-only.
3. **Paste the webhook public key** — after connecting, copy the **Public Key** from the same
   **Keys & Credentials** page into **Webhook public key** and **Save**. Telnyx signs every webhook with it
   (Ed25519); it is not a secret you generate, and there is no API to fetch it, so this one value is pasted
   by hand. Inbound call events are rejected until it is set.
4. **Confirm the caller id** — Connect suggests a number; change **Default outbound caller id** if you want
   a different one.

Use **Disconnect** to delete the resources Connect created and clear the ids.

:::note
**Manual setup (advanced).** You can skip Connect and create the Call Control application, Credential SIP
connection, outbound voice profile, and number assignment yourself in the portal, then enter the ids under
the advanced settings. Connect is simply the automated version of those same steps.
:::

## Configuration

Configure Telnyx on the **Telnyx** tab under **Settings → Communication → Telephony**. You need the
`Manage telephony settings` permission.

Before connecting, the screen shows only the fields you need — **Enable**, **API key**, and the **Connect
Telnyx** button. The rest appear after you connect:

| Setting | When shown | Description |
| --- | --- | --- |
| **Enable Telnyx provider** | Always | Turns the provider on and makes it selectable as the default provider. |
| **API key** | Always | The Telnyx v2 API key, presented as a bearer token on every REST call. Stored encrypted. Leave blank to keep the stored value. |
| **Connect Telnyx** / **Disconnect** | Before / after connecting | Auto-provisions (or removes) the Call Control application, Credential SIP connection, and outbound voice profile using the API key. |
| **Call Control / SIP / outbound voice profile ids** | After connecting | Managed by Connect and shown read-only. |
| **Default outbound caller id** | After connecting | The E.164 number presented on outbound calls when no per-agent or per-request caller id is supplied. Connect suggests one; editable. Must be a Telnyx-owned number for STIR/SHAKEN attestation. |
| **Webhook public key** | After connecting | The Telnyx account **Ed25519 public key** (from the portal) used to verify signed webhooks. Stored encrypted. Inbound webhooks are rejected when empty. |
| **Browser WebRTC (advanced)** | After connecting | Credential lifetime, SIP WebSocket URL (`wss://sip.telnyx.com:7443`), SIP domain (`sip.telnyx.com`), preferred codecs, ICE (STUN/TURN) URLs, TURN username/credential, ICE transport policy, and an optional REST API base URL override. Defaults work out of the box. |

When you enable Telnyx and no default provider is set yet, Telnyx becomes the default automatically. When
you disable Telnyx while it is the default provider, the default is cleared and the soft phone is disabled
until another provider is selected.

## Webhook signing

Telnyx signs every webhook with **Ed25519** over `{timestamp}|{raw_body}`, sending the base64 signature in
the `telnyx-signature-ed25519` header and the Unix-second timestamp in the `telnyx-timestamp` header. This
module verifies each delivery against the account **public key** configured above — it does **not** generate
a shared secret the way some providers do; you paste the Telnyx public key from the portal.

Register the webhook URL on the Telnyx Call Control application (portal or Admin API):

```text
https://<tenant-host>/api/telnyx/webhook/call
```

The endpoint validates the signature, rejects unsigned or stale deliveries, enforces a 1 MiB body limit,
and accepts state-changing processing through the durable provider webhook inbox. A configured public key
that cannot be decrypted returns a service-unavailable response instead of downgrading to unsigned
acceptance.

## Browser WebRTC soft phone

Telnyx is a **browser-audio** provider (`AudioCapabilities = Browser`). When an agent opens the soft phone,
the server mints a short-lived Telnyx **telephony credential** (`POST /v2/telephony_credentials`) bound to
the SIP connection and returns the SIP username/password to the browser, which logs in to Telnyx's WebRTC
gateway through the **Telnyx WebRTC SDK** (`@telnyx/webrtc`, vendored as the `telnyx-webrtc` media adapter).
The SDK owns the peer connection, media, and codec/SDP negotiation end to end, so this path needs no
browser-side SDP workarounds. Only the provider that is actually active loads its media library — Telnyx
loads the Telnyx SDK, a SIP provider such as Asterisk loads SIP.js, and neither downloads the other.
Credentials are capped per user, expire on their own, and are deleted at Telnyx on sign-out. The mapping of
user → SIP username is stored durably so the Contact Center can resolve the agent's live endpoint when
bridging a call.

## Outbound calls and caller id

Outbound calls are placed through `POST /v2/calls`. The caller id (`from`) is chosen **per call**: an agent
can present their assigned direct dial number (see [DID → agent routing](#did--agent-routing)), a campaign
number, or the tenant default. Present a number you **own on Telnyx** so calls earn STIR/SHAKEN
attestation and are not flagged as spam. Outbound dial requests carry a Telnyx `command_id` for
idempotency, so a retried request after a lost response is de-duplicated rather than placing a second call.

## Contact Center integration

Enable **Telnyx Contact Center Voice** to use Telnyx as the phone provider for the Contact Center. It
advertises the `ServerSideAcd` delivery model and supports outbound dialing, agent connect (bridge), and
call transfer.

- **Outbound / dialer** — the dialer routes outbound calls through the Voice Contact Center Call Router to
  Telnyx, which places the lead call.
- **Agent connect** — when an offered call is accepted, the provider originates the agent's browser SIP leg
  (`POST /v2/calls` to `sip:{agent}@sip.telnyx.com`, which the soft phone auto-answers) and bridges it to
  the caller (`POST /v2/calls/{caller}/actions/bridge`). Because the bridge is server-side, a live answer
  can be connected to a free agent — the basis of predictive/power dialing.
- **Inbound** — Telnyx posts signed call events to `/api/telnyx/webhook/call`; new inbound calls create a
  CRM activity and voice interaction and route through the matching entry point.

## DID → agent routing

Inbound calls route by their dialed number through **entry points** (**Contact Center → Entry points**). An
entry point maps one or more DIDs and now chooses a **Route to** target:

- **Queue** (default) — the call is enqueued and offered to an available agent by the queue's routing
  strategy.
- **Specific agent** — the call is offered **directly** to the named agent (a personal line). When that
  agent is unavailable, the call falls back to the entry point's target queue for normal routing.

To give an agent a dedicated inbound line, create an entry point with the agent's DID, set **Route to** to
**Specific agent**, pick the agent, and set a **Target queue** as the fallback. For outbound, set the same
number as the agent's **Outbound caller id** on their agent profile so callbacks reach them.

## Call recording

When the **Contact Center Call Recording** feature is enabled, Telnyx recordings are captured *and* stored on
your platform, encrypted at rest — not left only in Telnyx's cloud.

- **Recording control** — starting, pausing, resuming, and stopping a recording is driven by Contact Center
  recording governance and executed on the call through Telnyx Call Control (`record_start`, `record_pause`,
  `record_resume`, `record_stop`). The recording carries the interaction as `client_state` so the finished
  recording can be traced back to the conversation that owns it.
- **Secure ingestion** — Telnyx assigns a recording id only once the recording is saved, so when the
  `call.recording.saved` webhook arrives the platform stamps the interaction with the recording's retrieval
  handle and enqueues a durable ingest job. A background sweep resolves the recording's current download URL
  from the Telnyx recordings API, streams the media into the **encrypted media store**, and then **deletes the
  Telnyx-hosted copy** so no plaintext recording lingers off-platform. Transient failures are retried with
  exponential back-off and dead-lettered after the attempt budget, so a recording is never silently lost.
- **Right to erasure** — an interaction whose recording has been erased is never (re-)ingested; any media
  already written for it is removed, so a late ingest can never resurrect deleted media.

## Capabilities

The Telnyx telephony provider advertises dialing, hang up, hold, resume, mute, blind and attended transfer,
merge (conference), sending DTMF digits, and receiving inbound calls. Hold and mute are executed by the
browser media adapter because Telnyx delivers this call's audio to the browser. The Contact Center voice
provider advertises dialer dialing, agent connect (bridge), call transfer, and — with the Call Recording
feature — recording.

## Telnyx SMS

The **Telnyx SMS** feature (`CrestApps.OrchardCore.Telnyx.Sms`) adds Telnyx as an Orchard Core **SMS
provider**, so Telnyx numbers can send and receive text messages through the
[SMS Workspace](../omnichannel/sms-workspace) (human two-way) and [SMS Automation](../omnichannel/sms)
(AI-driven). It is categorized under **Communication**, not Telephony, and can be enabled independently of
the Telnyx voice soft phone — it only depends on `OrchardCore.Sms`.

It follows the same two-provider pattern as Orchard Core's built-in Twilio provider:

- an **appsettings-driven** provider, enabled automatically when configured; and
- a **UI-driven** provider, configured and validated from the SMS settings screen.

Both resolve their live values through `IOptionsMonitor`, so a settings change takes effect without a
manual restart.

### Configuration from appsettings

Configure the provider under the `OrchardCore_Sms_Telnyx` section (mirroring Orchard Core's
`OrchardCore_Sms_Twilio` convention):

```json
{
  "OrchardCore_Sms_Telnyx": {
    "IsEnabled": true,
    "ApiKey": "KEY0123...",
    "MessagingProfileId": "40017...",
    "WebhookPublicKey": "base64-ed25519-public-key"
  }
}
```

The provider is enabled only when it is configured with an API key.

### Configuration from the UI

Alternatively, go to **Settings → SMS** (`/Admin/Settings/sms`), open the **Telnyx** settings, tick
**Enable**, and enter the API key, messaging profile id, and webhook public key. Secrets are protected at
rest with the data-protection provider. Values entered in the UI take precedence over appsettings.

Set the tenant **default provider** on the same SMS settings screen if Telnyx should be the default sender.

### Webhook

Point your Telnyx **messaging profile** webhook at:

```
https://<your-site>/api/telnyx/webhook/sms
```

The endpoint verifies the Telnyx **Ed25519** signature (using the configured webhook public key), then
routes inbound `message.received` events onto the shared Omnichannel `SmsReceived` bus and applies
`message.finalized` delivery receipts to the sent message. Requests are rejected when the provider is
disabled, unsigned, or unverifiable.

