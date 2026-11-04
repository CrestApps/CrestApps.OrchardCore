---
sidebar_label: Soft Phone Widget
sidebar_position: 41.2
title: The Soft Phone Widget and Click to Dial
description: Show the soft phone as a floating button on the admin pages or on your public site, and call a number with one click.
technical_manual:
  - telephony/index
---

Besides the [browser extension and the Windows app](phone-apps.md), the soft phone can float on a page of the site itself: a round phone button at the bottom right that opens the keypad, the recent calls and the voicemail. People who work in the admin all day can then call without leaving the page.

| | |
| --- | --- |
| **Where** | A phone button at the bottom right of every admin page; optionally on the public site |
| **Permission** | Use the telephony soft phone |
| **Features** | Telephony Soft Phone (the floating phone); Telephony Soft Phone Extension (the separate window) |

<AskYourAdmin />

## On the admin pages

The button appears on every admin page for users who may use the soft phone, while **Enable the soft phone** and **Show the soft phone on the admin dashboard** are ticked under **Settings > Communication > Telephony > Soft Phone**. Untick **Enable the soft phone** to switch the phone off for the whole site at once. See [Soft Phone](soft-phone.md#administrator-options) for every option.

## On your public site

To show the phone to visitors who are signed in and allowed to use it, add the **Soft Phone** widget to a zone under **Design > Widgets**, the way you add any widget, and publish it. The same permission applies: a visitor without it sees nothing.

## Click to dial

A contact's phone number shows a green **Call with the soft phone** button next to it. Click it, and the soft phone opens with the number filled in, ready for you to press the green call button. A **Send SMS** button sits next to it when texting is turned on.

A call started this way is a [manual call](manual-dialing.md): it is checked against the do-not-call lists and the calling window before it is placed.

## The Diagnostics tab

When the diagnostics tab is turned on, the phone shows a **Diagnostics** tab with a microphone meter, live call quality and an audio test. Turn it on for everyone with **Enable the diagnostics tab**, or for one session by adding `?diag=1` to the page address. See [Diagnostics](soft-phone.md#diagnostics).
