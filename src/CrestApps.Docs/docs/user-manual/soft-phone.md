---
sidebar_label: Soft Phone
sidebar_position: 41
title: The Soft Phone
description: A tour of the soft phone - keypad, recent calls, voicemail, the Work tab where agents sign in, presence, audio settings and diagnostics.
technical_manual:
  - telephony/index
---

The **soft phone** makes and takes calls in the browser. Agents usually run it in the [browser extension or the Windows app](phone-apps.md), which open the site's `/softphone` page in a small window of its own, so a call keeps going while you move around the CRM and incoming calls ring even when the window is closed.

| | |
| --- | --- |
| **Page** | `/softphone` on your site, opened by the browser extension or the Windows app |
| **Permission** | Use the telephony soft phone. Only the *Administrator* role has it by default, not *Agent*: an administrator adds it to the roles that make and take calls. |
| **Feature** | Telephony Soft Phone Extension |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of the soft phone window: the Keypad, Recent, Voicemail and Work tabs, the presence menu and the audio settings">
  <source src="/img/docs/um-softphone-page.mp4" type="video/mp4" />
</video>

## The header

- The **status** on the left tells you what the phone is doing: *Ready*, *Connecting...*, *Ringing...*, *In call*, *In conference*, *On hold*, *Call ended*, *Call failed*, *Reconnecting...* or *Poor connection*. *Not Ready* shows for a moment while the phone starts.
- The **presence** menu (contact center agents): **Available**, your break reasons under **Break**, the other reason codes, and **Offline**. See [Agent states](agent-states.md).
- The **headset** button (**Settings**) opens the audio settings.

## The tabs

| Tab | What it is for |
| --- | --- |
| **Keypad** | Dial a number or, after **Dial extension**, an extension or a colleague by name. During a call it also holds the call buttons and the **Active calls** list. See [Placing and handling calls](calls.md). |
| **Recent** | Incoming, outgoing and missed calls. Missed calls are shown in red, and calls in progress stay highlighted. Click a call to dial it back; a call to an extension is dialed back as that extension. The tab shows your 30 most recent calls unless your administrator chose another number. |
| **Voicemail** | Your voicemail, with a badge that counts the messages you have not played yet. Play a message, or tick messages (or **Select all**) and click **Delete**. See [Voicemail](voicemail.md). |
| **Work** | Contact center agents: sign in to queues and campaigns. See [Agent workspace](agent-workspace.md). |
| **Diagnostics** | Shown only when it is turned on. See [Diagnostics](#diagnostics) below. |

## When the phone cannot make calls

| What you see | What to do |
| --- | --- |
| *No provider is configured...* at the top of the phone | No phone system is set up for the site yet. Ask your administrator to turn one on and choose it as the default (see [Phone and SMS provider setup](telephony-settings.md)). |
| **Connect to provider** | Some phone systems need you to connect your own account once. Click the button and sign in to the phone system in the window that opens. If nothing opens, allow pop-ups for the site and try again. The built-in Telnyx and Asterisk connections never ask for this. |
| A microphone message with **Retry microphone** | The browser could not use your microphone. The message says why, for example that access is blocked for the site, that no microphone was found, or that another application such as Teams or Zoom is using it. Fix the cause, then click **Retry microphone**. |

When the connection drops, the phone reconnects by itself and shows *Reconnecting...*. When call quality falls, it shows *Poor connection*.

## Audio settings

Click the **headset** button. Your choices are saved in this browser, so they stay the same the next time you open the phone.

| Group | Setting | What it does |
| --- | --- | --- |
| **Audio devices** | **Microphone** / **Speaker** | The devices the phone uses, instead of your computer's default. The lists update when you plug a headset in or out, and you can switch during a call. |
| **Microphone processing** | **Voice isolation** | On by default. Removes background noise and the voices of people around you before the caller hears you. Works best with a headset microphone close to your mouth. |
| | **Isolation strength** | **Low**, **Medium** (default) or **High**. How firmly the room is cut while you are not speaking. Use **High** on a loud floor, and **Low** if callers say the start or end of your words is cut off. |
| | **Isolation model** | **Enhanced** (default) removes more background voices. Choose **Light** if callers hear your voice crackle or break up, which can happen on an older computer. |
| | **Echo cancellation**, **Noise suppression**, **Automatic gain control** | The browser's own clean-up of your voice. Echo cancellation is on by default. Noise suppression and automatic gain control are off while voice isolation is on, and on when it is off. Changes apply at once, even during a call. If the other person says you sound hollow, distant or distorted, turn one off at a time. |
| | **Microphone boost** | **Off**, **+3 dB**, **+6 dB**, **+9 dB** or **+12 dB**. Makes your voice louder before it is sent, with a limiter so loud words do not distort. Use it when people say you are quiet. |
| **Incoming audio** | **Audio delay** | **Automatic**, **120 ms**, **80 ms**, **40 ms** or **20 ms**. How long incoming audio is held before it plays. A shorter delay shortens the pause before you hear the other person, but too short makes their voice break up. Applies to the call you are on. |
| **Connection** | **Region** | **Automatic**, or the provider location nearest you: **US West**, **US Central**, **US East**, **Canada Central**, **Europe**, **Asia Pacific** or **South Asia**. **Automatic** follows your team's setting and then the provider's own choice. Changing it reconnects the phone, so it waits until your current call ends. |

### Fix common audio problems

- **The other person cannot hear you.** Check that the **Microphone** is your real headset or microphone. A common cause is a virtual audio device (software that pretends to be a microphone) set as the computer's default; the phone warns you when it sees one. The microphone meter on the **Diagnostics** tab shows which device is live.
- **The other person hears people talking around you.** Make sure **Voice isolation** is on, and try **Isolation strength** **High**. Keep your headset microphone close to your mouth.
- **You sound quiet.** Try **Microphone boost**, and watch the microphone level on the **Diagnostics** tab while you speak. A level that moves around the middle is healthy; one pinned at the top means the boost is too high and loud words are clipped.
- **There is a pause before you hear a reply.** Shorten **Audio delay** one step at a time, and stop if the other person's voice starts to break up. A **Region** closer to you can also help; compare the round-trip time on the **Diagnostics** tab before and after.

Some limits no setting removes. A call to an ordinary phone number travels over the telephone network, so it sounds like a landline even when every setting is right, and a browser call always has a little more delay than a call between two desk phones.

## Diagnostics

The **Diagnostics** tab helps you and your support team find the cause of an audio or call-quality problem. It is hidden until it is turned on in one of two ways:

- Your administrator turns on **Enable the diagnostics tab** for everyone (see [Administrator options](#administrator-options)).
- You add `?diag=1` to the end of the phone's address, for example `/softphone?diag=1`. It is then shown in this browser session only, without changing any setting.

| Section | What it shows |
| --- | --- |
| **Microphone level** | A live meter, and **Using:** with the name of the microphone the phone hears. Speak: the bar should move. A flat bar means the selected microphone is silent. |
| **Live call quality** | During a call: an estimated call-quality score, packet loss, jitter, round-trip time, the data received, the audio codec and the type of connection. |
| **Provider warnings** | Warnings from the phone system, for example that your microphone sends very little audio. |
| **Audio test** | **Run audio test** calls a number that plays your voice back, so you can check audio without a second person. End your call first. It needs a test destination: your administrator sets one in the provider settings, or you type one in the box. |
| **Dump SDP/stats** | Prints the technical details of the current call for your support team. |

Turning the tab on does not change how calls work. Poor call quality is reported to your administrators automatically, even when nobody has the tab open.

## Administrator options

Under **Settings > Communication > Telephony**, the **Soft Phone** tab holds:

| Field | What it does |
| --- | --- |
| **Default telephony provider** | The phone system the soft phone uses. Turn a provider on, on its own tab, to make it available here. |
| **Allowed short codes** | Short numbers people may dial, one per line, for example a carrier service number. Short numbers are refused unless they are listed here. Emergency numbers can never be added. |

The optional **Telephony Soft Phone** feature also puts a phone button on every admin page. With it enabled, the same tab adds the settings below. Except for the first two, they also apply to the phone apps' `/softphone` page.

| Field | What it does |
| --- | --- |
| **Enable the soft phone** | Turns the in-page phone on or off for the whole site, at once. The phone apps keep working while the Telephony Soft Phone Extension feature is on. On by default. |
| **Show the soft phone on the admin dashboard** | Shows the in-page phone on admin pages. On by default. |
| **Enable the diagnostics tab** | Shows the **Diagnostics** tab to everyone. Turn it on to troubleshoot, then off again. Off by default. |
| **Recent calls to display** | How many calls the **Recent** tab shows, from 1 to 200. Default 30. |
| **Default country** | The country the number field starts with, so a national number is turned into a full international number. **Automatic (based on the current culture)** picks it from the site's language settings. |
| **Accent color** | The color of the phone's buttons. |
