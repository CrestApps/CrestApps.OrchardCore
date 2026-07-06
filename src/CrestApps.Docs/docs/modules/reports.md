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

The **Reports** module is a reusable reporting framework. It provides a single admin **Reports** area and a small contract that any module can implement to surface an industry-standard report — with a shared from/to date-range filter, extensible filters, a uniform renderer (metric cards, tables, and bars), and pluggable exports (CSV built in). Modules such as the [Contact Center](../contact-center/index.md) and [Omnichannel](../omnichannel/index.md) CRM contribute their reports through this framework so every report looks and behaves the same.

| | |
| --- | --- |
| **Add-on Feature Name** | Reports (OpenXml) |
| **Add-on Feature ID** | `CrestApps.OrchardCore.Reports.OpenXml` |

The optional **Reports (OpenXml)** add-on extends the Reports area with Excel workbook (`.xlsx`) exports using the `DocumentFormat.OpenXml` library. When the add-on is enabled, report pages show an additional export button alongside CSV.

The implementation is split into three layers:

- **`CrestApps.OrchardCore.Reports.Abstractions`** defines the shared report contracts and document models, including `IReport`, `IReportExportFormat`, `IReportManager`, and `IReportExportManager`.
- **`CrestApps.OrchardCore.Reports.Core`** contains the default non-Orchard-specific implementations such as the report/export registries and the built-in CSV export formatter.
- **`CrestApps.OrchardCore.Reports`** contains Orchard-specific wiring such as the admin menu, controller, views, and the display-driver-based filter UI.

## Concepts

- **`IReport`** — a report definition. It declares a technical `Name`, a `DisplayName`, a `Description`, a `Category` (used to group reports in the menu), a `Permission`, and a `RunAsync` method that returns a `ReportDocument` for a given `ReportContext`.
- **`ReportFilter`** — the filter applied when a report runs. Every report shares the built-in from/to date range; additional, report-specific filters are contributed with display drivers.
- **`ReportDocument`** — the uniform result. It is an ordered list of **sections**, where each section is a set of metric cards, a table (with optional emphasized totals rows for aggregated reports), or a set of horizontal bars. The same document is rendered in the browser and serialized by every exporter.
- **`IReportExportFormat`** — an export format. CSV ships in the box; the optional **Reports (OpenXml)** add-on adds Excel (`.xlsx`); and any module can add more formats by registering another implementation.

## Reports area

Enabling the feature adds a top-level **Reports** item to the admin menu. Reports are grouped by their category, and each entry is gated by the report's own permission, so a user only sees the reports they are allowed to run. Selecting a report opens a page with the filter form, the rendered document, and one export button per enabled format so the current filter can be downloaded as CSV and, when the add-on is enabled, Excel (`.xlsx`).

## Extensible filters

Every report automatically gets the from/to date range. To add a report-specific filter (for example a queue, campaign, or channel selector), register a display driver for `ReportFilter` and gate it to your report by checking `filter.ReportName`:

```csharp
public sealed class MyQueueFilterDisplayDriver : DisplayDriver<ReportFilter>
{
    public override IDisplayResult Edit(ReportFilter filter, BuildEditorContext context)
    {
        if (!string.Equals(filter.ReportName, "my-report", StringComparison.Ordinal))
        {
            return null;
        }

        return Initialize<MyQueueFilterViewModel>("MyQueueFilter_Edit", model => { /* ... */ })
            .Location("Content:2");
    }

    public override async Task<IDisplayResult> UpdateAsync(ReportFilter filter, UpdateEditorContext context)
    {
        if (!string.Equals(filter.ReportName, "my-report", StringComparison.Ordinal))
        {
            return null;
        }

        var model = new MyQueueFilterViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);
        filter.Properties["QueueId"] = model.QueueId;

        return Edit(filter, context);
    }
}
```

The report reads the bound value from `context.Filter.Properties` when it runs.

## Contributing a report

Implement `IReport` and register it as a scoped service:

```csharp
services.AddScoped<IReport, MyReport>();
```

`RunAsync` builds a `ReportDocument` from the resolved period (`context.FromUtc` / `context.ToUtc`) and any report-specific filter values. Use `ReportSection.ForMetrics`, `ReportSection.ForTable`, and `ReportSection.ForBars` to compose the document, and `ReportFormat` to format numbers, durations, and percentages consistently.

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

Enable `CrestApps.OrchardCore.Reports.OpenXml` only when you want Excel workbook exports. The base Reports feature is enabled automatically when you enable a feature that contributes reports, such as **Contact Center Reports & Analytics** or **Omnichannel Reports**.
