---
sidebar_label: Numbers Not In Service
sidebar_position: 19.8
title: Numbers Not In Service - Skip Dead Numbers
description: How calls that reach a disconnected or invalid number are completed automatically, and how those numbers are kept out of every later load and dial.
---

A list of phone numbers is never fully clean. Some numbers are disconnected, some were never valid, and some have changed hands. When a call reaches one of them, the network reports that the number is **not in service**. The platform then completes the attempt on its own and adds the number to the **Numbers Not In Service** list, so no campaign loads or dials it again.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Numbers Not In Service |
| **Permission** | Manage activities |
| **Feature** | Omnichannel Activities |

## What happens when a call finds a dead number

This works the same way for the **Preview**, **Power** and **Progressive** dialers and for **Automatic** (AI) loads:

1. The call is placed. The carrier answers with "unallocated number", "not found" or "does not exist" (SIP 404, 410, 484 or 604).
2. No agent is connected. An automatic call never starts the AI conversation or the AI review.
3. The activity is **completed** with the subject's not-in-service disposition (see below). The notes record the number and what the carrier said, for example *unallocated_number (SIP 404)*.
4. The number is added to the list, with the campaign and activity that found it.
5. The activity is not retried. The number is left out of every later load and dial.

The contact's history keeps the completed activity, so you can see that the dialer tried the number and found it out of service.

:::note
Some carriers answer the call and play a "the number you have dialed is not in service" recording, instead of rejecting the call. Those calls look answered. For those, the agent picks your not-in-service disposition, and the number is marked the same way. Number lookups (below) catch many of them before they are ever dialed.
:::

## Choose the disposition

1. Open **Interaction Center > Management > Dispositions**, add or edit a disposition (for example *Number Not In Service*) and set its **Outcome** to **Number not in service**.
2. Optionally, add it to a subject's flow (**Subject Flows > Manage Flow**) with a **Finish** action. That makes it available to agents on that subject, and lets you add other actions to it.

When a call finds a dead number, the platform uses the subject's disposition with the *Number not in service* outcome, or any disposition with that outcome if the subject's flow has none. If no disposition has the outcome, it creates one named **Number Not In Service** the first time it needs one. When an agent picks a disposition with this outcome, the number is marked too.

## Where numbers are skipped

| Where | What happens to a number on the list |
| --- | --- |
| [Loading activities](load-inventory.md) | The contact's next number is used instead, in the usual order (cell, home, office, work, other). A contact whose numbers are all on the list is skipped and counted as *has only numbers that are not in service*. |
| [Dialer](dialer-profiles.md#what-happens-to-each-record) | A record whose number was added after it was loaded is **cancelled** before it is dialed. |
| [Automatic loads](automated-ai.md) | The activity is cancelled instead of called or texted. |
| Contact card | The number shows a red **Not in service** badge. Hover over it to see why. |

## Catch dead numbers before you dial

With [phone number verification](../modules/phone-number-verifications.md) turned on, each contact's preferred number is checked by a lookup provider. A number the lookup reports as invalid, or as an inactive line, is added to the list before anybody dials it. A number reported as *unreachable* (switched off, or out of coverage) is not added.

If a later lookup finds the same number active again, the mark the earlier lookup made is removed, because the number has been given to somebody new. A mark made by a real call stays until you clear it.

## Manage the list

Open **Interaction Center > Management > Numbers Not In Service**. For each number, the list shows what found it (dialer call, automated call, agent disposition, number lookup or marked by hand), what the carrier or lookup said, the campaign, when it was last found, and how many times.

- **Search by number** filters the list by digits.
- **Mark as not in service** adds a number by hand. Enter it with its country code, for example +17025550123.
- **Clear** removes a number from the list, so it can be loaded and dialed again. Use this when you learn the number is back in service.

## Report on it

The **Campaign summary** and **Subject inventory** [reports](reports.md) have a **Not in service** column. It counts activities that were dialed and found dead, and activities cancelled because their number was already known to be dead. Those activities are also counted under *Completed* or *Cancelled*. Use it to see how clean a campaign's list was, and how much dialing went on dead numbers.
