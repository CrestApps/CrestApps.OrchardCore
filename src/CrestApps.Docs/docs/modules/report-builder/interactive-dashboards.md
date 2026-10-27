---
sidebar_label: Interactive Dashboards
title: Report Builder Interactive Dashboards
description: Several queries in one report, containers that frame and scroll visuals, and click actions that filter visuals, as the design stores them and the runner applies them.
user_manual:
  - user-manual/report-builder/dashboards
---

A designed report can work as an interactive dashboard: several queries grouped different ways on one page, containers that frame visuals and scroll them within a height, and click actions that filter some visuals by the value clicked in another. How people build one is described in the User Manual, at [Interactive dashboards](../../user-manual/report-builder/dashboards.md).

## The design

`ReportDesign` stores the three parts next to its query and visuals.

| Member | Type | What it holds |
| --- | --- | --- |
| `Query` | `ReportQueryDefinition` | The main query. Its result is the report's rows (`ReportRunResult.Rows`), and it shows as one table when the report has no visuals. |
| `Queries` | `IList<ReportNamedQuery>` | The other queries, each with an `Id`, a `Name` and a `Query`. `GetQueries()` lists the main query first, with no identifier. |
| `Visuals[].QueryId` | `string` | The query a visual shows; `null` for the main query. |
| `Containers` | `IList<ReportContainerDefinition>` | The containers, each with an `Id`, a `Title`, a `Width` on the twelve-column grid and a `Height` in pixels (`0` grows with its visuals, at most `ReportContainerDefinition.MaxHeight`). |
| `Visuals[].ContainerId` | `string` | The container a visual is shown in; `null` shows it on its own. |
| `Visuals[].Action` | `ReportVisualAction` | What clicking the visual does: the `ColumnId` whose value a click picks, and the `Targets` it filters, each a `VisualId` and an optional `Field`. |

`ReportDesignNormalizer` cleans each part when a design is saved, imported or previewed: it drops queries and containers without an identifier or with one already used, clamps container sizes, and gives every visual an identifier of its own, since sections, containers and click actions find visuals by identifier.

## How a report runs

`ReportDesignRunner` runs each query for the visuals that show it, and merges their sections in the order of the report's visuals. The main query always runs; another query runs only when a visual shows it. A query that cannot run is a warning that names it, and the report shows the others; the main query's problems stop the report, as before.

Viewer filters are matched by identifier: a value submitted for a filter applies to every query that has a viewer filter with that identifier, and the page shows one control for them, taken from the first query that has it. The builder copies a query with its filters, so a copied query keeps its filters linked. A widget's period is set on the main query's date filter and on the filters of the other queries with the same identifier.

`ReportDesignDocumentBuilder.AddContainers` adds the containers the sections use to the `ReportDocument`, in the order of their first sections. `ReportSection.Id` is the visual identifier and `ReportSection.ContainerId` its container. The report page renders a container as a card with its sections inside a scrolling body; exports read only `ReportDocument.Sections`, so they list every section.

## Click actions

A visual can be clicked when its action's column is a dimension it shows: a column of a table (shown by the table), or the category or series column of a chart. The document builder then gives each detail row of the table a `ReportRow.Key`, and the chart its `ReportChart.Keys` (categories) or `ReportChart.SeriesKeys`. `ReportSelectionKeys.ToKey` writes values in the invariant culture: dates as `yyyy-MM-dd`, or `yyyy-MM-ddTHH:mm:ss` with a time, and a missing value as an empty string. The section's `Selection` lists the visuals it filters and the keys picked now.

The picked keys come back as `ReportRunOptions.Selections`, by visual identifier. For each target, the runner adds a row filter to a copy of the target's query with `ReportSelectionKeys.ToFilter`:

| Column of the visual clicked | Filter on the target's field |
| --- | --- |
| No transform, **Upper** or **Lower** | `Equals` the value, or `In` the values. Text comparisons ignore case. |
| A date kept to its **Day**, **Week**, **Month**, **Year** or **Hour** | `Between` the start of the first period picked and the end of the last. |
| Only a missing value picked | `IsEmpty`. |
| Any other transform | None: those values do not map back to the field. |

The target's field is the clicked column's field unless the target names another one. The runner checks the target's query reads it (a data set alias it has, or a calculated field of its own) and reports a warning otherwise. Visuals filtered alike share one run of their query, the visuals no click filters run it unfiltered, and the visual clicked is never filtered by its own click.

### On the page

The report page carries the picked values in its filter form as `sel.` fields followed by the visual identifier, one per value, so filtering, exporting and reloading keep them. `report-interactions.js` handles clicks: it highlights the values picked, updates the form, and loads the page again with the form's values and `rv` set to the identifiers of the visuals to refresh, separated by commas. `DesignedReportPresenter` then runs only the queries those visuals show and doesn't raise the report's run event; the script swaps the returned sections in and draws their charts. Every page that shows a designed report (the admin page, the shared page and a share link) supports this with no extra endpoint, through the same permission checks. An export ignores `rv` and is filtered by the picked values.

`report-charts.js` raises a `report-chart-select` event when a clickable chart is clicked, and `CrestAppsReportCharts.select(canvas, keys)` fades what a pick leaves out. A section raises `report-selection-change` with the visual identifier, the keys and the target identifiers; the builder's preview listens for it and runs the preview again with the picked values.

## Recipes

The `ReportDesigns` recipe step imports the new members like the rest of a design (see [Recipes and deployment](recipes-and-deployment.md)):

```json
{
  "name": "ReportDesigns",
  "Reports": [
    {
      "itemId": "4z1f5b0c6t0m9z2k8w3d7r5q1q",
      "displayText": "Call quality",
      "query": {
        "dataSets": [ { "alias": "c", "source": "MyCalls", "dataSet": "Call" } ],
        "columns": [ { "id": "agent", "field": "c.Agent" }, { "id": "score", "field": "c.Score", "aggregate": "Average" } ]
      },
      "queries": [
        {
          "id": "byDay",
          "name": "By day",
          "query": {
            "dataSets": [ { "alias": "c", "source": "MyCalls", "dataSet": "Call" } ],
            "columns": [ { "id": "day", "field": "c.StartedUtc", "transform": "Day" }, { "id": "score", "field": "c.Score", "aggregate": "Average" } ]
          }
        }
      ],
      "containers": [
        { "id": "agents", "title": "Agent scorecard", "width": 6, "height": 400 },
        { "id": "trend", "title": "Score trend", "width": 6, "height": 400 }
      ],
      "visuals": [
        {
          "id": "agentTable",
          "type": "Table",
          "containerId": "agents",
          "action": { "columnId": "agent", "targets": [ { "visualId": "trendChart" } ] }
        },
        {
          "id": "trendChart",
          "type": "Chart",
          "queryId": "byDay",
          "containerId": "trend",
          "chartType": "Line",
          "categoryColumnId": "day",
          "valueColumnIds": [ "score" ]
        }
      ],
      "OwnerUserName": "admin"
    }
  ]
}
```

## Load on the data

A click runs the queries of the visuals it filters again, with one more row filter; data sources push the filter down like any other, so an indexed data set reads only the rows that match. Keep large dashboards fast the same way as any report (see [Large data](large-data.md)): a date filter from the start, and scheduled views for queries many people run. A click on a visual that filters nothing runs nothing.
