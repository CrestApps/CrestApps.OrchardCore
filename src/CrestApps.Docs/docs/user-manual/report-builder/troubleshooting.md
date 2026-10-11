---
sidebar_label: Troubleshooting
title: Report Builder Troubleshooting
description: What to do when a menu, data set or button is missing, a report does not run, or the numbers look wrong.
technical_manual:
  - modules/report-builder/index
  - modules/report-builder/large-data
---

This page lists the problems people most often meet in the Report Builder, and what to do about them.

| | |
| --- | --- |
| **Menu** | Reports > Report Builder |
| **Permission** | Build reports and manage own custom reports and views |
| **Feature** | Report Builder |

<AskYourAdmin />

## Menus, data and buttons

| Problem | What to do |
| --- | --- |
| There is no **Report Builder** under **Reports** | The Report Builder feature may be off, or you may not have the *Build reports and manage own custom reports and views* permission. People who only run shared reports see **Shared Reports** instead, once a report is shared with them. Ask your administrator. |
| **Add data set** says *No data sources are enabled* or *No data sets are available to you* | Your site does not use a feature that offers data, or you do not have the permission a data source asks for. See [Data sources](data-sources.md). |
| A data set says it is not available to you | You may not view that content type or that data. Ask your administrator. |
| A query offers no fields | A query's fields are found in the rows it returns. Make sure the query returns rows without parameters. |
| The **Anonymous** role cannot be checked, or there are no share links | You need the *Share custom reports publicly and through share links* permission. Share links also need the report to be published. |
| **Edit** or **Delete** is missing on a report | You may change only your own reports, unless you have *Manage all custom reports and views*. Use **Actions > Clone** to make your own copy. |
| The export button is missing | The report's owner turned off **Let people the report is shared with export it**, or the share link does not allow export. |

## Reports that do not run

| Problem | What to do |
| --- | --- |
| The preview says *Add a data set, then drag fields to Columns to see a preview* | Add a data set and at least one column. |
| A join is red, or says **Not joined yet** | Click the join and add the columns that must match. See [Join data sets](joins.md). |
| The report says it cannot run because its owner has no active account | Ask someone who designs reports to clone it, and share the copy again. |
| The report shows a list of problems instead of running | It was published with problems. Open it in the builder, fix them and publish again. |
| A formula says it mixes aggregated values with row-level fields | Wrap every field in an aggregate function, or remove the aggregate function. See [Columns and formulas](columns-and-formulas.md#formulas-that-work-once-per-group). |
| A formula says a field does not exist | The field was renamed or its data set removed. Click it again in the field list of the formula window. |
| Your change is not saved and a yellow bar names someone else | They changed the report since you opened it. Pick **Reload their changes** or **Keep mine**. See [Publish and versions](publish-and-versions.md#work-on-a-report-with-others). |
| A view cannot be deleted | Reports or other views read it. The message names them; remove the view from them first. |

## Numbers that look wrong

| Problem | What to do |
| --- | --- |
| A warning says only the first rows were read | The data set is larger than the builder reads at once. Add filters that narrow the data, such as a shorter period, or ask your administrator to raise the limits. |
| The report shows fewer records than expected | Check the date filter a new report starts with: it shows the last 30 days. Pick a longer **Default period** or **All time**. See [Filters](filters.md#the-date-filter-a-new-report-starts-with). |
| Totals are too high after joining two data sets | A join repeats a row for every match, such as a customer once per order. Use **Count distinct** instead of **Count**, or sum values from the data set with one row per record. |
| A shared report shows data the viewer cannot open | That is expected: a shared report reads data with its owner's access. Share only what viewers should see. |
| A scheduled view shows old data | It shows the data as of its last refresh. Click **Refresh now** on the view's **Settings** tab. |
| Dates are a few hours off | Dates and times are shown in the site's time zone. Ask your administrator if the site's time zone is wrong. |
