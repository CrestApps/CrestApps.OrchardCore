---
sidebar_label: Reports
sidebar_position: 7
title: Reports
description: A reusable reporting framework for OrchardCore with a shared admin Reports area, extensible filters, a uniform report renderer, and pluggable exports.
---

| | |
| --- | --- |
| **Feature Name** | Reports |
| **Feature ID** | `CrestApps.OrchardCore.Reports` |
| **Dependency** | `CrestApps.OrchardCore.Resources` |

The **Reports** module is a reusable reporting framework. It provides a single admin **Reports** area and a small contract that any module can implement to surface an industry-standard report — with a shared from/to date-range filter, extensible filters, a uniform renderer (metric cards, tables, bars, and interactive charts), and pluggable exports (CSV built in). The feature depends on **CrestApps Resources** and loads its named `chart.js` resource only when the current report contains a chart section. Modules such as [Omnichannel](../omnichannel/index.md) and [Phone Number Verifications](phone-number-verifications) contribute their reports through this framework so every report looks and behaves the same.

| | |
| --- | --- |
| **Add-on Feature Name** | Reports (OpenXml) |
| **Add-on Feature ID** | `CrestApps.OrchardCore.Reports.OpenXml` |

The optional **Reports (OpenXml)** add-on extends the Reports area with Excel workbook (`.xlsx`) exports using the `DocumentFormat.OpenXml` library. When the add-on is enabled alongside other exporters, report pages collapse those formats into a single **Export** dropdown so operators can choose the file type they want.

The implementation is split into three layers:

- **`CrestApps.OrchardCore.Reports.Abstractions`** defines the shared report contracts and document models, including `IReport`, `IReportExportFormat`, `IReportManager`, and `IReportExportManager`.
- **`CrestApps.OrchardCore.Reports.Core`** contains the default non-Orchard-specific implementations such as the report/export registries and the built-in CSV export formatter.
- **`CrestApps.OrchardCore.Reports`** contains Orchard-specific wiring such as the admin menu, controller, views, and the display-driver-based filter UI.

## Concepts

- **`IReport`** — a report definition. It declares a technical `Name`, a `DisplayName`, a `Description`, a `Category` (used to group reports in the menu), a `Permission`, and a `RunAsync` method that returns a `ReportDocument` for a given `ReportContext`.
- **`ReportFilter`** — the filter applied when a report runs. A report declares no fixed dimensions of its own: every filter is contributed with a display driver and stored in the extensible `ReportFilter.Properties` bag. The built-in tenant-local from/to date range is contributed the same way — it is just a filter — so a report that does not need a date selector can omit it. Use the `TryGet<T>`, `GetOrDefault<T>`, and `Set<T>` helpers to read and write typed values, and `GetDateRange()` / `SetDateRange()` for the resolved period.
- **`ReportDocument`** — the uniform result. It is an ordered list of **sections**, where each section is a set of metric cards, a table (with optional emphasized totals rows for aggregated reports), horizontal bars, or a responsive Chart.js line, bar, stacked-bar, or doughnut chart. The same document is rendered in the browser and serialized by every exporter; chart exports use a label-and-dataset table so the underlying values remain portable.
- **`IReportExportFormat`** — an export format. CSV ships in the box; the optional **Reports (OpenXml)** add-on adds Excel (`.xlsx`); and any module can add more formats by registering another implementation.

## Reports area

Enabling the feature adds a top-level **Reports** item to the admin menu. Reports are alphabetized within consistently ordered role-based groups: **Executive**, **Operations**, **Queue & Routing**, **Agent Performance**, **Workforce & Payroll**, **Billing & Usage**, **CRM & Campaigns**, **Compliance & Audit**, **Technical & IT**, and **General**. Each entry is gated by the report's own permission, so a user only sees the reports they are allowed to run. Selecting a report opens a page with the filter form, the rendered document, and export actions for the current filter. A single enabled exporter renders as a normal button, while multiple enabled exporters render as an **Export** dropdown that can download CSV and, when the add-on is enabled, Excel (`.xlsx`).

## Date range filter

The built-in **Date range** filter is contributed by the `ReportDateRangeFilterDisplayDriver`, which is registered for `ReportFilter` and therefore renders for every report by default. It is an ordinary filter: it stores the resolved from/to bounds and the selected preset key in the report filter property bag (through `SetDateRange`), so a report that does not need a date selector can be built without one. The driver also applies the default period (today from `00:00:00` through `23:59:59`) and swaps the bounds when they are inverted.

Every report shares this single tenant-local **Date range** control. Instead of two separate date inputs, it is a dropdown that offers common presets grouped into **Relative days** (**Today**, **Yesterday**, **Last 7 Days**, **Last 30 Days**, **Last 90 Days**), **Calendar periods** (**This Week**, **Last Week**, **This Month**, **Last Month**, **This Quarter**, **Last Quarter**, **This Year**, **Last Year**), and **Rolling months** (**Last 3 Months**, **Last 6 Months**, **Last 12 Months**) — plus:

- **Custom Range** — two date-time inputs (from and to) editable with [Flatpickr](https://flatpickr.js.org/). When the inputs are empty, the from value defaults to today's local date at `00:00:00`, and the to value defaults to today's local date at `23:59:59`.
- **On or before** — a single date-time picker that sets only the upper bound, leaving the start open. An empty picker defaults to today's local date at `23:59:59`.
- **On or after** — a single date-time picker that sets only the lower bound, leaving the end open. An empty picker defaults to today's local date at `00:00:00`.

The dropdown button always shows the current selection as readable text (for example, _From Jan 1, 2026 to Jan 31, 2026_, _On or before Jan 31, 2026_, or a preset name followed by its resolved range). Presets are computed in the browser using the current culture's first day of the week, and the selected range is written into the underlying from/to fields, which are converted to UTC before the report runs. The control is rendered by the reusable `DateRangePicker` view component (backed by the `date-range-picker` resource) provided by the **CrestApps Resources** feature, so every report presents the same date-range experience and any module can reuse it. See the [Resources](resources.md#date-range-picker) documentation for details.

## Extensible filters

Every filter — including the built-in date range — is contributed with a display driver for `ReportFilter`, so a report gets only the filters it needs. To add a report-specific filter (for example a queue, campaign, or channel selector), register a display driver for `ReportFilter` and gate it to your report by checking `filter.ReportName`:

```csharp
public sealed class MyQueueFilterDisplayDriver : DisplayDriver<ReportFilter>
{
    public override IDisplayResult Edit(ReportFilter filter, BuildEditorContext context)
    {
        if (!string.Equals(filter.ReportName, "my-report", StringComparison.Ordinal))
        {
            return null;
        }

        return Initialize<MyQueueFilterViewModel>("MyQueueFilter_Edit", model =>
        {
            model.QueueId = filter.GetOrDefault<string>("QueueId");
        }).Location("Content:2");
    }

    public override async Task<IDisplayResult> UpdateAsync(ReportFilter filter, UpdateEditorContext context)
    {
        if (!string.Equals(filter.ReportName, "my-report", StringComparison.Ordinal))
        {
            return null;
        }

        var model = new MyQueueFilterViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);
        filter.Set("QueueId", model.QueueId);

        return Edit(filter, context);
    }
}
```

Read and write filter values with the typed `ReportFilter` helpers instead of touching the property bag directly:

- `bool TryGet<T>(string key, out T value)` — reads a value when present (also parses enums stored as strings).
- `T GetOrDefault<T>(string key)` — reads a value or returns the default when absent.
- `void Set<T>(string key, T value)` — writes a value, or removes the key when the value is `null` or an empty string.
- `ReportDateRange GetDateRange()` / `void SetDateRange(ReportDateRange range)` — read or write the built-in date-range filter. The date range is stored like any other filter, so read it from `context.Filter.GetDateRange()` when the report runs.

The report reads the bound values when it runs. Because browser display and export use the same filter-building path, custom filter values are applied consistently in both outputs.

## Contributing a report

Implement `IReport` and register it as a scoped service:

```csharp
services.AddScoped<IReport, MyReport>();
```

`RunAsync` builds a `ReportDocument` from the resolved period (`context.Filter.GetDateRange()`) and any report-specific filter values. Use `ReportSection.ForMetrics`, `ReportSection.ForTable`, `ReportSection.ForBars`, and `ReportSection.ForChart` to compose the document, and `ReportFormat` to format numbers, durations, and percentages consistently. Charts accept ordered labels plus one or more numeric datasets; `Width` places sections on the shared responsive twelve-column layout.

## Color-coding table cells

Report table sections can color-code their headers and cells with `ReportStyle`, which carries a font **color**, a **background color**, and a **bold** flag. Apply it in three places:

- **Header cells** — pass a `HeaderStyle` to `ReportColumn`.
- **Whole rows** — call `ReportRow.WithStyle(...)`, useful for subtotal and grand-total rows.
- **Individual cells** — call `ReportRow.WithCellStyle(index, ...)`, which overrides the row style for that one cell.

Supply colors as hexadecimal values (for example `#2563EB`) so the same style renders in the browser **and** exports to Excel. The HTML renderer also accepts simple named colors, but only hexadecimal colors are carried into the Excel workbook.

```csharp
var columns = new[]
{
    new ReportColumn(S["Queue"].Value, ReportColumnAlign.Start, ReportStyle.Create("#FFFFFF", "#2563EB", bold: true)),
    new ReportColumn(S["Completed"].Value, ReportColumnAlign.End),
};

var rows = new List<ReportRow>
{
    new ReportRow(["Support", "18"]).WithCellStyle(1, ReportStyle.Create("#B91C1C")),
    new ReportRow(["All queues", "18"], ReportRowKind.GrandTotal).WithStyle(ReportStyle.Create(backgroundColor: "#EFF6FF")),
};

return new ReportDocument()
    .Add(ReportSection.ForTable(S["Queues"].Value, columns, rows));
```

When the **Reports (OpenXml)** add-on is enabled, the Excel (`.xlsx`) export applies the font color, background fill, and bold weight to the matching cells. Header rows, subtotal rows, and grand-total rows are exported bold automatically. CSV has no native styling, so it always exports values only.

## Enable via recipe

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
      "CrestApps.OrchardCore.Reports",
      "CrestApps.OrchardCore.Reports.OpenXml"
      ]
    }
  ]
}
```

Enable `CrestApps.OrchardCore.Reports.OpenXml` only when you want Excel workbook exports. Enabled modules such as **Omnichannel Management** and **Phone Number Verifications** contribute their reports automatically once `CrestApps.OrchardCore.Reports` is enabled.
