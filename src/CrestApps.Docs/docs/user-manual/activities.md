---
sidebar_label: Activities
sidebar_position: 19
title: Working Activities
description: How an agent finds their work, calls or texts the contact, logs inbound calls for new and existing contacts, and completes activities with a disposition.
---

An **activity** is one piece of work for one contact: a call to make, a text to send, or an inbound call to log. Agents work their activities from **Interaction Center > Activities** and from each contact's page.

| | |
| --- | --- |
| **Menu** | Interaction Center > Activities |
| **Permissions** | List activities, List contact activities, Complete own activity, and Create and edit activities for **Add Activity** and **Edit** (the *Agent* role has them; *Manage activities* also allows creating and editing) |
| **Feature** | Omnichannel Management |

## Your activity list

<video controls preload="metadata" width="100%" aria-label="Screencast of an agent opening their activity list, completing an activity and picking a disposition">
  <source src="/img/docs/um-complete-activity.mp4" type="video/mp4" />
</video>

**Interaction Center > Activities** lists the manual activities assigned to you that are not started yet, newest first. Each row shows the contact, the number or address, the channel, the attempt number, when it is scheduled, and the contact's **current local time**, so you know whether it is a good time to call.

Narrow the list with the filters: **Urgency**, **Subject**, **Channel**, **Time zone**, **Attempts** and **Scheduled**.

## Complete an activity

1. Click **Complete** on the activity.
2. Read the details and instructions, call or text the contact, and fill in the subject's fields.
3. If several contacts share the number, pick the right **Contact**.
4. Add **Notes** and pick the **Disposition**.
5. Check the preview of what happens next. Change a follow-up's **Schedule at** or add **Preparation notes** for whoever works it.
6. Click **Complete**. The subject flow runs straight away.

If someone else already completed the activity, you get a warning and your disposition is not recorded.

## Create activities by hand from a contact

Open the contact (**Interaction Center > Contacts**, then **List Activities**) and use **Add Activity**. This screencast schedules an outbound call and then logs an inbound call for the same contact:

<video controls preload="metadata" width="100%" aria-label="Screencast of opening a contact, scheduling an outbound activity and logging an inbound call whose disposition schedules a follow-up">
  <source src="/img/docs/um-contact-activities.mp4" type="video/mp4" />
</video>

### Schedule an outbound activity

1. Click **Add Activity > Outbound**.
2. Pick the **Subject content type**, the **Schedule at** time, the **User to assign activity to**, the **Urgency level** and any **Instructions**. Only outbound subjects are listed.
3. Click **Save**. The activity appears under **Scheduled Activities** on the contact, and in that user's activity list.

### Log an inbound call

When a customer calls you directly:

1. Search **Interaction Center > Contacts** for their number, for example `phone:7025550108`, and open the contact.
2. Click **Add Activity > Inbound** and pick the **Inbound subject**. The page reloads with that subject's fields and dispositions.
3. Fill in the subject's fields, add **Notes** and pick the **Disposition**. Only the dispositions the subject flow has an action for are listed.
4. Check **Workflow results**: it previews what the disposition does next, for example a follow-up activity with its **Schedule at** time. Change the time or add **Preparation notes** if you need to.
5. Click **Log Activity**.

The activity is saved as completed by you, and the subject flow runs at once. In the screencast, **Test Drive Booked** schedules a *Test Drive Follow-up* call two days later.

If the caller is not in the system yet, create the contact first, then log the call:

<video controls preload="metadata" width="100%" aria-label="Screencast of searching for an unknown caller, creating the contact and logging the inbound call">
  <source src="/img/docs/um-inbound-new-contact.mp4" type="video/mp4" />
</video>

## Other actions on a contact's activities

On a contact's **Activities** page, scheduled activities have **Complete**, **Edit** and **Purge**; completed ones have **Edit** and, for AI conversations, **Review AI conversation**. Editing a completed activity changes only its disposition and notes; it does not re-run the flow. **Purge** (with the *Purge activity* permission) permanently marks a manual, not-started activity as purged.
