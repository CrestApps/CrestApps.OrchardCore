---
sidebar_label: Agent States
sidebar_position: 22
title: Agent States (Reason Codes)
description: Create the reasons agents choose when they step away, such as lunch, a team meeting or training, and control where they appear in the presence menu.
technical_manual:
  - contact-center/agents-queues-dialer
---

When agents are not taking work, they pick a reason from their presence menu: *Lunch*, *Team meeting*, *Training* and so on. Each reason code maps to one of the built-in presence states, and reports and the live dashboard show both the state and the reason.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Agent states |
| **Permission** | Manage Contact Center agents |
| **Feature** | Contact Center Agents |

<AskYourAdmin />

<YouTube video="contact-center-agent-states" title="Video overview of agent states: creating a reason code, the presence menu and retiring a reason code" />

<YouTube video="contact-center-setup-walkthroughs" title="Video of creating an agent state reason code and seeing it in the presence menu" start={392} end={417} />

## Create a reason code

<YouTube video="contact-center-agent-states" title="Video of creating a reason code with a name, a state and a sort order" start={73} end={106} />

1. Open **Interaction Center > Management > Agent states** and click **Add reason code**.
2. Enter a **Name**, for example *Customer visit*, and an optional **Description**.
3. Choose which state it **Applies to state**: Break, Away, Do not disturb, Meeting, Training, or After-hours unavailable.
4. Set a **Sort order**. Lower numbers appear higher in the presence menu.
5. Leave **Enabled** ticked and click **Save**.

A disabled reason code is hidden from the presence menu but stays on historical records. To retire a code, open it, untick **Enabled** and click **Save**.

<YouTube video="contact-center-agent-states" title="Video of disabling a reason code so it leaves the presence menu" start={136} end={157} />

## What agents see

<YouTube video="contact-center-agent-states" title="Video of the presence menu in My workspace, with Available, the reason codes and the Break group" start={106} end={136} />

- Break reasons are grouped under a **Break** heading, listed after the other reasons. When the agent is busy on a call, the heading reads **Request break**: the break starts as soon as the current work ends.
- Every other reason code is listed on its own and sets its state right away.
- **Available** is always listed. **Offline** is in the soft phone menu only; the **My workspace** menu has no Offline item.
- If no reason codes exist at all, the menu falls back to the plain states: Away, Do not disturb, Meeting, Training and After-hours unavailable.

The tenant starts with these reason codes: *Short break* and *Lunch* (Break), *Away from desk* (Away), *Team meeting* and *Coaching* (Meeting), *Training* (Training), and *System issue* (Do not disturb).
