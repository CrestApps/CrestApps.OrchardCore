---
sidebar_label: Overview
title: Report Builder
description: A drag-and-drop report builder with pluggable data sources, joins, formulas, filters, charts, pivots, reusable views, and secure sharing.
user_manual:
  - user-manual/report-builder/index
  - user-manual/report-builder/getting-started
---

The **Report Builder** feature of the [Reports](../reports.md) module lets people build their own reports in the browser. A designed report reads data sets from one or more **data sources**, joins them, adds calculated fields, filters, groups and aggregates the rows, and renders tables, charts, headline metrics and pivot tables through the same renderer and exporters as every other report. The built-in sources cover content items, saved queries, reusable views and users, other CrestApps modules add their records, and any module can add more.

This video walks through the Report Builder: building a report, aggregates and formulas, filters, visuals, joins, reusable views, sharing, and every data source.

<video controls preload="metadata" width="100%" poster="/img/docs/report-builder.jpg" aria-label="Video overview of the Report Builder">
  <source src="/img/docs/report-builder.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/report-builder.vtt" srcLang="en" label="English" default />
</video>

| | |
| --- | --- |
| **Feature Name** | Report Builder |
| **Feature ID** | `CrestApps.OrchardCore.Reports.Builder` |
| **Dependency** | `CrestApps.OrchardCore.Reports` |

How people use the builder is described in the User Manual, starting at [Report Builder](../../user-manual/report-builder/index.md).

## In this section

| Page | What it covers |
| --- | --- |
| [Permissions and security](permissions.md) | The four permissions, how reports and views are authorized per resource, the owner's-access model, share links and limits. |
| [Built-in data sources](data-sources.md) | Every data source and data set the builder offers, when each is registered, and the permission each asks for. |
| [Add your own data source](custom-data-sources.md) | Extending the builder: `IReportDataSource`, `ReportRecordDataSource` and `ReportRecordDataSet<T>`, grouping in the source, join keys, and content field providers. |
| [Formula reference](formulas.md) | The formula language: syntax, operators, types, and every function. |
| [Large data](large-data.md) | Row limits, date push-down, grouping in the source, join key narrowing, and scheduled views. |
| [Drafts, versions and real time](drafts-versions-and-real-time.md) | Autosave, revisions and conflicts, publishing, versions, and SignalR presence. |
| [Recipes and deployment](recipes-and-deployment.md) | The deployment step and the `ReportDesigns` recipe step. |

## Architecture

The builder is split over the three Reports layers, so the engine can be reused outside Orchard Core and data sources can be added from any module:

- **`CrestApps.OrchardCore.Reports.Abstractions`** holds the data source contract (`IReportDataSource`, `IReportAggregateDataSource`, `IReportDataSourceManager`, `ReportDataType`, `ReportFieldDescriptor`, `ReportFieldReference`, `ReportDataSetDescriptor`, `ReportDataSetSchema`, `ReportDataSourceQuery`, `ReportDataCondition`, `ReportDataTable`, `ReportDataValues`, `ReportJoinKeys`, `ReportDateRange`, and the `ReportRecordDataSource` and `ReportRecordDataSet<T>` base classes) in the `CrestApps.OrchardCore.Reports.DataSources` namespace, and the designed query model (`ReportQueryDefinition`, `ReportDataSetReference`, `ReportJoinDefinition`, `ReportCalculatedField`, `ReportFilterDefinition`, `ReportColumnDefinition`, `ReportSortDefinition`, `ReportVisualDefinition`, and the `ReportAggregate`, `ReportFieldTransform`, `ReportFilterControl`, `ReportFilterStage`, `ReportJoinType` and `ReportVisualType` enums) in `CrestApps.OrchardCore.Reports.Designer`.
- **`CrestApps.OrchardCore.Reports.Core`** holds the engine: `ReportQueryPlanner` checks a query against the live schemas and compiles it, `ReportQueryEngine` runs it, the formula language lives in `Designer/Expressions`, `ReportDesignDocumentBuilder` turns a result into a `ReportDocument`, and `ReportIndexAggregator` lets YesSql-backed sources group in the database.
- **`CrestApps.OrchardCore.Reports`** holds the Orchard Core side: the stores of designed reports, views, drafts, versions and share links, permissions and the authorization handler, the builder pages and JSON endpoints, the admin menu, the background task that refreshes scheduled views, and the built-in **Report views**, **Users**, **Content items** and **Queries** data sources.

A run goes through these steps:

1. **Plan.** Every data set's schema is read with the principal the run reads data for. A data set the principal may not read fails the plan. Joins, calculated fields, columns, filters, sorts and the limit are checked, and every problem is reported at once.
2. **Read.** Each data source returns only the fields the query uses, up to the row limit, newest first. Row filters on data sets that no outer join can fill with empty values are offered to the source as `ReportDataCondition`s, which it may apply to read less; the engine always applies every filter again. Data sets are read in join order: a joined data set is read only for the keys the rows before it hold (see [Large data](large-data.md#joins-read-only-what-can-match)). A simple report over one data set can be answered by the source itself (see [Large data](large-data.md#grouping-in-the-data-source)).
3. **Join.** Data sets are hash-joined in order (inner, left, right or full). Keys of different types (text and number) are compared as text; empty keys never match.
4. **Calculate, filter, group.** Row-level calculated fields are evaluated, then fixed filters, then the exposed filters with the viewer's values. When any column is a measure, rows are grouped by the dimension columns.
5. **Shape.** Result filters, sorts, the row limit and the result-size limit are applied.
6. **Render.** Visuals regroup the rows behind the result by fewer dimensions (charts, metrics, pivot tables, totals and table subtotals), so non-additive measures such as averages and distinct counts stay correct. A table with `ShowSubtotals` keeps its rows together by its leading dimensions and adds a subtotal after each group, innermost first: with Role, User and Status, a subtotal per user within each role, then per role, then the grand total.

Processing runs in memory over the rows the sources return, so every source gets joins, formulas and aggregation without implementing them. Sources can take on part of the work to handle large data; see [Large data](large-data.md).

### Time zones

A data source returns `DateTime` field values in UTC. The engine converts them to the tenant time zone before anything else, so transforms, formulas, filters and display all work in local time. `Date` fields carry no time zone and are never shifted. Filter values written without a time (`2026-01-31`) cover the whole day.

### Values and formats

Values are formatted with the current culture. A column's **Format** is a .NET format string (`N0`, `C2`, `P1`, `yyyy-MM-dd`); left empty, whole numbers use `N0`, decimals `#,##0.##`, dates `d`, and date-times `g` (`d` when the time is midnight). Booleans show as Yes and No, a **Month** transform shows as month and year (`Y`), and **Day of week** and **Month of year** dimensions show as day and month names. A format that does not suit the value falls back to the default.

## Pages and URLs

| URL | What it is |
| --- | --- |
| `/Admin/reports/designs` | The designed reports the user can run (Report Builder, or Shared Reports for people who do not build reports). Accepts `q` (search title and category) and `status` (`published` or `unpublished`). |
| `/Admin/reports/designs/{id}` | Runs a designed report in the admin. |
| `/Admin/reports/designs/{id}/export/{format}` | Exports a designed report (`csv`, or `xlsx` with Reports (OpenXml)). |
| `/Admin/reports/builder/create`, `/Admin/reports/builder/edit/{id}` | The builder. |
| `/Admin/reports/views`, `/Admin/reports/views/create`, `/Admin/reports/views/edit/{id}` | Reusable views. |
| `/reports/view/{id}` | A report shared with users or roles, outside the admin, for people without admin access. |
| `/reports/shared/{token}` | A report opened through a share link. |

Run and export pages read exposed filter values from the query string: `applied=1`, then `f.{filterId}` (repeatable) or `f.{filterId}.from` and `f.{filterId}.to` for ranges.

The builder talks to JSON endpoints under `/Admin/reports/builder/api/` (`sources`, `datasets`, `schema`, `functions`, `plan`, `users`), `/Admin/reports/builder/preview`, `/Admin/reports/builder/save`, `/Admin/reports/views/save`, the draft and version endpoints described in [Drafts, versions and real time](drafts-versions-and-real-time.md), and the share link endpoints `/Admin/reports/builder/{id}/links`. They are internal to the builder and may change.

## Configuration

The size limits of one run and the number of versions kept can be changed in the host's `appsettings.json` (a tenant's own `App_Data/Sites/{tenant}/appsettings.json` takes the same section without the `OrchardCore` wrapper):

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Reports": {
        "Builder": {
          "Limits": {
            "MaxRowsPerDataSet": 50000,
            "MaxJoinedRows": 250000,
            "MaxResultRows": 10000,
            "MaxFilterOptions": 500,
            "MaxJoinKeys": 10000,
            "JoinKeyBatchSize": 500
          },
          "Versions": {
            "MaxVersions": 50
          }
        }
      }
    }
  }
}
```

As environment variables:

```text
OrchardCore__CrestApps__Reports__Builder__Limits__MaxRowsPerDataSet=50000
OrchardCore__CrestApps__Reports__Builder__Limits__MaxJoinedRows=250000
OrchardCore__CrestApps__Reports__Builder__Limits__MaxResultRows=10000
OrchardCore__CrestApps__Reports__Builder__Limits__MaxFilterOptions=500
OrchardCore__CrestApps__Reports__Builder__Limits__MaxJoinKeys=10000
OrchardCore__CrestApps__Reports__Builder__Limits__JoinKeyBatchSize=500
OrchardCore__CrestApps__Reports__Builder__Versions__MaxVersions=50
```

| Setting | Default | What it limits |
| --- | --- | --- |
| `MaxRowsPerDataSet` | `50000` | Rows read from one data set. A report that hits it shows a warning. Also the most groups a source may return when it groups a report itself, and the most rows a scheduled view stores. |
| `MaxJoinedRows` | `250000` | Rows the joins may produce. |
| `MaxResultRows` | `10000` | Result rows kept after sorting. |
| `MaxFilterOptions` | `500` | Values listed by a drop-down filter. |
| `MaxJoinKeys` | `10000` | Distinct keys a join sends to the data set it joins; with more, that data set is read in full. |
| `JoinKeyBatchSize` | `500` | Keys sent in one read of a joined data set. |
| `Versions:MaxVersions` | `50` | Published versions kept per report; the oldest are deleted. `0` keeps them all. |

The sections are also listed in [Configuration](../../configuration.md).
