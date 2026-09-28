---
sidebar_label: Subject Flows
sidebar_position: 13
title: Subject Flows
description: Decide what happens after each disposition - finish, try again later, or create a new activity for another subject - and who gets the follow-up.
---

A **subject flow** is the list of actions a subject takes for each disposition. When an agent (or the AI) completes an activity with *No answer*, the flow might schedule another try tomorrow; with *Lead won*, it might create a welcome call three days later; with *Do not call*, it finishes and flags the contact.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Subject Flows > Manage Flow |
| **Permission** | Manage subject flows |
| **Feature** | Omnichannel Management |

<video controls preload="metadata" width="100%" aria-label="Screencast of configuring a subject flow with Finish, Try Again and New Activity actions">
  <source src="/img/docs/um-subject-flow.mp4" type="video/mp4" />
</video>

## Add actions to a flow

1. Open **Interaction Center > Management > Subject Flows** and click **Manage Flow** next to the subject.
2. Click **Add Action** and pick the action type (below).
3. Pick the **Disposition** it responds to and fill in the fields.
4. Click **Create**. Repeat for every disposition the agent can choose.

A disposition can have more than one action. For example, *Lead won* can both **Finish** the current work and create a **New Activity** for the welcome call.

## Action types

| Type | What it does |
| --- | --- |
| **Finish** | Ends the work for this contact on this subject. |
| **Try Again** | Schedules the same activity again. |
| **New Activity** | Creates a new activity, for the same subject or another one. |

Fields on every action:

| Field | What it does |
| --- | --- |
| **Disposition** | The outcome this action responds to. Required. |
| **When to choose this disposition** | Plain-language guidance the AI reads when it picks an outcome at the end of an automated conversation. |
| **Show communication preferences** | Shows **Set do not call**, **Set do not SMS** and **Set do not email**, so the action can flag the contact. |

Extra fields on **Try Again** and **New Activity**:

| Field | What it does |
| --- | --- |
| **Target subject type** | New Activity only: the subject of the new activity. Empty means the same subject. |
| **Maximum attempts** | Try Again only: how many tries in total. Empty means no limit. |
| **Urgency level** | The follow-up's urgency. The default keeps the original. |
| **Assignment type** | **Same owner** or **Specific owner**. |
| **Assign to user** | The owner when Specific owner is chosen. |
| **Default schedule hours** | How many hours later the follow-up is scheduled (24 by default). The agent can change the date on the Complete page. |

## What the agent sees

When the agent picks a disposition on the Complete page, a preview lists the actions that will run, with a **Schedule at** date and **Preparation notes** for each follow-up. The notes become the instructions of the new activity. See [Activities](activities.md#complete-an-activity).
