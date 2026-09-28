---
sidebar_label: Campaigns
sidebar_position: 14
title: Campaigns and Campaign Groups
description: Create campaigns to group work for reporting and for the outbound dialer, and campaign groups to roll related campaigns up in reports.
---

A **campaign** is a named push of work, such as *Spring lead drive*. Every activity carries a campaign, and reports can be filtered by it. For the outbound dialer the campaign matters even more: **agents sign in to a campaign** to receive its dialer calls. A **campaign group** rolls related campaigns up in reports.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Campaigns, and Campaign Groups |
| **Permission** | Manage campaigns; Manage campaign groups |
| **Feature** | Omnichannel Management |

<video controls preload="metadata" width="100%" aria-label="Screencast of creating a campaign group and a campaign">
  <source src="/img/docs/um-campaigns.mp4" type="video/mp4" />
</video>

## Create a campaign group (optional)

1. Open **Interaction Center > Management > Campaign Groups** and click **Add campaign group**.
2. Enter a **Name** and optional **Description**, then **Save**.

## Create a campaign

1. Open **Interaction Center > Management > Campaigns** and click **Add Campaign**.
2. Enter a **Name**, pick a **Campaign group** (or *No campaign group*), and add an optional **Description**.
3. Click **Save**.

Campaigns and campaign groups cannot be deleted, because activities and reports keep pointing at them. Moving a campaign to another group changes how its past activities are grouped in reports too.

## Where the campaign is used

- On an [inventory load](load-inventory.md), to stamp the campaign on every activity it creates. Without one, the subject's **Default campaign** is used.
- On a **dialer** load, the campaign is required (unless the subject has a default campaign): it is the line of work agents sign in to. See [Dialer profiles](dialer-profiles.md).
- On an agent's [entitlements](skills-and-entitlements.md), under **Allowed campaigns**.
- In [reports](reports.md), as the **Campaign** and **Campaign group** filters.
