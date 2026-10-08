---
sidebar_label: Troubleshooting
title: Troubleshooting
description: Answers to common problems by symptom, such as a missing menu, a phone that does not ring, calls without audio, texts that do not arrive, an AI that does not answer and an empty report.
technical_manual:
  - contact-center/runbooks
  - contact-center/production-support
  - telephony/index
---

Find your symptom below and work through the checks in order. Most problems are a missing permission, a missing setup record, or a phone that is not signed in. When a check needs your administrator or IT, it says so.

## I don't see a menu or a button

1. **Check the page for that task in this manual.** The table at the top names the **Menu**, the **Permission** and the **Feature** it needs. If you lack the permission, or the feature is off, the menu is hidden.
2. **Ask your administrator** for the permission, or to turn on the feature. [Roles and permissions](getting-started/roles-and-permissions.md) has a message you can copy.
3. **Some buttons need a site setting.** For example, **Pause recording** and **Collect data securely** appear only when the administrator switches them on in [Contact Center settings](contact-center-settings.md). The **Diagnostics** tab of the soft phone appears only when the administrator enables it.
4. **Some buttons appear only at the right moment.** The supervisor's **Listen**, **Whisper** and **Barge** buttons show on an agent's card only while that agent is on a call. **Complete activity** shows once there is an activity to complete.
5. **Agents: the soft phone needs its own permission.** The Agent role does not include **Use the telephony soft phone**. Ask your administrator to add it.

## A page opens but is empty

- **Live dashboard or shared voicemail is empty.** These show only the queues and campaigns on **your own** [agent entitlement](skills-and-entitlements.md) record, even for administrators. Ask your manager to give you an entitlement record with the queues you supervise.
- **A report shows nothing.** See [A report is empty](#a-report-is-empty).
- **Your activity list is empty.** **Interaction Center > Activities** lists only manual activities assigned to you that are not started yet. Unassigned dialer work and automated (AI) activities are not listed there. Check the list's filters too, such as **Scheduled**. See [Activities](activities.md).

## I can't sign in to a queue or a campaign

1. **You only see the queues and campaigns you are entitled to.** If one is missing from the soft phone's **Work** tab, ask your manager to add it to your [entitlement record](skills-and-entitlements.md).
2. **You need the permission** **Sign in to Contact Center queues and campaigns**. The Agent role has it.
3. **The phone app must be signed in to the site.** Sign in to the site in the same browser as the extension, or inside the Windows app. See [Browser extension and Windows app](phone-apps.md).
4. **Removed from a queue?** When a manager removes a queue from your entitlements, you are signed out of it.

## My phone doesn't ring

1. **Is the phone app running?** The browser extension or Windows app must be installed and signed in to the site. The Windows app keeps running in the system tray after you close its window; if you chose **Quit**, calls do not ring. See [Browser extension and Windows app](phone-apps.md).
2. **Are you signed in and Available?** In the soft phone's **Work** tab you must be signed in to the queue, and your presence must be **Available**. A break or another reason code stops new offers. See [Agent workspace](agent-workspace.md).
3. **Is the queue open?** Routing pauses while the queue's business hours calendar is closed. See [Queues](queues.md).
4. **Is the number routed to your queue?** The number's entry point decides where its calls go. Ask your manager to check it. See [Inbound entry points and IVR menus](entry-points-and-ivr.md).
5. **Do you have the skills?** A queue can require skills at a minimum proficiency. If you lack one, the work goes to other agents. See [Skills and agent entitlements](skills-and-entitlements.md).
6. **Is the workspace connected?** **My workspace** shows *Connected* while live updates work. With *Reconnecting...* or *Disconnected*, new offers do not appear until it reconnects. Reload the page.
7. **An offer came but you missed it.** An offer rings for the queue's reservation timeout (30 seconds by default), then follows the queue's unanswered-offer action, usually offering it to the next agent.

**A colleague says your extension "is not available right now".** Your soft phone is not open and signed in, so your extension cannot be reached. Open the phone app. To call an extension yourself, your own soft phone must be signed in too.

**Nothing rings for anyone.** Ask your administrator. With Telnyx, every call event is rejected until the **Webhook public key** is set in [Phone and SMS setup](telephony-settings.md). IT can follow the provider steps in the [runbooks](../contact-center/runbooks.md) and the [Telnyx reference](../telephony/telnyx.md).

## My calls have no audio, or the audio is poor

1. **Is the microphone allowed?** If the browser blocked the microphone, allow it in the browser and click **Retry microphone** in the soft phone.
2. **Is the right device picked?** Click the **headset** button and check the **Microphone** and **Speaker**. You can switch during a call. See [Soft phone](soft-phone.md).
3. **Is the call muted or on hold?** Check the **Mute** and **Hold** buttons. See [Placing and handling calls](calls.md).
4. **Does the soft phone show *Poor connection*?** Your network is struggling. Move closer to your Wi-Fi, use a wired connection, or close large downloads. Try a different **Region** in the audio settings.
5. **Run the audio test.** If your administrator enabled the **Diagnostics** tab, click **Run audio test** and share the result with support.
6. **Still one-way or silent audio for everyone?** Ask your administrator. It is often a network or firewall setting that IT fixes (the STUN and TURN settings in the [Telnyx reference](../telephony/telnyx.md)).

Supervisors: listening is blocked while the agent has paused the recording to take card details. That is expected.

## Texts aren't being sent or received

1. **Is the number set up for texting?** It must be on **Omnichannel Addresses** with **Text messages (SMS)** ticked and the right SMS provider. See [Omnichannel addresses](channel-endpoints.md).
2. **Did the customer opt out?** A customer who texted **STOP**, or is marked **Do not SMS**, gets no messages until they text **START** or the mark is removed. See [Messaging workspace](messaging.md) and [Contacts](contacts.md).
3. **Incoming texts don't show in your inbox?** Check the **Unassigned** filter: a number with no enabled text entry point still receives texts, and they land there. Texts answered by the AI appear in the inbox only if the AI hands the conversation to a person. See [Inbound entry points and IVR menus](entry-points-and-ivr.md).
4. **Routed distribution?** If your queue hands conversations to one available agent, turn on the **Available** switch in the inbox.
5. **Starting a conversation from the wrong number?** Agents text from the number that lists them under **Agents who text from this number**, or else from the default SMS number. See [Omnichannel addresses](channel-endpoints.md).
6. **Nothing arrives at all?** Ask your administrator. IT checks the provider's webhook settings: see [Phone and SMS setup](telephony-settings.md), the [SMS reference](../omnichannel/sms.md) and the [Telnyx reference](../telephony/telnyx.md).

The **quiet hours** banner only warns you; it does not stop a text from being sent.

## The AI doesn't answer

**On a number (calls or texts)**

1. **Is the entry point open?** While it is closed, calls go to voicemail and texts go to people with the closed auto-reply.
2. **Is the entry point set to the AI?** **Route to** must be **AI voice agent** (calls) or **AI agent** (texts), with a profile picked. If the profile was deleted, texts go to people.
3. **Texts go to a person when** a person already has an open conversation with the customer on that number, or the customer's last AI conversation there ended less than an hour ago.
4. **Calls:** the **Telnyx AI Voice Agent** feature must be on. If it was turned off, calls to an AI entry point are refused.

See [Automated AI SMS and voice](automated-ai.md) and [Inbound entry points and IVR menus](entry-points-and-ivr.md).

**On an automatic load (the AI reaching out)**

1. **Did the load run?** Its status must reach *Loaded*, and its report says how many records it loaded and why others were skipped. See [Load activities](load-inventory.md).
2. **Wait a few minutes.** Due automated activities are picked up every five minutes, and replies wait for the reply delay.
3. **Did the contact opt out?** Each send checks the contact's opt-outs first. Calls also check do-not-call registries and the calling window.

**In a chat or on the website**

- If the AI replies with an error or not at all, the AI connection or deployment may have a problem. Ask the person who manages [AI connections](ai/connections.md).
- If it answers but not from your documents, see [Knowledge](ai/knowledge.md).

## The dialer isn't calling

1. **Are agents signed in to the campaign and Available?** The dialer only calls for available agents signed in to the campaign. With power and progressive, the first call comes within about a minute.
2. **Is the dialer profile enabled?** A disabled profile places no calls. See [Dialer profiles](dialer-profiles.md).
3. **Is the calling window open?** When the profile enforces a calling window, records are tried again in a later cycle while the calendar is closed, checked in each contact's time zone.
4. **Has the abandonment cap stopped dialing?** With the cap on, power and progressive dialing pause when too many answered calls found no agent.
5. **Are records left?** Records with no valid number or no attempts left become *Failed*; records on a do-not-call list or with a dead number become *Cancelled*. Check **Manage Activities**. See [Managing activities in bulk](bulk-activities.md).

## I'm stuck in wrap-up

Complete the activity: pick a disposition and click **Complete**. You become available again. If you leave it open, wrap-up ends on its own after 15 minutes unless your administrator changed that. See [Agent workspace](agent-workspace.md).

## A report is empty

1. **Check the date range.** It starts on **today**. Pick *Last 7 Days* or a custom range, then click **Show**.
2. **Clear the filters.** A queue, agent or campaign filter with no matching work gives an empty report.
3. **Check your permission.** Contact Center reports need **View Contact Center reports**; the CRM reports need **View Omnichannel reports**.
4. **Was there any work?** Reports show work that happened in the range; a new site or a quiet day has little to show.

See [Reports](reports.md).

## I can't find a contact

1. **Search by name or number.** The search box on **Interaction Center > Contacts** matches a name or a phone number.
2. **Try fewer digits.** `phone:` followed by part of the number finds numbers that contain those digits. Start with `+` only if you include the country code. See [Contacts](contacts.md).
3. **Is it a lead?** Leads are listed under **Interaction Center > Leads**, not with contacts. A converted lead becomes a contact. See [Leads, accounts and opportunities](leads-accounts-opportunities.md).
4. **Was it skipped on import?** An import that ends *Completed with errors* skipped some rows, for example numbers on a do-not-call list or duplicates. See [Contacts](contacts.md).
5. **Still missing?** You may not have permission to see that type of record. Ask your administrator.

## An activity load didn't create what I expected

- Read the load's report: it counts every matching record that was not loaded, with the reason. See [Load activities](load-inventory.md).
- A load cannot be edited once it has started. Create a new one.
- Numbers on the [Numbers not in service](numbers-not-in-service.md) list are never loaded. Use **Allow dialing** on a number that is back in service.

## Still stuck?

- Write down what you did, what you expected, what happened, and the time, and send it to your administrator.
- For IT and support teams, the Technical Manual has the [operations runbooks](../contact-center/runbooks.md), [production support](../contact-center/production-support.md) and the [telephony](../telephony/index.md) pages.
