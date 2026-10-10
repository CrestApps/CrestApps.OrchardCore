---
sidebar_label: Build a Report
title: Build a Report
description: Create a report, add data sets, drag fields onto columns, sort, keep the top rows, preview and publish.
technical_manual:
  - modules/report-builder/index
---

A report starts with one or more **data sets**, such as your customers or the activities of your team. You drag their fields onto **Columns**, and the preview shows the result as you work.

Watch the short video, then follow the steps below.

<video controls preload="metadata" width="100%" poster="/img/docs/report-builder-build-a-report.jpg" aria-label="Video: building a report from a data set, with columns, sorting and a live preview">
  <source src="/img/docs/report-builder-build-a-report.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/report-builder-build-a-report.vtt" srcLang="en" label="English" default />
</video>

| | |
| --- | --- |
| **Menu** | Reports > Report Builder > New Report |
| **Permission** | Build reports and manage own custom reports and views |
| **Feature** | Report Builder |

<AskYourAdmin />

## Design a report

1. Open **Reports > Report Builder** and click **New Report**.
2. The builder opens on **Design**. Give the report a title on the **Settings** tab, the first tab, where you can also add a description and a category.
3. Click **Add data set**. Pick a data source on the left (or **All**), then click **Add** on the data set's card. Its fields appear in the **Data** pane on the left, grouped by part.
4. Drag fields onto **Columns**. You can also click the column icon next to a field. Numbers are added as a **Sum** by default; text, dates and identifiers become dimensions.
5. Click a column to change it in **Properties** on the right (see [Columns and formulas](columns-and-formulas.md)).
6. The **Preview** under the shelves refreshes as you work. Click **Refresh** to run it again.
7. Click **Publish** (or press Ctrl+S). Unfinished designs can be published: the builder lists their problems, and the report shows them when it runs until they are fixed.

Drag a column along the **Columns** shelf to move it. Click the cross on a column to remove it.

## Add data sets

**Add data set** opens a window that lists every data set you may use:

- The list on the left shows **All** data sets, then one entry per data source, with how many data sets each one has. Type in **Filter** to find a data set by its name or description.
- Once your report has a data set, the window opens on **Related**, which lists the data sets linked to the ones you already have. Their cards say **Related to** and the data set they link to; they are joined for you when you add them.
- A data set you already added says **Added**, and its button becomes **Add again**. Adding a data set twice is useful to report on the same data in two roles, such as the person who created an activity and the person it is assigned to.

In the **Data** pane, each data set is a card with its fields. The first data set is marked **Base**, and the others are numbered in the order they are joined. A small sign before each field tells its type: **#** for numbers, **Date** for dates and times, and **T/F** for yes or no values. Use **Search fields** at the top to find a field across all data sets. Use the buttons on a data set's card to edit its join or to remove it.

When a report reads more than one data set, the data sets must be joined. See [Join data sets](joins.md).

## Group and add up

A column is either a **dimension** or a **measure**:

- A **dimension** shows values as they are, such as *Region* or *Status*.
- A **measure** combines the values of many rows, such as the **Sum** of *Total* or the **Count** of orders.

A report without measures shows one row per record. As soon as a report has a measure, it shows one row for each group of dimensions: with *Region* and the **Sum** of *Total*, one row per region with its total.

To count rows, drag **Number of rows** from **Calculated fields** in the **Data** pane onto **Columns**. It counts the rows of each group.

## Sort and keep the top rows

Under the shelves:

- **Sort**: pick a column in **Add sort…** to sort by it. Click a sort to switch between ascending and descending, or click the cross next to it to remove it. Add more sorts to break ties.
- **Top rows**: type a number to keep only the first rows after sorting, for example the top 10 customers by revenue. Leave it empty to keep all rows.

## The date filter a new report starts with

A new report starts filtered on the last 30 days of its first data set's main date, such as when records were created, which keeps reports over a lot of data quick. People who run the report can pick another period; to change the default or remove the filter, click it under **Filters**. See [Filters](filters.md).

## Preview your report

The **Preview** runs the report as you build it, with your own access to the data. When something is not right, a message above the preview says what to fix, such as a join that still needs matching columns.

When you publish, the builder checks the report. If it still has problems, it is published anyway and lists them, so you can come back and fix them; until then, the report shows the problems instead of running. See [Publish and versions](publish-and-versions.md).

Next: [Columns and formulas](columns-and-formulas.md).
