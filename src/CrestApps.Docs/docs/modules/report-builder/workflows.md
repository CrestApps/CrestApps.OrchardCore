---
sidebar_label: Workflows and Report Events
title: Report Builder Workflows and Report Events
description: The report workflow events and tasks, their inputs and outputs, and the IReportEventHandler extension point for reacting to report runs, exports and publishes in code.
user_manual:
  - user-manual/report-builder/workflows
---

<YouTube video="report-builder-workflows" title="Video overview of Report Builder workflows" />

When the `OrchardCore.Workflows` feature is on, the Report Builder adds workflow events raised when a designed report is run, exported or published, and tasks that run, export, email and share reports. The same moments are available to code through `IReportEventHandler`, without Workflows.

## Registration

| Startup | Requires | Registers |
| --- | --- | --- |
| `ReportsWorkflowsStartup` | Report Builder, `OrchardCore.Workflows` | `ReportRunEvent`, `ReportExportedEvent`, `ReportPublishedEvent`, `RunReportTask`, `ExportReportTask`, `CreateReportShareLinkTask`, and the `IReportEventHandler` that triggers the events. |
| `ReportsEmailWorkflowsStartup` | Report Builder, `OrchardCore.Workflows`, `OrchardCore.Email` | `EmailReportTask`. |

`ExportReportTask` resolves `IMediaFileStore` at run time, so the Media feature is needed only when a media folder is set.

## Events

All three events are in the **Reports** category and share one setting, `ReportIds`: the reports that start the workflow, or empty for every report. Each is triggered with the report's id as the correlation id, and has one outcome, `Done`.

| Event | Raised when | Raised from |
| --- | --- | --- |
| `ReportRunEvent` | A report is run. | The report's admin page (`Source` = `Admin`), the shared reports page (`Shared`) or a share link (`Link`). |
| `ReportExportedEvent` | A report is exported. | The same three places. |
| `ReportPublishedEvent` | A version of a report is published. | `ReportDesignHistoryService.PublishAsync`. |

The workflow input holds `ReportId`, `ReportTitle`, `UserName` (null for an anonymous visitor), `Source`, `FilterValues` (the values by filter id), and `RowCount` (run), `Format` (export) or `VersionNumber` (publish).

Reports run by the report tasks do not raise events, so a workflow started by `ReportRunEvent` can run the same report without starting itself again.

## Tasks

Every task finds its report from `ReportId`, or from `ReportIdExpression` (Liquid) when it is set, and takes the report's filter values from `Filters`: one `filter id = value` per line, after Liquid, with a filter named on several lines getting every value. Filters not named keep their defaults. A report that cannot be found or is not published takes the `Failed` outcome.

The tasks run the report with its owner's access through the same runner as a shared report, so a workflow cannot read more than the report's owner can.

| Task | Outcomes | Output (default name) |
| --- | --- | --- |
| `RunReportTask` | `Done`, `No rows`, `Failed` | `Report`: `ReportId`, `Title`, `RowCount`, `Columns` (labels), `Rows` (at most 1,000, each a dictionary by column label) and `Errors`. |
| `ExportReportTask` | `Done`, `Failed` | `ReportFile`: `ReportId`, `Title`, `FileName`, `ContentType`, `Format`, `RowCount`, `Size`, and either `Path` and `Url` (with a `MediaFolder`) or `Base64`. |
| `EmailReportTask` | `Done`, `Failed` | None. Sends `To`, `Cc`, `Bcc`, `Subject` (the title when empty) and `Body` (HTML), all Liquid, with the report's metrics and tables rendered as inline-styled HTML when `IncludeReport` is set, and the export in `AttachmentFormat` attached unless it is empty. The report runs once for both. |
| `CreateReportShareLinkTask` | `Done`, `Failed` | `ReportShareLink`: `ReportId`, `Title`, `Url`, `LinkId`, `ExpiresUtc`, `Recipients` (the recipients' user names) and `Notified` (the recipients the link was emailed to). Takes `Recipients` (Liquid: user names or emails of users of the site, separated by commas), `Note` (Liquid), `ExpiresAfterDays` (at least 1, 7 by default), `MaxUses` (`0` for no limit), `AllowExport` and `NotifyByEmail` (`true` by default). |

The output name can be changed on each task with `OutputName`. `Workflow.LastResult` is set to the output of `RunReportTask` and `ExportReportTask`, to the link's URL by `CreateReportShareLinkTask`, and to the report's id by `EmailReportTask`.

`CreateReportShareLinkTask` creates an Orchard Core shared link with `ISharedLinkManager`, like the report's **Sharing** tab (see [Share links](permissions.md#share-links)): only its recipients can open it, once signed in, until it expires, is used up or is revoked. With `NotifyByEmail`, Orchard Core emails the link and the note to the recipients; turn it off to send `Url` with another activity. The task takes `Failed` when there is no report, when there is no recipient or a recipient is not a user of the site, or when `NotifyByEmail` is set while the Email feature is off; the reason is logged.

The URL is built from the site's base URL, or from the current request when the base URL is not set. Set the base URL for workflows started by a timer, which have no request. Links created by a workflow can be revoked on the report's **Sharing** tab or under **Tools > Shared Links**.

## React in code with `IReportEventHandler`

Implement `IReportEventHandler`, or derive from `ReportEventHandlerBase` and override only what you need, and register it as scoped:

```csharp
public sealed class ExportAuditHandler : ReportEventHandlerBase
{
    private readonly ILogger _logger;

    public ExportAuditHandler(ILogger<ExportAuditHandler> logger)
    {
        _logger = logger;
    }

    public override Task ExportedAsync(ReportEventContext context)
    {
        _logger.LogInformation("{User} exported '{Report}' as {Format} from {Source}.",
            context.UserName, context.Design.DisplayText, context.Format, context.Source);

        return Task.CompletedTask;
    }
}

services.AddScoped<IReportEventHandler, ExportAuditHandler>();
```

`ReportEventContext` carries the `Design`, `UserName`, `Source`, `FilterValues`, and `RowCount`, `Format` or `VersionNumber`. Handlers are called after the report has run, been exported or been published, and an exception thrown by one handler is logged without failing the request or stopping the other handlers.

## Recipes

Report workflows are ordinary workflow types, exported and imported with the `WorkflowType` recipe step. Activities name reports by id, so import the reports with the [`ReportDesigns` step](recipes-and-deployment.md) first, with the same ids.

```json
{
  "ActivityId": "email",
  "Name": "EmailReportTask",
  "Properties": {
    "ReportId": "4z1f5b0c6t0m9z2k8w3d7r5q1v",
    "To": "manager@example.com",
    "AttachmentFormat": "csv",
    "IncludeReport": true
  }
}
```
