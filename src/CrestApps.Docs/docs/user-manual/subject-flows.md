---
sidebar_label: Subject Flows
sidebar_position: 13
title: Subject Flows
description: Decide what happens after each disposition - finish, try again later, or create a new activity for another subject - and who gets the follow-up.
technical_manual:
  - omnichannel/management
---

A **subject flow** is the list of actions a subject takes for each disposition. When an agent (or the AI) completes an activity with *No answer*, the flow might schedule another try tomorrow; with *Lead won*, it might create a welcome call three days later; with *Do not call*, it finishes and flags the contact.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Subject Flows > Manage Flow |
| **Permission** | Manage subject flows |
| **Feature** | Omnichannel Management |

<AskYourAdmin />

The two screencasts below build the flows for a pair of subjects that work together. The outbound *Test Drive Follow-up* call finishes when a drive is booked or the customer is not interested, and tries again when nobody answers or the customer asks for a call back:

<video controls preload="metadata" width="100%" aria-label="Screencast of building an outbound subject flow with Finish and Try Again actions for four dispositions">
  <source src="/img/docs/um-flow-outbound.mp4" type="video/mp4" />
</video>

The inbound *Sales Inquiry* call finishes when the question is answered, and when the caller books a test drive it creates a **New Activity** for the *Test Drive Follow-up* subject, scheduled 48 hours later with a higher urgency:

<video controls preload="metadata" width="100%" aria-label="Screencast of building an inbound subject flow whose disposition creates an outbound follow-up activity for another subject">
  <source src="/img/docs/um-flow-inbound.mp4" type="video/mp4" />
</video>

## Add actions to a flow

1. Open **Interaction Center > Management > Subject Flows** and click **Manage Flow** next to the subject.
2. Click **Add Action** and pick the action type (below).
3. Pick the **Disposition** it responds to and fill in the fields.
4. Click **Create**. Repeat for every disposition the agent can choose.

A disposition can have more than one action. For example, *Lead won* can both **Finish** the current work and create a **New Activity** for the welcome call.

To change or remove an action, use **Edit** or **Delete** on its row. Subjects with no actions show a **Missing flow** badge on the Subject Flows list, so you can spot unfinished setups.

### Example: a lead-generation flow

| Disposition | Action | Settings |
| --- | --- | --- |
| *No answer* | **Try Again** | **Maximum attempts** 3. |
| *Follow up 30 days* | **New Activity** | **Target subject type** *Lead Generation - 30 day follow-up*, 720 hours later. |
| *Lead won* | **New Activity** | **Target subject type** *New Customer - Welcome*, 72 hours later. |
| *Do not call* | **Finish** | **Set do not call** ticked, so the contact is never called again. |

The screencast below builds this flow for the *Spring Lead Drive* campaign of a made-up *X Company*:

<video controls preload="metadata" width="100%" aria-label="Screencast of configuring a subject flow with disposition actions">
  <source src="/img/docs/omni-subject-flow.mp4" type="video/mp4" />
</video>

## Action types

| Type | What it does |
| --- | --- |
| **Finish** | Completes the task. Nothing else happens. |
| **Try Again** | Creates a new try of the same activity, with the same details and the attempt count raised by one. |
| **New Activity** | Creates a new activity, for the same subject or another one. The new activity takes its campaign, channel and interaction type from its subject. |

With the Omnichannel CRM feature on, a **Convert Lead** action is offered too. See [Convert from a disposition](leads-accounts-opportunities.md#convert-from-a-disposition).

Fields on every action:

| Field | What it does |
| --- | --- |
| **Disposition** | The outcome this action responds to. Required. |
| **When to choose this disposition** | Plain-language guidance the AI reads when it picks an outcome at the end of an automated conversation. It starts as the disposition's own description; edit it to say what the disposition means for this subject. |
| **Show communication preferences** | Shows **Set do not call**, **Set do not SMS** and **Set do not email**, so the action can flag the contact. |

Extra fields on **Try Again** and **New Activity**:

| Field | What it does |
| --- | --- |
| **Target subject type** | New Activity only: the subject of the new activity. Empty means the same subject. |
| **Maximum attempts** | Try Again only: how many tries in total. Empty means no limit. |
| **Urgency level** | The follow-up's urgency, from **Very low** to **Very high**. **Use original activity's urgency level** keeps the original. |
| **Assignment type** | **Same owner** gives the follow-up to the user who completes the current activity. **Specific owner** gives it to the user you pick. |
| **Assign to user** | The owner when **Specific owner** is chosen. Required then. |
| **Default schedule hours** | How many hours later the follow-up is scheduled (24 by default). The agent can change the date on the Complete page. |

## What the agent sees

When the agent picks a disposition on the Complete page, a preview lists the actions that will run, with a **Schedule at** date and **Preparation notes** for each follow-up. The notes become the instructions of the new activity. See [Activities](activities.md#complete-an-activity).
