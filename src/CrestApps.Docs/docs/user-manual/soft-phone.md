---
sidebar_label: Soft Phone
sidebar_position: 41
title: The Soft Phone
description: A tour of the soft phone - keypad, recent calls, voicemail, the Work tab where agents sign in, presence, audio settings and diagnostics.
---

The **soft phone** makes and takes calls in the browser. Agents usually run it in the [browser extension or the Windows app](phone-apps.md), which open the site's `/softphone` page in a small window of its own, so a call keeps going while you move around the CRM and incoming calls ring even when the window is closed.

| | |
| --- | --- |
| **Page** | `/softphone` on your site, opened by the browser extension or the Windows app |
| **Permission** | Use the telephony soft phone |
| **Feature** | Telephony Soft Phone Extension (`CrestApps.OrchardCore.Telephony.SoftPhone.Extension`) |

<video controls preload="metadata" width="100%" aria-label="Screencast of the soft phone window: the Keypad, Recent, Voicemail and Work tabs, the presence menu and the audio settings">
  <source src="/img/docs/um-softphone-page.mp4" type="video/mp4" />
</video>

## The header

- The **status** on the left: *Ready*, *In call*, *On hold*, *Reconnecting...* or *Poor connection*.
- The **presence** menu (contact center agents): **Available**, your break reasons under **Break**, the other reason codes, and **Offline**. See [Agent states](agent-states.md).
- The **headset** button opens the audio settings.

## The tabs

| Tab | What it is for |
| --- | --- |
| **Keypad** | Dial a number or, after **Dial extension**, an extension or a colleague by name. See [Placing and handling calls](calls.md). |
| **Recent** | Incoming, outgoing and missed calls. Click a call to dial it back. |
| **Voicemail** | Your voicemail, with a badge for new messages. Play, select and delete. |
| **Work** | Contact center agents: sign in to queues and campaigns. See [Agent workspace](agent-workspace.md). |
| **Diagnostics** | Shown when your administrator enables it (or add `?diag=1` to the page address): microphone level, live call quality, **Run audio test** and **Dump SDP/stats** for support. |

## Audio settings

Click the **headset** button:

| Setting | What it does |
| --- | --- |
| **Microphone** / **Speaker** | The devices to use. You can switch during a call. |
| **Voice isolation** | On by default. Removes background noise and the voices of people around you before the caller hears you. Works best with a headset microphone close to your mouth. |
| **Isolation strength** | Low, Medium or High. Use High on a loud floor, and Low if callers say the start or end of your words is cut off. |
| **Isolation model** | Enhanced removes more background voices. Choose Light if callers hear your voice crackle, which can happen on an older computer. |
| **Echo cancellation**, **Noise suppression**, **Automatic gain control** | The browser's own audio clean-up. With voice isolation on, noise suppression and automatic gain control are off. Leave these as they are unless support asks you to change them. |
| **Microphone boost** | Off up to +12 dB, for a quiet microphone. |
| **Audio delay** | Automatic, or a fixed buffer from 120 down to 20 ms. |
| **Region** | The provider location closest to you. Changing it re-registers the phone after the current call. |

If the browser blocked the microphone, allow it and click **Retry microphone**.

When the connection drops, the phone reconnects by itself and shows *Reconnecting...*. When call quality falls, it shows *Poor connection*.

## Administrator options

Under **Settings > Communication > Telephony**, the **Soft Phone** tab holds the **Default telephony provider** and the **Allowed short codes** (short numbers people may dial, one per line; emergency numbers can never be added).

The optional **Telephony Soft Phone** feature also puts a phone button on every admin page. With it enabled, the same tab adds: **Enable the soft phone**, **Show the soft phone on the admin dashboard**, **Enable the diagnostics tab**, **Recent calls to display**, **Default country** and **Accent color**. The **Enable the soft phone** switch only affects that in-page phone; the apps keep working while the Soft Phone Extension feature is on.
