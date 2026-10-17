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

<YouTube video="report-builder" title="Narrated video of turning on the Report Builder and finding your way around the list of reports" start={110} end={144} />

| | |
| --- | --- |
| **Menu** | Reports > Report Builder (or Reports > Shared Reports) |
| **Permission** | Build reports and manage own custom reports and views, to design reports. None to run a report shared with you. |
| **Feature** | Report Builder |

<AskYourAdmin />

## Turn on the Report Builder

Turning features on is work for an administrator.

1. Open **Tools > Features**.
2. Find **Report Builder** in the **Reporting** group and click **Enable**. It turns on **Reports** too, and Orchard Core's **Data Pipelines Core**, which provides the data sources, the formulas and the share links the builder uses. You never turn Data Pipelines Core on yourself: the features that need it turn it on.
3. To report on your content types (such as customers or orders), make sure **Contents** is on. To report on your saved queries, turn on **Queries**. To email share links to people, turn on **Email** and set up how the site sends email.
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

- Type in **Search reports** to find a report by its title or category, and press Enter or click the search button.
- **New Report** opens the builder on a new report. It is shown to people who build reports.
- The header of the list says how many reports it shows. If you build reports, the **Status** menu on the right of it shows **All** reports, only the **Published** ones, or only the ones **Not published**.

Reports you started but never published are listed first, with a **Not published** badge. Only you, and the people who manage every report, can see them. Click **Edit** to keep working on one, or choose **Delete** from its actions to throw it away.

Each published report shows its category, a **Menu** badge when it is in the admin menu, a **Shared** badge when it is shared with other people, and **Yours** or the name of its owner. The buttons depend on what you may do:

| Button | What it does |
| --- | --- |
| **Run** | Opens the report. Set the filters and click **Show**. See [Share and run reports](share-and-run.md#run-and-export-a-report). |
| **Edit** | Opens the report in the builder. Shown when you may change the report. |
| **Clone** (in the actions) | Makes your own copy of the report. The copy is not shared with anybody. Shown when you may build reports. |
| **Delete** (in the actions) | Removes the report, its versions and all its share links. Shown when you may change the report. |

## Find your way around the builder

The builder fills the window. At the top are the report's title, a few words that say whether your changes are saved, and the buttons **Versions**, **Run report** and **Publish**. Below them are four tabs:

| Tab | What you do there |
| --- | --- |
| **Settings** | Give the report a title, description and category, put it in the admin menu, and choose whether people may export it. |
| **Design** | Build the report. The builder opens on this tab. |
| **Data model** | See your data sets as cards and join them by dragging one column onto another. See [Join data sets](joins.md). |
| **Sharing** | Choose the people and roles who may run the report, and send people links to it. See [Share and run reports](share-and-run.md). |

The **Design** tab has three areas:

- the **Data** pane on the left, with the fields of your data sets and your calculated fields;
- the middle, with the **Joins**, **Columns** and **Filters** rows, **Sort** and **Top rows**, and the live **Preview** under them;
- the **Properties and visuals** pane on the right, where you change the column, filter or join you clicked, and add charts and other visuals.

Each pane scrolls on its own. Collapse the **Data** pane, the **Properties and visuals** pane, or the **Columns and filters** section to give the preview more room; the builder remembers your choice.

Next: [Build a report](build-a-report.md).
