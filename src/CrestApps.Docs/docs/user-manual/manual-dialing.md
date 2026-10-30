---
sidebar_label: Manual Dialing and Compliance
sidebar_position: 41.5
title: Manual Dialing and Compliance
description: What happens when an agent dials a number by hand from the soft phone, which calls are blocked, and why.
technical_manual:
  - contact-center/agents-queues-dialer
  - telephony/index
---

A call an agent dials by hand, from the soft phone keypad or by clicking a phone number, is not part of a campaign: there is no dialer profile, no retry rule and no abandonment cap. It is still checked before it is placed, so a hand-dialed call cannot reach a number that a campaign call would never be allowed to reach.

| | |
| --- | --- |
| **Where** | The soft phone keypad, and the call button next to a phone number on a contact |
| **Feature** | Contact Center (the Outbound Dialer feature adds the checks) and Telephony |
| **Set up by** | Your technical team. The settings are on the site's configuration, not on a screen. |

<AskYourAdmin />

## What is checked

| Check | What it does |
| --- | --- |
| **Do not call** | The call is blocked when the contact opted out of phone calls, when somebody reachable at one of the contact's numbers opted out, or when the number is on a connected [do-not-call list or national registry](administration/do-not-call-lists.md). It is on by default. |
| **A number that cannot be read** | A number that cannot be turned into a full international number cannot be compared with a do-not-call list, so the call is blocked instead of guessed. Type the number with its country code, or ask your administrator to set a default country for dialing. |
| **Calling window** | When your technical team turns it on, the call is blocked outside the hours of the [business hours calendar](business-hours.md) chosen for manual calls. It is off by default. A calendar that is disabled or missing blocks every manual call while the window is on. |
| **Extensions** | A call to a colleague's [extension](extensions.md) is never checked: it stays inside your own phone system. |
| **Emergency numbers** | Emergency numbers can never be dialed, and cannot be added to the allowed short codes. |

Short numbers, such as a carrier service number, are refused unless they are listed under **Allowed short codes** in [Phone and SMS provider setup](telephony-settings.md#choose-the-default-provider-and-soft-phone-options).

## What the agent sees

The soft phone does not place the call and shows the reason, for example *The contact opted out of phone calls.*, *The destination is listed on a national do-not-call registry.* or *The destination is outside the permitted manual calling window.*

Every blocked call is recorded as an event, with the number and the reason, so a supervisor or an auditor can see what was stopped and why.

## If a call you need is blocked

- **Contact opted out:** do not call. If the customer asked to be called again, update the contact's communication preferences first.
- **On a list:** the number is blocked for everyone. Ask the person who manages the lists whether it should be there.
- **Outside the window:** call during the permitted hours.
