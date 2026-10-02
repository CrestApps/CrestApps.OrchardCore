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
3. In the list, open the load's **Actions** menu, choose **Load batch** and confirm with **Ok**. The load runs in the background: the status moves through *Started* and *Loading* to *Loaded*, and the row then shows how many activities it created, how many contacts matched the filters, and why any matching contact was skipped (see [What a load reports](#what-a-load-reports)).

Once a load has started it can no longer be edited or deleted.

## Dialer loads

<video controls preload="metadata" width="100%" aria-label="Screencast of creating a dialer inventory load with a dialer profile and a campaign">
  <source src="/img/docs/um-load-dialer.mp4" type="video/mp4" />
</video>

1. Click **Add Inventory Load** and choose **Dialer**.
2. Pick the **Dialer profile** (it decides preview, power or progressive) and the **Campaign**. Agents sign in to this campaign to get the calls, so a dialer load will not save without one unless the subject has a default campaign.
3. Fill in the record filters, save, and choose **Actions > Load batch**.

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
| **Prevent duplicate activity with the same subject** | Skips records that already have an open activity for this subject, on any campaign or channel. An open activity for a different subject does not stop a record from loading. |
| **Record type** | The contact or lead type to load. Required. A lead type adds a **Lead filters** panel right under it; see [Leads, Accounts and Opportunities](leads-accounts-opportunities.md#call-and-text-leads). |
| **Created from / to** | Only records created in this range. |
| **Only published records** | Skips drafts. |
| **Include records marked Do not call / Do not SMS / Do not email** | By default, records that opted out of the channel are skipped, including any record that shares their number. Tick to include them. |
| **Phone number** and match type | Contains, Exact, Begins with, or Ends with. **Exact** finds the number however it was stored: `5555550123`, `15555550123` and `+15555550123` all find the same contact. A ten-digit number is read as a North American number. |
| **Time zones** | Only records in these time zones. |
| **Limit** | The most activities to create. |
| **Last activity subject** / **Last activity disposition** | Only records whose last completed activity had this subject and outcome, for example everyone whose last *Lead generation* call was *No answer*. |

The automatic source adds AI fields; see [Automated AI SMS and voice](automated-ai.md).

## What a load reports

When a load finishes, its row in the list and the top of its page say how many of the matching records were loaded, for example *Loaded 0 of 2 matching records. 2 already have an open activity for this subject.* Every matching record that was not loaded is counted against one reason:

| Reason | Why |
| --- | --- |
| Already has an open activity for this subject | **Prevent duplicate activity with the same subject** is ticked. |
| Asked not to be reached on this channel | The record opted out, and the matching **Include records marked Do not ...** box is not ticked. |
| Shares a phone number with someone who asked not to be reached | Another record with the same number opted out. |
| Has no address on this channel | An automatic load needs a number or email to send to. |
| Has only numbers that are not in service | Every number the contact has on the channel is on the [Numbers Not In Service](numbers-not-in-service.md) list. A contact with at least one working number is loaded on that number. |
| Lead already converted into a contact | Lead loads only: converted leads are never loaded. |
| Lead shares a phone number with a contact | Lead loads only, when **Skip leads that are already contacts** is ticked. |
| Not loaded because the limit was reached | The **Limit** was reached before this record. |

If the filters match nobody, the load says so. Loading the same batch again replaces the counts rather than adding to them.
