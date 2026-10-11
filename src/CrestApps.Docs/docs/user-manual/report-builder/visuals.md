---
sidebar_label: Visuals
title: Tables, Charts, Metrics and Pivot Tables
description: Show a report's result as tables with totals and subtotals, charts, headline numbers and pivot tables, side by side.
technical_manual:
  - modules/report-builder/index
---

Visuals decide how the people who run a report see its result. Without visuals, a report shows one table. Add a chart, a few headline numbers, or a pivot table, and place them side by side.

Watch the short video, then follow the steps below.

<video controls preload="metadata" width="100%" poster="/img/docs/report-builder-visuals.jpg" aria-label="Video: adding a table with subtotals, a chart, metrics and a pivot table to a report">
  <source src="/img/docs/report-builder-visuals.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/report-builder-visuals.vtt" srcLang="en" label="English" default />
</video>

| | |
| --- | --- |
| **Menu** | Reports > Report Builder > *a report* > Design |
| **Permission** | Build reports and manage own custom reports and views |
| **Feature** | Report Builder |

<AskYourAdmin />

## Add a visual

The **Visuals** card on the right, under **Properties**, lists what the report shows, in order.

1. Click **Table**, **Chart**, **Metrics** or **Pivot table** at the bottom of the card. The builder fills the new visual with your columns: the first dimension as categories or rows, and up to three measures as values.
2. The visual opens in the card. Change its settings.
3. Use the arrow to move a visual up, and the cross to remove it. Click a visual in the list to change it again.

Every visual has a **Title**, shown above it, and a **Width**: **Quarter**, **Third**, **Half**, **Two thirds** or **Full**. Visuals that fit on one line are placed side by side, for example two charts at **Half**. Tables and metrics start at **Full**, charts and pivot tables at **Half**.

You can also drag a field from the **Data** pane straight onto a visual's **Categories**, **Values**, **Split into series by**, **Rows**, **Columns across** or **Value** box. The builder adds the column for you: a number dropped on values is summed, and other fields dropped on values are counted.

## Table

A table shows the result rows.

| Setting | What it does |
| --- | --- |
| **Columns shown** | The columns the table shows. Leave all unchecked to show every column that is not hidden. |
| **Show totals** | Adds a grand total row at the bottom. |
| **Show subtotals** | Keeps rows together by their first dimensions and adds a subtotal after each group. |

With subtotals on and columns *Role*, *User* and *Status* followed by a measure, the table shows a subtotal for each user within a role, then for each role, then the grand total. Put the dimensions in the order you want them grouped: the first column is the outermost group.

## Chart

| Setting | What it does |
| --- | --- |
| **Chart type** | **Bar**, **Horizontal bar**, **Line**, **Area**, **Pie** or **Doughnut**. |
| **Categories** | The dimension along the axis, or the slices of a pie. |
| **Values** | The measures to draw. |
| **Split into series by** | Draws one series per value of another dimension, using the first value column. For example, one line per region. |
| **Stack series** | Stacks the series on top of each other. |
| **Show legend** | Shows which color is which. |

Pick a chart that suits the question:

| Question | Chart |
| --- | --- |
| Which is biggest? | **Bar** or **Horizontal bar** (long names read better horizontally). |
| How does it change over time? | **Line** or **Area**, with a date grouped by **Month** or **Week** as categories. |
| What share does each part have? | **Pie** or **Doughnut**, with a few categories. |
| How is each total made up? | **Bar** with **Split into series by** and **Stack series**. |

## Metrics

Metrics show headline numbers: the total of each value column over the whole report, such as the number of orders and the revenue. Pick the **Values** to show.

## Pivot table

A pivot table is a cross-tab:

| Setting | What it does |
| --- | --- |
| **Rows** | The dimensions down the side. |
| **Columns across** | The dimension whose values become the columns along the top. |
| **Value** | The measure shown in each cell. |
| **Show totals** | Adds totals for each row and column. |

For example, *Assigned to* as rows, *Status* across, and the number of rows as the value shows how many activities each person has in each status.

## Totals stay correct

Charts, metrics, pivot tables, totals and subtotals add up the underlying rows again rather than adding the numbers shown. An average stays a true average and a distinct count stays distinct, even in a total.

Next: [Join data sets](joins.md).
