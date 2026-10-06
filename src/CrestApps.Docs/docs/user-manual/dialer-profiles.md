---
sidebar_label: Dialer Profiles
sidebar_position: 26
title: Dialer Profiles - Preview, Power and Progressive Dialing
description: Choose how outbound calls are placed for a campaign, what the customer sees as caller ID, and the compliance rules every call must pass.
technical_manual:
  - contact-center/agents-queues-dialer
---

A **dialer profile** decides **how** outbound calls are placed: whether an agent reviews each record before the call (preview), or the system dials for available agents (power and progressive). It also carries the caller ID and the compliance rules. You pick the profile when you [load dialer activities](load-inventory.md#dialer-loads); the campaign you pick there is what agents sign in to. A profile names no campaign or queue, so one profile can serve many loads.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Dialer Profiles |
| **Permission** | Manage the Contact Center dialer |
| **Features** | Contact Center Outbound Dialer for Preview. Contact Center Paced Dialing adds Power and Progressive. |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of creating a dialer profile and switching between preview, power and progressive modes">
  <source src="/img/docs/um-dialer-profiles.mp4" type="video/mp4" />
</video>

## The dialing modes

| Mode | Who presses dial | What the agent sees |
| --- | --- | --- |
| **Preview** | The agent. The record is offered to an agent signed in to the campaign; the agent reviews it and clicks **Dial** (or **Skip**). The server then places the call after the compliance checks. | The record opens, then the call starts when they choose Dial. |
| **Power** | The system. Every minute the dialer reserves available agents in the campaign and places up to **Calls per agent** calls for the campaign, each with its own reserved agent. | The record pops up when the call starts; there is nothing to press. |
| **Progressive** | The system, like Power, reserving one agent per call, with up to 100 calls per pacing cycle. | Same as Power. |
| **Predictive** | Not available. The editor does not offer it and refuses to save it. | - |

Older profiles saved as *Manual* are shown and saved as **Preview**.

### Preview dialing, start to finish

<video controls preload="metadata" width="100%" aria-label="Screencast of a live preview-dial call: the offer arrives, the agent clicks Dial, uses mute, hold, keypad and transfer, hangs up and completes the activity">
  <source src="/img/docs/um-preview-dial.mp4" type="video/mp4" />
</video>

The agent is signed in to the campaign in the soft phone (right). The record is offered in the docked bar and the workspace; the agent clicks **Dial**, the customer answers, and when the call ends the agent is in **Wrap-up** until the activity is completed.

### Power dialing, start to finish

<video controls preload="metadata" width="100%" aria-label="Screencast of a live power-dial call: the agent signs in to the campaign, the dialer places the call, the record opens and the activity is completed">
  <source src="/img/docs/um-power-dial.mp4" type="video/mp4" />
</video>

With a Power profile the agent only signs in and stays **Available**. Within a minute the dialer places the call. Nothing appears on the agent's screen while the call rings: the record opens on its own once the customer answers and the agent is connected. A call that does not reach the agent is never shown to them (see [What happens to each record](#what-happens-to-each-record)).

## Create a profile

1. Open **Interaction Center > Management > Dialer Profiles** and click **Add Dialer Profile**.
2. On **General**, enter a **Name**, an optional **Description**, and leave **Enabled** ticked. A disabled profile places no calls.
3. On **Dialing**, pick the **Mode** and the **Voice call provider** (or *Default provider*). Power and Progressive show extra fields:

   | Field | Modes | What it does |
   | --- | --- | --- |
   | **Calls per agent** | Power | Calls started per pacing cycle for the campaign, 1 to 3. Each call reserves its own agent, so it never dials more calls than there are available agents. |
   | **Max attempts** | Power, Progressive | How many attempts one contact may get, counting the first call and every follow-up activity created to try again. Default 3. |
   | **Retry delay (minutes)** | Power, Progressive | The shortest wait after an attempt before the next attempt is dialed. Default 60. |
   | **Screen out answering machines** | Power, Progressive | **Off** (default), **Standard detection** or **Premium detection**. When on, the agent is connected only after the provider hears a person. A call answered by a voicemail or fax machine is hung up, the agent goes straight back to Ready without wrap-up or a pop, and the dialer completes the activity with the *Answering machine* disposition. The person who answers hears a few seconds of silence while the call is screened. Telnyx only. |

4. On **Caller ID**, pick the **Caller ID** number customers see from your [Omnichannel Addresses](channel-endpoints.md) used for **Voice calls** (**Provider default** uses the provider's caller ID). A **Dial from** number picked when activities are loaded is shown instead for that load's calls, unless **Always show this caller ID** is ticked, and pick the **Default calling region** used for numbers written without a country code. A number typed before caller IDs were picked stays selected until you change it.
5. On **Compliance**:

   | Field | What it does |
   | --- | --- |
   | **Respect do-not-call and communication preferences** | Skips contacts who opted out of calls and numbers on a national do-not-call registry. On by default. |
   | **Enforce a calling window** | Dials only while the chosen calendar is open, checked in the **contact's** time zone. |
   | **Outbound calling calendar** | The [business hours calendar](business-hours.md) for the calling window. Required when the window is enforced. |

6. On **Abandonment and safe harbor** (Power and Progressive only):

   | Field | What it does |
   | --- | --- |
   | **Enforce an abandonment-rate cap** | Stops automated dialing when too many answered calls find no agent. Needs safe harbor on. |
   | **Maximum abandonment rate** | The cap, as a share of calls a person answered. Default 3. |
   | **Abandonment sample floor** | How many answered calls are needed before the cap applies. Default 30. |
   | **Play a safe-harbor announcement when abandoned** / **Safe-harbor announcement** | The message played instead of silence when no agent is free. |

7. Click **Save**.

## What happens to each record

Before an agent is reserved for a record, the dialer checks that the record is due: its scheduled time and the retry delay after its last call have passed, and it has attempts left. A record that is not due yet waits at the back of the queue, and no agent sees it. Before every call the dialer then checks, in order: the number is valid, the number is not [known to be out of service](numbers-not-in-service.md), the record has attempts left, the retry delay has passed, the contact has not opted out, the calling window is open, the abandonment cap allows dialing, and the number is not on a do-not-call registry.

- A record with no valid number becomes **Failed**.
- A record with no attempts left is **Completed** by the dialer with the disposition for how its last call ended, and the terminal reason `dialer_max_attempts`.
- A record on a do-not-call list becomes **Cancelled**.
- A record whose number is known not to be in service becomes **Cancelled**, and is never dialed.
- Anything else (closed calling window, cap reached, registry unreachable) is simply tried again in a later cycle.

The agent receives a call only when it is something they can act on: a customer answered and the agent was connected. Every call that ends before that is dispositioned by the dialer itself, as the system, with the disposition whose [outcome](dispositions.md#outcomes) matches, and the agent goes straight back to Ready with no pop, no call card and no wrap-up:

| How the call ended | Disposition outcome | Terminal reason |
| --- | --- | --- |
| Rang out, or was given up on, with nobody answering | *No answer* | `dialer_no_answer` |
| The line was busy | *Busy* | `dialer_busy` |
| A machine answered and answering-machine screening is on | *Answering machine* | `dialer_answering_machine` |
| The called party or the network rejected it | *Rejected* | `dialer_rejected` |
| The network failed it, or the provider refused to place it | *Call failed* | `dialer_failed` |
| A customer answered but hung up before the agent was connected | *Disconnected* | `dialer_disconnected` |
| The carrier reported the number not in service (unallocated, SIP 404/410/484/604) | *Number not in service* | `number_not_in_service` |

A number not in service is also added to [Numbers Not In Service](numbers-not-in-service.md) and never dialed again. With answering-machine screening off, a call a voicemail answers is connected to the agent like any answered call, and is theirs to disposition. In Preview mode the agent clicks **Dial**, so they see the call ring; if it ends before they are connected, the dialer dispositions it the same way and the agent goes back to Ready.

The disposition decides what happens next. Wire **Try Again** to it in the [subject flow](subject-flows.md) to call the contact again: the next attempt is a new activity, put back in the same campaign with the same dialer profile, due no sooner than the retry delay, and it is not created once **Max attempts** is used. A workflow can make the same decision with the **Schedule Dialer Retry** task; see [Workflows](workflows.md#call-a-contact-again).

Queue callbacks use a built-in preview profile that skips the do-not-call and calling-window checks, because the customer asked to be called.

## Next steps

- Before you start an automated (power or progressive) campaign, set up the [dispositions](dispositions.md) and the [subject flow](subject-flows.md) first, so every call outcome has a result, and check the do-not-call, retry delay and calling window settings above.
- [Load dialer activities](load-inventory.md#dialer-loads) with this profile and a campaign.
- Give agents the campaign on their [entitlements](skills-and-entitlements.md), then have them [sign in to it](agent-workspace.md). [Calls from a dialer campaign](agent-workspace.md#calls-from-a-dialer-campaign) explains what agents see in each mode.
