---
sidebar_label: AI Assistant for Managers
sidebar_position: 25
title: Ask the Contact Center Assistant
description: Ask an AI assistant why an agent is not getting calls, what is in a queue or campaign, why a record was not dialed and what is stuck, in plain words, from any admin page.
technical_manual:
  - contact-center/ai-tools
---

The **contact center assistant** is an AI chat for managers and supervisors. Instead of opening several screens and reports to work out why an agent is quiet or a campaign is not dialing, you ask in plain words and it checks the contact center for you, then tells you what it found and what you can do about it.

It only looks. It cannot sign an agent in, release a call, requeue a record or pause a queue. You make any change on the usual screens.

| | |
| --- | --- |
| **Menu** | Artificial Intelligence > Profiles (to set it up); the chat button on every admin page (to use it) |
| **Permission** | Monitor the Contact Center in real time (to use it); Manage AI profiles (to set it up); Query any AI profile, or Query AI profile - *profile name*, and Access any AI tool, or Access AI tool - *tool name* (to chat) |
| **Feature** | Contact Center AI Tools, with AI Chat Admin Widget to chat from every admin page |

<AskYourAdmin />

## Set up the assistant

1. Ask your administrator to turn on the **Contact Center AI Tools** and **AI Chat Admin Widget** features.
2. Open **Artificial Intelligence > Profiles** and click **Add Profile**. In the **New AI Profile** picker, filter by the **Contact center** category and choose **Contact center manager**.
3. Complete the short setup step and click **Create profile**. The profile already has the contact center tools ticked and instructions that keep it to what the tools report.
4. Open **Settings > Artificial Intelligence**, go to the **Admin Widget** section, choose the new profile as the **Chat profile** and click **Save**.
5. Make sure the supervisors' role may use the profile: give it **Query AI profile - *profile name*** (or **Query any AI profile**) under **Access Control > Roles**.

If you would rather build your own profile, open its **Capabilities** tab and tick the tools in the **Contact Center** category. See [Tools, agents and connections](ai/tools-and-agents.md) and [Chat widgets](ai/chat-widgets.md).

## Ask a question

Click the round chat button in the bottom-right corner of any admin page and type your question. Here is what to ask and what the assistant checks.

| You want to know | Ask something like | What it checks |
| --- | --- | --- |
| Why an agent gets no calls | *Why isn't Jane Smith getting calls from Support?* | **Explain why an agent gets no calls**: the agent's queues and campaigns, status, live session, capacity, work that holds them, what each queue would offer them now and their recent offers and decisions. |
| What a queue or campaign holds | *What is in the Lead Generation campaign?* | **Queue and campaign inventory**: records waiting, reserved, assigned and dialable now, what is held back and why, and records that are in no queue. |
| Whether routing and the dialer are working | *Which queues have work due but nothing happening?* | **Dispatch diagnostics**: queues that are off or closed, a stalled dialer, who is available and what routing last decided. |
| Why a record was not dialed | *Why wasn't activity 12345 dialed?* (with the record's id) | **Explain an activity**, then the **Dialer decision log** for that record: its status, owner, channel and number, and what the dialer decided. |
| Why many records were skipped | *Why did the Lead Generation queue empty in seconds this morning?* | **Dialer decision log**: skipped dials and routing decisions by reason and time, with a note when a queue was drained in seconds. |
| How something is set up | *What is the offer timeout on the Support queue?* | **Campaigns, profiles and queues**: modes, timeouts, attempts, pacing, calling windows and hours. |
| Who is signed in | *Who is signed in to Support and available?* | **Agent roster**: status, capacity and current work for each agent. |
| What is stuck | *Is anything stuck?* | **Stuck work**: reservations nobody released, records stranded in progress, stalled queues and agents with a stale status. |

Give names as you know them, such as an agent's user name or a queue's name. If a name fits more than one, the assistant lists them and asks which you mean.

## Read the answer

The assistant starts with a short answer, then lists what it found, **most likely cause first**. For each one it says how sure it is:

- **Certain**: read straight from a record.
- **Likely**: follows from the records.
- **Possible**: fits, but is not proven.
- **For information**: part of the picture, but explains nothing by itself.

It ends with what you can do next, for example fix a record's channel or phone number, ask the agent to sign in to the queue or set their status to Available, finish their after-call work, or turn a queue on. Phone numbers show as their last four digits only.

## What the assistant will and will not show

- It shows only the queues and campaigns **you oversee**: the ones on your own [agent entitlement](skills-and-entitlements.md) record, the same as the [live dashboard](live-dashboard.md). A supervisor without an entitlement record sees nothing, even with the Administrator role.
- If it says you do not oversee a queue or campaign, ask your administrator to add it to your access.
- It answers only from what the contact center reports. If the information is not there it says so, and it will not guess.
- A very broad question can return too much to read. It then asks you to narrow the question to one queue, one campaign, one agent or a shorter time range.
- Without the supervisor permission, it tells you it cannot be used and to ask an administrator.

## What it cannot do yet

The assistant finds problems but does not fix them. These are not available:

- Releasing a stuck reservation.
- Requeueing failed activities.
- Pausing or resuming a queue.

Make these changes on the Contact Center screens, such as the [live dashboard](live-dashboard.md) and [Queues](queues.md).

:::tip[Check the screen too]
The assistant reads the same information as the live dashboard and the reports. If an answer surprises you, open the matching screen to confirm it before you act.
:::
