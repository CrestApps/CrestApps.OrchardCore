---
sidebar_label: Kinds of Reports
title: Kinds of Reports You Can Build
description: Step-by-step examples of common reports - a plain list, totals with a chart, a trend over time, subtotals, a pivot table and a joined top-10 report.
technical_manual:
  - modules/report-builder/index
---

The same few steps build very different reports. This page walks through common kinds of reports, from a plain list to a dashboard with charts. Use them as recipes and change the data sets and fields to yours.

Watch the short video, then follow the examples below.

<video controls preload="metadata" width="100%" poster="/img/docs/report-builder-kinds-of-reports.jpg" aria-label="Video: building a list, a chart report, a report with subtotals and a pivot table">
  <source src="/img/docs/report-builder-kinds-of-reports.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/report-builder-kinds-of-reports.vtt" srcLang="en" label="English" default />
</video>

| | |
| --- | --- |
| **Menu** | Reports > Report Builder > New Report |
| **Permission** | Build reports and manage own custom reports and views |
| **Feature** | Report Builder |

<AskYourAdmin />

The examples use the **Users** data set, the **Activities** data set of the **Omnichannel** data source, and two content types, *Customer* and *Order*. You see only the data sets your permissions and your site's features allow; see [Data sources](data-sources.md).

## A plain list

A list shows one row per record, with no totals. Use it for a contact list, or to check records.

*Example: the user accounts that are turned on.*

1. Add the **Users** data set.
2. Drag **User name**, **Email**, **Roles** and **Enabled** onto **Columns**.
3. Drag **Enabled** onto **Filters** and set it to **is** **Yes**.
4. Under **Sort**, add **User name**.

There is no measure, so the report shows one row per user.

## Totals per group, with a chart

Add a measure, and the report shows one row per group.

*Example: how many activities ended with each disposition.*

1. Add the **Activities** data set. The report starts on the last 30 days of when activities were created.
2. Drag **Disposition** onto **Columns**, then **Number of rows** from **Calculated fields**.
3. Sort by the number of rows, descending.
4. In **Visuals**, click **Chart**, set **Chart type** to **Pie** or **Bar**. Then click **Table** and check **Show totals**.

## A trend over time

Group a date by a period to see how something changes.

*Example: activities created per month over the last year.*

1. Add the **Activities** data set.
2. Drag **Created** onto **Columns**, click it, and set **Transform** to **Month**.
3. Drag **Number of rows** onto **Columns**.
4. Click the date filter under **Filters** and set its **Default period** to **Last 12 months**.
5. Sort by **Created**, then add a **Chart** with **Chart type** **Line**.

To compare channels, add **Channel** to **Columns** and pick it in **Split into series by**: the chart draws one line per channel.

## Subtotals

A table can add a subtotal after each group.

*Example: activities per person and status, with a subtotal per person.*

1. Add the **Activities** data set.
2. Drag **Assigned to**, **Status** and **Number of rows** onto **Columns**, in that order.
3. Add a **Table** visual and check **Show subtotals** and **Show totals**.

The table lists each person's statuses, then a subtotal for that person, and a grand total at the end. The first column is the outer group, so put the columns in the order you want them grouped.

## A pivot table

A pivot table turns the values of one column into columns across the top.

*Example: the same counts, with one column per status.*

1. Build the subtotals report above.
2. Add a **Pivot table** visual. Set **Rows** to **Assigned to**, **Columns across** to **Status**, and **Value** to the number of rows. Check **Show totals**.

## Averages and calculated values

*Example: the average time each person takes to complete an activity.*

1. Add the **Activities** data set.
2. Drag **Assigned to** and **Minutes to complete** onto **Columns**.
3. Click **Minutes to complete**, set **Aggregate** to **Average** and **Format** to **N1**.
4. Add **Number of rows** so you can see how many activities each average is based on.

For a value the data does not hold, add a [calculated field](columns-and-formulas.md#calculated-fields), such as the share of activities that were escalated.

## A joined top-10 report

*Example: the ten customers with the most revenue.*

1. Add the *Customer* data set, then the *Order* data set. If the order has a content picker for its customer, the two are joined for you; otherwise join the order's *Customer* to the customer's *Content item ID* (see [Join data sets](joins.md)).
2. Drag the customer's **Display text** onto **Columns**, then the order's *Total* (summed), and the order's **Content item ID** with **Aggregate** set to **Count distinct**, which counts the orders.
3. Sort by the total, descending, and set **Top rows** to 10.
4. Add a **Chart** with **Chart type** **Horizontal bar**, and **Metrics** for the total revenue.

## A dashboard

Put several visuals side by side with their **Width**: **Metrics** at **Full** across the top, then two charts at **Half**, then a **Table** at **Full**. Check **Show in the admin menu** on the **Settings** tab, and the dashboard is one click away under **Reports**. See [Share and run reports](share-and-run.md).

## Prepare data once for many reports

When several reports need the same joined and filtered data, save it as a [reusable view](views.md) and build the reports on the view.

Next: [Data sources](data-sources.md).
