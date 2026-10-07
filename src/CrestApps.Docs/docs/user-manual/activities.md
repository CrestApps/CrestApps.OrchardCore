---
sidebar_label: Activities
sidebar_position: 19
title: Working Activities
description: How an agent finds their work, calls or texts the contact, logs inbound calls for new and existing contacts, and completes activities with a disposition.
technical_manual:
  - omnichannel/management
---

An **activity** is one piece of work for one contact: a call to make, a text to send, or an inbound call to log. Agents work their activities from **Interaction Center > Activities** and from each contact's page.

| | |
| --- | --- |
| **Menu** | Interaction Center > Activities |
| **Permissions** | List activities, List Contact activities, Complete own activity, and Create and edit activities for **Add Activity** and **Edit**. The *Agent* role has them by default. Complete activity lets you complete other people's activities; Purge activity adds **Purge**. Manage activities includes Create and edit activities and Purge activity. |
| **Feature** | Omnichannel Management |

<AskYourAdmin />

## Your activity list

<video controls preload="metadata" width="100%" aria-label="Screencast of an agent opening their activity list, completing an activity and picking a disposition">
  <source src="/img/docs/um-complete-activity.mp4" type="video/mp4" />
</video>

**Interaction Center > Activities** lists the manual activities assigned to you that are not started yet, newest first. Each row shows the contact, the number or address, the channel, the attempt number, when it is scheduled, and the contact's **current local time**, so you know whether it is a good time to call. Hover over the local time to see the contact's full local date and time zone.

Each row also says who will handle the activity: whether it is **Manual** or **Automated (AI)** work and where it came from (for example *Automatic*, *Preview dialer*, *Callback* or *Inbound*), the **AI profile** that will hold an automated conversation, the **dialer profile** that will dial it, its **campaign**, and the user it is **assigned to**. Badges only appear when the activity has that information. Hover over a badge to see what it stands for.

Narrow the list with the filters: **Urgency**, **Subject**, **Channel**, **Time zone**, **Attempts** and **Scheduled**. For example, filter by time zone to work only the contacts where it is a good time of day.

## Complete an activity

1. Click **Complete** on the activity.
2. Read the details and instructions, call or text the contact, and fill in the subject's fields.
3. If several contacts share the number, pick the right **Contact**.
4. Add **Notes** and pick the **Disposition**.
5. Check the preview of what happens next, for example **Try Again** with its date, or a **New Activity** for another subject. Change a follow-up's **Schedule at** or add **Preparation notes** for whoever works it. The notes become the instructions of the follow-up activity.
6. Click **Complete**. The subject flow runs straight away.

In the screencast, the agent completes an activity with *No Answer*, and the subject flow's **Try Again** action schedules the next try.

If the activity is already finished, you are sent back to where you came from with the warning *This activity was already completed, so your disposition was not recorded.* The outcome already on record is kept. This is normal, not an error: someone else may have completed it, or an automated call that handed the customer to you, or a caller who reached voicemail, may have closed it while you were still wrapping up.

## Create activities by hand from a contact

Open the contact (**Interaction Center > Contacts**, then **List Activities**) and use **Add Activity**. This screencast schedules an outbound call and then logs an inbound call for the same contact:

<video controls preload="metadata" width="100%" aria-label="Screencast of opening a contact, scheduling an outbound activity and logging an inbound call whose disposition schedules a follow-up">
  <source src="/img/docs/um-contact-activities.mp4" type="video/mp4" />
</video>

### Schedule an outbound activity

1. Click **Add Activity > Outbound**.
2. Pick the **Subject content type**, the **Schedule at** time, the **User to assign activity to**, the **Urgency level** and any **Instructions**, and fill in the subject's fields. Only outbound subjects are listed; when there is only one, it is picked for you.
3. Click **Save**. The activity appears under **Scheduled Activities** on the contact, and in that user's activity list.

If no outbound subject exists yet, the page says so and you cannot schedule an activity. Ask your administrator to [create one](subjects.md).

### Log an inbound call

When a customer calls you directly:

1. Search **Interaction Center > Contacts** for their number, for example `phone:7025550108`, and open the contact.
2. Click **Add Activity > Inbound** and pick the **Inbound subject**. When there is only one, it is picked for you. The page reloads with that subject's fields, right under the subject, and its dispositions.
3. Check the contact details, fill in the subject's fields, add **Notes** and pick the **Disposition**. Only the dispositions the subject flow has an action for are listed.
4. Check **Workflow Results**: it previews what the disposition does next, for example a follow-up activity with its **Schedule at** time. Change the time or add **Preparation notes** if you need to.
5. Click **Log Activity**.

The activity is saved as completed by you, and the subject flow runs at once. It then shows under the contact's **Completed Activities**. In the screencast, **Test Drive Booked** schedules a *Test Drive Follow-up* call two days later.

If no inbound subject exists yet, the page says so and you cannot log the call. Ask your administrator to [create one](subjects.md).

If the caller is not in the system yet, create the contact first, then log the call. In this screencast the search finds nobody, so the agent creates a *Customer* contact with the caller's name and cell number, then logs the call on it:

<video controls preload="metadata" width="100%" aria-label="Screencast of searching for an unknown caller, creating the contact and logging the inbound call">
  <source src="/img/docs/um-inbound-new-contact.mp4" type="video/mp4" />
</video>

## Other actions on a contact's activities

On a contact's **Activities** page, scheduled activities have **Complete**, **Edit** and **Purge**; completed ones have **Edit** and, for AI conversations, **Review AI conversation**.

A completed activity shows who dispositioned it, and how, in its **Dispositioned by** badge and on its **Edit** page: the user's name when a person completed it, **AI voice agent (profile: …)** or **AI agent (profile: …)** when the AI conversation concluded it, **Dialer (automatic)** when the dialer completed the attempt on its own (for example no answer, a busy line, an answering machine, an abandoned call or a number not in service), or **Automatically (system)** for anything else the platform closed by itself, such as a call transferred out by the IVR. Activities completed before this was recorded show the best match from what they do record. Editing a completed activity lets you correct its disposition and notes; it does not re-run the flow, so no new tries or follow-ups are created.

**Purge** (with the *Purge activity* permission) takes a scheduled activity out of the work for good. It cannot be undone. The activity is kept with the status *Purged*, together with who purged it and when, and it keeps its owner. To purge many activities at once, use [Manage Activities](bulk-activities.md).
