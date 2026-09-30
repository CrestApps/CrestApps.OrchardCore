---
sidebar_label: Dialer Profiles
sidebar_position: 26
title: Dialer Profiles - Preview, Power and Progressive Dialing
description: Choose how outbound calls are placed for a campaign, what the customer sees as caller ID, and the compliance rules every call must pass.
---

A **dialer profile** decides **how** outbound calls are placed: whether an agent reviews each record before the call (preview), or the system dials for available agents (power and progressive). It also carries the caller ID and the compliance rules. You pick the profile when you [load dialer inventory](load-inventory.md#dialer-loads); the campaign you pick there is what agents sign in to.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Dialer Profiles |
| **Permission** | Manage the Contact Center dialer |
| **Features** | Contact Center Outbound Dialer (`CrestApps.OrchardCore.ContactCenter.Dialer`) for Preview. Contact Center Paced Dialing (`CrestApps.OrchardCore.ContactCenter.Dialer.Paced`) adds Power and Progressive. |

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

With a Power profile the agent only signs in and stays **Available**. Within a minute the dialer places the call, and the record opens on its own as the call rings.

## Create a profile

1. Open **Interaction Center > Management > Dialer Profiles** and click **Add Dialer Profile**.
2. On **General**, enter a **Name**, an optional **Description**, and leave **Enabled** ticked. A disabled profile places no calls.
3. On **Dialing**, pick the **Mode** and the **Voice call provider** (or *Default provider*). Power and Progressive show extra fields:

   | Field | Modes | What it does |
   | --- | --- | --- |
   | **Calls per agent** | Power | Calls started per pacing cycle for the campaign, 1 to 3. Each call reserves its own agent, so it never dials more calls than there are available agents. |
   | **Max attempts** | Power, Progressive | How many times one record may be dialed. Default 3. |
   | **Retry delay (minutes)** | Power, Progressive | How long to wait after an attempt before dialing the record again. Default 60. |
   | **Screen out answering machines** | Power, Progressive | **Off** (default), **Standard detection** or **Premium detection**. When on, the agent is connected only after the provider hears a person. A call answered by a voicemail or fax machine is hung up, the agent goes straight back to Ready without wrap-up, and the record is dialed again after the retry delay (it counts as an attempt). The person who answers hears a few seconds of silence while the call is screened. Telnyx only. |

4. On **Caller ID**, enter the **Caller ID** number customers see (empty uses the provider's default), and pick the **Default calling region** used for numbers written without a country code.
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

Before every call the dialer checks, in order: the number is valid, the number is not [known to be out of service](numbers-not-in-service.md), the record has attempts left, the retry delay has passed, the contact has not opted out, the calling window is open, the abandonment cap allows dialing, and the number is not on a do-not-call registry.

- A record with no valid number, or with no attempts left, becomes **Failed**.
- A record on a do-not-call list becomes **Cancelled**.
- A record whose number is known not to be in service becomes **Cancelled**, and is never dialed.
- A call the carrier rejects as not in service (unallocated, SIP 404/410/484/604) is **Completed** with the not-in-service disposition, without an agent. The number is not dialed again. See [Numbers Not In Service](numbers-not-in-service.md).
- Anything else (closed calling window, cap reached, registry unreachable) is simply tried again in a later cycle.

Queue callbacks use a built-in preview profile that skips the do-not-call and calling-window checks, because the customer asked to be called.

## Next steps

- [Load dialer inventory](load-inventory.md#dialer-loads) with this profile and a campaign.
- Give agents the campaign on their [entitlements](skills-and-entitlements.md), then have them [sign in to it](agent-workspace.md).
