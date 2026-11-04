---
sidebar_label: Campaigns
sidebar_position: 14
title: Campaigns and Campaign Groups
description: Create campaigns to group work for reporting and for the outbound dialer, and campaign groups to roll related campaigns up in reports.
technical_manual:
  - omnichannel/management
  - contact-center/agents-queues-dialer
---

A **campaign** is a named push of work, such as *Spring lead drive*. Every activity carries a campaign, and reports can be filtered by it. For the outbound dialer the campaign matters even more: **agents sign in to a campaign** to receive its dialer calls. A **campaign group** rolls related campaigns up in reports.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Campaigns, and Campaign Groups |
| **Permission** | Manage campaigns; Manage campaign groups |
| **Feature** | Omnichannel Management |

<AskYourAdmin />

<YouTube video="contact-center-campaigns" title="Video overview of campaigns and campaign groups: why they exist, creating them, giving agents the campaign, and where a campaign is used" />

<YouTube video="contact-center-setup-walkthroughs" title="Video of creating a campaign group and a campaign" start={960} end={998} />

## Create a campaign group (optional)

<YouTube video="contact-center-campaigns" title="Video of creating a campaign group" start={57} end={80} />

1. Open **Interaction Center > Management > Campaign Groups** and click **Add campaign group**.
2. Enter a **Name** and optional **Description**, then **Save**.

## Create a campaign

<YouTube video="contact-center-campaigns" title="Video of creating two campaigns" start={80} end={137} />

1. Open **Interaction Center > Management > Campaigns** and click **Add Campaign**.
2. Enter a **Name**, pick a **Campaign group** (or *No campaign group*), and add an optional **Description**.
3. Click **Save**.

Campaigns and campaign groups cannot be deleted, because activities and reports keep pointing at them: their list rows have an **Edit** button and no **Delete** button. Moving a campaign to another group changes how its past activities are grouped in reports too.

A campaign only groups and reports work. It does not decide the channel, the number used, or what happens after each disposition: those come from the [subject](subjects.md), its [subject flow](subject-flows.md) and the [activity load](load-inventory.md). So two subjects in the same campaign can behave differently.

## Let agents sign in to the campaign

<YouTube video="contact-center-campaigns" title="Video of adding a campaign to an agent's entitlements" start={137} end={180} />

Agents sign in only to the campaigns on their entitlements (when the **Contact Center Agent Entitlements** feature is on).

1. Open **Interaction Center > Management > Agent entitlements** and click **Add agent entitlements**.
2. Pick the **User**.
3. Under **Allowed campaigns**, pick the campaigns the agent may sign in to. **Allowed queues** is on the same page, for inbound work.
4. Click **Save**. The campaign now appears in the **Work** tab of the agent's soft phone. Removing a campaign here also signs the agent out of it.

See [Skills and agent entitlements](skills-and-entitlements.md) for the rest of the page.

## Where the campaign is used

<YouTube video="contact-center-campaigns" title="Video of where a campaign is used, and how teams use campaigns" start={212} end={279} />

- On an [activity load](load-inventory.md), to stamp the campaign on every activity it creates. Without one, the subject's **Default campaign** is used.
- On a **dialer** load, the campaign is required (unless the subject has a default campaign): it is the line of work agents sign in to. See [Dialer profiles](dialer-profiles.md).
- On an agent's [entitlements](skills-and-entitlements.md), under **Allowed campaigns**.
- In [reports](reports.md), as the **Campaign** and **Campaign group** filters.
