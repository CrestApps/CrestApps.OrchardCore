---
sidebar_label: Teams
title: Teams
description: "Build the team tree of your organization, from regions and sites down to teams and crews, once for the whole site, so Workforce and the other features that organize people use the same teams."
technical_manual:
  - modules/teams
---

Teams describe how your organization is built: a region, a site, a department, a team and a crew. You build the tree once, and every feature that organizes people uses the same teams. Workforce, for example, schedules the workers of each team and gives each team a calendar.

| | |
| --- | --- |
| **Menu** | Teams |
| **Permission** | Manage teams (Manage workforce teams gives it too when Workforce is on) |
| **Feature** | Teams (Workforce turns it on) |

<AskYourAdmin />

## Build the team tree

1. Open **Teams** and choose **Add team**.
2. Start at the top, such as your organization or a region, and choose **Save**.
3. Add the teams under it. From a team's **Actions** menu, **Add a team under it** starts a new team with that team in **Part of**.

**Team**

| Field | What it does |
| --- | --- |
| **Name** | The team's name. |
| **Kind** | **Organization**, **Region**, **Site**, **Department**, **Team** or **Crew**. A crew is a team sent out as one unit for field work. |
| **Part of** | The team it belongs to, or **Nothing (top level)**. A team can't be placed under itself or under one of its own teams. The managers of a team also manage the teams under it. |
| **Description** | What the team does, shown under its name in the list. |
| **Time zone** | Where the team works. Its days start and end at midnight in this time zone. Leave it on **The site's time zone** to use the site's. |
| **Linked role** | Optional. Users in this role are treated as part of the team, for example to read its Workforce calendar. |
| **In use** | Untick to stop using a team without deleting it. |

Other features add their own cards to the team editor. With Workforce on, the **Workforce** card holds the team's address and its scheduling and time clock settings; see [Workers and Teams](./workers-and-teams.md#the-workforce-card).

## Find a team

The list shows each team followed by the teams under it, with badges for its kind, the team it is part of, its time zone, and **Not in use** when it is turned off. Type part of a name in the search box and choose the search button to find a team.

## Delete a team

Open the team's **Actions** menu and choose **Delete**. A team that still has teams under it can't be deleted; move or delete them first. A feature can keep a team it still uses: Workforce keeps a team that still has workers. The message says why. Deleting a team keeps its Workforce calendar.
