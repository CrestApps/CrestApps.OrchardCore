---
sidebar_label: Phone & SMS Setup
sidebar_position: 40
title: Phone and SMS Provider Setup
description: Connect Telnyx for calls, choose the default phone provider, and set up the SMS provider used by the messaging workspace and automated SMS.
---

Calls and texts go through a provider account. This page covers the settings an administrator changes once when the site is set up.

| | |
| --- | --- |
| **Menu** | Settings > Communication > Telephony, and Settings > Communication > SMS |
| **Permission** | Manage telephony settings; Manage settings for SMS |
| **Features** | Telephony and a provider feature such as **Telnyx** (`CrestApps.OrchardCore.Telnyx`) or **Asterisk**; **Telnyx SMS** or Orchard Core's Twilio SMS for texting |

<video controls preload="metadata" width="100%" aria-label="Screencast of the Telephony settings tabs and the SMS provider settings">
  <source src="/img/docs/um-telephony-settings.mp4" type="video/mp4" />
</video>

## Connect Telnyx for calls

1. Open **Settings > Communication > Telephony** and choose the **Telnyx** tab.
2. Tick **Enable Telnyx provider**, paste your Telnyx **API key**, and save.
3. Click **Connect Telnyx**. The site creates the Telnyx resources it needs and shows a green *Connected* panel with their ids.
4. Enter the **Default outbound caller id**, the Telnyx number customers see.
5. Paste the **Webhook public key** from the Telnyx portal. Until it is set, every call event from Telnyx is rejected.
6. Save.

The **Advanced - browser WebRTC (optional)** section holds settings you rarely change: credential lifetime, an audio test destination, what to do with calls that have no local record after a restart, answering machine detection, the text-to-speech voice and language, SIP and ICE (STUN/TURN) settings, codecs, the soft phone region, and an API base URL override. The [Telnyx reference](../telephony/telnyx.md) explains each one.

**Disconnect** removes the resources Connect created.

## Choose the default provider and soft phone options

On the **Soft Phone** tab of the same page, pick the **Default telephony provider** and set the soft phone options described in [Soft phone](soft-phone.md#administrator-options).

## Set up SMS

1. Open **Settings > Communication > SMS**.
2. On the **Providers** tab, pick the **Default SMS provider**. Then fill in that provider's own tab (**Twilio** or **Telnyx**). For Telnyx: tick **Enable the Telnyx SMS provider**, enter the **API key**, the optional **Messaging profile id**, and the **Public key**, and copy the read-only **Webhook URL** into your Telnyx messaging profile (as both the webhook and the failover URL).
3. Save.
4. Add each SMS number under [Channel endpoints](channel-endpoints.md).

Twilio inbound texts use the webhook `/api/twilio/webhook/sms`, which is provided by the **SMS Omnichannel Automation** feature.
