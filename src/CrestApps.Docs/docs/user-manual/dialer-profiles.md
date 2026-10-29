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

<YouTube video="contact-center-dialer-modes-compared" title="Video overview comparing the Preview, Power, Progressive and Predictive dialing modes" />

<YouTube video="contact-center-setup-walkthroughs" title="Video of creating a dialer profile and switching between preview, power and progressive modes" start={1001} end={1067} />

## The dialing modes

| Mode | Who presses dial | What the agent sees |
| --- | --- | --- |
| **Preview** | The agent. The record is offered to an agent signed in to the campaign; the agent reviews it and clicks **Dial** (or **Skip**). The server then places the call after the compliance checks. | The record opens first; the call starts when they choose Dial, and they are on it from the first ring. |
| **Power** | The system. When an agent in the campaign is available, the dialer reserves that agent and places the call. Each look at the campaign starts up to **Calls per agent** calls (1 to 3), and each call has its own reserved agent. | While the customer's phone rings, the agent is *Reserved* and sees *Waiting for a connected call*. When a person answers and the agent is connected, the record opens on its own; there is nothing to press. |
| **Progressive** | The system, like Power, reserving one agent per call. A look keeps placing calls until no free agent is left, with a safety limit of 100 calls per look. There is no **Calls per agent**. | Same as Power. |
| **Predictive** | The system, like Power: with **One call per reserved agent** every call reserves its own available agent before it is placed, and fewer calls are placed per look as the abandonment rate approaches the cap. With **Over-dial** it places more calls than there are free agents. See [Predictive dialing](#predictive-dialing). | Same as Power; with Over-dial there is no *Reserved* state, and the call is connected to the agent when a person answers. |

Older profiles saved as *Manual* are shown and saved as **Preview**.

### Compare the dialing modes

<YouTube video="contact-center-dialer-modes-compared" title="Video of how each dialing mode works, and when to choose which" start={0} end={0} />

| | **Preview** | **Power** | **Progressive** | **Predictive** |
| --- | --- | --- | --- | --- |
| **Who presses Dial** | The agent | Nobody: the system dials | Nobody | Nobody |
| **Is an agent held before the customer's phone rings** | Yes: the agent is offered the record, and is on the call from the first ring | Yes: one reserved agent for each call | Yes: one reserved agent for each call | Yes with **One call per reserved agent**; no with **Over-dial** |
| **How many calls per look at the campaign** | None: the agent decides | Up to **Calls per agent** (1 to 3) | As many as there are free agents, up to 100 | One call (with the cap off) or up to **Calls per agent**, fewer as the abandonment rate nears the cap; with Over-dial, more than the free agents |
| **Can a call be abandoned** | No: the agent places the call and listens to it | Yes, when a person answers and the agent is not connected within two seconds | Yes, in the same way | Yes, in the same way; with Over-dial also when nobody is free, by design |
| **Dialing card fields** | **Mode** and **Voice call provider** only | Also **Calls per agent**, **Max attempts**, **Retry delay**, **Ring time**, **Screen out answering machines**, **Show each dial to the agent** | The same, without **Calls per agent** | The same as Power |
| **Abandoned calls card** | Not shown | Shown | Shown | Shown, plus **Predictive pacing** |
| **Needs the feature** | Contact Center Outbound Dialer | Contact Center Paced Dialing | Contact Center Paced Dialing | Contact Center Paced Dialing |

A *look* is one time the dialer checks the campaign for waiting records and free agents. **Calls per agent** limits the calls one look may start; it does not multiply the number of agents. Because looks follow each other within about a second (see [When calls are placed](#when-calls-are-placed)), Power with one call per look and Progressive end up placing calls at nearly the same pace. They differ in how fast a burst of free agents is filled, not in who is reserved or what can go wrong. Predictive with **One call per reserved agent** never calls ahead of the free agents: it is Power with an abandonment brake. Only **Over-dial** calls ahead.

A path that works for most teams:

1. Start with **Preview**: agents read every record first, nobody can be abandoned, and it is the easiest to train. Use it for new teams and for high-value lists.
2. Move to **Progressive** or **Power** when the list is steady and agents wait between calls. Turn on **Enforce an abandonment-rate cap** and **Play a message when a call is abandoned** first.
3. Choose **Predictive** with one call per reserved agent when you want the brake. Choose **Over-dial** only when you have measured the answer rate and your rules allow abandoned calls, as the cap and the message are required.

### Preview dialing, start to finish

<YouTube video="contact-center-agent-walkthroughs" title="Video of a live preview-dial call: the offer arrives, the agent clicks Dial, uses mute, hold, keypad and transfer, hangs up and completes the activity" start={172} end={222} />

The agent is signed in to the campaign in the soft phone (right). The record is offered in the docked bar and the workspace; the agent clicks **Dial**, the customer answers, and when the call ends the agent is in **Wrap-up** until the activity is completed.

### Power dialing, start to finish

<YouTube video="contact-center-agent-walkthroughs" title="Video of a live power-dial call: the agent signs in to the campaign, the dialer places the call, the record opens and the activity is completed" start={225} end={263} />

With a Power profile the agent only signs in and stays **Available**. Within about a second the dialer places the call. Nothing appears on the agent's screen while the call rings: once the customer answers and the agent is connected, the docked agent bar opens and the activity's **Complete activity** page opens on its own (unless the page the agent is on has unsaved changes; **Open activity** on the bar then opens it). A call that does not reach the agent is never shown to them (see [What happens to each record](#what-happens-to-each-record)).

### When calls are placed

Power, Progressive and Predictive campaigns are dialed as soon as there is a record to dial and an agent free to take the call. The dialer looks at the campaign again, within about a second, whenever:

- an agent signs in to the campaign, or sets themselves **Available**,
- an agent comes back to **Available** after a call or after wrap-up,
- a call the dialer placed ends without reaching an agent (no answer, busy, an answering machine, or a failed call),
- an offer is released or runs out,
- records are loaded into the campaign.

So when numbers keep going unanswered, the next number is dialed right after the last one ends, instead of the agent waiting for the next minute. A burst of changes at the same moment, such as several agents signing in together, is handled as one look, and the campaign keeps being dialed while calls are being placed.

Each look follows the profile's own limits exactly: **Calls per agent**, the Progressive limit, and the Predictive abandonment rules. Only how soon the dialer looks changes, not how many calls it places.

The dialer still looks at every campaign once a minute as well, whatever else is going on. That is a safety net: if a change is missed, for example while the site restarts, the campaign is dialed again within a minute.

## Create a profile

1. Open **Interaction Center > Management > Dialer Profiles** and click **Add Dialer Profile**.
2. On **General**, enter a **Name**, an optional **Description**, and leave **Enabled** ticked. A disabled profile places no calls, and the records loaded under it aren't offered to agents until you turn it back on.
3. On **Dialing**, pick the **Mode** and the **Voice call provider** (or *Default provider*). Power, Progressive and Predictive show extra fields:

   | Field | Modes | What it does |
   | --- | --- | --- |
   | **Calls per agent** | Power, Predictive | The most calls started each time the dialer looks at the campaign (see [When calls are placed](#when-calls-are-placed)), 1 to 3. It is a limit per look, not per agent: each call reserves its own available agent first, so it never dials more calls than there are available agents. Predictive uses it too, until over-dialing takes over. |
   | **Max attempts** | Power, Progressive, Predictive | The fallback limit on how many attempts one contact may get, counting the first call and every follow-up activity created to try again. It applies when the subject flow sets no maximum of its own; see [Retries and the attempt limit](#retries-and-the-attempt-limit). Default 3. |
   | **Retry delay (minutes)** | Power, Progressive, Predictive | The wait after an attempt before the next attempt is dialed when the subject flow does not say when, and the shortest wait when it does. See [Retries and the attempt limit](#retries-and-the-attempt-limit). Default 60. |
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
Agents on an over-dialing campaign do not see an offer before a call: the call is theirs as soon as they are connected. While an agent is **Available** and signed in to a campaign, the soft phone stands by and answers the call it is given at once, with no click; inbound offers still ring for the agent to accept. If the agent's phone does not pick up within a few seconds, the agent is put back to **Available**, the person hears the abandoned-call message and the call counts as abandoned. If they also take inbound calls, an inbound call that reaches them first takes them out of the campaign until it ends.
:::

The settings on the **Predictive pacing** card:

| Field | What it does |
| --- | --- |
| **Measured answer rate** | Shown once a Predictive profile has placed calls: the share of recent calls with an outcome that a person answered, how long people take to answer, and how long agents take to be connected. A call still ringing is left out until it has an outcome. |
| **Last pacing decision** | Shown once an over-dialing profile has paced a campaign: for each campaign, when the last cycle ran, whether it over-dialed, reserved an agent per call or did not dial and why, how many calls it placed of those allowed, the free agents (plus those about to free up when counted), the calls ringing without an agent, the answer rate and the abandonment rates over the rolling window and the compliance window. Read-only; it changes every cycle. |
| **Pacing** | **One call per reserved agent** (the default) or **Over-dial**. Over-dialing needs **Enforce an abandonment-rate cap**, the abandoned-call message and a target below the cap. |
| **Target abandonment rate** | The rate over-dialing steers toward. Default 2%. For over-dialing it must be lower than the **Maximum abandonment rate**, which stays the hard limit. |
| **Lines per agent** | The most calls ringing for each free agent, from 1 to 5. Default 2. |
| **Calls in flight** | The most calls ringing at once for one campaign, from 1 to 1000. Default 100. |
| **Answer rate sample floor** | How many calls with an outcome are needed before the answer rate is trusted, from 10 to 10000. Default 50. Until then the profile reserves an agent for every call. |
| **Answer rate window (minutes)** | How far back the answer rate is measured, from 5 to 240. Default 15. |
| **Count agents about to free up** | Also counts agents expected to finish their call and wrap-up before a new call is answered. Off by default. |
| **Share of agents about to free up** | How many of those agents are counted, from 0 to 100%. Default 50%. |
| **Connect wait (milliseconds)** | How long a person who answered may wait for an agent to free up before the abandoned-call message plays, from 0 to 1500. Default 0, which is recommended: a call not connected within two seconds counts as abandoned. A wait above 0 can only be saved once the profile has connected at least 20 calls, and only while the wait plus the time the slowest 5% of those connects took stays within two seconds; the message names the longest wait allowed. |
| **Retry abandoned calls only with an agent reserved** | When the contact is called again after their call was abandoned, an agent is reserved for the call first, so the person is not abandoned twice. This covers the next attempt created by **Try again** or by a workflow too. On by default. |

## Abandoned calls

With Power, Progressive and Predictive dialing, the customer is dialed first and the reserved agent is connected once a person answers. A call is **abandoned** when a person answers and no agent is connected to them within two seconds. That happens when the agent's phone does not pick up (the browser was closed, the phone is not registered, the network dropped), when the agent cannot be connected at all, when the agent is connected later than two seconds after the answer, or when the person hangs up after waiting more than two seconds.

These settings support the common abandoned-call rules for automated dialing. Confirm which rules apply to your calls; the settings do not make a campaign compliant on their own.

### Answer to agent time

When a call is placed for an agent who is already reserved for it (Preview, Power, Progressive, and Predictive with one call per reserved agent), the agent's soft phone is rung while the customer's phone is still ringing. The soft phone answers at once, without a click, and the agent hears silence until the customer answers. The agent's line is then joined to the customer, which takes well under a second, so the person who answers hears the agent almost at once.

- **Without answering-machine screening**, the agent is joined as soon as the customer answers.
- **With answering-machine screening**, the agent is joined as soon as the provider says a person answered. The two seconds are measured from that verdict, not from the answer: the person spends the screening time saying hello, and the agent never hears a voicemail greeting.
- **When a machine answers, nobody answers, or the customer hangs up first**, the agent's line is hung up. The agent goes straight back to Ready, with no wrap-up and no record opened.
- **When the agent's line drops before the customer answers** (the soft phone was closed, the network dropped), nothing is lost: the answer rings the agent the usual way. That call may take longer than two seconds to connect and count as abandoned.

The application log shows the time for every connected call, for example `connected to an agent 0.4 seconds after the answer, within the abandonment threshold`. A call connected later than two seconds is logged as abandoned.

:::note[Telnyx]
Holding the agent's line while the customer rings needs the Telnyx soft phone to be open and registered when the call is placed. Your administrator can turn this off with `CrestApps:ContactCenter:Coordination:DialerAgentParkingEnabled`, which rings the agent only once the customer answers, as before.
:::

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

The disposition decides what happens next. Wire **Try Again** to it in the [subject flow](subject-flows.md) to call the contact again: the next attempt is a new activity, put back in the same campaign with the same dialer profile, due no sooner than the retry delay. A workflow can make the same decision with the **Schedule Dialer Retry** task; see [Workflows](workflows.md#call-a-contact-again).

### Retries and the attempt limit

The subject flow decides first, and the dialer profile's **Max attempts** is the fallback:

| The subject flow | What happens |
| --- | --- |
| Has a disposition for how the call ended, and its **Try Again** sets **Maximum attempts** | The contact is tried up to that maximum. The profile's **Max attempts** does not apply. |
| Has a disposition for how the call ended, and its **Try Again** leaves **Maximum attempts** empty | The contact is tried up to the profile's **Max attempts**. |
| Has a disposition for how the call ended, with no **Try Again** | The contact is not called again: the flow decided. |
| Has no disposition for how the call ended (for example, nothing has the *No answer* outcome) | The dialer dispositions the call with the matching outcome, such as *No answer*, and calls the contact again after the retry delay, up to the profile's **Max attempts**. |

When the next attempt is dialed follows the same order. A date picked when the call is dispositioned, or the **Try Again** action's **Default schedule hours**, sets it. When neither is set, as when the dialer dispositions a call on its own, the next attempt is dialed after the profile's **Retry delay**. No attempt is dialed sooner than the **Retry delay**.

The dialer retries this way only calls that rang out, were busy, reached a machine, or were answered and dropped before an agent joined. A call the network failed or the far end rejected, and a number not in service, are dispositioned but not retried. Attempts count across the follow-ups for one contact, so a limit of 3 means the first call and two more.

Queue callbacks use a built-in preview profile that skips the do-not-call and calling-window checks, because the customer asked to be called.

## Next steps

- Before you start an automated (power or progressive) campaign, set up the [dispositions](dispositions.md) and the [subject flow](subject-flows.md) first, so every call outcome has a result, and check the do-not-call, retry delay and calling window settings above.
- [Load dialer activities](load-inventory.md#dialer-loads) with this profile and a campaign.
- Give agents the campaign on their [entitlements](skills-and-entitlements.md), then have them [sign in to it](agent-workspace.md). [Calls from a dialer campaign](agent-workspace.md#calls-from-a-dialer-campaign) explains what agents see in each mode.
