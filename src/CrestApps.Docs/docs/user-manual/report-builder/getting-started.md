---
sidebar_label: Get Started
title: Get Started with the Report Builder
description: Turn on the Report Builder, find it in the Reports menu, and work with the list of reports.
technical_manual:
  - modules/report-builder/index
  - modules/report-builder/permissions
---

This page shows how the Report Builder is turned on, where to find it, and how to work with the list of reports: run, edit, clone and delete them.

Watch the short video, then follow the steps below.

<video controls preload="metadata" width="100%" poster="/img/docs/report-builder-getting-started.jpg" aria-label="Video: turning on the Report Builder and finding your way around the list of reports">
  <source src="/img/docs/report-builder-getting-started.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/report-builder-getting-started.vtt" srcLang="en" label="English" default />
</video>

| | |
| --- | --- |
| **Menu** | Reports > Report Builder (or Reports > Shared Reports) |
| **Permission** | Build reports and manage own custom reports and views, to design reports. None to run a report shared with you. |
| **Feature** | Report Builder |

<AskYourAdmin />

## Turn on the Report Builder

Turning features on is work for an administrator.

1. Open **Tools > Features**.
2. Find **Report Builder** in the **Reporting** group and click **Enable**. It turns on **Reports** too.
3. To report on your content types (such as customers or orders), make sure **Contents** is on. To report on your saved queries, turn on **Queries**.
4. Give the people who build reports the **Build reports and manage own custom reports and views** permission in their role. See [Roles and permissions](../administration/roles.md) and the permission list on the [Report Builder overview](index.md#who-can-do-what).

Features you already use, such as the Contact Center, Omnichannel, AI chat or the messaging workspace, add their own data to the Report Builder as soon as it is on. See [Data sources](data-sources.md).

## Find the Report Builder

Everything is under **Reports** in the admin menu:

| Menu | Who sees it | What it opens |
| --- | --- | --- |
| **Report Builder** | People who may build reports. | The list of every report you can open, and **New Report**. |
| **Shared Reports** | People who may not build reports but can run at least one report. | The list of the reports shared with you. |
| **Report Views** | People who may build reports. | The list of [reusable views](views.md). |
| *A category*, such as **Custom Reports** | Everyone who can run a report pinned to the menu. | The reports put in the admin menu, grouped by their category. |

## Work with the list of reports

The **Report Builder** page lists the reports you can open.

- Type in **Search reports** to find a report by its title or category, and press Enter.
- If you build reports, use the status list next to it to show **All reports**, only the **Published** ones, or only the ones **Not published yet**, then click **Filter**.
- **Views** opens the list of reusable views, and **New Report** opens the builder on a new report. Both are shown to people who build reports.

Reports you started but never published are listed first, under **Not published yet**, with a **Draft** badge. Only you, and the people who manage every report, can see them. Click **Edit** to keep working on one, or **Delete** to throw it away.

Published reports are listed under **Published**. Each one shows its category, a **Menu** badge when it is in the admin menu, a **Shared** badge when it is shared with other people, and **Yours** or the name of its owner. The buttons depend on what you may do:

| Button | What it does |
| --- | --- |
| **Run** | Opens the report. Set the filters and click **Show**. See [Share and run reports](share-and-run.md#run-and-export-a-report). |
| **Edit** | Opens the report in the builder. Shown when you may change the report. |
| **Delete** | Removes the report, its versions and all its share links. Shown when you may change the report. |
| **Actions > Clone** | Makes your own copy of the report. The copy is not shared with anybody. Shown when you may build reports. |

## Find your way around the builder

The builder fills the window. At the top are the report's title, a few words that say whether your changes are saved, and the buttons **Versions**, **Run report** and **Publish**. Below them are four tabs:

| Tab | What you do there |
| --- | --- |
| **Settings** | Give the report a title, description and category, put it in the admin menu, and choose whether people may export it. |
| **Design** | Build the report. The builder opens on this tab. |
| **Data model** | See your data sets as cards and join them by dragging one column onto another. See [Join data sets](joins.md). |
| **Sharing** | Choose the people and roles who may run the report, and create share links. See [Share and run reports](share-and-run.md). |

The **Design** tab has three areas:

- the **Data** pane on the left, with the fields of your data sets and your calculated fields;
- the middle, with the **Joins**, **Columns** and **Filters** rows, **Sort** and **Top rows**, and the live **Preview** under them;
- the **Properties and visuals** pane on the right, where you change the column, filter or join you clicked, and add charts and other visuals.

Each pane scrolls on its own. Collapse the **Data** pane, the **Properties and visuals** pane, or the **Columns and filters** section to give the preview more room; the builder remembers your choice.

Next: [Build a report](build-a-report.md).
