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

<video controls preload="metadata" width="100%" aria-label="Screencast of creating an agent state reason code and seeing it in the presence menu">
  <source src="/img/docs/um-agent-states.mp4" type="video/mp4" />
</video>

## Create a reason code

1. Open **Interaction Center > Management > Agent states** and click **Add reason code**.
2. Enter a **Name**, for example *Coaching*.
3. Choose which state it **Applies to**: Break, Away, Do not disturb, Meeting, Training, or After-hours unavailable.
4. Set a **Sort order**. Lower numbers appear higher in the presence menu.
5. Leave **Enabled** ticked and click **Save**.

A disabled reason code is hidden from the presence menu but stays on historical records.

## What agents see

- Break reasons are grouped under a **Break** heading. When the agent is busy on a call, the heading reads **Request break**: the break starts as soon as the current work ends.
- Every other reason code is listed on its own and sets its state right away.
- **Available** and **Offline** are always listed.
- If no reason codes exist at all, the menu falls back to the plain states: Away, Do not disturb, Meeting, Training and After-hours unavailable.

The tenant starts with these reason codes: *Short break* and *Lunch* (Break), *Away from desk* (Away), *Team meeting* and *Coaching* (Meeting), *Training* (Training), and *System issue* (Do not disturb).
