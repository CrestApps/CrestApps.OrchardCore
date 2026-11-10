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

<YouTube video="report-builder-administration" title="Video of what the limits mean on large data" start={145} end={187} />

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

- reads one data set, with no joins and no row-level calculated fields among those its columns and filters use (calculated fields the query has but doesn't use don't count);
- groups by data set fields, as they are or with the **Hour of day** (date-times), **Day**, **Week**, **Day of week**, **Month**, **Month of year**, **Quarter** or **Year** transform;
- measures data set fields without a transform with **Count**, **Sum**, **Average**, **Minimum** or **Maximum**, or counts rows;
- filters rows on data set fields with conditions the source can apply exactly, and has no viewer filter that lists options.

Row filters are sent as exact conditions and date periods as UTC boundaries of local hours, days or months (at most 1,000 buckets per date). Whatever a source cannot answer exactly (a computed field, a text "contains" filter, a median or distinct count, a calculated field, an exposed filter that lists options) is read row by row as before, and so is anything the source declines by returning `null`. A source may return at most `MaxRowsPerDataSet` groups.

`ReportIndexAggregator` answers such a query with one `GROUP BY` statement over a YesSql index table; Omnichannel activities and the Contact Center data sets use it for the fields their index holds. Text is grouped without regard to case, like filters and joins compare it; MySQL leaves a group by text to the engine, and every database leaves the smallest or largest text value to it. See [Grouping in the data source](custom-data-sources.md#grouping-in-the-data-source) to add it to your own data sets.

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

## Calculated fields a query doesn't use

A query computes only the calculated fields its columns and filters use, and the fields those read, and reads from its data sets only the fields it needs. A dashboard whose queries are copies of one query, each with the same set of calculated fields, computes in each query only the fields that query shows or filters on. Every calculated field is still checked, so the builder lists the problems of all of them.

## Queries that read the same data set

The queries of one run that read the same data set share one read (see [Interactive dashboards](interactive-dashboards.md#how-a-report-runs)): the first one reads it from its source, with every field any of the queries reads, and keeps the rows; the others read the rows kept. A query whose row filters include all of those of a kept read, such as one a click filters, reads the kept rows too. A run keeps at most `MaxSharedReadRows` rows of one read; a larger data set is read once per query.

On a scorecard dashboard shaped like a real one (2,000 calls, four queries that copy the same 70 calculated fields that search each call's summary, and three viewer filters), the full run reads the data set once instead of four times, and clicking an agent reads it once for the calls and the trend chart. The test `ReportDashboardScaleTests` builds that dashboard and writes the time it takes to its output.

## Checklist for a large data set

- Give the data set a main date, and keep a date filter on reports over it.
- Index the main date and every key column a join filters, and mark those keys `IsKeyFilterable`.
- Implement grouping in the source for the fields its index holds.
- For a heavy view that many reports read, schedule it.
- Read the store a page at a time (`ReportIndexPages` for YesSql) rather than loading every record at once.
- Raise the row limits in [Configuration](index.md#configuration) only after the above: memory stays bounded, but every row read takes time, and rows that don't fit in memory are written to disk.

## Telemetry

Reports emit OpenTelemetry spans and metrics through the .NET `ActivitySource` and `Meter` named `CrestApps.OrchardCore.Reports` (`ReportTelemetry.SourceName` and `ReportTelemetry.MeterName`): designed report runs, coded reports, exports, views, subscriptions, the designer and the widgets. The PDF export adds one instrument to a meter of the same name. While nothing listens to them, no span is started and no measurement is taken. To export them, add the name to the host's OpenTelemetry setup (the `CrestApps.OrchardCore.*` wildcard covers it too), for example in the `Program.cs` of the web host, or in the `ConfigureOpenTelemetry` method of an Aspire service defaults project:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("CrestApps.OrchardCore.Reports"))
    .WithMetrics(metrics => metrics.AddMeter("CrestApps.OrchardCore.Reports"));
```

### Tags

Metrics carry bounded tags only, so a site with thousands of reports still stores a handful of series:

- `crestapps.report.source`: what the run is for (`ReportRunSources`): `Admin`, `Shared` or `Link` for the report pages, `Preview` for the builder, `Widget`, `Workflow`, `Subscription`, `ViewRefresh` for the refresh of a scheduled view, `ViewRead` for a live view another report reads, and `Other` for anything else. A value that is not in this list is tagged `Other`.
- `crestapps.report.data_source`: the registered data source, such as `Contents` or `ReportViews`.
- `outcome`: `ok`, `error`, `cancelled` or `timeout`, plus the values an instrument lists below.
- `crestapps.report.name`: the technical name of a coded report in the report registry.
- `format`: the name of a registered export format; a format typed in an address that is not registered is `other`.

The report and query identifiers (`crestapps.report.id`, `crestapps.report.query.id`), the data set (`crestapps.report.data_set`), the view and subscription identifiers and the report title are **span tags only**: they no longer tag metrics. The `crestapps.report.outcome` tag of a run (`success`, `failure`, `canceled`) is kept on `report.run` and `crestapps.report.run.duration` for one release next to `outcome`, which replaces it.

### Spans

| Span | One per | Tags besides the common ones |
| --- | --- | --- |
| `report.run` | Run of a designed report, including the preview and a click that refreshes some visuals | `crestapps.report.click_filtered`, `crestapps.report.refreshed_visuals` (the visuals a click refreshes), `crestapps.report.rows.returned`, `crestapps.report.warnings`, `crestapps.report.queries`, `crestapps.report.queries.failed`, `outcome`, `crestapps.report.outcome` |
| `report.plan` | Run of a query (a query filtered by a click is a run of its own) | `crestapps.report.calculated_fields`, `crestapps.report.row_calculations` (the ones it computes), `crestapps.report.errors` |
| `report.schema` | Data set schema a plan reads, whoever plans: report runs, views, the builder | `crestapps.report.data_source`, `crestapps.report.data_set`, `outcome` (`ok`, `not_found`, `error`) |
| `report.query` | Run of a query | `crestapps.report.gate_wait_ms` (time waiting for `MaxConcurrentRuns`), `crestapps.report.rows.source`, `crestapps.report.rows.filtered`, `crestapps.report.rows.returned`, `crestapps.report.calculate_ms`, `crestapps.report.calc_errors`, `crestapps.report.grouped`, `crestapps.report.grouped_by_source`, `crestapps.report.pushdown_fallback`, `crestapps.report.sorted`, `crestapps.report.spills` |
| `report.stream` | Query whose rows stream to another report, such as a live view | `crestapps.report.rows.source`, `crestapps.report.rows.returned`, `outcome` |
| `report.read` | Read of a data set | `crestapps.report.data_source`, `crestapps.report.data_set`, `crestapps.report.fields`, `crestapps.report.rows.read`, `crestapps.report.read_shared` (read from the rows another query kept), `crestapps.report.truncated`, `crestapps.report.join_key_batches`, `crestapps.report.view.mode` (a view) |
| `report.join` | Join | `crestapps.report.data_source`, `crestapps.report.data_set`, `crestapps.report.join_type`, `crestapps.report.join_keys`, `crestapps.report.rows.joined` |
| `report.result` | Run of a query, while it builds its result and the groupings its totals, charts and pivots need | |
| `report.render` | Run of a query that shows visuals, while it builds their sections | `crestapps.report.visuals` |
| `report.present` | Report page, shared page or share link, around its run | `crestapps.report.id`, `crestapps.report.source` |
| `report.events` | Fan-out of a run or an export to the report event handlers, such as the workflow events | `crestapps.report.id`, `crestapps.report.event` (`ran`, `exported`) |
| `report.coded.run` | Run of a coded report | `crestapps.report.name`, `crestapps.report.mode`, `outcome` |
| `report.export` | File an export format writes | `format`, `crestapps.report.source`, `crestapps.report.export.size`, `crestapps.report.export.rows`, `outcome` |
| `report.view.refresh` | Refresh of a scheduled view that runs the view | `crestapps.report.view.id`, `trigger`, `crestapps.report.view.snapshot.rows`, `outcome` (`refreshed`, `failed`) |
| `report.view.sweep` | Run of the **Report view refresh** background task | `crestapps.report.views.due`, `.refreshed`, `.failed`, `.skipped` |
| `report.subscription.send` | Run of a subscription | `crestapps.report.subscription.id`, `trigger`, `reason`, `outcome` |
| `report.subscription.sweep` | Run of the **Report subscriptions** background task | `crestapps.report.subscriptions.due`, `.sent`, `.not_sent`, `.failed` |

The designed run spans carry `crestapps.report.id`, `crestapps.report.title`, `crestapps.report.source` and `crestapps.report.query.id` (empty for the main query). Rows stream through a query, so the read, join and calculation of one query overlap: `report.read` lasts while its rows are consumed, and `crestapps.report.calculate_ms` adds up the time spent on calculated fields.

### Metrics

Designed report runs:

| Instrument | Kind | Unit | Tags | What it measures |
| --- | --- | --- | --- | --- |
| `crestapps.report.run.duration` | Histogram | s | source, `outcome`, `crestapps.report.outcome` (one release) | Designed report runs |
| `crestapps.report.run.failures` | Counter | `{run}` | source, `crestapps.report.level` (`run`, `query`) | Each failed run once at `run`, each failed query once at `query` (an invalid plan, or an exception in the engine). A cancellation isn't a failure. A run whose secondary query fails is `ok` and counts that query at `query`. |
| `crestapps.report.run.queries` | Histogram | `{query}` | source | Queries one run executed |
| `crestapps.report.run.queries.skipped` | Counter | `{query}` | source, `reason` (`invalid`, `failed`) | Queries of a run that did not run |
| `crestapps.report.run.rejected` | Counter | `{run}` | source, `reason` (`owner_missing`) | Runs refused before they started |
| `crestapps.report.query.duration` | Histogram | s | source, `outcome` | Queries |
| `crestapps.report.stream.duration` | Histogram | s | source, `outcome` | Queries streamed to another report |
| `crestapps.report.plan.duration` | Histogram | s | `outcome` (`ok`, `invalid`, `error`) | Query plans, whoever plans |
| `crestapps.report.schema.duration` | Histogram | s | data source, `outcome` (`ok`, `not_found`, `error`) | Data set schema reads of plans |
| `crestapps.report.rows.read` | Counter | `{row}` | source, data source | Rows read from data sources |
| `crestapps.report.rows.returned` | Counter | `{row}` | source | Rows queries return |
| `crestapps.report.reads.shared` | Counter | `{read}` | source, data source | Reads served from the rows another query kept |
| `crestapps.report.reads.shared.miss` | Counter | `{read}` | data source, `reason` (`disabled`, `join_key`, `conditions`, `too_many_rows`) | Reads of a data set several queries read that the rows kept could not serve |
| `crestapps.report.limit.hits` | Counter | `{hit}` | `crestapps.report.limit` (`rows_per_dataset`, `joined_rows`, `result_rows`, `join_keys`, `shared_read_rows`), source | Runs that reached a limit of [Configuration](index.md#configuration) |
| `crestapps.report.aggregate.pushdown` | Counter | `{query}` | `outcome` (`used`, `fallback`), `reason` (`multiple_data_sets`, `joins`, `row_calculations`, `not_supported`, `filter`, `dimension`, `measure`, `too_many_buckets`, `declined`), data source | Grouped queries the data source grouped, or why they read every row |
| `crestapps.report.aggregate.index` | Counter | `{statement}` | `result` (`built`, `unbuildable`, `too_many_groups`), `crestapps.report.index` | `ReportIndexAggregator` statements over a YesSql index |
| `crestapps.report.index.page.duration` | Histogram | s | `crestapps.report.index`, `crestapps.report.phase` (`index`, `records`) | Pages `ReportIndexPages` reads |
| `crestapps.report.index.pages` | Counter | `{page}` | `crestapps.report.index`, `crestapps.report.phase` | Pages `ReportIndexPages` reads |
| `crestapps.report.calc.errors` | Counter | `{error}` | source | Calculated field values that failed and were left empty, added once per query |
| `crestapps.report.spills` | Counter | `{file}` | source | Temporary files a query wrote because its rows didn't fit in `MemoryLimitMegabytes` |
| `crestapps.report.gate.wait` | Histogram | s | source | Time runs waited for `MaxConcurrentRuns` |
| `crestapps.report.gate.active` | UpDownCounter | `{run}` | source | Runs that hold the gate |
| `crestapps.report.gate.waiting` | UpDownCounter | `{run}` | source | Runs that wait for the gate |

Coded reports, exports, views and subscriptions:

| Instrument | Kind | Unit | Tags | What it measures |
| --- | --- | --- | --- | --- |
| `crestapps.report.coded.duration` | Histogram | s | `crestapps.report.name`, `crestapps.report.mode` (`display`, `export`), `outcome` | Coded report runs |
| `crestapps.report.coded.runs` | Counter | `{run}` | `crestapps.report.name`, `crestapps.report.mode`, `outcome` | Coded report runs |
| `crestapps.report.coded.index_rows` | Histogram | `{row}` | `crestapps.report.name` | Index rows a coded report read at once; from 50,000 rows (`ReportTelemetry.CodedIndexRowsWarningThreshold`) a warning is logged |
| `crestapps.report.export.duration` | Histogram | s | `format`, `outcome` | Time export formats took to write their file |
| `crestapps.report.export.size` | Histogram | By | `format` | Size of the exported files |
| `crestapps.report.export.rows` | Histogram | `{row}` | `format` | Table rows of the exported files |
| `crestapps.report.exports` | Counter | `{export}` | source, `format`, `outcome` (`ok`, `error`, `not_found`, `forbidden`) | Export requests, including those refused for an unknown format or a missing permission |
| `crestapps.report.pdf.font_fallbacks` | Counter | `{export}` | | PDF exports written with the built-in Helvetica font, which shows Latin-1 text only; a warning is logged once |
| `crestapps.report.view.refresh.duration` | Histogram | s | `trigger` (`schedule`, `manual`), `outcome` (`refreshed`, `failed`) | Refreshes that ran a view |
| `crestapps.report.view.refreshes` | Counter | `{refresh}` | `trigger`, `outcome` (`refreshed`, `failed`, `busy`, `not_due`, `not_scheduled`, `not_found`) | Refresh attempts |
| `crestapps.report.view.snapshot.rows` | Histogram | `{row}` | | Rows a refresh stored |
| `crestapps.report.view.snapshot.age` | Gauge | s | | Age of the oldest stored rows of a scheduled view, at each run of the background task. Alert when it grows well past the longest refresh interval. |
| `crestapps.report.view.reads` | Counter | `{read}` | `crestapps.report.view.mode` (`snapshot`, `live`, `stale_snapshot`, `first_run`) | Reads of views by other reports |
| `crestapps.report.view.rejected` | Counter | `{read}` | `reason` (`loop`, `depth`) | View reads refused |
| `crestapps.report.subscription.sends` | Counter | `{email}` | `trigger`, `outcome` (`ok`, `skipped`, `error`), `reason` (`no_email`, `no_recipients`, `report_unavailable`, `export_denied`, `send_failed`, `exception`) | Subscription runs |
| `crestapps.report.subscription.duration` | Histogram | s | `trigger`, `outcome` | Subscription runs, from running the report to sending the email |

The builder and the widgets:

| Instrument | Kind | Unit | Tags | What it measures |
| --- | --- | --- | --- | --- |
| `crestapps.report.design.publishes` | Counter | `{publish}` | `outcome` (`saved`, `invalid`, `conflict`, `busy`, `not_found`) | Publishes |
| `crestapps.report.design.conflicts` | Counter | `{request}` | `reason` (`conflict`, `busy`) | Saves refused with a 409 |
| `crestapps.report.design.validation_errors` | Counter | `{error}` | | Problems the builder found in the queries it checked |
| `crestapps.report.shared_link.requests` | Counter | `{request}` | `result` (`opened`, `exported`, `not_found`, `export_denied`) | Requests of share links, which anonymous visitors can reach |
| `crestapps.report.workflow.tasks` | Counter | `{task}` | `crestapps.report.workflow_task` (`run`, `export`, `email`, `share_link`), `outcome` | Report workflow tasks |
| `crestapps.report.widget.requests` | Counter | `{request}` | `crestapps.report.kind` (`design`, `coded`), `crestapps.report.cache` (`hit`, `miss`, `off`), `outcome` | Dashboard widget renders |
| `crestapps.report.hub.connections` | UpDownCounter | `{connection}` | | Open connections to the builder presence hub |
| `crestapps.report.hub.denied` | Counter | `{call}` | `operation` | Presence hub calls refused |

Both background tasks also report `crestapps.background_task.runs` and `crestapps.background_task.items` (`refreshed`, `failed`, `skipped` for views; `sent`, `not_sent`, `failed` for subscriptions), the builder notifications run as the deferred task `report_design_notify`, and the view refresh and report history locks report `crestapps.lock.acquisitions` with the `lock.area` `reports.view_snapshot` and `reports.design`.

### Logs

The report engine, the Reports module and the PDF export log with event ids from 14000 to 14999:

| Event id | Level | When |
| --- | --- | --- |
| 14001 | Warning | A coded report read at least 50,000 index rows at once |
| 14002 | Debug | The temporary files of a query could not be deleted |
| 14101 | Warning | A designed report run ended with errors |
| 14102 | Warning | A designed report run took longer than 10 seconds |
| 14103 | Information | Summary of a view refresh pass: due, refreshed, failed, skipped |
| 14104 | Information | Summary of a subscription pass: due, sent, not sent, failed |
| 14105 | Error | A subscription failed; the other due subscriptions still run |
| 14106 | Warning | A subscription sent nothing, with its reason |
| 14107 to 14109 | Debug | The presence hub refused a call, a connection opened, a connection closed |
| 14110 | Debug | The builder found problems in a query |
| 14111, 14112 | Information | Summary of the report designs recipe step and deployment step |
| 14400 | Warning | The PDF export has no TrueType font (logged once) |
