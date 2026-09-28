---
sidebar_label: Load Inventory
sidebar_position: 17
title: Load Inventory
description: Turn a filtered list of contacts into activities - assigned to agents, handled by the AI, or queued for the outbound dialer.
---

**Load inventory** creates activities in bulk. You describe which contacts to pick (their type, when they were created, phone number, time zone, last outcome...) and what kind of work to create, and the load runs in the background.

There are three sources:

| Source | Creates | Used for |
| --- | --- | --- |
| **Manual** | Activities assigned to the agents you pick, shared out in turn. | Agent call or text lists worked from **Activities**. |
| **Automatic** | Unassigned **automated** activities an AI profile works on its own. | AI SMS outreach and AI voice calls. See [Automated AI SMS and voice](automated-ai.md). |
| **Dialer** | Unassigned phone activities queued for the outbound dialer. | Preview, power and progressive dialing. Shown when the Contact Center Outbound Dialer feature is on. |

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Load Inventory |
| **Permission** | Manage activity batches |
| **Feature** | Omnichannel Management |

## Manual loads

<video controls preload="metadata" width="100%" aria-label="Screencast of creating a manual inventory load for two agents, loading it, and finding the new activities on Manage Activities">
  <source src="/img/docs/um-load-manual.mp4" type="video/mp4" />
</video>

1. Open **Interaction Center > Management > Load Inventory**, click **Add Inventory Load** and choose **Manual**.
2. Fill in the load (fields below). A manual load needs a **Channel** and at least one user in **User(s) to assign activities to**; the activities are shared between the users you pick. Click **Save**.
3. In the list, open the load's **Actions** menu, choose **Load batch** and confirm with **Ok**. The load runs in the background: the status moves through *Started* and *Loading* to *Loaded*, and the row then shows how many activities it created.

Once a load has started it can no longer be edited or deleted.

## Dialer loads

<video controls preload="metadata" width="100%" aria-label="Screencast of creating a dialer inventory load with a dialer profile and a campaign">
  <source src="/img/docs/um-load-dialer.mp4" type="video/mp4" />
</video>

1. Click **Add Inventory Load** and choose **Dialer**.
2. Pick the **Dialer profile** (it decides preview, power or progressive) and the **Campaign**. Agents sign in to this campaign to get the calls, so a dialer load will not save without one unless the subject has a default campaign.
3. Fill in the contact filters, save, and choose **Actions > Load batch**.

Dialer loads always use the phone channel and create manual (agent-handled) activities. Each activity is queued for the campaign as it is created.

## Fields

| Field | What it does |
| --- | --- |
| **Title** | A name for the load. Required. |
| **Subject content type** | The subject of every activity. Required. |
| **Campaign** | Stamped on every activity; falls back to the subject's default campaign. |
| **Channel** | **Phone** or **SMS**. Hidden for dialer loads. |
| **Channel endpoint** | Automatic loads: the number to send from or call from. |
| **Dialer profile** | Dialer loads: required. |
| **Schedule at** | When the activities become due. |
| **Users** | Manual loads: who gets the work. |
| **Urgency** / **Instructions** | Copied onto every activity. Instructions are notes the agent reads first. |
| **Prevent duplicate activity with the same subject** | Skips contacts that already have an open activity for this subject. |
| **Contact content type** | Which contacts to pick. Required. |
| **Contact created from / to** | Only contacts created in this range. |
| **Only published contacts** | Skips drafts. |
| **Include do not call / SMS / email contacts** | By default, contacts who opted out of the channel are skipped, including any contact that shares their number. Tick to include them. |
| **Phone number** and match type | Contains, Exact, Begins with, or Ends with. |
| **Time zones** | Only contacts in these time zones. |
| **Limit** | The most activities to create. |
| **Last activity subject** / **Last activity disposition** | Only contacts whose last completed activity had this subject and outcome, for example everyone whose last *Lead generation* call was *No answer*. |

The automatic source adds AI fields; see [Automated AI SMS and voice](automated-ai.md).
