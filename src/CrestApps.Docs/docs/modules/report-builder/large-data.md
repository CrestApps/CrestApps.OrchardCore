---
sidebar_label: Large Data
title: Report Builder and Large Data
description: How the Report Builder stays fast and complete over large data sets - bounded memory, row limits, date push-down, grouping in the source, join key narrowing, and scheduled views.
user_manual:
  - user-manual/report-builder/views
  - user-manual/report-builder/filters
---

Every data source gets joins, formulas and aggregation for free. To keep that fast, complete, and safe for the server over large data sets, the engine streams its rows with a bounded amount of memory, bounds what it reads, and lets sources take on part of the work.

## Bounded memory

A run never holds a whole data set in memory. Its rows stream from the data sources through the joins, calculations, filters and groupings a batch at a time, and every step that must see many rows at once is one of Orchard Core's data operations, which keep up to `MemoryLimitMegabytes` of rows in memory and write the others to temporary files:

- **Joins.** The rows before a join are held in a buffer that writes the rows that don't fit to a temporary file, while the engine collects the keys they hold to read the joined data set for them. Orchard Core's `DataJoin` then matches them: in memory when the joined rows fit, otherwise by sorting both sides in temporary files and merging them.
- **Grouping.** Orchard Core's `DataAggregator` computes the measures of each group as the rows arrive, without keeping them, including calculated fields that aggregate, such as `SUM([o.Total]) / COUNT()`. Only the values of a median and of a distinct count stay with their group.
- **Totals, subtotals, charts and pivots.** A grouped result keeps a copy of the rows it grouped, reduced to the values its groups need, in a buffer that writes them to a temporary file, and regroups them while the query runs.
- **Sorting.** Orchard Core's `DataSorter` sorts the result in sorted runs written to temporary files when it doesn't fit.
- **Reading less.** A result that is neither grouped nor sorted stops reading once it has its rows, unless a viewer filter lists the values found in the data. A report that reads a live view reads the view's rows as its query computes them.

Each run writes its temporary files to a folder of its own under `TemporaryDirectory`, and deletes it when it ends. At most `MaxConcurrentRuns` reports run at once in a tenant; the others wait for one to end.

## Limits

Data sources stream their rows in batches (500 rows by default), and record-backed sources read their store a page at a time, so a source never loads more than the engine asks for. The CrestApps sources find each page through a database index on their main date (keyset paging), so the thousandth page costs the same as the first. The engine reads each data set up to `MaxRowsPerDataSet` rows, in the order the source reads (newest first for the CrestApps sources), and warns when a data set has more: it asks for one row more than the limit to tell. Joins may produce at most `MaxJoinedRows` rows, and a result keeps at most `MaxResultRows` rows after sorting. The defaults and how to change them are in [Configuration](index.md#configuration).

Four mechanisms keep large data sets fast and complete.

## A date filter from the start

Data sets name their main date (`DataSetDescriptor.DefaultDateField`, such as when a record was created). When the first data set of a new report has one, the builder adds a filter on it that viewers see as a **recent period** (today, the last 7, 30 or 90 days, the last 12 months, or all time), starting at the last 30 days.

Relative date filters (in the last or next N days) are passed to the sources as a date range, so they read only that period. Record-backed sources read the range with `DataConditions.DateRange` and apply it to an indexed date column; see [Add your own data source](custom-data-sources.md#conditions-you-may-push-down).

## Grouping in the data source

A report over one data set that groups by fields or date periods and counts, sums, averages, or takes the smallest or largest values can be answered by its source, through Orchard Core's `IDataAggregateSource` (a `RecordDataSource` passes it to the data set when it implements `IAggregateRecordDataSet`). The source returns per group the row count and the count, sum, smallest and largest value of each measured field; the engine merges these partial aggregates wherever it would aggregate rows, so sorting, result filters, totals, charts and pivots give the same result as reading every row.

The engine asks the source only when the report:

- reads one data set, with no joins and no row-level calculated fields;
- groups by data set fields, as they are or with the **Hour of day** (date-times), **Day**, **Week**, **Day of week**, **Month**, **Month of year**, **Quarter** or **Year** transform;
- measures data set fields without a transform with **Count**, **Sum**, **Average**, **Minimum** or **Maximum**, or counts rows;
- filters rows on data set fields with conditions the source can apply exactly, and has no viewer filter that lists options.

Row filters are sent as exact conditions and date periods as UTC boundaries of local hours, days or months (at most 1,000 buckets per date). Whatever a source cannot answer exactly (a computed field, a text "contains" filter, a median or distinct count, a calculated field, an exposed filter that lists options) is read row by row as before, and so is anything the source declines by returning `null`. A source may return at most `MaxRowsPerDataSet` groups.

`ReportIndexAggregator` answers such a query with one `GROUP BY` statement over a YesSql index table; Omnichannel activities and the Contact Center data sets use it for the fields their index holds. See [Grouping in the data source](custom-data-sources.md#grouping-in-the-data-source) to add it to your own data sets.

## Viewer filters read only what matches

A row filter on a data set is passed to its source, which applies what it can in the database and leaves the rest to the engine, which applies every filter again. The CrestApps sources apply date ranges on their main date, and filters that limit a field to a list of values (such as **Channel is SMS** or **Status is one of**) on the fields their index holds, comparing text without regard to case. A data set then reads only the matching records, so `MaxRowsPerDataSet` counts matching records, and a filter on a rare value returns all of them instead of the matches among the newest records.

A filter the source cannot apply, such as one on a field its index doesn't hold or a text "contains" filter, is applied by the engine to the rows read, so with large data combine it with a date filter.

## Joins read only what can match

For an inner or left join on a field the source can filter exactly (`DataField.IsKeyFilterable`), the engine sends the distinct keys of the rows before it as an `In` `DataCondition` marked `IsJoinKey`, in batches of `JoinKeyBatchSize` up to `MaxJoinKeys` keys. The joined data set then reads the records that can match instead of its newest records. Only text and whole-number keys joined to a field of the same type are narrowed this way.

Users, content items, Omnichannel activities, Contact Center records, AI chat sessions and messaging conversations and messages filter their key columns this way; a join with more keys, or on another field, reads the joined data set in full, up to `MaxRowsPerDataSet`. When the rows before a join hold no key at all, the joined data set is not read. See [Join keys](custom-data-sources.md#join-keys).

## Scheduled views

A view's **Refresh** setting (`ReportView.RefreshIntervalMinutes`: `0` for live; the builder offers 15, 60, 360 and 1440 minutes, and any other value, such as one from a recipe, is raised to at least 15) stores its result instead of running the view every time a report reads it:

- The **Report view refresh** background task runs every 5 minutes and refreshes each due view in its own scope, with a distributed lock so two nodes never refresh the same view. A failed refresh keeps the previous rows and records the error. Tenants can turn the task off under **Background Tasks**.
- A scheduled view runs with its **owner's** current access, since there is no reader at refresh time; it does not refresh when its owner is deleted or disabled or can no longer read it. Reports that read the view still need to be allowed to read it and to plan its query, but they get the rows the owner's access produced. Schedule only views whose data everyone who can read the view may see.
- Reports read the stored rows (at most `MaxRowsPerDataSet`, held in memory while they're read) while the stored fields match the view. Editing the view's query, switching it to live, or importing a changed view drops the stored rows, and reports run the view live until the next refresh.
- **Refresh now** (`POST /Admin/reports/views/{id}/refresh`, which needs `ManageAllReportDesigns` on the view) refreshes a view at once and returns its status.
- Deleting a view deletes its stored rows.

How people schedule a view is described in the User Manual: [Reusable views](../../user-manual/report-builder/views.md#refresh-a-view-on-a-schedule).

## Checklist for a large data set

- Give the data set a main date, and keep a date filter on reports over it.
- Index the main date and every key column a join filters, and mark those keys `IsKeyFilterable`.
- Implement grouping in the source for the fields its index holds.
- For a heavy view that many reports read, schedule it.
- Read the store a page at a time (`ReportIndexPages` for YesSql) rather than loading every record at once.
- Raise the row limits in [Configuration](index.md#configuration) only after the above: memory stays bounded, but every row read takes time, and rows that don't fit in memory are written to disk.
