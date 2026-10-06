---
sidebar_label: Inbound Call Center
title: Run an Inbound Call Center
description: Set up your numbers, queues, opening hours, phone menu and agents so customers who call you reach the right person, and supervisors can watch and help live.
technical_manual:
  - contact-center/voice-routing
  - contact-center/agents-queues-dialer
---

## Who it's for and what you get

For **managers** and **administrators** setting up a phone line for customers, and the **supervisors** and **agents** who run it.

When it is done:

- a customer who calls your number hears a welcome and, if you want one, a keypad menu;
- the call waits in the right queue, with hold music and updates on their place in line, and is offered to a free agent with the right skills;
- after hours, callers hear a closed message and are held, sent to voicemail, moved to another queue or refused, as you choose;
- agents answer in the soft phone and record the outcome; supervisors watch the queues live, listen in, and read reports.

<video controls preload="metadata" width="100%" aria-label="Screencast of adding a voice calls entry point that routes a number's calls to a queue, with business hours and voicemail settings">
  <source src="/img/docs/um-entry-point.mp4" type="video/mp4" />
</video>

## Before you start

| You need | Who sets it up |
| --- | --- |
| A phone provider account (such as Telnyx) with the numbers customers will call | Your administrator, with IT |
| The features **Telephony**, your provider's feature (such as **Telnyx**), **Contact Center Work Distribution**, **Contact Center Inbound Entry Points**, **Contact Center Inbound Voice**, **Contact Center Agents**, **Telephony Soft Phone Extension**, and for supervisors **Contact Center Supervision & Live Dashboard** and **Reports**. **Contact Center Agent Entitlements** is recommended once you have more than one team. | Your administrator, in **Tools > Features** |
| A manager role with the permissions for queues, skills, agents, business hours, voice media and omnichannel addresses | Your administrator; see [Roles and permissions](../getting-started/roles-and-permissions.md) |
| The **Agent** role, plus **Use the telephony soft phone**, for everyone who answers calls; the **Supervisor** role for team leads | Your administrator |

<AskYourAdmin />

## Steps

1. **Connect the phone provider.** The administrator connects the provider under **Settings > Communication > Telephony**, and picks the default caller ID. See [Phone and SMS setup](../telephony-settings.md).
2. **List your numbers.** Add each number customers call under **Interaction Center > Management > Omnichannel Addresses**, with **Voice calls** ticked. See [Omnichannel addresses](../channel-endpoints.md).
3. **Set your opening hours.** Create a calendar with your weekly hours and holidays under **Interaction Center > Management > Business hours**. See [Business hours](../business-hours.md).
4. **Upload hold music and prompts (optional).** Add your hold music and any recorded menu prompts under **Voice Media**. See [Voice media](../voice-media.md).
5. **Create the queues.** Add a queue for each team, such as *Sales* and *Support*, under **Queues**, with its routing strategy, service level, business hours calendar, overflow, and what callers hear while they wait. Turn on callbacks here if you want them. Queue groups are optional and only organize reports. See [Queues and queue groups](../queues.md).
6. **Decide who works each queue.** Create skills such as *Spanish*, then give each agent their queues, priorities and skills under **Agent entitlements**. Give each supervisor an entitlement record with the queues they supervise too. See [Skills and agent entitlements](../skills-and-entitlements.md).
7. **Review the break reasons.** Check the reason codes agents pick when they step away, and add your own. See [Agent states](../agent-states.md).
8. **Route the number.** Add a **Voice calls** entry point under **Inbound entry points**: pick the number, set **Route to** to your main queue, pick the calendar and the closed action, write the welcome message, and choose where voicemail goes. Build a keypad menu if callers should choose a department. See [Inbound entry points and IVR menus](../entry-points-and-ivr.md).
9. **Give people extensions (optional).** Short internal numbers let colleagues call and transfer to each other by extension or by name. See [Extensions](../extensions.md).
10. **Set the house rules.** The administrator sets call recording and consent, and the outside numbers agents may transfer to, under **Settings > Contact Center**. See [Contact Center settings](../contact-center-settings.md).
11. **Get the agents ready.** Each agent installs the phone app, signs in to their queues, and records a voicemail greeting. See [Browser extension and Windows app](../phone-apps.md), [Agent workspace](../agent-workspace.md) and [Voicemail](../voicemail.md).
12. **Train agents on calls and wrap-up.** Agents answer, transfer and conference in the soft phone, then complete the activity with a disposition. See [Placing and handling calls](../calls.md), [Activities](../activities.md) and [Dispositions](../dispositions.md).
13. **Supervise.** Supervisors watch the queues and agents, step into calls, and work the shared voicemail. See [Live dashboard](../live-dashboard.md) and [Voicemail](../voicemail.md).
14. **Measure.** Use **Call insights**, **Queue usage** and **Agent productivity** to see how the line performs. See [Reports](../reports.md).

## Check that it works

1. Have an agent sign in to the queue and set themselves **Available**.
2. Call the number from another phone during opening hours. You should hear the welcome message and, if you built one, the menu.
3. The agent's docked agent bar and **My workspace** show the offer with a countdown. The agent accepts, talks, hangs up, and lands in **Wrap-up** until they complete the activity.
4. While the call waits or is connected, a supervisor sees it on the **Live dashboard**.
5. Call again with no agent available, and check that callers hear hold music and the announcements you set.
6. To test after-hours handling, call outside the calendar's hours, or add today's date to a test calendar's holidays. You should hear the closed message, then the closed action.
7. Open the **Call insights** report under **Reports** with today's date range, and find your test calls.

## Tips

- **Start with one queue and one entry point**, test them, and add menus and overflow after.
- **Clone** a queue or an entry point from its **Actions** menu to make a similar one quickly.
- A number is answered by one enabled entry point per channel. To change where a number goes, edit its entry point.
- A **disabled** business hours calendar never closes anything: queues that use it are always open.
- Callbacks need the **Contact Center Outbound Dialer** feature; without it no callback is stored.
- Supervisors see only the queues on **their own** entitlement record. An empty live dashboard almost always means a missing record.
- Agents who also answer texts can use the same queues for text conversations. See [Text your contacts](text-your-contacts.md).
- To let an AI answer the line instead, see [Let the AI answer your customers](ai-answers-customers.md).
