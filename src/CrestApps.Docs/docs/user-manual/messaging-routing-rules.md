---
sidebar_label: Messaging Routing Rules
sidebar_position: 34
title: Messaging Routing Rules - Give Unowned Conversations to an Agent
description: Hand text conversations nobody owns, such as a reply to a workflow text or a broadcast, to an agent automatically, and choose who is eligible and how one is picked.
technical_manual:
  - omnichannel/messaging-workspace
---

A conversation nobody owns waits in the inbox until someone claims it. That includes every conversation a workflow's **Send Text Message** step or a broadcast starts without naming an agent: when the contact replies, the reply lands in a conversation that belongs to nobody, and a lead can go cold before anyone notices. **Routing rules** give those conversations to an agent automatically, the moment they need one.

| | |
| --- | --- |
| **Menu** | **Messaging** → **Routing Rules** |
| **Permission** | Manage messaging routing rules |
| **Feature** | Omnichannel Messaging Workspace |

<AskYourAdmin />

## How the rules work

Rules are tried from the lowest **Order** up. The first rule that applies to the conversation and finds an agent assigns it, and the rest are skipped. A rule that applies but finds nobody passes the conversation on to the next rule. When no rule assigns the conversation, it stays exactly where it was, in the queue's shared inbox or unowned, as if there were no rules. A conversation that already has an agent, or is one person's own, is never touched.

Rules work on every channel. Leave **Channels** empty and a channel you add later, such as email, is routed by the same rules.

## Create a rule

Open **Messaging** → **Routing Rules** and choose **Add Routing Rule**. The editor has four cards.

### General

| Field | What it does |
| --- | --- |
| **Name** | The rule's name. It is shown to agents on the conversation (*Assigned to (agent) by the routing rule (name)*), so choose one that makes sense to them. |
| **Description** | A note for administrators about what the rule is for. |
| **Order** | The rule's place among the rules. The lowest number is tried first. Leave gaps (10, 20, 30) so you can add a rule between two later. |
| **Enabled** | A disabled rule is never tried. Turn a rule off instead of deleting it while you test another. |

### When the rule applies

Tick each moment the rule is tried at. A rule is tried at the moments you tick and no others.

| Moment | What it covers |
| --- | --- |
| **A contact replies to a conversation nobody owns** | A reply on a conversation that is unassigned or waiting in a queue's shared inbox: the conversations workflows and broadcasts start, and the threads waiting in a shared pool. On by default. |
| **A contact starts a new conversation** | A first message on a number whose entry point sends it to a queue or to nobody. A number whose entry point names one agent keeps sending its texts to that agent. On by default. |
| **A workflow or broadcast starts a conversation without an agent** | The conversation is assigned as soon as the message goes out, before the contact replies. Off by default, because most texts a workflow sends are never answered, and an agent would be given conversations nobody wrote in. |
| **A contact's message is still waiting for a person** | Every minute, conversations nobody owns whose contact wrote last are tried again once the message has waited for **Wait (minutes)** (5 by default). A conversation that found no agent available when the contact wrote goes to one as soon as one is. On by default. |

Without **A contact's message is still waiting for a person**, a conversation that finds nobody the moment the contact writes stays in the inbox until someone claims it. Keep it on for any rule that only picks available agents.

### Conversations the rule takes

Limit a rule to some conversations. A list left empty takes every value, so a rule with all four lists empty takes every conversation.

| Field | What it does |
| --- | --- |
| **Channels** | The channels the rule takes, such as text messages. |
| **Business numbers and addresses** | The numbers or addresses your customers wrote to. Use it to give each of your numbers its own team. |
| **Queues** | The queue that owns the conversation or, for a new one, the queue its entry point sends it to. Empty takes conversations of any queue and of none. |
| **Campaigns** | The campaigns the conversation came from, such as the campaign of a workflow's text. Empty takes any campaign and none. |

### Who gets the conversation

First choose where the eligible agents come from (**Agents**), then how one of them is picked (**Pick**), and then what to leave out.

| Agents | Who they are |
| --- | --- |
| **Agents signed in to a queue** | The agents signed in to the queue right now, whatever their presence (available, on a call or on a break), but not those who are offline. |
| **Every agent who works a queue** | Every agent allowed to take the queue's work, signed in or not. |
| **Members of teams** | The members of the teams you pick and of the teams under them, on the current day. |
| **Users in roles** | The users in the roles you pick. |
| **Specific users** | The users you pick. |

For the two queue choices, **Queue** names the queue to use. Leave it on *The conversation's own queue* to follow each conversation; a conversation that belongs to no queue is then skipped by that rule, because there is no queue to look at. Texts that a workflow or a power dialer sends, and the replies to them, usually belong to no queue (the inbox shows them as **Unassigned**, not **Pooled**). To route those with a queue choice, pick the queue by name instead of *The conversation's own queue*, or use **Members of teams**, **Users in roles** or **Specific users**. Where the queue requires skills, only agents with them are picked, as for calls. Only users with an agent profile can get conversations; a profile is created the first time a user opens the messaging workspace.

| Pick | The agent chosen |
| --- | --- |
| **Round robin** | The one who was given a conversation by a routing rule longest ago, so conversations go around the agents in turn. When two agents are tied, the one with fewer open conversations. |
| **Fewest open conversations** | The one who holds the fewest open conversations. |
| **Least busy** | The one with the least work, counting open conversations and live calls, where a live call weighs as several conversations. |

Two checkboxes narrow the agents further. Both are on by default.

| Checkbox | What it does |
| --- | --- |
| **Only agents available for messaging** | Picks only agents whose **Available** switch is on and who have the messaging **Inbox** open right now. The switch is at the top of the inbox, beside the conversation list, and it is on until the agent turns it off, so agents do not have to set anything for the rule to work. An agent who closes the inbox or the browser stops being picked after a short while. Agents get the switch only when they have an agent profile. Clear this checkbox to also give conversations to agents who are not in the inbox: they find them waiting the next time they open it. |
| **Skip agents at their conversation limit** | Leaves out an agent who already holds as many conversations as their messaging limit allows (ten by default). A live call counts as several conversations. |

:::tip[Agents are not being picked]
If a conversation stays unassigned although a rule should take it, check, in this order: the rule is **Enabled** and its moment is ticked; its **Channels**, **Business numbers and addresses**, **Queues** and **Campaigns** include the conversation; the **Agents** choice finds people (for the queue choices, someone is signed in to that queue, and the conversation has a queue unless **Queue** names one); those people have the **Inbox** open with **Available** on, or **Only agents available for messaging** is cleared; and they are below their conversation limit. A rule that finds nobody is skipped, so the next rule is tried.
:::

## What happens when a rule assigns a conversation

The conversation is assigned to the agent, who is notified as with any other conversation given to them, and the thread says *Assigned to (agent) by the routing rule (name)*. A conversation that belongs to a queue stays the queue's, so supervisors still find it under the department. When the queue uses routed distribution, an agent who does not open the conversation in time loses it back to the queue, as with any routed conversation. A conversation that belongs to no queue becomes the agent's own.

## Example: send workflow text replies to the sales team

You have a workflow that texts new leads from the *Sales* number, and you want replies to reach whoever on the sales team is in the inbox.

1. Open **Messaging** → **Routing Rules** and choose **Add Routing Rule**.
2. **Name** it *Lead replies to sales*, and set **Order** to *10*.
3. Under **When the rule applies**, keep **A contact replies to a conversation nobody owns** and **A contact's message is still waiting for a person** ticked, and set **Wait (minutes)** to *5*.
4. Under **Conversations the rule takes**, set **Business numbers and addresses** to the *Sales* number.
5. Under **Who gets the conversation**, choose **Members of teams**, pick the sales team, and choose **Least busy**.
6. Keep both checkboxes ticked and save.

When a lead replies, the rule picks the least busy sales agent who is in the inbox. If no one is, the conversation stays in the inbox, and five minutes later the rule tries again and gives it to the first agent who has arrived.

## Move rules between sites

Rules are included in the **Messaging routing rules** deployment step and recipe step, so you can build them on a test site and move them to production. See the [technical manual](../omnichannel/messaging-workspace.md#exporting-and-importing-routing-rules).
