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
| **Permissions** | List activities, List contact activities and Complete own activity (the *Agent* role has them) |
| **Feature** | Omnichannel Management |

## Your activity list

<video controls preload="metadata" width="100%" aria-label="Screen cast of an agent completing activities with dispositions">
  <source src="/img/docs/omni-agent-activities.mp4" type="video/mp4" />
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

## Schedule an outbound activity for a contact

1. Open the contact and click **Add Activity > Outbound**.
2. Pick the **Subject content type**, the **Schedule at** time, the **User to assign activity to**, the **Urgency level** and any **Instructions**.
3. Save. The activity appears in that user's activity list.

## Log an inbound call

<video controls preload="metadata" width="100%" aria-label="Screen cast of creating an inbound subject and logging an inbound call for an existing contact">
  <source src="/img/docs/omni-inbound-existing.mp4" type="video/mp4" />
</video>

When a customer calls you directly:

1. Search **Interaction Center > Contacts** for their number, for example `phone:7025556666`.
2. Open the contact and click **Add Activity > Inbound**.
3. Pick the **Inbound subject**, fill in its fields, add **Notes**, pick the **Disposition** and click **Log Activity**.

The activity is saved as completed by you, and the subject flow runs at once.

If the caller is not in the system yet, create the contact first, then log the call:

<video controls preload="metadata" width="100%" aria-label="Screen cast of creating a new contact for an unknown inbound caller and logging the call">
  <source src="/img/docs/omni-inbound-new-contact.mp4" type="video/mp4" />
</video>

## Other actions on a contact's activities

On a contact's **Activities** page, scheduled activities have **Complete**, **Edit** and **Purge**; completed ones have **Edit** and, for AI conversations, **Review AI conversation**. Editing a completed activity changes only its disposition and notes; it does not re-run the flow. **Purge** (with the *Purge activity* permission) permanently marks a manual, not-started activity as purged.

:::caution Creating activities needs a super user today
Scheduling, logging and editing activities are checked against the *Edit activity* permission, which cannot currently be granted to a role. Until that is fixed, only users with full administrative rights can use **Add Activity** and **Edit**.
:::
