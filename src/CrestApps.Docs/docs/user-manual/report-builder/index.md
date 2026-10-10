---
sidebar_label: Report Builder
title: Report Builder
description: Build your own reports with drag and drop, join data sets, add formulas, filters, charts and pivot tables, and share them.
technical_manual:
  - modules/report-builder/index
  - modules/report-builder/permissions
---

The **Report Builder** lets you build your own reports without writing code. You pick the data you need (for example your customers and their orders), drag fields onto the report, choose how numbers are added up, and add charts, headline numbers, and pivot tables. You can save a report, put it in the admin menu so you can run it again with one click, and share it with people, roles, or a link.

This video shows how to build a report, add totals, formulas, filters and charts, join data sets, save reusable views, and share your reports.

<video controls preload="metadata" width="100%" poster="/img/docs/report-builder.jpg" aria-label="Video overview of the Report Builder">
  <source src="/img/docs/report-builder.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/report-builder.vtt" srcLang="en" label="English" default />
</video>

| | |
| --- | --- |
| **Menu** | Reports > Report Builder, and Reports > Report Views |
| **Permissions** | Build reports and manage own custom reports and views (to design); Share custom reports publicly and through share links (to share with everyone or create links); Manage all custom reports and views (to change other people's reports) |
| **Features** | Report Builder. Your content types (such as customers or orders) are offered as data when Contents is on, and your saved queries when Queries is on. |

<AskYourAdmin />

People who only run reports that were shared with them need no permission: they see **Reports > Shared Reports**, and any report pinned to the menu that they may open.

## What you can do

| You want to... | Read |
| --- | --- |
| Turn the Report Builder on, find it, and work with the list of reports | [Get started](getting-started.md) |
| Design your first report: pick data, add columns, sort and preview | [Build a report](build-a-report.md) |
| Add up numbers, group by month, format values, and work out new values with formulas | [Columns and formulas](columns-and-formulas.md) |
| Narrow the data, and let the people who run the report pick their own period or values | [Filters](filters.md) |
| Show tables with totals and subtotals, charts, headline numbers and pivot tables | [Visuals](visuals.md) |
| Report on two kinds of data together, such as customers and their orders | [Join data sets](joins.md) |
| See examples of common reports and how they are built | [Kinds of reports](kinds-of-reports.md) |
| Know which data you can report on | [Data sources](data-sources.md) |
| Save drafts, publish, and go back to an earlier version | [Publish and versions](publish-and-versions.md) |
| Prepare data once and reuse it in many reports | [Reusable views](views.md) |
| Share a report, put it in the menu, run it and export it | [Share and run reports](share-and-run.md) |
| Fix a report that does not work as expected | [Troubleshooting](troubleshooting.md) |

## Who can do what

Your administrator gives these permissions to roles. Administrators have all of them.

| Permission | What it lets you do |
| --- | --- |
| *(none)* | Run the reports that are shared with you, and the reports you own. |
| **Build reports and manage own custom reports and views** | Design reports and views, change and delete your own, use any view in your reports, and clone a report you can run. |
| **View all custom reports** | Run every report, whoever owns it and whoever it is shared with. |
| **Manage all custom reports and views** | Change and delete every report and view, whoever owns it. It includes the two permissions above. |
| **Share custom reports publicly and through share links** | Share a report with the **Anonymous** role (everyone, even visitors who are not signed in), and create share links. |

## Words you will see

| Word | What it means |
| --- | --- |
| **Data source** | Where data comes from, such as **Content items** (the content types of the site), **Queries** (saved queries), **Report views** or **Users**. Other features add their own. See [Data sources](data-sources.md). |
| **Data set** | One table of data from a source, such as the *Customer* content type. |
| **Field** | One piece of information in a data set, such as *Email* or *Total*. |
| **Column** | A field you placed on the report, with its settings. |
| **Dimension** | A column the report groups by, such as *Region*. |
| **Measure** | A column that adds up values, such as the *Sum* of *Total*. When a report has a measure, it shows one row per group of dimensions. |
| **Calculated field** | A field you work out with a formula, like a spreadsheet formula. |
| **Join** | How two data sets are matched, such as an order to its customer. |
| **Visual** | A table, chart, set of headline numbers or pivot table that shows the result. |
| **View** | A saved, reusable data set: it joins, filters, and calculates once, and other reports can use its columns as fields. |
| **Draft** | Your saved changes that nobody else sees until you publish them. |
| **Version** | A copy of the report kept each time you publish a change. |
| **Share link** | A secret address that opens one report for anyone who has it. |
