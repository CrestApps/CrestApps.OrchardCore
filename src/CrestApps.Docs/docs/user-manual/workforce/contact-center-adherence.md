---
sidebar_label: Contact Center Adherence
title: Contact Center Adherence
description: "Give every contact center agent a worker, clock agents in when they sign in, and watch whether agents follow their published schedule: the real-time board, a worker's day, exceptions, rule sets and alerts."
technical_manual:
  - modules/workforce-contact-center
---

Adherence tells you whether contact center agents are doing what their published schedule says: taking calls when they should, and breaks and meetings when those are scheduled. The **real-time board** shows every agent now, and each worker's **day** shows their adherence and conformance once it is over.

| | |
| --- | --- |
| **Menu** | Workforce > Contact Center Agents; Workforce > Adherence > Real-time, Exceptions, Rule Sets |
| **Permission** | Manage workers; View adherence; Manage adherence rules and exceptions |
| **Feature** | Workforce for Contact Center, Contact Center Sign-in Punches, Workforce Adherence |

<AskYourAdmin />

## Give every agent a worker

An agent is scheduled, takes time off and is paid as a worker. A new agent gets a worker when it is added. For agents that existed before:

1. Go to **Workforce > Contact Center Agents**. Agents without a worker come first.
2. Choose the **team for the new workers**, then **Create workers**.

An agent and a worker are linked through their user account, so a worker who already has the agent's user is used as is.

## Clock agents in when they sign in

With **Contact Center Sign-in Punches** turned on, an agent who signs in to the contact center is clocked in, and clocked out when they sign out. A worker who already clocked in another way, such as from My Work, isn't clocked in again, and signing out of the contact center doesn't clock them out. The team's time clock policy must allow punches from the contact center.

## Watch the real-time board

Go to **Workforce > Adherence > Real-time**. Each agent in the teams you manage shows:

- what their schedule has them doing now, and next;
- their contact center state, and for how long;
- **In adherence**, or **Out** with the minutes out. A red card is out longer than the alert time.

The board refreshes every five seconds. Pick a team to see only that team and its sub-teams. Select a name to see the worker's day.

:::tip[Alerts]
An agent out of adherence longer than the alert time starts the **Agent Out Of Adherence** workflow event once, so a workflow can message the team lead.
:::

## See a worker's day

The day shows adherence (the share of scheduled time in adherence), conformance (the hours worked against the scheduled work), the scheduled hours and the hours covered by exceptions, then a timeline and each stretch of the day: green in adherence, red out, grey covered by an exception. Times are in the worker's time zone.

A day is saved each hour after it ends. Before that, the page works it out from the schedule and the agent's states. Choose **Save**, or **Compute again** after adding an exception.

## Record an exception

Some time shouldn't count against an agent, such as coaching or a phone outage.

1. Select **Add exception** on the board, a worker's day or **Workforce > Adherence > Exceptions**.
2. Choose the worker, the day and the times, in the worker's time zone, and the reason.
3. Select **Add**. Compute the worker's day again if it is already saved.

## Set the rules

Go to **Workforce > Adherence > Rule Sets** and choose **Add rule set**. Name it and choose its teams; their sub-teams use it too. For each activity:

| Field | What it does |
| --- | --- |
| **States** | The contact center states that are in adherence. |
| **Reasons** | Optional. The state must have one of these reasons, such as *Meal* for a break. |
| **Start grace** | How long the agent may still be in the previous activity. |
| **End grace** | How long before the end the agent may already be in the next activity. |
| **Shortest slip** | Time out shorter than this is forgiven. |
| **Alert after** | How long out of adherence before an alert. |

A team without a rule set uses the defaults: work is available, offered work, busy or wrap-up; breaks and meals are a break; meetings and training are their own states; two minutes of grace; alerts after five minutes.

## Reports

In **Reports > Report Builder**, the **Workforce** data source has **Adherence days** and **Adherence segments**.
