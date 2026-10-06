---
sidebar_label: Outbound Calling Campaign
title: Run an Outbound Calling Campaign
description: Import a contact list, define what the call is about and its outcomes, then let the dialer call the list for your agents, with follow-ups and reports.
technical_manual:
  - contact-center/agents-queues-dialer
  - omnichannel/management
---

## Who it's for and what you get

For **managers** who run calling campaigns, such as a lead drive, renewals or appointment reminders, and the **agents** and **supervisors** who work them.

When it is done:

- your contacts are loaded as call activities for one campaign;
- agents sign in to the campaign, and the dialer offers each record (**preview**) or places the call for them (**power** or **progressive**);
- every call passes the compliance checks first: opt-outs, do-not-call registries, your calling window and dead numbers;
- agents record an outcome, and the subject flow schedules the retry or the follow-up on its own;
- reports show how far the campaign got and what the outcomes were.

<video controls preload="metadata" width="100%" aria-label="Screencast of a live power-dial call: the agent signs in to the campaign, the dialer places the call, the record opens and the activity is completed">
  <source src="/img/docs/um-power-dial.mp4" type="video/mp4" />
</video>

## Before you start

| You need | Who sets it up |
| --- | --- |
| A phone provider and at least one number to call from | Your administrator; see [Phone and SMS setup](../telephony-settings.md) |
| The features **Omnichannel Management**, **Contact Center Outbound Dialer** (preview dialing), **Contact Center Paced Dialing** (power and progressive), **Contact Center Agents**, **Contact Center Agent Entitlements**, **Telephony Soft Phone Extension**, **Content Transfer** (to import lists) and **Reports** | Your administrator, in **Tools > Features** |
| A manager role with Manage subject flows, Manage dispositions, Manage campaigns, Manage the Contact Center dialer, Manage Contact Center agents and Manage activity batches | Your administrator; see [Roles and permissions](../getting-started/roles-and-permissions.md) |
| The **Agent** role, plus **Use the telephony soft phone**, for the callers | Your administrator |

<AskYourAdmin />

## Steps

1. **Get your contacts in.** Set up the contact type once, then import your list under **Content > Import**. Pick the lead country, skip duplicates, and scrub the do-not-call registries. Check the import's result for skipped rows. See [Contacts](../contacts.md). Working with prospects rather than customers? See [Turn leads into deals](leads-to-deals.md).
2. **Create the subject.** Add an **Outbound** subject, such as *Spring lead drive*, with the fields agents fill in during the call. See [Subjects](../subjects.md).
3. **Create the outcomes.** Add the dispositions agents can pick, such as *No answer*, *Call back*, *Interested* and *Not interested*. Add one with the **Number not in service** outcome so dead numbers are handled for you. See [Dispositions](../dispositions.md).
4. **Decide what happens next.** On the subject's flow, add an action for every disposition: **Finish**, **Try Again** later, or a **New Activity** such as a follow-up call. See [Subject flows](../subject-flows.md).
5. **Create the campaign.** Add the campaign, and a campaign group if you want to roll it up in reports. Agents sign in to this campaign to get its calls. See [Campaigns](../campaigns.md).
6. **Set the calling window.** Create a business hours calendar for the hours you may call. The dialer checks it in each contact's own time zone. See [Business hours](../business-hours.md).
7. **Choose how to dial.** Create a dialer profile: the mode (**Preview**, **Power** or **Progressive**), the caller ID customers see, the compliance rules with your calling calendar, and, for power and progressive, attempts, retry delay and the abandonment cap. See [Dialer profiles](../dialer-profiles.md).
8. **Give agents the campaign.** On each agent's entitlement record, add the campaign under **Allowed campaigns**. See [Skills and agent entitlements](../skills-and-entitlements.md).
9. **Load the work.** Create a **Dialer** activity load with the subject, the dialer profile and the campaign, filter the contacts, and pick a **Dial from** number if this campaign should show its own number. Save, then choose **Actions > Load batch**, and read the load's report of what was loaded and skipped. See [Load activities](../load-inventory.md).
10. **Agents dial.** Agents sign in to the campaign from the soft phone's **Work** tab and set themselves **Available**. With preview, each record is offered with **Dial** and **Skip**; with power or progressive, the call is placed for them and the record opens as it rings. See [Agent workspace](../agent-workspace.md) and [Placing and handling calls](../calls.md).
11. **Agents record the outcome.** After the call, the agent completes the activity with a disposition and notes, which ends wrap-up and runs the subject flow. See [Activities](../activities.md).
12. **Watch and adjust.** Supervisors filter the live dashboard by the campaign. Managers reassign, reschedule or move work to another dialer profile in bulk, and review dead numbers. See [Live dashboard](../live-dashboard.md), [Managing activities in bulk](../bulk-activities.md) and [Numbers not in service](../numbers-not-in-service.md).
13. **Measure.** Use **Campaign summary**, **Subject inventory**, **Disposition breakdown** and **Agent productivity**. See [Reports](../reports.md).

## Check that it works

1. After **Load batch**, the load's status reaches *Loaded* and says how many records it loaded. If it loaded none, its report says why.
2. Sign in to the campaign as a test agent and set yourself **Available**.
3. With a preview profile, an offer appears in the docked agent bar and **My workspace**: click **Dial**. With a power profile, a call is placed within about a minute.
4. Complete the activity with *Call back*, and check on the contact's **List Activities** page that the flow scheduled the next attempt.
5. Run the **Campaign summary** report for today.

## Tips

- **Start with Preview.** It is the easiest to train, and agents see the record before every call. Move to Power or Progressive once agents are comfortable.
- The dialer does not disposition a call that rang out, was busy or reached a machine: it dials the record again after the retry delay, up to **Max attempts**.
- Tick **Prevent duplicate activity with the same subject** on the load so contacts who already have an open activity are not loaded twice.
- A call outside the calling window is not dropped; it is tried again in a later cycle.
- To text the customers you could not reach, use a workflow on the **Dialer attempt completed** event. See [Contact Center workflows](../workflows.md).
- Without the **Contact Center Agent Entitlements** feature, any agent may sign in to any campaign.
