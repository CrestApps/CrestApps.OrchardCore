# Reports

Provides a reusable reporting framework with a shared admin Reports area, extensible filters, and exports.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Reports | `CrestApps.OrchardCore.Reports` | Adds the admin Reports area, the extensible report filter with a from/to date range, the uniform report renderer, and CSV export. Other modules contribute reports to this area. |
| Report Subscriptions | `CrestApps.OrchardCore.Reports.Subscriptions` | Emails reports on a schedule: a report every day, week or month for the period before, such as the sales report every Monday, with its numbers and tables and the report as a file. |
| Report Builder | `CrestApps.OrchardCore.Reports.Builder` | Lets people build their own reports with drag and drop: pick data sets from any data source, join them, add calculated fields, filters, charts, and pivot tables, save reusable views, pin reports to the admin menu, and share reports with people, roles, or links. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.Resources`](../CrestApps.OrchardCore.Resources/README.md)
- `OrchardCore.Users`
- `OrchardCore.Email`
- `OrchardCore.DataPipelines.Core`

## Extension points

- `IReport` (CrestApps.OrchardCore.Reports.Abstractions): A report surfaced under the admin Reports area.
- `IReportExportFormat` (CrestApps.OrchardCore.Reports.Abstractions): Defines an export format for a report document, such as CSV, Excel, or PDF.
- `IReportFilterMetadata` (CrestApps.OrchardCore.Reports.Abstractions): Describes the named filter dimensions supported by a report.
- `IReportRunner` (CrestApps.OrchardCore.Reports.Abstractions): Runs a report: builds its document for a filter.
- `IReportExportManager` (CrestApps.OrchardCore.Reports.Abstractions): Lists the registered report export formats from their descriptors, and resolves a format to export a report.
- and 1 more contracts in the same projects.

## Documentation

See [Reports](../../CrestApps.Docs/docs/modules/reports.md) in the technical manual.
