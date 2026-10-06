---
sidebar_label: Dialer Profiles
sidebar_position: 26
title: Dialer Profiles - Preview, Power, Progressive and Predictive Dialing
description: Choose how outbound calls are placed for a campaign, what the customer sees as caller ID, and the compliance rules every call must pass.
technical_manual:
  - contact-center/agents-queues-dialer
---

A **dialer profile** decides **how** outbound calls are placed: whether an agent reviews each record before the call (preview), or the system dials for available agents (power, progressive and predictive). It also carries the caller ID and the compliance rules. You pick the profile when you [load dialer activities](load-inventory.md#dialer-loads); the campaign you pick there is what agents sign in to. A profile names no campaign or queue, so one profile can serve many loads.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Dialer Profiles |
| **Permission** | Manage the Contact Center dialer |
| **Features** | Contact Center Outbound Dialer for Preview. Contact Center Paced Dialing adds Power, Progressive and Predictive. |

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
| **Predictive** | The system, like Power: every call reserves its own available agent before it is placed, and fewer calls are placed per cycle as the abandonment rate approaches the cap. See [Predictive dialing](#predictive-dialing). | Same as Power. |

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
3. On **Dialing**, pick the **Mode** and the **Voice call provider** (or *Default provider*). Power, Progressive and Predictive show extra fields:

   | Field | Modes | What it does |
   | --- | --- | --- |
   | **Calls per agent** | Power, Predictive | Calls started per pacing cycle for the campaign, 1 to 3. Each call reserves its own agent, so it never dials more calls than there are available agents. |
   | **Max attempts** | Power, Progressive, Predictive | How many attempts one contact may get, counting the first call and every follow-up activity created to try again. Default 3. |
   | **Retry delay (minutes)** | Power, Progressive, Predictive | The shortest wait after an attempt before the next attempt is dialed. Default 60. |
   | **Ring time (seconds)** | Power, Progressive, Predictive | How long a call rings before it is given up as unanswered, from 15 to 120. Default 30. A call never rings for less than 15 seconds. |
   | **Screen out answering machines** | Power, Progressive, Predictive | **Off** (default), **Standard detection** or **Premium detection**. When on, the agent is connected only after the provider hears a person. A call answered by a voicemail or fax machine is hung up, the agent goes straight back to Ready without wrap-up or a pop, and the dialer completes the activity with the *Answering machine* disposition. The person who answers hears a few seconds of silence while the call is screened. Telnyx only. |

4. On **Caller ID**, pick the **Caller ID** number customers see from your [Omnichannel Addresses](channel-endpoints.md) used for **Voice calls** (**Provider default** uses the provider's caller ID). A **Dial from** number picked when activities are loaded is shown instead for that load's calls, unless **Always show this caller ID** is ticked, and pick the **Default calling region** used for numbers written without a country code. A number typed before caller IDs were picked stays selected until you change it.
5. On **Compliance**:

   | Field | What it does |
   | --- | --- |
   | **Respect do-not-call and communication preferences** | Skips contacts who opted out of calls and numbers on a national do-not-call registry. On by default. |
   | **Enforce a calling window** | Dials only while the chosen calendar is open, checked in the **contact's** time zone. |
   | **Outbound calling calendar** | The [business hours calendar](business-hours.md) for the calling window. Required when the window is enforced. |

6. On **Abandoned calls** (Power, Progressive and Predictive):

   | Field | What it does |
   | --- | --- |
   | **Measured abandonment** | Shown once the profile has placed calls: the abandonment rate over the rolling window and over the last 30 days, with the counts behind it. |
   | **Enforce an abandonment-rate cap** | Pauses automated dialing while too many answered calls find no agent. Needs the abandoned-call message on. |
   | **Maximum abandonment rate** | The cap, as a share of calls a person answered. Default 3. |
   | **Abandonment sample floor** | How many answered calls are needed in the window before the cap applies. Default 30. |
   | **Play a message when a call is abandoned** | Plays the abandoned-call message instead of hanging up in silence. A warning shows while it is off. |
   | **Abandoned call message** | What the person hears. Required when the message is on. See [Abandoned calls](#abandoned-calls). |

7. On **Predictive pacing** (Predictive only), see [Predictive dialing](#predictive-dialing).
8. Click **Save**.

## Predictive dialing

Predictive dialing is part of the **Contact Center Paced Dialing** feature, like Power and Progressive. Without it the **Predictive** mode is not offered and a Predictive profile cannot be saved.

A Predictive profile paces its calls one of two ways, chosen in **Pacing** on the **Predictive pacing** card:

- **One call per reserved agent** (the default) dials like Power: each call reserves its own available agent before it is placed, so a person who answers always has an agent waiting. As the abandonment rate climbs toward the **Maximum abandonment rate**, fewer calls are placed each cycle, down to one per agent at the cap.
- **Over-dial** places more calls than there are free agents, because most calls are not answered. How many is worked out from the measured answer rate so that the share of answered calls with no agent free stays near the **Target abandonment rate**. No agent is reserved while the calls ring: when a person answers, the free agent who has waited longest is connected to them. When nobody is free at that moment, the person hears the abandoned-call message straight away and the call counts as abandoned.

Over-dialing needs **Enforce an abandonment-rate cap**, the **Abandoned call message** and a **Target abandonment rate** below the **Maximum abandonment rate**; a profile without them cannot be saved with **Over-dial**. An over-dialing profile does not over-dial until it can trust its measurements. It dials one call per reserved agent instead while:

- fewer calls than the **Answer rate sample floor** have an outcome, or fewer people than the **Abandonment sample floor** have answered in the rolling window,
- the abandonment rate over the rolling window or over the last 30 days is at or above the **Maximum abandonment rate**, or the rolling rate is so close to it that no extra call is allowed.

It stops dialing altogether when the abandonment rate cannot be measured at all. Watch the abandonment rate during the first days of a new over-dialing campaign, and start with **Lines per agent** at 1.5 and a **Target abandonment rate** of 1%.

:::tip[Agents on an over-dialing campaign]
Agents on an over-dialing campaign do not see an offer before a call: the call is theirs as soon as they are connected. Keep their phone ready to answer automatically, and if they also take inbound calls, an inbound call that reaches them first takes them out of the campaign until it ends.
:::

The settings on the **Predictive pacing** card:

| Field | What it does |
| --- | --- |
| **Measured answer rate** | Shown once a Predictive profile has placed calls: the share of recent calls with an outcome that a person answered, how long people take to answer, and how long agents take to be connected. A call still ringing is left out until it has an outcome. |
| **Pacing** | **One call per reserved agent** (the default) or **Over-dial**. Over-dialing needs **Enforce an abandonment-rate cap**, the abandoned-call message and a target below the cap. |
| **Target abandonment rate** | The rate over-dialing steers toward. Default 2%. For over-dialing it must be lower than the **Maximum abandonment rate**, which stays the hard limit. |
| **Lines per agent** | The most calls ringing for each free agent, from 1 to 5. Default 2. |
| **Calls in flight** | The most calls ringing at once for one campaign, from 1 to 1000. Default 100. |
| **Answer rate sample floor** | How many calls with an outcome are needed before the answer rate is trusted, from 10 to 10000. Default 50. Until then the profile reserves an agent for every call. |
| **Answer rate window (minutes)** | How far back the answer rate is measured, from 5 to 240. Default 15. |
| **Count agents about to free up** | Also counts agents expected to finish their call and wrap-up before a new call is answered. Off by default. |
| **Share of agents about to free up** | How many of those agents are counted, from 0 to 100%. Default 50%. |
| **Connect wait (milliseconds)** | How long a person who answered may wait for an agent to free up before the abandoned-call message plays, from 0 to 1500. Default 0, which is recommended: a call not connected within two seconds counts as abandoned. |
| **Retry abandoned calls only with an agent reserved** | When the same activity is dialed again after its call was abandoned, an agent is reserved for it first, so the person is not abandoned twice. On by default. |

## Abandoned calls

With Power, Progressive and Predictive dialing, the customer is dialed first and the reserved agent is connected once a person answers. A call is **abandoned** when a person answers and no agent is connected to them within two seconds. That happens when the agent's phone does not pick up (the browser was closed, the phone is not registered, the network dropped), when the agent cannot be connected at all, when the agent is connected later than two seconds after the answer, or when the person hangs up after waiting more than two seconds.

These settings support the common abandoned-call rules for automated dialing. Confirm which rules apply to your calls; the settings do not make a campaign compliant on their own.

### The abandoned-call message

When the agent cannot be connected, the person hears the profile's **Abandoned call message** straight away, and the call ends when the message has been spoken. If the message is not on, the call is hung up without a word.

Keep the message short and say who is calling and a number the person can call to reach you or to ask not to be called again. Two placeholders are filled in for each call:

| Placeholder | Replaced with |
| --- | --- |
| `{company}` | The site name (**Configuration > Settings > General**). |
| `{number}` | The number the call came from (the caller ID the person saw), read digit by digit. |

The editor suggests this message when the field is empty: `Sorry we missed you. This call was from {company}. To be removed from our list or to reach us, please call {number}. Goodbye.`

:::note[Telnyx]
The message is read out by the voice provider's text-to-speech, in the voice and language set for the provider.
:::

### How the abandonment rate is measured

The rate is measured for each dialer profile:

- **Answered by a person** counts every Power, Progressive or Predictive call a person picked up. With answering-machine screening on, the call counts from the moment the provider says a person answered. Calls answered by a machine or fax, busy, unanswered, failed and out-of-service calls are not counted.
- **Abandoned** counts the answered calls no agent reached within two seconds, for any of the reasons above.
- The **rate** is abandoned calls divided by calls answered by a person, over the rolling window your administrator sets (30 minutes unless changed). The editor also shows the last 30 days.

When **Enforce an abandonment-rate cap** is on and the rate over the window is above the **Maximum abandonment rate**, the dialer places no new calls for the profile until the rate falls back. The cap waits until the window holds at least **Abandonment sample floor** answered calls.

:::note[One profile per campaign]
The rate is measured per profile, not per campaign. If one profile dials several campaigns, their calls are measured together. Use a profile for each campaign when each campaign's rate must be kept on its own.
:::

### Ring time

An unanswered call rings for the profile's **Ring time** before it is given up, and never for less than 15 seconds, which is what the common abandoned-call rules expect.

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
