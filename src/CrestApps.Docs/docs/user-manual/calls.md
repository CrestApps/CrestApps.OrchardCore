---
sidebar_label: Placing & Handling Calls
sidebar_position: 31
title: Placing and Handling Calls
description: Dial a number or an extension from the soft phone, answer incoming calls, and use hold, mute, keypad, transfer, add call, merge and conference.
---

All call controls live in the **soft phone**, which agents run in the [browser extension or the Windows app](phone-apps.md). This page walks through the everyday call tasks. See [Soft phone](soft-phone.md) for its tabs and settings.

<video controls preload="metadata" width="100%" aria-label="Screencast of the soft phone keypad: typing a number, switching to extension dialing and searching for a colleague by name">
  <source src="/img/docs/um-manual-dial.mp4" type="video/mp4" />
</video>

## Dial a phone number

1. In the soft phone, choose the **Keypad** tab.
2. Type the number, or pick the country flag first and type the national number. The number is formatted as you type.
3. Click the green call button or press Enter.

When the in-page soft phone is enabled, a phone button also appears beside phone fields in the CRM, for example on a contact. To call a customer as part of your work, open their activity first so the call is logged against it; see [Activities](activities.md).

## Call an extension

1. On the **Keypad** tab, click **Dial extension**.
2. Type the extension number and click the call button, or start typing a name: matching people appear with their extension, and clicking one calls them straight away.
3. Click **Dial phone number** to go back to ordinary numbers.

## Answer an incoming call

An incoming call opens a window in the soft phone with the caller and any **Matched records**.

| Button | What it does |
| --- | --- |
| **Answer** | Connects the call. |
| **Answer & open** | Connects and opens the matched record. |
| **Open** | Opens the matched record without answering. |
| **Voicemail** | Sends the caller to voicemail. |
| **Ignore** | Stops ringing on this page. |

When you have the phone open in several tabs, answering in one stops the others ringing.

A call from a queue arrives as an **offer** first: the workspace and the docked agent bar show the caller, the queue and a countdown, and the soft phone lists the contacts that match the number. This screencast signs in to the *Support* queue, accepts a live call, puts the caller on hold and back, mutes and unmutes, hangs up, and lands in wrap-up with **Complete activity**:

<video controls preload="metadata" width="100%" aria-label="Screencast of an agent signing in to a queue, accepting an inbound call from the queue, using hold and mute, hanging up and entering wrap-up">
  <source src="/img/docs/um-answer-queue.mp4" type="video/mp4" />
</video>

## During a call

<video controls preload="metadata" width="100%" aria-label="Screencast of a live call in the soft phone window: mute, hold, keypad, the transfer panel and hang up">
  <source src="/img/docs/um-preview-dial.mp4" type="video/mp4" />
</video>


| Button | What it does |
| --- | --- |
| **Mute** / **Unmute** | Stops the customer hearing you. |
| **Hold** / **Resume** | Puts the customer on hold; they hear hold music or a soft tone. |
| **Keypad** | Sends key presses (touch tones), for example to navigate another company's menu. |
| **Hang up** | Ends the call. |
| **Transfer** | Opens the transfer panel (below). |
| **Add call** | Holds this call and lets you dial another. **Back to call** returns to the first one. |
| **End all** | Ends every call you have; asks you to confirm (the default choice is *Keep talking*). |

## Transfer a call

1. Click **Transfer**.
2. Choose **Blind** (hand the call over immediately) or **Warm** (talk to the person first).
3. Pick the target from the directory: **Agents**, **Queues**, **Outside numbers** (the [approved list](contact-center-settings.md#external-transfer-destinations)), or type a number or extension.
4. For a blind transfer the call leaves you. For a warm transfer the soft phone shows *Calling...*, then *Talking to X. The caller is on hold.* Click **Complete transfer** to hand over, or **Cancel transfer** to return to the caller.

Transferring to an outside number needs the *Transfer calls externally* permission.

## Merge calls into a conference

1. With two or more calls in the **Active calls** list, tick the calls to join (or **Select all**).
2. Click **Merge calls** (or **Add to conference** when a conference already exists).
3. The list shows *Conference · N participants*; each participant has their own hang-up button.
4. **Leave** takes you out and keeps the others talking. **End for all** ends the conference.

## After the call

A queue or campaign call puts you in **Wrap-up** until you complete the activity. See [Agent workspace](agent-workspace.md#after-the-call-wrap-up).

:::note[About the screencast]
The first screencast shows the keypad and the extension search and stops before a call is placed; the others are live calls. Completing a transfer and conferencing use the buttons described above.
:::
