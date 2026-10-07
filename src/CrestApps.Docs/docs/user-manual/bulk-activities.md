---
sidebar_label: Managing Activities in Bulk
sidebar_position: 19.5
title: Managing Activities in Bulk
description: Filter every open activity and reassign, reschedule, purge, re-prioritize or move many of them to another subject, source or dialer profile at once.
technical_manual:
  - omnichannel/management
  - contact-center/agents-queues-dialer
---

**Manage Activities** is the manager's view of all open work: not started, scheduled, pending, awaiting an agent, failed and cancelled activities, whoever owns them. Filter down to the activities you want, then apply one action to all of them.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Manage Activities |
| **Permission** | Manage activities (it includes Purge activity, which the **Purge** action needs) |
| **Feature** | Omnichannel Management |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of a manager filtering activities, reassigning two of them to another agent, and raising the urgency of every matching activity">
  <source src="/img/docs/um-manage-activities.mp4" type="video/mp4" />
</video>

Each row shows who will handle the activity: **Manual** or **Automated (AI)** work and its source, the **AI profile** of an automated conversation, the **dialer profile** and **campaign** of dialed work, and the user it is **assigned to**.

## Filter the activities

Click **Filters** to open or close the filter panel; the page remembers your choice in this browser. Combine any of these:

**Contact filters**

| Filter | What it finds |
| --- | --- |
| **Contact status** | Published or unpublished contacts. |
| **Phone number** and **Phone match type** | Contacts whose main cell or home number contains, matches, begins with or ends with the number. Start with `+` to include the country code. The contact's latest saved details are searched. |
| **Time zones** | Contacts in one or more time zones. |
| **Do not call** | Contacts marked Do not call within a date range. |

**Activity filters**

| Filter | What it finds |
| --- | --- |
| **Attempts** | Activities on a given attempt. *0* and *1* both mean the first try, *2* the second. |
| **Subject type** | Activities of one subject. |
| **Channel** / **Source** | See below. |
| **Interaction type** | Manual (agent) or automated (AI) activities. |
| **Status** | One of the open statuses listed above. |
| **Assignment status** | Unassigned, available, reserved, assigned, in-progress or released work. |
| **Campaign** | Activities of one campaign. |
| **Assigned to users** | Activities owned by one or more users. It searches all users, not only agents. |
| **Urgency level** | Activities of one urgency. A row's urgency also shows as an icon. |
| **Scheduled** / **Created** | Activities scheduled or created within a date range. |
| **Limit** | The most activities to return. |

**Channel** offers **Phone** and **SMS**, the channels subjects and loads can use.

**Source** is where the activity came from. It lists only the sources the enabled features create:

| Source | Activities it finds | Shown when |
| --- | --- | --- |
| **Manual** | Manual loads and activities created by hand. | Always. |
| **Automatic** | Automatic loads worked by an AI profile. | Always. |
| **Inbound** | Inbound calls and inbound activities logged by an agent. | Always. |
| **Dialer** | Dialer loads, whatever the profile's mode: preview, and power or progressive when **Contact Center Paced Dialing** is on. | **Contact Center Outbound Dialer** is on. |
| **Callback** | Callbacks scheduled for a contact. | **Contact Center Outbound Dialer** is on. |

A link or saved filter that names a source the list no longer offers still shows that value, so you can see what the page is filtering on.

Choose a **Page size** of 10, 25, 50 or 100 to see more at once.

## Apply a bulk action

1. Tick the activities on the page. Or, in the **Bulk actions** card, tick **Apply to all *N* matching activities** to act on every result of the filter, including those on other pages.
2. Pick an action in **Select an action** and fill in its fields.
3. Click **Execute Action**. **Purge** asks you to confirm first.

In the screencast, the manager filters by subject and assignment status, ticks two activities and uses **Assign** to give them to another agent. Then they tick **Apply to all matching activities** and raise the urgency of every result at once.

| Action | What it does |
| --- | --- |
| **Assign** | Shares the activities out in turn among the users you pick, and resets them to not started. |
| **Reschedule** | Moves them to a new date (midnight, site time). |
| **Purge** | Marks them purged, with your name and the time. This cannot be undone. |
| **Set Instructions** | Replaces the notes the agent reads first. |
| **Set Urgency Level** | Changes their urgency. |
| **Change Subject** | Moves them to another subject and applies that subject's campaign, channel and address. Subject field values are cleared. |
| **Clear Assignment** | Removes the owner and any reservation so the work can be routed or dialed again. |
| **Change Source** | Sets the source to **Manual** or **Automatic**, and by default clears assignment and reservation. The dialer sources are not offered here. |
| **Change Dialer Profile** | Hands them to another dialer profile's mode and makes them agent-handled calls. Each activity keeps its campaign. Shown when dialer profiles exist. |

To turn assigned manual work into dialer work, use **Change Dialer Profile** with **Clear assignment and reservation state** ticked. Use the same action to have outbound activities dialed in a different mode without creating them again.
