---
sidebar_label: Reports
sidebar_position: 7
title: Reports
description: A reusable reporting framework for OrchardCore with a shared admin Reports area, extensible filters, a uniform report renderer, and pluggable exports.
user_manual:
  - user-manual/reports
  - user-manual/report-builder/index
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

| | |
| --- | --- |
| **Add-on Feature Name** | Reports (PDF) |
| **Add-on Feature ID** | `CrestApps.OrchardCore.Reports.Pdf` |

The optional **Reports (PDF)** add-on adds PDF (`.pdf`) exports, laid out like the report on screen: the report title, metric cards, tables with their headers repeated on every page and their cell colors, progress bars, and charts drawn as charts, with page numbers in the footer. Pages are US Letter, and turn to landscape when a table is too wide for a portrait page. Column widths follow the values: each column gets room for its longest word, and a word too long for its column, such as a URL, is broken to fit. It uses the MIT-licensed PDFsharp-MigraDoc 6.2.4.

PDFsharp reads the host's fonts. On Windows it uses the Windows fonts; on Linux and macOS the add-on serves the TrueType fonts in the system font folders, preferring Liberation Sans (`fonts-liberation`), which has the widths of Arial. A container image without fonts needs a font package installed. The PDF has no right-to-left shaping, so Arabic or Hebrew text is written left to right; export those reports to Excel instead.

| | |
| --- | --- |
| **Add-on Feature Name** | Report Builder |
| **Add-on Feature ID** | `CrestApps.OrchardCore.Reports.Builder` |

The optional **Report Builder** add-on lets people design their own reports with drag and drop from pluggable data sources, and share them. See [Report Builder](report-builder/index.md).

The implementation is split into three layers:

- **`CrestApps.OrchardCore.Reports.Abstractions`** defines the shared report contracts and document models, including `IReport`, `IReportRunner`, `ReportDescriptor`, `IReportExportFormat`, `IReportManager`, and `IReportExportManager`.
- **`CrestApps.OrchardCore.Reports.Core`** contains the default non-Orchard-specific implementations such as the report/export registries and the built-in CSV export formatter.
- **`CrestApps.OrchardCore.Reports`** contains Orchard-specific wiring such as the admin menu, controller, views, and the display-driver-based filter UI.

## Concepts

- **`IReport`** — a report. What describes it is static, like the data sources of Orchard Core's Data Pipelines: its technical `Name`, its `DisplayName` and `Description` (each a `LocalizationSource`), its `Category` (used to group reports in the menu) and its `Permission`. Its `RunAsync` method, from `IReportRunner`, returns a `ReportDocument` for a given `ReportContext`. `IReportManager` lists the reports from their descriptors, so listing, routing and authorizing a report never creates it; a report is created only to run.
- **`ReportContext`** — what a report runs with: the `Filter`, and the `Mode` the document is for (`ReportRunMode.Display` when it is rendered in the browser, `ReportRunMode.Export` when an export format will serialize it). A report that pages its rows on screen reads the mode to give an export every matching row.
- **`ReportFilter`** — the filter applied when a report runs. A report declares no fixed dimensions of its own: every filter is contributed with a display driver and stored in the extensible `ReportFilter.Properties` bag. The built-in tenant-local from/to date range is contributed the same way — it is just a filter — so a report that does not need a date selector can omit it. Use the `TryGet<T>`, `GetOrDefault<T>`, and `Set<T>` helpers to read and write typed values, and `GetDateRange()` / `SetDateRange()` for the resolved period.
- **`ReportDocument`** — the uniform result. It is an ordered list of **sections**, where each section is a set of metric cards, a table (with optional emphasized totals rows for aggregated reports), horizontal bars, or a responsive Chart.js line, bar, stacked-bar, or doughnut chart. The same document is rendered in the browser and serialized by every exporter; chart exports use a label-and-dataset table so the underlying values remain portable.
- **`IReportExportFormat`** — an export format, which serializes a `ReportDocument`. What describes a format is static, like the file formats of Orchard Core's Data Pipelines: its `Name`, its `DisplayName` (a `LocalizationSource`), its `Extension` (with its leading dot) and its `ContentType`, so formats are listed without being created. A module registers a format with `services.AddReportExportFormat<TFormat>()`, and `IReportExportManager` lists the descriptors of the formats and resolves one to export a report. CSV ships in the box, the optional **Reports (OpenXml)** add-on adds Excel (`.xlsx`), and the optional **Reports (PDF)** add-on adds PDF (`.pdf`).

## Reports area

How people find a report, set its date range and filters, and export it is described in the User Manual: [Reports](../user-manual/reports.md). The Contact Center reports are listed in the [Report Catalog](../contact-center/report-catalog.md).

Enabling the feature adds a top-level **Reports** item to the admin menu. Reports are alphabetized within consistently ordered role-based groups: **Executive**, **Operations**, **Queue & Routing**, **Agent Performance**, **Workforce & Payroll**, **Billing & Usage**, **CRM & Campaigns**, **Compliance & Audit**, **Technical & IT**, and **General**. Each entry is gated by the report's own permission, so a user only sees the reports they are allowed to run. Selecting a report opens a page with the filter form, the rendered document, and export actions for the current filter. A single enabled exporter renders as a normal button, while multiple enabled exporters render as an **Export** dropdown that can download CSV and, when the add-on is enabled, Excel (`.xlsx`).

## Date range filter

The built-in **Date range** filter is contributed by the `ReportDateRangeFilterDisplayDriver`, which is registered for `ReportFilter` and therefore renders for every report by default. It is an ordinary filter: it stores the resolved from/to bounds and the selected preset key in the report filter property bag (through `SetDateRange`), so a report that does not need a date selector can be built without one. The driver also applies the default period (today from `00:00:00` through `23:59:59`) and swaps the bounds when they are inverted.

Every report shares this single tenant-local **Date range** control. Instead of two separate date inputs, it is a dropdown that offers common presets grouped into **Relative days** (**Today**, **Yesterday**, **Last 7 Days**, **Last 30 Days**, **Last 90 Days**), **Calendar periods** (**This Week**, **Last Week**, **This Month**, **Last Month**, **This Quarter**, **Last Quarter**, **This Year**, **Last Year**), and **Rolling months** (**Last 3 Months**, **Last 6 Months**, **Last 12 Months**) — plus:

- **Custom Range** — two date-time inputs (from and to) editable with [Flatpickr](https://flatpickr.js.org/). When the inputs are empty, the from value defaults to today's local date at `00:00`, and the to value defaults to today's local date at `23:59`. Reports treat submitted `23:59` upper bounds as `23:59:59`.
- **On or before** — a single date-time picker that sets only the upper bound, leaving the start open. An empty picker defaults to today's local date at `23:59`; when that value is submitted, reports treat it as `23:59:59`.
- **On or after** — a single date-time picker that sets only the lower bound, leaving the end open. An empty picker defaults to today's local date at `00:00`.

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

Implement `IReport` with its static members, and register it with `AddReport`, which registers it as a scoped service and adds its descriptor to `ReportOptions`:

```csharp
public sealed class MyReport : IReport
{
    public static string Name => "my-report";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<MyReport>("My report");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<MyReport>("What my report shows.");

    public static string Category => ReportsConstants.Categories.Operations;

    public static Permission Permission => MyPermissions.ViewReports;

    public Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        // Build the document.
    }
}

services.AddReport<MyReport>();
```

Localize a descriptor's `DisplayName` and `Description` with `IStringLocalizerFactory.Localize` when you show them.

`RunAsync` builds a `ReportDocument` from the resolved period (`context.Filter.GetDateRange()`) and any report-specific filter values. Use `ReportSection.ForMetrics`, `ReportSection.ForTable`, `ReportSection.ForBars`, and `ReportSection.ForChart` to compose the document, and `ReportFormat` to format numbers, durations, and percentages consistently. Charts accept ordered labels plus one or more numeric datasets; `Width` places sections on the shared responsive twelve-column layout.

## Color-coding table cells

Report table sections can color-code their headers and cells with `ReportStyle`, which carries a font **color**, a **background color**, and a **bold** flag. Apply it in three places:

- **Header cells** — pass a `HeaderStyle` to `ReportColumn`.
- **Whole rows** — call `ReportRow.WithStyle(...)`, useful for subtotal and grand-total rows.
- **Individual cells** — call `ReportRow.WithCellStyle(index, ...)`, which overrides the row style for that one cell.

Supply colors as hexadecimal values (for example `#2563EB`) so the same style renders in the browser **and** exports to Excel and PDF. The HTML renderer also accepts simple named colors, but only hexadecimal colors are carried into the Excel workbook and the PDF.

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

When the **Reports (OpenXml)** add-on is enabled, the Excel (`.xlsx`) export applies the font color, background fill, and bold weight to the matching cells. Header rows, subtotal rows, and grand-total rows are exported bold automatically. The **Reports (PDF)** add-on applies the same styles, and shades subtotal and grand-total rows as the report on screen does. CSV has no native styling, so it always exports values only.

### Contributing a family of reports

When one class runs a whole family of reports — for example a catalog of reports that differ only by what they measure — the class can't describe each report with static members. Implement `IReportRunner` instead, and add a `ReportDescriptor` per report to `ReportOptions`, with a function that creates the report when it runs:

```csharp
private static readonly ObjectFactory<MyCatalogReport> _factory =
    ActivatorUtilities.CreateFactory<MyCatalogReport>([typeof(MyReportKind)]);

services.Configure<ReportOptions>(options => options.Add(new ReportDescriptor(
    "my-volume-report",
    LocalizationSource.Create<MyStartup>("Volume"),
    LocalizationSource.Create<MyStartup>("How much work came in, by day."),
    ReportsConstants.Categories.Operations,
    MyPermissions.ViewReports,
    typeof(MyCatalogReport),
    services => _factory(services, [MyReportKind.Volume]))));
```

`IReportManager` enforces unique report names across every descriptor. Because a family is described through options, another feature can add or remove reports of the family without editing the module that registers them. The Contact Center module describes its enterprise interaction and agent workforce report catalogs this way, and Omnichannel its enterprise activity reports.

## Report subscriptions

The **Report Subscriptions** feature (`CrestApps.OrchardCore.Reports.Subscriptions`, needs Email) emails a registered report on a schedule. `ReportSubscription` (`ReportSubscriptionIndex`) has the report's `ReportName`, a `Frequency` (`Daily`, `Weekly` on `DayOfWeek`, `Monthly` on `DayOfMonth` 1 to 28), the `Hour` in the site's time zone, the `Recipients`, an optional `AttachmentFormat` (an export format such as `csv` or `xlsx`), `IsEnabled`, and `NextRunUtc`, `LastRunUtc` and `LastError`.

- `ReportSubscriptionSchedule.GetNextRunUtc` and `GetPeriod` work in the site's time zone. A run covers the day, the seven days, or the calendar month before it, from midnight to midnight.
- `ReportSubscriptionsBackgroundTask` (every five minutes) sends the subscriptions that are due. A late run covers the period it was due for. `ReportSubscriptionService.SendAsync` runs the report with that date range, resolves its display values, and emails its metrics and tables (`ReportWorkflowService.ToHtml`), with the export attached when asked. It records `LastError` when the email or the report fails.
- **Reports > Subscriptions** (`ReportSubscriptionsController`, `Reports/subscriptions`, the `AdminList`) needs **Manage report subscriptions**, and lists only the reports the person can see. **Save and send now** sends one at once without moving its schedule.

## Dashboard widgets

With Orchard Core's **Admin Dashboard** enabled next to **Reports**, any report can be shown on the admin dashboard. There is no feature of its own to turn on: the widgets appear when both are enabled. Add a widget from **Manage Dashboard > Add Widget** and set:

- **Report**: any report the person adding it may view. A ready-made widget fixes the report, so only the options below are set.
- **Period**: today, yesterday, the last 7, 30 or 90 days, this month, last month, this year, the last 12 months, all time, or custom dates. Every period except custom dates moves with the clock, so "last 30 days" is always the 30 days up to now, in the site's time zone. This is the date range filter of the report.
- **Show**: figures and tables (the default), figures only, tables only, charts only, or everything. Charts are left out of the default because they need room.
- **Rows per table**: how many rows of each table, and bars of each bar list, to show. A total row stays.
- **Keep for (minutes)**: how long the widget keeps what it shows before it runs the report again. The default is 30 minutes, so the dashboard stays quick however often it opens. 0 runs the report on every visit. Saving the widget starts over.

Each widget keeps its result in the tenant's memory cache, per widget and language. A person sees a widget only when they may view its report: the kept result is checked against the report's permission on every visit. A report that fails shows a message in its own tile and is not kept, so one broken report does not stop the dashboard.

The same part, `ReportWidgetPart`, is on every widget below. A module ships a ready-made widget with `IContentDefinitionManager.DefineReportWidgetAsync`, which creates a `DashboardWidget` type fixed to a report with defaults for the period, what to show and the rows. A module adds its widgets in a startup class marked `[RequireFeatures(ReportsConstants.Feature, ReportWidgetConstants.AdminDashboardFeature)]`, so they exist only when both features are on.

| Widget | Report | Default |
| --- | --- | --- |
| Report | Any report | Last 30 days, figures and tables |
| Sales Overview | `order-sales` | Last 30 days, figures |
| Sales Trend | `order-sales` | Last 30 days, chart |
| Top Selling Products | `order-product-sales` | Last 30 days, top 5 |
| Sales by Category | `order-category-sales` | Last 30 days, top 5 |
| Sales by Country | `order-country-sales` | Last 30 days, 5 rows |
| Orders to Ship | `unfulfilled-orders` | All time, oldest 5 |
| Customer Insights | `order-customers` | Last 30 days, figures and 5 buyers |
| Checkout Conversion | `checkout-conversion` | Last 30 days, figures |
| Refunds | `order-refunds` | Last 30 days, figures and 5 refunds |
| Payment Disputes | `order-disputes` | Last 90 days, open ones first |
| Discount Codes Used | `order-discounts` | Last 30 days, 5 codes |
| Tax Collected | `order-tax` | This month, figures |
| Low Stock | `stock-on-hand` | All time, the 5 items with the least stock |
| Stock Movements | `stock-movements` | Last 30 days |
| Product Reviews | `product-reviews` | Last 30 days |
| Shipping Times | `shipping-performance` | Last 30 days |
| Digital Downloads | `digital-downloads` | Last 30 days |
| Subscriptions Overview | `subscriptions-dashboard` | Last 30 days, figures |
| Subscription Revenue | `subscription-revenue` | Last 30 days, figures |
| Expiring Subscriptions | `expiring-subscriptions` | All time, table |
| New Subscriptions Trend | `new-subscriptions-trend` | Last 12 months, chart |
| Subscription Products | `product-performance` | Last 30 days |

The **Sales Summary** and **Subscription Summary** widgets are not report widgets. They count the store's orders or subscriptions directly and have their own options; see [Sales Summary widget](carts-and-orders#sales-summary-widget) and [Subscriptions](subscriptions).

### Report Builder widget

With the **Report Builder** (`CrestApps.OrchardCore.Reports.Builder`) and Orchard Core's **Admin Dashboard** both enabled, the **Custom Report** widget (`ReportDesignWidget`, part `ReportDesignWidgetPart`, both in `ReportWidgetConstants`) shows a published report built in the report builder, or a reusable view as a table, with the same options as the report widget. It is registered by `DesignWidgetsStartup`, which belongs to the Report Builder feature and is marked `[RequireFeatures(ReportWidgetConstants.AdminDashboardFeature)]`; its migration creates the part and the `DashboardWidget` type. A store owner can build a report and put it on the dashboard without a developer.

- **Report**: `ReportId` (a published `ReportDesign`) or `ViewId` (a `ReportView`), never both. The editor lists the published reports the editor may run and the views the editor may use, and refuses any other. A report that was never published is not in the list, because only published reports are in the catalog.
- **Period**: the same choices as the report widget, resolved by `ReportWidgetPeriods` in the site's time zone. It is passed to `ReportDesignRunner.RunAsync` as a `ReportRunPeriod` of whole days and set on the report's first exposed filter whose control is a date range (`ReportDesignRunner.FindPeriodFilter`), overriding that filter's default. **All time** turns the filter off. A report with no such filter keeps its own dates, `ReportDesignWidgetResult.PeriodApplied` is `false`, and the tile says **Its own dates**.
- **Show**: `ReportWidgetContent` applied to the report's visuals through `ReportWidgetTrimmer`: figures are the headline-number visuals, tables are the table and pivot visuals, charts are the chart visuals. The default is everything, since the person who built the report chose its visuals. A view is always shown as one table.
- **Rows per table**: 10 by default; a total row stays. **Keep for (minutes)**: `ReportWidgetConstants.DefaultCacheMinutes` (30) by default; 0 runs the report on every visit.

`ReportDesignWidgetService` checks `ReportDesignerPermissions.ViewAllReportDesigns` against the report or view on every visit, before it reads the cache: the same check as the report's own page, so a person sees the tile's numbers only when they may run the report (an owner, a person or role it is shared with, or *View all custom reports*), and a view only when they build reports. A report reads its data with its owner's access, whoever views it, so its kept result is keyed by the widget and its last change, the report and its last publish, the owner, the period, what to show, the rows and the language, and is shared by everyone who may run the report. A view reads with the viewer's access, so its result is also keyed by the viewer. A report whose run fails (for example its owner's account is disabled) shows the same errors as its page and is not kept; a report or view that was deleted, or never published, says so. The widget runs the report live and does not raise the report-run workflow event; a view runs live with the viewer's access and does not read its scheduled snapshot.

The widget renders the report's document with the same `_ReportWidgetSections` partial as the report widget, and its editor uses the same `_ReportWidgetOptions` partial and `ReportWidgetChoices`.

## Enable via recipe

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
      "CrestApps.OrchardCore.Reports",
      "CrestApps.OrchardCore.Reports.OpenXml",
      "CrestApps.OrchardCore.Reports.Pdf"
      ]
    }
  ]
}
```

Enable `CrestApps.OrchardCore.Reports.OpenXml` only when you want Excel workbook exports, and `CrestApps.OrchardCore.Reports.Pdf` only when you want PDF exports. Enabled modules such as **Omnichannel Management** and **Phone Number Verifications** contribute their reports automatically once `CrestApps.OrchardCore.Reports` is enabled.
