---
sidebar_label: Load Activities
sidebar_position: 17
title: Load Activities
description: Turn a filtered list of contacts into activities - assigned to agents, handled by the AI, or queued for the outbound dialer.
technical_manual:
  - omnichannel/management
  - contact-center/agents-queues-dialer
---

**Load activities** creates activities in bulk. You describe which contacts to pick (their type, when they were created, phone number, time zone, last outcome...) and what kind of work to create, and the load runs in the background, so even a large list does not slow the site down.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Load Activities |
| **Permission** | Manage activity batches. Deleting a finished load also needs **Delete loaded activity batches**. |
| **Feature** | Omnichannel Management. The **Dialer** source needs Contact Center Outbound Dialer. |

<AskYourAdmin />

There are three sources:

| Source | Creates | Used for |
| --- | --- | --- |
| **Manual** | Activities assigned to the agents you pick, shared out in turn. | Agent call or text lists worked from **Activities**. |
| **Automatic** | Unassigned **automated** activities an AI profile works on its own. | AI SMS outreach and AI voice calls. See [Automated AI SMS and voice](automated-ai.md). |
| **Dialer** | Unassigned phone activities queued for the outbound dialer. | Preview, power and progressive dialing. Shown when the Contact Center Outbound Dialer feature is on. |

The **Load Activities** list shows the newest loads first. A load does nothing when you save it: it starts only when you choose **Load activities** from its **Actions** menu, or click **Save & Load activities** on the load form.

## Manual loads

<video controls preload="metadata" width="100%" aria-label="Screencast of creating a manual activity load for two agents, loading it, and finding the new activities on Manage Activities">
  <source src="/img/docs/um-load-manual.mp4" type="video/mp4" />
</video>

1. Open **Interaction Center > Management > Load Activities**, click **Add Activity Load** and choose **Manual**.
2. Fill in the load (fields below). A manual load needs a **Channel** and at least one user in **User(s) to assign activities to**; the activities are shared between the users you pick. Click **Save**.
3. In the list, open the load's **Actions** menu, choose **Load activities** and confirm with **Ok**. To save and start the load in one step, click **Save & Load activities** on the form instead of **Save**, and confirm. The load runs in the background: the status moves through *Started* and *Loading* to *Loaded*, and the row then shows how many activities it created, how many contacts matched the filters, and why any matching contact was skipped (see [What a load reports](#what-a-load-reports)).

Once a load has started it can no longer be edited. While it is *Started* or *Loading* it cannot be deleted either.

### Clone a load

To run a load again, or a load much like it, open its **Actions** menu and choose **Clone**. This works whatever the load's status. The copy has every setting of the original -- subject, campaign, channel, AI options, filters, limit and schedule -- and is named after it with *(copy)* added. It starts as *New* with nothing loaded, and opens for editing so you can adjust it before you load it.

### Delete a finished load

A load whose status is *Loaded* can be deleted by users with the **Delete loaded activity batches** permission. Click **Delete** on its row and confirm. Only the load and its report are removed. The activities it created are kept, with their assignments and schedules. To withdraw those activities, cancel or purge them under **Manage Activities**.

## Dialer loads

<video controls preload="metadata" width="100%" aria-label="Screencast of creating a dialer activity load with a dialer profile, a campaign and the number its calls are dialed from">
  <source src="/img/docs/um-load-dialer.mp4" type="video/mp4" />
</video>

1. Click **Add Activity Load** and choose **Dialer**.
2. Pick the **Dialer profile** (it decides preview, power or progressive) and the **Campaign**. Agents sign in to this campaign to get the calls, so a dialer load will not save without one unless the subject has a default campaign.
3. Fill in the record filters, save, and choose **Actions > Load activities**.

Dialer loads always use the phone channel and create manual (agent-handled) activities. The activities are left unassigned, take the dialing mode of the dialer profile, and are queued for the campaign as they are created; the dialer then offers them to the agents signed in to that campaign. The campaign always comes from the load (or the subject's default campaign), never from the dialer profile.

## Fields

The form has three cards: **Activity load settings** (what to create), **Record filters** (which contacts or leads to pick) and **Last activity filters**.

### Activity load settings

| Field | What it does |
| --- | --- |
| **Source** | The source you picked. It cannot be changed. |
| **Title** | A name for the load. Required. |
| **Subject content type** | The subject of every activity. Required. |
| **Campaign** | Stamped on every activity. Leave it empty to use the subject's default campaign. |
| **Channel** | **Phone** or **SMS**. Required, and hidden for dialer loads. |
| **Address** | Automatic loads: the number to send from or call from. Only the [Omnichannel Addresses](channel-endpoints.md) used for the **Channel** picked are listed: numbers used for text messages for SMS, numbers used for voice calls for Phone. |
| **Dialer profile** | Dialer loads: required. It decides the dialing mode (preview, power or progressive) and its pacing. |
| **Dial from** | Dialer loads: the number the customers called from this load see, picked from the addresses used for voice calls. It is shown instead of the agent's own number and the dialer profile's **Caller ID**, unless the profile is set to **Always show this caller ID**. Leave it on **Default caller ID** to keep those. |
| **Schedule at** | When the activities become due. Required. |
| **User(s) to assign activities to** | Manual loads: who gets the work. The activities are shared out in turn between them. |
| **Urgency level** / **Instructions** | Copied onto every activity. Instructions are notes the agent reads first. |
| **Prevent duplicate activity with the same subject** | Skips records that already have an open activity for this subject, on any campaign or channel. An open activity for a different subject does not stop a record from loading. |

The automatic source adds AI fields; see [Automated AI SMS and voice](automated-ai.md#ai-options-on-an-automatic-load).

### Record filters

| Field | What it does |
| --- | --- |
| **Record type** | The contact or lead type to load. Required. A lead type adds a **Lead filters** panel right under it; see [Leads, Accounts and Opportunities](leads-accounts-opportunities.md#call-and-text-leads). |
| **Created from** / **Created to** | Only records created in this range. |
| **Only published records** | Skips drafts. When it is off, the latest version of each record is used, published or not. |
| **Include records marked Do not call / Do not SMS / Do not email** | By default, records that opted out of the channel are skipped, including any record that shares their number. Tick to include them. |
| **Phone number filter** | A match type (**Contains**, **Exact match**, **Begins with** or **Ends with**) and a number. See the tips below. |
| **Time zones** | Only records in these time zones, for example to call only where it is a good time of day. |
| **Limit** | The most activities to create. |

### Last activity filters

| Field | What it does |
| --- | --- |
| **Last activity subject** / **Last activity disposition** | Only records whose last completed activity had this subject and outcome, for example everyone whose last *Lead generation* call was *No answer*. |

### Phone number tips

- The filter looks at each record's main cell and home numbers.
- Type a national number or part of one, such as `702499`. Spaces, brackets and dashes are ignored.
- Start with `+` to include the country code, such as `+1702499`. Without it, only the national number is compared, so records from more than one country can match.
- **Exact match** finds the number however it was stored: `5555550123`, `15555550123` and `+15555550123` all find the same contact. A ten-digit number is read as a North American number.

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
