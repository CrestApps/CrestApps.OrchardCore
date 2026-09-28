---
sidebar_label: Managing Activities in Bulk
sidebar_position: 19.5
title: Managing Activities in Bulk
description: Filter every open activity and reassign, reschedule, purge, re-prioritize or move many of them to another subject, source or dialer profile at once.
---

**Manage Activities** is the manager's view of all open work: not started, scheduled, pending, awaiting an agent, failed and cancelled activities, whoever owns them. Filter down to the activities you want, then apply one action to all of them.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Manage Activities |
| **Permission** | Manage activities (the Purge action also needs Purge activity) |
| **Feature** | Omnichannel Management |

<video controls preload="metadata" width="100%" aria-label="Screencast of a manager filtering activities, reassigning two of them to another agent, and raising the urgency of every matching activity">
  <source src="/img/docs/um-manage-activities.mp4" type="video/mp4" />
</video>

## Filter the activities

Expand **Filters** and combine any of these:

- **Contact filters**: contact status, **Phone number** with a match type, **Time zones**, and a **Do not call** date range.
- **Activity filters**: **Attempts**, **Subject**, **Channel**, **Source** (Manual, Automatic, Dialer, Preview dial, Power dial, Progressive dial, Predictive dial, Callback, Inbound, Workflow, API), **Interaction type**, **Status**, **Assignment status**, **Campaign**, **Urgency**, **Scheduled** and **Created** date ranges, **Limit**, and **Assigned to users**.

Choose a **Page size** of 10, 25, 50 or 100 to see more at once.

## Apply a bulk action

1. Tick the activities, or choose **Apply to all *N* matching activities** to act on every result of the filter.
2. Pick an action in **Select an action** and fill in its fields.
3. Click **Execute Action**.

| Action | What it does |
| --- | --- |
| **Assign** | Shares the activities out in turn among the users you pick, and resets them to not started. |
| **Reschedule** | Moves them to a new date (midnight, site time). |
| **Purge** | Marks them purged. This cannot be undone. |
| **Set Instructions** | Replaces the notes the agent reads first. |
| **Set Urgency Level** | Changes their urgency. |
| **Change Subject** | Moves them to another subject and applies that subject's campaign, channel and endpoint. Subject field values are cleared. |
| **Clear Assignment** | Removes the owner and any reservation so the work can be routed or dialed again. |
| **Change Source** | Reclassifies them as manual, automatic, dialer and so on, and by default clears assignment and reservation. |
| **Change Dialer Profile** | Hands them to another dialer profile's mode. The activity keeps its campaign. Shown when dialer profiles exist. |

To turn assigned manual work into dialer work, use **Change Dialer Profile** (or **Change Source** plus **Clear Assignment**).
