---
sidebar_label: AI Tools
title: Contact Center AI Tools
description: Read-only AI tools that let a contact center manager ask an AI profile why an agent is not getting calls, what is in a queue or campaign, why a record was not dialed and what is stuck, plus a ready-made Contact center manager profile template.
user_manual:
  - user-manual/contact-center-ai-assistant
---

| | |
| --- | --- |
| **Feature Name** | Contact Center AI Tools |
| **Feature ID** | `CrestApps.OrchardCore.ContactCenter.AITools` |
| **Depends on** | Contact Center Work Distribution (`CrestApps.OrchardCore.ContactCenter.Queues`) and Artificial Intelligence (`CrestApps.OrchardCore.AI`) |

The **Contact Center AI Tools** feature adds eight tools to the **Contact Center** category of the AI tool picker. Give them to an AI profile and a manager can ask, in plain words, why an agent is not getting calls, what a queue or campaign holds, why a record was not dialed, who is signed in and what is stuck. Each answer comes from the same services the supervisor dashboard and the Contact Center reports read, so the assistant and the screens never disagree.

Every tool only reads. Nothing in this feature changes a queue, a reservation, an activity, an agent or a setting.

For the people who use the assistant, see [Ask the contact center assistant](../user-manual/contact-center-ai-assistant.md) in the User Manual.

## Enabling the feature

Enable **Contact Center AI Tools** under **Tools > Features**. Orchard Core turns on **Contact Center Work Distribution** and **Artificial Intelligence** with it. The tools hold no configuration of their own, so there is no `appsettings.json` section, site setting, recipe step or deployment step for them.

To enable it from a recipe:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.ContactCenter.AITools"
      ]
    }
  ]
}
```

To chat with the tools from any admin page, also enable **AI Chat Admin Widget** (`CrestApps.OrchardCore.AI.Chat.AdminWidget`) and choose the profile in **Settings > Artificial Intelligence** under **Admin Widget**. See [Admin Chat Widget](../ai/chat.md#admin-chat-widget).

## Permissions

| Who | What they need |
| --- | --- |
| Anyone who uses a tool | **Monitor the Contact Center in real time** (`MonitorContactCenter`), which **Manage the Contact Center** (`ManageContactCenter`) implies. The *Supervisor* and *Administrator* roles hold it. |
| Anyone who chats with the profile | **Query any AI profile**, or **Query AI profile - *profile name***, and the permission to use the profile's tools (**Access any AI tool**, or **Access AI tool - *tool name*** for each one). |

A tool runs as the signed-in person and refuses an anonymous caller or a caller without the monitor permission. The refusal is an `error` in the result, which the assistant relays to the manager.

### Only what the person oversees

The tools show only the queues and campaigns the person oversees, by the same rule as the [live dashboard](../user-manual/live-dashboard.md): the queues and campaigns on the supervisor's own agent entitlement record, with a campaign's own work queue following the campaign. Queues, campaigns, agents, activities and decisions outside that scope are left out of every list, and a tool that is asked about one of them answers with an error such as `You don't oversee any queue or campaign that matches`.

A person with the Administrator role but no entitlement record therefore sees nothing, exactly as on the dashboard.

## The tools

Every tool is selectable in the **Contact Center** category of the **Capabilities** tab of an AI profile.

| Tool name | Title | Arguments | Answers |
| --- | --- | --- | --- |
| `contactCenterExplainAgentNoCalls` | Explain why an agent gets no calls | `agent` (required), `queueOrCampaign`, `lookbackHours` (default 12, at most 72) | Why one agent is not receiving calls or preview offers: sign-in, presence, live session, capacity, held reservations or unfinished work, what each queue would offer them now, and their recent offers, routing decisions and suppressions. |
| `contactCenterQueueInventory` | Queue and campaign inventory | `campaignOrQueue` | What each campaign and queue holds: waiting, reserved and assigned, dialable now, held back and why, the oldest waiting record, records in no queue and records assigned to a user for manual work. Calls out a campaign with no queue. |
| `contactCenterDispatchDiagnostics` | Dispatch diagnostics | `campaignOrQueue`, `includeHealthy` (default false) | Whether routing and the dialer are working for each queue and campaign: queue off or closed, a stalled paced dialer, who is signed in and available, what routing last decided and when, and the queues with work due but nothing happening. Without `includeHealthy`, only queues with a problem are listed. |
| `contactCenterDecisionLog` | Dialer decision log | `agent`, `campaign`, `queue`, `activityId`, `reason`, `kind` (`routing` or `suppression`), `fromUtc`, `toUtc` | Routing decisions and dial suppressions by agent, campaign, queue, activity, reason and period (the last 24 hours by default), with a summary by reason, the recent decisions and a call-out when a queue was drained in seconds, for example by `NoDestination`. |
| `contactCenterExplainActivity` | Explain an activity | `activityId` (required) | One record to call: its status, owner or reservation, queue item, dialer profile, channel and destination, why it is or is not dialable now, whether the contact's number covers an empty channel, and the dialer's last decisions about it. |
| `contactCenterListConfiguration` | Campaigns, profiles and queues | `section` (`all`, `campaigns`, `profiles` or `queues`), `name` | How things are set up: dialing mode, offer timeout, attempts, retry delay, pacing, calling window, routing strategy, reservation timeout, business hours and whether each is on. |
| `contactCenterAgentRoster` | Agent roster | `campaignOrQueue`, `presence`, `signedInOnly` (default true) | Who is signed in to which queues and campaigns, their presence, whether their session is live, calls held against capacity, any open offer, current work and stale state, and how many can be offered work right now. |
| `contactCenterStuckWork` | Stuck work | none | Reservations that expired but were never released (including one blocking a waiting queue item), activities stranded in progress with nothing live behind them, queues with work whose dialer stalled or cannot take its pacing lock, and agents whose busy or reserved state nothing live backs. |

Names and ids in the arguments are matched in words: a user name, display name or id for an agent, and a name or id for a queue or campaign. When a name matches more than one, the tool lists the candidates and asks for the exact one instead of choosing.

## What a result looks like

A result is JSON text with the same shape wherever a tool diagnoses something:

| Field | Meaning |
| --- | --- |
| `verdict` | The answer in a sentence or two. |
| `findings` | The things the tool found, ordered most likely first. |

Each finding has:

| Field | Meaning |
| --- | --- |
| `code` | A stable name for the kind of finding. |
| `likelihood` | How sure the tool is: `certain` (read straight from a record), `likely` (follows from the records), `possible` (fits, but is not proven) or `informational` (belongs in the picture, but explains nothing by itself). |
| `summary` | What was found, in a sentence a manager can read. |
| `facts` | The records the finding rests on, such as ids, counts and times in UTC. |

Lists such as queues, decisions or agents are capped at 40 rows, and each says how many there were in all. A tool that returns an `error` has not answered; the message says what to change.

## Safety

- **Supervisor permission.** Every tool needs the monitor permission and refuses everyone else.
- **Queue oversight.** Results are limited to the queues and campaigns the person oversees, as described above.
- **Read only.** No tool writes data, sends a message, places a call or changes a setting.
- **Size caps.** A result is at most 24,000 characters and 40 rows per list. A larger answer is replaced by an error asking for a narrower question, such as one queue, one campaign, one agent or a shorter period, so one call can never return a whole tenant's records.
- **Numbers.** Phone numbers appear as their last four digits.
- **Answer from the result.** Each tool's description tells the model to answer only from what it returns, and the Contact center manager template repeats the rule.

## The Contact center manager template

The module ships a profile template, **Contact center manager**, in the **Contact center** category of the **New AI Profile** picker (`Templates/Profiles/contact-center-manager.md`). Its card is greyed out until **Contact Center AI Tools** and **AI Chat** are enabled.

It creates a **Chat** profile with all eight tools already ticked and a system prompt that:

- makes the assistant a contact center operations assistant for managers, which always calls a Contact Center tool before answering about agents, queues, campaigns, activities, dialing or routing, and answers only from the results;
- maps each kind of question to its tool, for example why an agent gets no calls to `contactCenterExplainAgentNoCalls`, and why a record was not dialed to `contactCenterExplainActivity` followed by `contactCenterDecisionLog` with the activity id;
- leads with the verdict, lists the findings most likely first, and ends with what the manager can do next;
- says plainly when a tool returns an error (permission, no match, too large, not overseen) and how to rephrase, instead of guessing;
- reminds the manager that the tools only read and cannot fix anything.

After creating the profile, choose a chat deployment if the setup step did not, then pick the profile as the **Chat profile** of the Admin Widget. Add the profile's **Query AI profile - *profile name*** permission to the supervisor role if supervisors do not hold **Query any AI profile**. See [AI Profile Templates](../ai/profile-templates.md) for how templates and the picker work.

You can also build your own profile: tick the tools you want on the **Capabilities** tab, and write instructions that keep the assistant to the results.

## Example questions

| Ask | The tool that answers |
| --- | --- |
| Why isn't Jane Smith getting calls from Support? | `contactCenterExplainAgentNoCalls` |
| What is in the Lead Generation campaign right now? | `contactCenterQueueInventory` |
| Which queues have work due but nothing happening? | `contactCenterDispatchDiagnostics` |
| Why did the dialer skip this record? (with the activity id) | `contactCenterExplainActivity`, then `contactCenterDecisionLog` with the id |
| Why was the Lead Generation queue emptied in a few seconds this morning? | `contactCenterDecisionLog` |
| What is the offer timeout on the Support queue? | `contactCenterListConfiguration` |
| Who is signed in to Support and available? | `contactCenterAgentRoster` |
| Is anything stuck? | `contactCenterStuckWork` |

## Not yet available

The tools find problems; they do not fix them. These actions are not available, and no tool offers them, so an assistant that has these tools cannot do them for you:

- releasing a stuck reservation;
- requeueing failed activities;
- pausing or resuming a queue.

A manager makes these changes on the Contact Center screens, as before.
