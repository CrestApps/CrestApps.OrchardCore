---
sidebar_label: Extension Dialing
sidebar_position: 5
title: Internal Extension Dialing
description: Call another on-platform user by extension, and add an extension into a call as a conference participant, through the provider-agnostic capability model.
---

# Internal Extension Dialing

Extension dialing lets one on-platform user call another by a short **extension number** (for example
`1001`) instead of a phone number, connecting two soft phones without routing through the PSTN. It also
supports adding an extension into an active call as a **conference participant**.

Like every other soft-phone operation, extension dialing is **capability-gated**: a provider offers it only
when it can connect two of its own registered endpoints. A provider that cannot does not advertise the
capability, the soft phone hides the control, and the server fails closed.

## The extension registry

Extensions are a provider-neutral, tenant-scoped map from a dialed **number** to an Orchard **user**. This is
the system of record: providers translate the resolved user into their own live endpoint, so the same
extension keeps working if you switch providers.

Manage extensions under **Communication → Extensions** (requires the **Manage telephony extensions**
permission). Each extension has:

| Field | Description |
| --- | --- |
| **Name** | A label for the entry. |
| **Extension number** | The number an agent dials, unique per tenant (for example `1001`). |
| **User** | The Orchard user the extension rings. |
| **Display name** | A name for the extension itself (for example `Front desk`). Leave it empty, or equal to the user name, to show the user's own display name. |
| **Enabled** | A disabled extension is not dialable and is skipped by the resolver. |

## Who an extension rings, by name

Wherever the soft phone shows an extension it also shows who it rings: the transfer panel offers
**Transfer to extension 2 · Jane Doe**, the keypad names the person while an extension is typed, a call to an
extension is shown as **Jane Doe · ext 2**, and so are extension calls in **Recent**. A colleague whose extension
you ring sees your name on their ringing call.

The name is resolved on the server, in this order:

1. The extension's own **Display name**, when it is set to something other than the user name.
2. The user's display name, as the site shows users (the CrestApps Users display name settings, for example first
   and last name). Without the CrestApps Users feature this is the user name.
3. The user name.
4. The bare extension number.

The soft phone reads every extension's name in one request when it connects (the `GetExtensionDirectory` hub
method) and keeps the list, reading it again only after five minutes; nothing is looked up per keystroke. When the
provider has no directory of its own (Telnyx), the transfer panel lists these extensions, and picking one
transfers to it as an extension.

The transfer panel's field searches that list by name as well as by number, in either of its modes: type
**Test** and the list narrows to **Test 2 · Ext 2**; when a name narrows it to one person, **Enter** transfers to
them. Digits still offer **Transfer to extension 2 · Test 2**. In the phone-number mode the field keeps its
country flag for numbers; in the extension mode it is a plain search box.

## Your own extension

The directory a user reads leaves out their own extensions, and tells the soft phone which they are. Calling or
transferring to your own extension would only ring the phone you are using, so it is refused with
**That's your own extension.** — by the soft phone before anything is sent, and by the server for an extension
call (`DialExtension`), a blind or warm transfer through the provider, and an extension typed into a Contact Center
call's transfer panel. A Contact Center agent cannot pick themselves either: the agent list leaves them out, and
the Contact Center refuses a transfer to the agent already on the call.

## Placing an extension call

On the soft phone, toggle **Dial extension**, enter the extension, and dial. The dialed value is sent
verbatim (it is not canonicalized to E.164) and **skips outbound compliance screening**, because an internal
extension is not consumer outreach.

The call is always resolved and bridged **server-side** — the browser cannot originate directly to a
colleague because it does not know the target's ephemeral provider endpoint:

```text
Soft phone (Dial extension) ──► TelephonyHub.DialExtension
                                     │
                                     ▼
        ITelephonyService resolves the extension → target user  (fails closed if unknown/disabled)
                                     │
                                     ▼
        ITelephonyExtensionDialProvider  (capability + contract checked together)
                                     │
                                     ▼
        Provider rings the caller, dials the target endpoint, and bridges the two legs
```

## Voicemail on no answer

If the target does not answer within the ring window, the caller is routed to the target user's voicemail
instead of the call simply ending. The recording is ingested into that user's existing voicemail inbox through
the standard saved-recording pipeline, so it appears on their soft-phone **Voicemail** tab like any other
message. A caller who hangs up before the target answers, or a normally completed call, is not treated as a
no-answer and does not leave a message.

## Adding an extension to a conference

While on a call, an extension can be added as a conference participant through
`AddExtensionToConference`. The provider rings the resolved target and joins their leg to the existing
conversation. This complements the existing **merge** operation, which conferences calls that are already
active.

## The provider contract

A provider implements `ITelephonyExtensionDialProvider` and advertises the matching capabilities:

| Operation | Capability | Method |
| --- | --- | --- |
| Call an extension | `ExtensionDial` | `DialExtensionAsync` |
| Add an extension to a conference | `ExtensionConference` | `AddExtensionToConferenceAsync` |

`TelephonyCapabilityContracts` maps both capabilities to `ITelephonyExtensionDialProvider`, so advertising a
capability without implementing the contract — or implementing the contract without advertising the
capability — both fail closed, exactly like every other telephony operation.

The telephony service resolves the dialed extension to a **target user id** before invoking the provider; the
provider is responsible only for turning that user into its own live endpoint and connecting the call.

## Provider support

| Provider | Extension dialing | Notes |
| --- | --- | --- |
| **Telnyx** | ✅ Supported | Both legs are Telnyx SIP-over-WebSocket registrations. Extension dialing reuses the same two-leg originate-and-bridge orchestration as browser-audio outbound calls, with a SIP target on both sides. Conference-add originates the participant leg and joins it to a Telnyx conference formed from the active call. |
| **Asterisk** | Not yet | Connecting two just-in-time WebRTC PJSIP browser endpoints over ARI is part of the same server-side originate/bridge wave that gates browser-agent connection, so Asterisk does not advertise the capability yet and the control is hidden for it. |
| **Dialpad** | Not applicable | Dialpad has no in-browser audio; it is a control surface for the Dialpad app. |

## Related guides

- [Telephony soft phone](./index.md)
- [Telnyx](./telnyx.md)
- [Custom telephony and Contact Center providers](./custom-providers.md)
