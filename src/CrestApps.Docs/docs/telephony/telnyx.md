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
| **Telnyx SMS** | `CrestApps.OrchardCore.Telnyx.Sms` | Adds the Telnyx SMS/MMS provider and its signed inbound and delivery-receipt messaging webhook, so Telnyx numbers can send and receive text through the [Messaging Workspace](../omnichannel/messaging-workspace) or [SMS Automation](../omnichannel/sms). Category **Communication**. Depends on **Telnyx** and `OrchardCore.Sms`, so enabling it also enables the Telnyx voice provider. See [Telnyx SMS](#telnyx-sms). |
| **Telnyx AI Voice Agent** | `CrestApps.OrchardCore.Telnyx.AiVoice` | Adds an automated outbound AI voice agent: the **Phone** omnichannel processor dials a contact over Telnyx, converses using Telnyx text-to-speech and real-time transcription driven by an AI chat profile, and settles the omnichannel activity with a summary and disposition. Category **Contact Center**. Depends on **Telnyx**, the **AI** and **AI Chat** features, and **Omnichannel Management**. See [Telnyx AI Voice Agent](#telnyx-ai-voice-agent). |

## Dependencies

Enabling **Telnyx** automatically enables the **Telephony** and **WebSockets** features it depends on.
There is no separate "Telnyx Contact Center Voice" feature to enable: the Telnyx contact center voice
adapter is integration glue that activates automatically whenever the Telnyx provider and Contact Center
Voice are both enabled, so install and enable the Contact Center module to turn it on.

The two add-on features build on the Telnyx provider:

- **Telnyx SMS** depends on **Telnyx** and `OrchardCore.Sms`, so enabling it also enables the Telnyx voice
  provider.
- **Telnyx AI Voice Agent** depends on **Telnyx**, the **AI** and **AI Chat** features, and **Omnichannel
  Management**.

Webhook signature verification uses `BouncyCastle.Cryptography` (Ed25519), referenced by the Telnyx Core
library.

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
| **Browser WebRTC (advanced)** | After connecting | Credential lifetime, SIP signaling, codecs, and ICE (STUN/TURN) settings — see [Browser WebRTC settings](#browser-webrtc-settings) for each field. Defaults work out of the box. |
| **Text-to-speech voice** | After connecting | The voice the platform's spoken prompts use: entry-point phone menus, the voicemail greeting, queue announcements, a queue's callback offer and its confirmation, the AI voice agent's key collection, and the apology read to an orphaned call. `female`, `male`, or a Telnyx voice name in the form `Provider.Model.VoiceId`, such as `AWS.Polly.Joanna-Neural`. Blank means `female`. Telnyx requires a voice on every `speak` and `gather_using_speak` and refuses the command without one. |
| **Text-to-speech language** | After connecting | The language those prompts are spoken in, such as `en-US` or `es-ES`. Blank means `en-US`. Telnyx ignores it for `AWS.Polly` voices, which carry their own language. |

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

### Browser WebRTC settings

These live under **Browser WebRTC (advanced)** in the provider settings and are only shown after you connect.
Every one has a working default, so you can leave them empty unless your network or account requires otherwise.

| Setting | Default | Description |
| --- | --- | --- |
| **Credential lifetime (minutes)** | `180` | How long a browser SIP telephony credential is valid. Kept short so a lost session cannot register indefinitely; renewal is deferred during a live call so media is never dropped mid-call. |
| **SIP WebSocket URL** | `wss://sip.telnyx.com:7443` | The SIP-over-WebSocket signaling endpoint the browser registers against. Telnyx SIP-over-WSS is on **:7443** (not :443); a wrong port produces a `1006` socket close. |
| **SIP domain** | `sip.telnyx.com` | The SIP domain browser credentials register under. |
| **Preferred codecs** | Telnyx SDK default | Comma- or space-separated preferred WebRTC audio codecs advertised to the browser (for example `opus,g722,pcmu`). |
| **Soft phone region** | Automatic | The Telnyx location agents' soft phones connect to. Telnyx normally resolves this by looking up where the browser appears to be, and documents that the answer can be wrong (their example: a client in India routed to Frankfurt rather than Chennai). Set it when your team is in one place. Each agent can still pick a nearer location for themselves under the soft phone's gear icon, which is what a team split across continents needs. This moves the **signaling** edge; Telnyx does not document how a browser leg's media gateway is chosen, so confirm any improvement with the `rtt` figure in the diagnostics readout. |
| **ICE (STUN/TURN) URLs** | Telnyx SDK default | Comma- or space-separated STUN/TURN URLs advertised to the browser. See [STUN and TURN](#stun-and-turn) — the behavior here is Telnyx-specific. |
| **TURN username** | — | Optional static TURN username advertised alongside the ICE URLs. |
| **TURN credential** | — | Optional static TURN credential (password) advertised alongside the ICE URLs. Stored encrypted. |
| **ICE transport policy** | `all` | `all` uses direct/host, STUN, and TURN candidates; `relay` forces all media through TURN (useful for locked-down networks or TURN validation). |
| **Echo test destination** | — | Optional Telnyx number or SIP URI that echoes audio back, used by the diagnostics **Run audio test** action and the health canary to verify round-trip audio without a second person. When empty, the audio test is unavailable. |
| **REST API base URL** | `https://api.telnyx.com/v2/` | Optional override of the Telnyx REST API base address (internal/testing use). |

### STUN and TURN

ICE (NAT traversal) decides how browser media reaches Telnyx. It always tries a **direct/host** path first,
then **STUN** (server-reflexive), and relays through **TURN** only when a direct path is impossible (strict or
symmetric NAT, blocked UDP).

Telnyx behaves differently from a raw SIP provider here, because the **`@telnyx/webrtc` SDK ships with its own
default ICE servers — including Telnyx's TURN relays**. Because of that:

- **Leave *ICE URLs* empty (the default)** and every agent gets Telnyx's STUN **and** TURN out of the box —
  direct when possible, relayed when not.
- **A STUN-only *ICE URLs* value is deliberately ignored.** Supplying only STUN would *replace* the SDK's
  defaults and strip its TURN relays, leaving agents behind a restrictive NAT able to send but not receive
  audio (**one-way audio**). To prevent that, the module only overrides the SDK's ICE servers when your list
  **includes a TURN URL** (`turn:`/`turns:`); a STUN-only list is left unused and the SDK defaults stay in place.
- **To use your own TURN**, put a `turn:`/`turns:` URL in *ICE URLs* (with a **TURN username/credential** if
  your TURN server requires them). That set then replaces the SDK defaults.
- Set **ICE transport policy** to `relay` to force all media through TURN — useful on networks where host/STUN
  never works, or to validate your TURN server.

Unlike the Asterisk provider, Telnyx does **not** mint ephemeral coturn credentials; it uses the SDK's default
relays or the static TURN username/credential you supply.

## Outbound calls and caller id

Outbound calls are placed through `POST /v2/calls`. The caller id (`from`) is chosen **per call**: an agent
can present their assigned direct dial number (see [DID → agent routing](#did--agent-routing)), a campaign
number, or the tenant default. Present a number you **own on Telnyx** so calls earn STIR/SHAKEN
attestation and are not flagged as spam. Outbound dial requests carry a Telnyx `command_id` for
idempotency, so a retried request after a lost response is de-duplicated rather than placing a second call.

## Contact Center integration

Enable the **Contact Center Voice** feature (from the Contact Center module) alongside Telnyx to use Telnyx
as the phone provider for the Contact Center. There is no separate Telnyx feature to turn on — the Telnyx
contact center voice adapter activates automatically whenever both are enabled. It advertises the
`ServerSideAcd` delivery model and supports outbound dialing, agent connect (bridge), and call transfer.

- **Outbound / dialer** — the dialer routes outbound calls through the Voice Contact Center Call Router to
  Telnyx, which places the lead call.
- **Agent connect** — when an offered call is accepted, the provider originates the agent's browser SIP leg
  (`POST /v2/calls` to `sip:{agent}@sip.telnyx.com`, which the soft phone auto-answers) and bridges it to
  the caller (`POST /v2/calls/{caller}/actions/bridge`). Because the bridge is server-side, a live answer
  can be connected to a free agent — the basis of predictive/power dialing.
- **Inbound** — Telnyx posts signed call events to `/api/telnyx/webhook/call`; new inbound calls create a
  CRM activity and voice interaction and route through the matching entry point.

## Supervisor monitoring

A supervisor can **listen** to, **whisper** to (coach — only the agent hears them) or **barge** into (everyone hears
them) any Contact Center call on Telnyx from the **Live dashboard**, switch between the three without being rung again,
stop, and **take the call over**. The supervisor hears the call on their **own soft phone**, so it must be open and
registered (the same Telnyx browser credential an agent uses).

How a call is supervised, command by command:

1. **The supervisor's phone is rung.** The platform first tells the supervisor's phone, over the Contact Center hub, the
   one-off token of the leg it is about to ring, then originates a leg to the supervisor's registered credential
   (`POST /v2/calls` to `sip:{credential}@sip.telnyx.com`, chosen registered-first like any agent leg). The leg carries
   the token in its `client_state` (intent `cc-sv`) and in an `X-Monitor-Leg` SIP header. The phone answers that leg by
   itself and shows it as a **Monitoring** banner, never as a call; a monitor leg the phone did not ask for is hung up,
   and every other call rings as before.
2. **The call moves into a conference when the supervisor answers** — not before, so a supervisor who never picks up
   never disturbs the call. A Contact Center call runs as the agent's leg bridged to the caller's with
   `park_after_unbridge=self`. The platform creates a conference named `cc-sv-{caller leg}` from the caller's leg
   ([Create conference](https://developers.telnyx.com/api-reference/conference-commands/create-conference),
   `beep_enabled: never`, no hold audio), which takes the caller out of the bridge and parks the agent's leg; the agent's
   leg then [joins](https://developers.telnyx.com/api-reference/conference-commands/join-a-conference) it (`beep_enabled:
   never`, not `end_conference_on_exit`). Nobody is hung up and nobody hears a tone; the caller hears a moment of
   silence.
3. **The supervisor joins** ([Join a conference](https://developers.telnyx.com/api-reference/conference-commands/join-a-conference)).
   **Listen** joins as an ordinary participant with `mute: true` (heard by nobody); **Whisper** joins with
   `supervisor_role: whisper` and `whisper_call_control_ids: [agent leg]` (heard by the agent only); **Barge** with
   `supervisor_role: barge` (heard by both).
   :::warning Listen does not use `supervisor_role: monitor`
   Live, joining a supervisor with `monitor` left the caller and the agent unable to hear each other for as long as the
   supervisor listened (the agent's received audio dropped to silence while its packets kept arriving), and changing the
   role afterwards did not bring it back. A muted participant hears everything and is heard by nobody.
   :::
4. **Switching mode** changes the participant in place. To Listen: the supervisor is muted
   ([Mute participants](https://developers.telnyx.com/api-reference/conference-commands/mute-conference-participants),
   `POST /v2/conferences/{id}/actions/mute` naming only the supervisor's leg: an empty list mutes everybody), then their
   role is cleared (`supervisor_role: none`). To Whisper or Barge: the role is set first
   ([Update conference participant](https://developers.telnyx.com/api-reference/conference-commands/update-conference-participant),
   `POST /v2/conferences/{id}/actions/update`), then the supervisor is unmuted (`actions/unmute`), so they are never
   heard by the customer while coaching. A switch made while the phone is still answering updates the role the leg will
   join with. The supervisor's soft phone follows the mode too: its microphone is off while listening and on while
   coaching, joining, and after a takeover.
5. **Stop** hangs up only the supervisor's leg (marked detached in its client state), then — once no supervisor is left —
   takes the caller and the agent out of the conference and bridges them again exactly as before (`bridge` on the agent's
   leg with `park_after_unbridge=self`), so hold, transfer, consult and park keep working on the topology they expect.
   A supervisor hanging up their own phone does the same.
6. **Take over** switches the supervisor to `barge` (and unmutes them) first, so the customer is never alone, then hangs up the agent's leg
   with its client state marked detached, so its hang-up does not end the call. The interaction, the call's agent leg, its
   talk time and its after-call work move to the supervisor; the released agent goes to wrap-up (queue calls) or back to
   ready (direct calls). The supervisor's leg is then the call's agent leg: hanging it up ends the call. The released
   agent's soft phone is told the call has left it, so its Hang up can no longer end the call on the supervisor.

When the call ends, every supervisor leg still on it is hung up. A transfer or a consult releases the supervisors first,
since none of them can follow the call where it goes. While a sensitive-data capture has recording paused, no supervisor
can start, change or take over an engagement, and supervisors already listening are released.

The live dashboard lists the other interventions too — **End call**, **Transfer** (to a queue, an agent or a number),
**Record** on or off, the agent's state, and a **Message** to the agent's phone; see the
[Agent & Supervisor User Manual](../contact-center/user-manual.md).

### An agent's own phone calls

A number an agent dialed from the keypad and an internal extension call are phone calls of the agent's own, not
Contact Center interactions, but they can be monitored too. The dashboard shows such an agent as **On a call**, with the
number or colleague and how long the call has lasted, whatever their presence.

- **A number dialed from the keypad** runs on the server the way a Contact Center call does: the agent's leg bridged to
  the number's leg with `park_after_unbridge=self`, the number's leg written onto the agent's leg's client state. It is
  supervised the same way — the number's leg makes the conference `cc-sv-{number leg}`, the agent's leg joins it, and
  when the last supervisor leaves both leave it (which frees the number's leg from the conference it made) and are
  bridged again. **Take over** releases the agent's leg marked detached and points the number's leg at the supervisor's
  leg, so the number hanging up ends the supervisor's call, and the supervisor hanging up ends the number's.
- **An extension call** already runs in its own conference, `ext-{caller leg}`, made from the caller's own leg (see
  below). The supervisor joins that conference as it is, for either colleague — no new conference is made and nobody
  moves, so no leg is bound to a conference it has to outlive — and **Stop** only hangs the supervisor's leg up. It
  cannot be taken over, since each colleague's end hangs the other up.

Transfer and Record are the interaction's, so a phone call offers neither. A call the soft phone placed itself (when the
server could not bridge it) has no leg the platform controls, and still shows **Cannot be monitored**. Supervisors still
on a phone call are let go when it ends.

:::note Recordings
With a supervisor barging, the barge audio is part of what the caller's leg hears, so a call recording running on that
leg includes it; a monitoring or whispering supervisor is not heard by the caller and is not in the caller's recording.
:::

## Transfers

A Contact Center call on Telnyx is transferred by the Contact Center, not by Telnyx's own `transfer` action,
because an agent or a queue is not something Telnyx can dial. The soft phone's transfer panel calls the
Contact Center's transfer endpoints (see [How to transfer a call](../contact-center/user-manual.md)).

- **Blind to an agent or a queue** — the caller is first taken out of the agent's bridge and parked: a conference
  of their own is created from the caller's leg (`POST /v2/conferences`, name `cc-park-…`), which parks the agent's
  leg, and the caller leaves it again (`conferences/{id}/actions/leave`), which parks the caller. Only then is the
  transferring agent's leg taken off the call topology and hung up, while the caller hears the queue's hold music
  (`playback_start` on the parked caller's leg), or its treatment when nobody is free. A queue with no music, one
  whose treatment plays nothing, or a direct call with no queue at all plays the generated ringing tone instead
  (`playback_start` with `playback_content`, looped), because the caller was answered long ago and nothing on the
  network rings for them any more. If Telnyx refuses to park the caller, the transfer is refused and the call stays with the
  agent. The call is offered through the normal reservation pipeline, never back to the agent who transferred it:
  with nobody else free it waits in the queue. When the new agent accepts, their leg is joined to the caller
  exactly as for a new call, and the music stops.
- **Blind to an outside number** — `POST /v2/calls/{caller}/actions/transfer` from the platform's default
  caller id, then the call settles as *Transferred* and the agent is released.
- **From an entry point's phone menu to an outside number** — the same `transfer` action, presenting the number
  the caller dialled, with `target_leg_client_state` (intent `cc-xfer`, carrying the interaction id) on the leg it
  rings. Telnyx documents that a transfer which fails sends `call.hangup` for that leg and leaves the caller's leg
  up for further commands ([Transfer call](https://developers.telnyx.com/api-reference/call-commands/transfer-call)).
  The call is therefore settled as *Transferred* only when that leg reports `call.answered` or `call.bridged`; a
  `call.hangup` before that (`user_busy`, `timeout`, `call_rejected` and the like) puts the caller where the menu's
  fallback sends callers, or through to the entry point's own target. A leg cancelled because the caller hung up
  (`originator_cancel`) reroutes nobody.
- **Warm (consult)** — the caller is moved into a conference named `cc-consult-{consultId}`
  (`POST /v2/conferences`) and held there with the queue's hold music (`conferences/{id}/actions/hold`); the
  agent's leg joins the conference, and the destination is rung on a leg of its own (client state intent
  `cc-consult`, 30-second ring window). When it answers it joins the conference, and the agent and the
  destination talk while the caller holds. **Complete** unholds the caller; for an outside party both legs
  then leave the conference and are bridged directly. **Cancel** hangs up the destination and unholds the
  caller. The agent's own leg is hung up by the Contact Center only after it has recorded that the leg no
  longer carries the call, so its hangup is not mistaken for the agent ending the call.
- Agent legs are bridged with `park_after_unbridge: self`, so moving the caller into the consult conference
  parks the agent's leg instead of hanging it up. That setting protects only the leg the bridge was issued on:
  the caller's leg keeps Telnyx's default and is hung up when the bridge ends, so hanging up the agent's leg while
  the caller is still bridged to it ends the caller too. That is why a blind transfer parks the caller first.
  When a call ends, the Contact Center releases any agent leg still up.
- **To an extension** — an extension typed in the panel's extension mode is sent to the Contact Center as an
  `extension` target and resolved to the agent it rings, then transferred (or consulted) exactly as that agent.

A call that is not a Contact Center call is transferred with the provider's own `transfer` action. An extension
target (`TransferRequest.IsExtension`) is not dialed as digits: it is sent to the SIP address the colleague's
browser is registered on (the same address an extension call rings), and is refused as *not available right now*
while they have no live registration.

Merging calls creates a conference named `conf-{first call}` from the first call and joins the others. The merge
result carries that name as `conferenceName`; a later merge that names it (adding a call to the running
conference) finds the conference (`GET /v2/conferences?filter[name]=…`) and joins only the new calls, creating it
again only when it has ended.

### Numbers dialed from the keypad

A call the browser dials through its own Telnyx SDK goes out on the credential connection, which reports no Call
Control events, so the platform would have no `call_control_id` for it. Telnyx therefore advertises `BridgedDial`, and
the soft phone asks the platform to place a keypad dial instead, naming the credential it is registered on:

1. The provider rings that credential from the Call Control connection (`POST /v2/calls` to
   `sip:{credential}@sip.telnyx.com`, client state intent `ob-agent`), exactly as for an extension call; the phone answers
   its own leg without ringing. It rings only a credential of the dialing user that is live, registered, and whose client
   reported `bridged-dial-leg` -- the window that dialed, not whichever registered last.
2. When the browser answers, the number is dialed from the tenant's caller id through the outbound voice profile, and the
   number's `call_control_id` is written onto the agent leg (`PUT /v2/calls/{agent}/actions/client_state_update`).
3. When the number answers, the two are bridged on the agent leg with `park_after_unbridge: self`, as a Contact Center
   agent leg is.

The soft phone tracks the agent leg, but Telnyx's commands act on the leg they are given, so every command on such a call
is sent to the dialed party's leg, read back from the agent leg's client state (`GET /v2/calls/{agent}`):

- **Transfer** rings the destination first and hands the dialed party over only when it answers (see
  [Transferring a keypad call](#transferring-a-keypad-call)).
- **Merge** creates the conference from the first call's dialed party, which parks the agent's first leg. That leg joins
  the conference *without* `end_conference_on_exit`, so the agent can leave while the others stay connected. Every other
  call's dialed party joins too. The agent's other legs stay parked, one per participant, so hanging up a participant's
  line ends that participant. **Add to conference** joins only the new call's dialed party.
- **Nothing moves until everything can.** Every call is read before anything moves. A call whose party has not
  answered yet is refused up front with "A call you are merging has not been answered yet" (`ErrorCode` =
  `not-answered`). The orchestrator marks the agent's leg `PeerAnswered` (`j`) `false` when it dials the party and
  `true` when the party answers, because Telnyx refuses to join an unanswered call ("Call not answered yet", 90034).
- **A merge that fails part way is undone.** Each party leaves the conference again with `actions/leave`, a dialed party
  is re-attached and bridged back to its agent leg, and a colleague rejoins their extension call's conference. The
  conference is never ended, because ending it would hang up the party it was made from; empty, it expires.
- **A retry reuses the conference.** If an earlier attempt left a conference of the same name (create refused with
  90033, "Conference with given name already exists"), the retry finds it
  (`GET /v2/conferences?filter[name]=…&filter[status]=in_progress`) and uses it.
- **An extension call's own conference.** When the colleague answers, the orchestrator makes `ext-{agent leg}` *from
  the agent's own leg* and joins the colleague to it, with nobody joined with `end_conference_on_exit`. The agent's leg
  ending still hangs up the colleague, and the colleague's ending still hangs up the agent's leg, through the
  orchestrator.

  It used to be made from the colleague's leg, with the agent's leg joined with `end_conference_on_exit`. Telnyx hangs up
  the call a conference was created from (cause `time_limit`) when that conference is ended, wherever that call has gone
  since. Twice live, a colleague merged into another conference was hung up the moment the agent left and the extension
  call's conference ended: once after moving by a join, once after `actions/leave` and a join. That left the dialed party
  alone.
- **Merging an extension call** moves the colleague, never makes them the creator of a conference the agent's leaving
  could end:
  - A dialed number leads any merge it is part of, whatever order the phone names the calls in. The colleague joins its
    conference, and the agent's extension leg stays behind alone in its own conference, the way a dialed number's agent
    leg stays parked. When the agent leaves, that conference goes with the agent's leg and takes nobody else with it.
  - With no dialed number in the merge (two extension calls, or a Contact Center caller and an extension call), a new
    conference, `conf-{agent leg}`, is made from the first colleague, marked detached. The agent's extension leg leaves
    its own conference with `actions/leave` and joins the new one without `end_conference_on_exit`. The old conference,
    now empty, expires. A call that left the conference it created this way has been seen live to outlive it expiring;
    it is only a forced end that takes the creator down.
  - If Telnyx refuses to make the new conference, the extension call's own conference is the merge's.

  The agent leg of an extension call records the colleague's leg in its client state for this, and hanging that leg up
  releases the colleague as well.
- **Digits** are sent from the dialed party's leg (`send_dtmf` plays tones to the far end of the leg it is sent on).
- **Hang-up** from either side ends both: the agent's leg ending hangs up the dialed party (also while it is still
  ringing), and the dialed party ending -- answered or not -- hangs up the agent's leg. A leg the platform transferred or
  moved into a conference is marked detached in its client state and no longer takes the other with it.

If the credential cannot be rung (not live, not registered, a client that predates this) or Telnyx refuses the agent
leg outright, nothing was dialed: the provider logs a warning with the reason and answers `bridge-unavailable`, and the
phone dials the number from the browser as before. Such a call still cannot be transferred or merged, and the soft phone
says so in the transfer panel and disables its checkbox in the active-call list. An agent leg Telnyx did not confirm
(a timeout) is not dialed again from the browser.

The agent hears no ringback while the number rings: the agent's leg is answered and silent until the number answers,
as on an extension call.

### Leaving a conference and ending it for everyone

The soft phone's **Hang up** in a conference leaves it, and the others stay connected (see
[the soft phone's conference controls](index.md#keypad-recent-calls-and-extension-tabs)). For each of the agent's calls
in the conference, the hang-up is flagged `conferenceLeave`, and the provider handles it in three steps:

1. It reads the agent's leg (`GET /v2/calls/{agent leg}`).
2. It marks both the agent's leg and its party detached (`PUT /v2/calls/{id}/actions/client_state_update`). With both
   detached, the agent's leg ending no longer releases the party, and the party ending later no longer reaches back for
   the leg.
3. It hangs up the agent's leg alone (`POST /v2/calls/{agent leg}/actions/hangup`).

The agent's leg joined without `end_conference_on_exit`, so the conference runs on. Telnyx's
[Join a conference](https://developers.telnyx.com/api-reference/conference-commands/join-a-conference) defines that flag
as "the conference should end and all remaining participants be hung up after the participant leaves". Three cases
differ:

- If the agent's leg cannot be marked detached, nothing is hung up and the phone says it could not leave. Hung up still
  attached, the leg would disconnect its party.
- A call that is another party's own leg, such as a Contact Center caller's, is never hung up by leaving.
- With only one other party left, the phone does not leave. It hangs up as usual, so nobody is left alone in a
  conference.
- A party can drop between the phone deciding to leave and the leave arriving. So after hanging up the agent's leg, the
  provider reads the conference named by the call's `conferenceName` (`GET /v2/conferences/{id}/participants`). It counts
  the participants that are still up and are not one of the agent's own legs. If one party or fewer is left, it ends the
  conference rather than leave them on a silent line.

**End for all** sends the hang-up of the call the conference was made from flagged `conferenceEnd`, with its
`conferenceName`. The provider finds the conference by name (`GET /v2/conferences?filter[name]=…`) and ends it with
[End a conference](https://developers.telnyx.com/api-reference/conference-commands/end-a-conference)
(`POST /v2/conferences/{id}/actions/end`, "End a conference and terminate all active participants"). It then hangs up the
call. The phone hangs up every other call of the conference as well.

Once the agent has left, the remaining parties are in a conference with no end-on-exit participant. When one of them
hangs up, the last one stays until they hang up too.

### Transferring a keypad call

A transfer used to hand the dialed party straight to the destination with `actions/transfer`. The colleague it reached got
a leg the platform had not placed -- no client state naming the party, nothing in their call history -- so their phone
treated it as a call it had dialed itself and could not transfer it again, and a destination that did not answer left the
party alone on a parked line. A warm transfer was refused. Now the soft phone holds the call (its usual hold tone, sent on
that call's own leg), and the provider rings the destination on a leg of its own (client state intent `ob-xfer`), leaving
the party with the agent until that leg answers.

| Step | Telnyx command | Leg | Notes |
| --- | --- | --- | --- |
| Ring a colleague | `POST /v2/calls` | new transfer leg | To the colleague's registered SIP address, no outbound voice profile, `from_display_name` = the party's number, SIP header `X-Transfer-Leg`, `timeout_secs: 30`. Recorded in the colleague's history as a ringing incoming call. |
| Ring a number | `POST /v2/calls` | new transfer leg | Through the outbound voice profile. |
| Wait | `PUT /v2/calls/{id}/actions/client_state_update` | agent leg, party leg | Both name the ringing leg: the agent hanging up keeps the party for the transfer, and the party hanging up releases the ringing leg. |
| Hand over | `POST /v2/calls/{transfer leg}/actions/bridge` with `call_control_id: {party}` | transfer leg | A colleague's leg is bridged with `park_after_unbridge: self`, like the agent's was. Bridging takes the party out of the agent's bridge, which parks the agent's leg. |
| Rewrite | `client_state_update` | transfer leg, party leg | A colleague's leg becomes an `ob-agent` leg naming the party, exactly like a keypad dial, so it can be held, transferred and merged again. Two outside parties each name the other as the leg to release (`w`). |
| Release | `POST /v2/calls/{agent}/actions/hangup` (client state detached) | agent leg | Its hang-up no longer reaches back for the party. |

A **blind transfer** hands the party over as soon as the destination answers. If the destination declines, is busy or
does not answer within 30 seconds, the party is still with the agent, held; the agent resumes the call. If the agent has
hung up in the meantime, a colleague's caller is sent to that colleague's voicemail (as an unanswered extension call is),
and a caller whose outside destination did not answer is released.

A **warm transfer** rings the agent's own phone for a consult -- a second call, `ob-agent` with the call it consults about
(`o`) and that call's party (`y`) in its client state, which the phone answers without ringing, exactly like a keypad
dial. When it answers, a colleague is rung on a transfer leg and joined with the consult leg in a conference
`consult-{consult leg}` (two browser legs pass audio both ways only through Telnyx's mixer; neither joins with
`end_conference_on_exit`), and a number is dialed and bridged like a keypad dial. The consult is marked answered (`s`) when
the destination answers, and the transfer panel follows it through the hub's `GetConsult` (logged at `Debug`, since
it polls every couple of seconds):

- **Complete** (`CompleteConsult`, only once the destination answered): a colleague leaves the consult conference
  (`POST /v2/conferences/{id}/actions/leave`, which parks their leg) and is handed the party as above; an outside number is
  bridged to the party directly. Both of the agent's legs are then hung up, detached.
- **Cancel** (`CancelConsult`): the consult leg is hung up as not answered, which releases the destination, and the soft
  phone takes the call off hold.
- **The destination hangs up** during the consult: the consult leg is hung up and the call is the agent's again, still held.
- **The caller hangs up** during the consult: the agent's first leg goes with them, and the consult carries on as an
  ordinary call.
- **The agent hangs up the consult** (or closes the phone) after the destination answered: the transfer completes, as on
  most phone systems. Before the destination answered, the consult is cancelled; if the agent's first leg is gone too, its
  caller is released.

The consult rings a credential of the transferring user only, one that is live, registered and reported
`bridged-dial-leg`: the one the request names when it qualifies, else the one registered from the hub connection the
transfer came from, else the user's most recently registered one. A page opened before the soft phone named its
credential on a transfer, or one whose credential was renewed since, still gets its consult -- a colleague who took
over a handed-over call can transfer it warm -- and only a user with no such credential at all is told to transfer blind.

On the colleague's phone the transfer leg rings with **Answer** and **Decline** (no voicemail), showing the caller's number
and *Transferred by* and the agent's name; the phone recognizes it by its client state or the `X-Transfer-Leg` header and, once
answered, follows it as a call the platform tracks rather than one it dialed. Declining it is the transfer not being
answered. A call that is not a keypad dial (an extension call, a call the browser dialed itself) is transferred the old
way, and a call dialed from the browser still says in the transfer panel that it cannot be transferred.

## DID → agent routing

Inbound calls route by their dialed number through **entry points** (**Contact Center → Entry points**). An
entry point maps one or more DIDs and now chooses a **Route to** target:

- **Queue** (default) — the call is enqueued and offered to an available agent by the queue's routing
  strategy.
- **Specific agent** — the call is offered **directly** to the named agent (a personal line). When that
  agent is unavailable, the call falls back to the entry point's target queue for normal routing.

To give an agent a dedicated inbound line, create an entry point with the agent's DID, set **Route to** to
**Specific agent**, pick the agent, and set a **Target queue** as the fallback.

:::note
A **per-agent** outbound caller id is not yet wired up: the `AgentProfile.OutboundCallerId` field exists on
the model but has no editor UI and is not resolved on the dial path. Today outbound calls present the tenant
**Default outbound caller id** (or a per-call/campaign number), so a caller who dials back an agent's personal
DID still reaches them through that DID's entry point rather than because the agent dialed out from it.
:::

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
browser media adapter because Telnyx delivers this call's audio to the browser. Telnyx therefore never
reports a call muted, so the soft phone keeps the agent's mute itself: it lasts through state reports, refreshes,
hold and resume, a merge (a conference is muted or unmuted as a whole) and a replaced microphone, until the agent
unmutes or the call ends. The Contact Center voice
provider advertises dialer dialing, agent connect (bridge), call transfer, attended (consult) transfer, supervisor
monitor, whisper and barge (with mode switching and takeover, see [Supervisor monitoring](#supervisor-monitoring)),
and — with the Call Recording feature — recording.

## Telnyx AI Voice Agent

The **Telnyx AI Voice Agent** feature (`CrestApps.OrchardCore.Telnyx.AiVoice`) is the **voice** counterpart
to [SMS Automation](../omnichannel/sms): instead of a human agent or a text conversation, an **AI agent**
places an outbound call over Telnyx and talks to the contact. It registers the **Phone**-channel omnichannel
processor, so it is driven entirely by the [Omnichannel Management](../omnichannel/management) automated
activity pipeline — the same **subject flow → campaign → load inventory** model used by automated SMS.

How a call runs:

1. An automated **Phone** activity starts. The processor dials the contact over Telnyx (`POST /v2/calls`),
   using the activity's channel endpoint as the caller id when one is set, and tags the call's `client_state`
   with the activity so the webhook conversation loop can correlate later events back to it.
2. When the contact answers, the agent **speaks a greeting** and then runs a speak/listen loop: Telnyx
   **text-to-speech** renders each AI reply, and **real-time transcription** turns the contact's speech into
   text that is appended to the AI chat session. The selected AI chat profile generates the next turn.
3. When the conversation ends, the activity is **settled** with an AI-generated summary and a disposition,
   just like any other omnichannel activity.

How the disposition is chosen matters, because a disposition is not a label — it is what the subject's workflow
acts on next:

- The model is offered **the dispositions the subject's own actions are wired to**, so whatever it picks always
  has somewhere to lead. A subject with no actions configured falls back to every disposition on the site, so a
  call is never left unclassified.
- It is **held to that list**. A disposition it invents, or one belonging to a different subject, is discarded in
  favour of the first choice it was offered.
- A call **nobody spoke on** — rang out, declined, answered and hung up — is never sent for review. Asked to
  summarize an empty transcript the model writes a plausible account of a conversation that did not happen, and
  that account would be saved as fact; such a call is recorded as having produced no conversation.
- A call **handed to a live agent** is not concluded here at all. The model's leg ends the moment the caller is
  transferred, and the outcome belongs to the agent who took it.

The AI profile, speech-to-text deployment, text-to-speech deployment, voice, update permissions, and reply
delay are the **automated voice settings** configured on the subject flow (and overridable per activity
batch), resolved in order **activity batch → subject flow → global AI site settings**. See
[Subject Flow](../omnichannel/management#subject-flow) for where these fields live and how they cascade.

Bidirectional call audio is carried over a Telnyx **media-streaming WebSocket** (`api/telnyx/media/stream`,
G.711 mu-law) — the Telnyx equivalent of Asterisk's ARI External Media seam — so the agent both hears the
contact and injects its spoken audio on the same live leg. That endpoint must be reachable at the tenant's
public base URL, because Telnyx dials it after the streaming command starts.

## Telnyx SMS

The **Telnyx SMS** feature (`CrestApps.OrchardCore.Telnyx.Sms`) adds Telnyx as an Orchard Core **SMS
provider**, so Telnyx numbers can send and receive text messages through the
[Messaging Workspace](../omnichannel/messaging-workspace) (human two-way) and [SMS Automation](../omnichannel/sms)
(AI-driven). It is categorized under **Communication**, not Telephony. It depends on both **Telnyx** and
`OrchardCore.Sms`, so enabling Telnyx SMS also enables the Telnyx voice provider on the tenant.

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

