---
sidebar_label: Phone & SMS Setup
sidebar_position: 40
title: Phone and SMS Provider Setup
description: Connect Telnyx or Asterisk for calls, choose the default phone provider, and set up the SMS provider used by the messaging workspace and automated SMS.
technical_manual:
  - telephony/telnyx
  - telephony/asterisk
  - telephony/index
---

Calls and texts go through a provider account. This page covers the settings an administrator changes once when the site is set up, and again only when the account changes.

| | |
| --- | --- |
| **Menu** | Settings > Communication > Telephony, and Settings > Communication > SMS |
| **Permission** | Manage telephony settings; Manage SMS Settings |
| **Features** | Telephony and a provider feature such as **Telnyx** or **Asterisk**; **Telnyx SMS** or Orchard Core's Twilio SMS for texting |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of the Telephony settings tabs and the SMS provider settings">
  <source src="/img/docs/um-telephony-settings.mp4" type="video/mp4" />
</video>

Each provider you turn on adds its own tab to **Settings > Communication > Telephony**. The **Soft Phone** tab holds the settings that apply whichever provider you use.

## Connect Telnyx for calls

Before you start, create an API key in the Telnyx portal under **Account > Keys & Credentials > API Keys** (a V2 key). Sign in to Telnyx as an account **Owner** or **Admin** when you create it: a key made by a restricted member can read the account but cannot create what **Connect Telnyx** needs, and connecting fails with *Not authorized*.

1. Open **Settings > Communication > Telephony** and choose the **Telnyx** tab.
2. Tick **Enable Telnyx provider**, paste the key into **API key**, and save. The **Connect Telnyx** button appears once the key is stored.
3. Click **Connect Telnyx**. The site creates the Telnyx resources it needs and shows a green **Connected to Telnyx** panel with their ids. The other settings appear once you are connected.
4. Pick the **Default outbound caller id**: the Telnyx number customers see when a call does not bring its own. The list holds your [Omnichannel Addresses](channel-endpoints.md) used for **Voice calls**; Connect suggests one.
5. Paste the **Webhook public key**. You find it in the Telnyx portal under **Account > Keys & Credentials**, as the **Public Key**. Until it is set, a yellow **Add the webhook public key to finish setup** panel reminds you, and every call event from Telnyx is rejected: calls do not connect and inbound calls are not routed.
6. Save.

| Field | What it does |
| --- | --- |
| **Enable Telnyx provider** | Turns Telnyx on and makes it selectable as the default telephony provider. When no default is chosen yet, Telnyx becomes the default. |
| **API key** | The Telnyx V2 API key. It is stored encrypted; leave the box empty to keep the stored key. |
| **Default outbound caller id** | The number shown on outbound calls. A dialer profile's **Caller ID** replaces it for the calls that profile places, and a caller sent on from a phone menu to an outside number is shown as the number they dialed. Use a number you own on Telnyx, so calls are not flagged as spam. |
| **Webhook public key** | The key that proves call events really come from Telnyx. It is not a secret you create; you copy it from the portal. Stored encrypted. |
| **Webhook endpoint** | The address Connect set on your Telnyx account, shown for reference. |

**Noise suppression** asks Telnyx to clean background sound, such as colleagues talking nearby, out of your agents' calls. Pick **Krisp** for a busy floor, then tick whether to clean the agent's voice (what callers hear, on by default), the caller's voice (what the agent hears), or both. It is a Telnyx beta feature billed for each voice cleaned; see [Background noise suppression](../telephony/telnyx.md#background-noise-suppression).

The **Advanced — browser WebRTC (optional)** section holds settings you rarely change: **Credential lifetime (minutes)**, **Audio test destination** (a number that plays your voice back, used by the soft phone's **Run audio test**), **Calls with no local record** (what to do with a call left over from a restart: **Report** or **End call**), **Answering machine detection** (**Premium**, **Standard** or **Off**; Telnyx bills the first two per call), **Text-to-speech voice** and **Text-to-speech language** for spoken prompts, the SIP and ICE (STUN/TURN) connection settings, **Preferred audio codecs**, **Soft phone region** and an API address override. Every one has a working default. Change them only when Telnyx support tells you to, or when your network needs a relay server. The [Telnyx reference](../telephony/telnyx.md#browser-webrtc-settings) explains each one.

**Disconnect** deletes the resources Connect created on Telnyx, after you confirm. To start over, click **Disconnect** and then **Connect Telnyx** again.

## Connect Asterisk for calls

Use Asterisk when your company runs its own Asterisk phone system. The values come from whoever looks after that system.

1. Open **Settings > Communication > Telephony** and choose the **Asterisk** tab.
2. Tick **Enable Asterisk provider**. The rest of the fields appear.
3. Enter the **ARI base URL**, **ARI user name**, **ARI password** and **Stasis application name**. The application name is required; when several sites share one Asterisk server, each site needs its own name.
4. Optionally fill in the **Endpoint template**, **Outbound caller id**, **Dial timeout (seconds)** and the voicemail fields.
5. For call audio in the browser, fill in the browser audio fields (**SIP WebSocket URL**, **SIP domain**, the ICE and TURN fields, **Browser audio codecs** and the PJSIP fields).
6. Save.

The [Asterisk reference](../telephony/asterisk.md#tenant-configured-asterisk-settings) explains each field. When no default provider is chosen yet, Asterisk becomes the default. Your IT team can also set up a shared Asterisk connection for every site; it then appears as **Default Asterisk** in the **Default telephony provider** list, with no tab of its own.

## Choose the default provider and soft phone options

On the **Soft Phone** tab of the same page, pick the **Default telephony provider** and set the soft phone options described in [Soft phone](soft-phone.md#administrator-options).

When you turn off the provider that is the default, the default is cleared and the soft phone stops working until you choose another one.

## Set up SMS

1. Open **Settings > Communication > SMS**.
2. On the **Providers** tab, pick the **Default SMS provider**.
3. Fill in that provider's own tab, as described below.
4. Save.
5. Add each SMS number under [Omnichannel Addresses](channel-endpoints.md).

### Telnyx

| Field | What it does |
| --- | --- |
| **Enable the Telnyx SMS provider** | Makes Telnyx selectable as the SMS provider. |
| **API key** | The Telnyx V2 API key. |
| **Messaging profile id** | Optional. Sends through one Telnyx messaging profile; leave it empty to send using only the From number. |
| **Public Key** | The Telnyx public key that proves incoming texts and delivery receipts really come from Telnyx. |
| **Webhook URL** | Read-only. In the Telnyx portal, create a messaging profile under **Messaging Suite > Programmable Messaging**, and set both its **Webhook URL** and **Webhook Failover URL** to this address. |

The tab has buttons that open the right Telnyx portal pages. If your site sits behind a proxy and the **Webhook URL** shows the wrong address, ask your IT team to fix the site's base URL.

### Twilio

Fill in the Twilio account details on the **Twilio** tab. To receive texts, copy the tab's **Webhook URL**. In the Twilio console, open your phone number and, under **Messaging**, set **A message comes in** to **Webhook**, **HTTP POST**, and that address. The **Webhook URL** is shown when the **SMS Omnichannel Automation** or **SMS Messaging Channel** feature is on. The [Omnichannel SMS reference](../omnichannel/sms.md) lists the webhook address.
