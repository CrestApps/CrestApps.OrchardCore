---
sidebar_label: Large Data
title: Report Builder and Large Data
description: How the Report Builder stays fast and complete over large data sets - row limits, date push-down, grouping in the source, join key narrowing, and scheduled views.
user_manual:
  - user-manual/report-builder/views
  - user-manual/report-builder/filters
---

The engine processes rows in memory, so every data source gets joins, formulas and aggregation for free. To keep that fast and complete over large data sets, it bounds what it reads and lets sources take on part of the work.

## Limits

The engine reads each data set up to `MaxRowsPerDataSet` rows (newest first) and warns when a data set has more. Joins may produce at most `MaxJoinedRows` rows, and a result keeps at most `MaxResultRows` rows after sorting. The defaults and how to change them are in [Configuration](index.md#configuration).

Four mechanisms keep large data sets fast and complete.

## A date filter from the start

Data sets name their main date (`ReportDataSetDescriptor.DefaultDateField`, such as when a record was created). When the first data set of a new report has one, the builder adds a filter on it that viewers see as a **recent period** (today, the last 7, 30 or 90 days, the last 12 months, or all time), starting at the last 30 days.

Relative date filters (in the last or next N days) are passed to the sources as a date range, so they read only that period. Record-backed sources read the range with `ReportDateRange.For` and apply it to an indexed date column; see [Add your own data source](custom-data-sources.md#conditions-you-may-push-down).

## Grouping in the data source

A report over one data set that groups by fields or date periods and counts, sums, averages, or takes the smallest or largest values can be answered by its source (`IReportAggregateDataSource`). The source returns per group the row count and the count, sum, smallest and largest value of each measured field; the engine merges these partial aggregates wherever it would aggregate rows, so sorting, result filters, totals, charts and pivots give the same result as reading every row.

The engine asks the source only when the report:

- reads one data set, with no joins and no row-level calculated fields;
- groups by data set fields, as they are or with the **Hour of day** (date-times), **Day**, **Week**, **Day of week**, **Month**, **Month of year**, **Quarter** or **Year** transform;
- measures data set fields without a transform with **Count**, **Sum**, **Average**, **Minimum** or **Maximum**, or counts rows;
- filters rows on data set fields with conditions the source can apply exactly, and has no viewer filter that lists options.

Row filters are sent as exact conditions and date periods as UTC boundaries of local hours, days or months (at most 1,000 buckets per date). Whatever a source cannot answer exactly (a computed field, a text "contains" filter, a median or distinct count, a calculated field, an exposed filter that lists options) is read row by row as before, and so is anything the source declines by returning `null`. A source may return at most `MaxRowsPerDataSet` groups.

`ReportIndexAggregator` answers such a query with one `GROUP BY` statement over a YesSql index table; Omnichannel activities and the Contact Center data sets use it for the fields their index holds. See [Grouping in the data source](custom-data-sources.md#grouping-in-the-data-source) to add it to your own data sets.

## Joins read only what can match

For an inner or left join on a field the source can filter exactly (`ReportFieldDescriptor.IsKeyFilterable`), the engine sends the distinct keys of the rows before it as an `IN` condition marked `IsJoinKey`, in batches of `JoinKeyBatchSize` up to `MaxJoinKeys` keys. The joined data set then reads the records that can match instead of its newest records. Only text and whole-number keys joined to a field of the same type are narrowed this way.

Users, content items, Omnichannel activities, Contact Center records, AI chat sessions and messaging conversations and messages filter their key columns this way; a join with more keys, or on another field, reads the joined data set in full, up to `MaxRowsPerDataSet`. When the rows before a join hold no key at all, the joined data set is not read. See [Join keys](custom-data-sources.md#join-keys).

## Scheduled views

A view's **Refresh** setting (`ReportView.RefreshIntervalMinutes`: `0` for live; the builder offers 15, 60, 360 and 1440 minutes, and any other value, such as one from a recipe, is raised to at least 15) stores its result instead of running the view every time a report reads it:

- The **Report view refresh** background task runs every 5 minutes and refreshes each due view in its own scope, with a distributed lock so two nodes never refresh the same view. A failed refresh keeps the previous rows and records the error. Tenants can turn the task off under **Background Tasks**.
- A scheduled view runs with its **owner's** current access, since there is no reader at refresh time; it does not refresh when its owner is deleted or disabled or can no longer read it. Reports that read the view still need to be allowed to read it and to plan its query, but they get the rows the owner's access produced. Schedule only views whose data everyone who can read the view may see.
- Reports read the stored rows (at most `MaxRowsPerDataSet`) while the stored fields match the view. Editing the view's query, switching it to live, or importing a changed view drops the stored rows, and reports run the view live until the next refresh.
- **Refresh now** (`POST /Admin/reports/views/{id}/refresh`, which needs `ManageAllReportDesigns` on the view) refreshes a view at once and returns its status.
- Deleting a view deletes its stored rows.

How people schedule a view is described in the User Manual: [Reusable views](../../user-manual/report-builder/views.md#refresh-a-view-on-a-schedule).

## Checklist for a large data set

- Give the data set a main date, and keep a date filter on reports over it.
- Index the main date and every key column a join filters, and mark those keys `IsKeyFilterable`.
- Implement grouping in the source for the fields its index holds.
- For a heavy view that many reports read, schedule it.
- Raise the limits in [Configuration](index.md#configuration) only after the above, and watch memory: every row read is held in memory during the run.
