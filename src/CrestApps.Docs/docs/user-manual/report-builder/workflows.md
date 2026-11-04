---
sidebar_label: Automate with Workflows
title: Automate Reports with Workflows
description: Run, export and email reports on a schedule, send share links, and react when a report is run, exported or published.
technical_manual:
  - modules/report-builder/workflows
---

Workflows let a report work for you without anyone opening it. Email a report to your manager every morning, save an Excel copy to the media library each week, send someone a link to a report, or do something every time a report is run, exported or published.

| | |
| --- | --- |
| **Menu** | Design > Workflows |
| **Permission** | Manage workflows |
| **Feature** | Report Builder, with Workflows. Emailing needs Email, with a configured provider. Saving files needs Media. |

<AskYourAdmin />

<YouTube video="report-builder-workflows" title="Video overview of Report Builder workflows" />

## What you can use

<YouTube video="report-builder-workflows" title="Video of the report events and the report tasks" start={56} end={132} />

The report activities are in the **Reports** category of the activity picker.

| Activity | Kind | What it does |
| --- | --- | --- |
| **Report Run** | Event | Starts the workflow when someone runs a report: on its page, on the shared reports page, or through a share link. |
| **Report Exported** | Event | Starts the workflow when someone exports a report. |
| **Report Published** | Event | Starts the workflow when someone publishes a report. |
| **Run Report** | Task | Runs a report and keeps its rows for the next activities. |
| **Export Report** | Task | Exports a report to CSV, to Excel when the Reports (OpenXml) feature is on, or to PDF when the Reports (PDF) feature is on, and saves the file in the media library. |
| **Email Report** | Task | Emails a report: your message, the report's headline numbers and tables, and the report attached as a file. |
| **Create Report Share Link** | Task | Creates a share link to a report for users of the site, and emails it to them. |

Each event can be limited to some reports. Leave **Reports** empty to start the workflow for every report.

:::warning[Reports run with their owner's access]
A report run by a workflow reads data with its **owner's** access, exactly like a shared report. An emailed report or a share link shows its recipients what the owner can see. Send reports only to people who should see that data.
:::

## Choose the report

Every report task has the same first fields:

| Field | What it does |
| --- | --- |
| **Report** | The published report to use. |
| **Report ID expression** | Instead of choosing a report, a Liquid expression that gives its ID. After a report event, `{{ Workflow.Input.ReportId }}` is the report that was run, exported or published. |
| **Filters** | One filter per line, as *filter ID* = *value*. Repeat a filter for several values. A recent period takes a number of days, such as `created = 7`. Filters you leave out use the report's defaults. Values can use Liquid. |
| **Output name** | The name the task stores its result under, read by the next activities in Liquid. |

To find a filter's ID, run the report, set the filter and click **Show**: the address of the page holds `f.` and the ID, such as `f.created=7` for the filter ID `created`.

## Email a report every morning

<YouTube video="report-builder-workflows" title="Video of the Email Report task" start={181} end={211} />

This is the way to email a report you built in the Report Builder. **Report Subscriptions** (see [Reports](../reports.md#email-a-report-on-a-schedule)) cover the reports under **Reports** that modules provide, and cannot be used for a Report Builder report.

1. Go to **Design > Workflows** and click **Create Workflow**. Name it, for example *Daily sales*, and save.
2. Add the **Timer Event** from the **Background** category as the start activity. Set its schedule, such as `0 7 * * *` for 7:00 every day.
3. Add **Email Report**. Choose the report, and enter the recipients in **To**. Leave **Subject** empty to use the report's title.
4. Write a **Message** if you want one. Keep **Attachment** on **CSV**, or choose **Excel (.xlsx)** or **PDF** (each when its feature is on), or **No attachment** to send only the tables.
5. Connect **Done** of the timer to the email task, and save the workflow.

The recipients get your message, the report's headline numbers and tables, and the file attached. Charts are left out of the email; they are in the attached file's data and in the report itself.

## Email someone a link to a report

<YouTube video="report-builder-workflows" title="Video of publishing a report and the workflow that shares a link" start={132} end={157} />

A link is better than an attachment when the report is large or the reader wants to change its filters. A link is for users of the site: they open it once signed in.

1. Add **Create Report Share Link** and choose the report.
2. Fill in its settings:

| Setting | What it does |
| --- | --- |
| **Recipients** | The user names or emails of the users the link is for, separated by commas. It can use Liquid. |
| **Note** | Optional. What the link is for, sent with the email and shown on the report's **Sharing** tab. It can use Liquid. |
| **Expires after (days)** | How many days the link works, at least 1. It starts at 7. Every link expires. |
| **Most uses** | How many times the link can be used, or `0` for no limit. Opening the report, changing its filters and exporting it are each a use. |
| **Allow export** | Lets the recipients download the report. |
| **Email the link to the recipients** | On at first: the task emails the link and the note to each recipient. It needs the Email feature. Turn it off to send the link with another task, such as Orchard Core's **Email** task, with `{{ Workflow.Output.ReportShareLink.Url }}` in the message. |

3. Connect the outcomes: **Done** when the link was created, **Failed** otherwise.

The task keeps the link for the next activities, read in Liquid as `{{ Workflow.Output.ReportShareLink.Url }}` (or the **Output name** you entered):

| Value | What it holds |
| --- | --- |
| `Url` | The link. |
| `LinkId` | The link's ID. |
| `ExpiresUtc` | When the link stops working. |
| `Recipients` | The user names of the recipients. |
| `Notified` | The recipients the email was sent to. |

The link appears on the report's **Sharing** tab with your note, and under **Tools > Shared Links**, where you can revoke it like any other link. Set the site's base URL under **Settings > General** so links created on a timer are complete.

## React when a report is run

Start a workflow with **Report Run**, **Report Exported** or **Report Published** to do something when people use a report. For example, email the owner when a report is published, or keep a log of who exports a report.

The event gives the workflow these values, read in Liquid as `{{ Workflow.Input.ReportTitle }}`:

| Value | What it holds |
| --- | --- |
| `ReportId` | The ID of the report. |
| `ReportTitle` | The title of the report. |
| `UserName` | Who ran, exported or published it. Empty for a visitor who is not signed in. |
| `Source` | Where it was run or exported: `Admin` (the report's page), `Shared` (the shared reports page) or `Link` (a share link). |
| `RowCount` | **Report Run** only: the number of rows. |
| `Format` | **Report Exported** only: `csv`, `xlsx` or `pdf`. |
| `VersionNumber` | **Report Published** only: the version that was published. |
| `FilterValues` | The filters that were used. |

Runs made by the report tasks of a workflow do not start these events, so a workflow that runs a report after **Report Run** does not start itself again.

## Use a report's rows

**Run Report** keeps the report's result for the next activities, read in Liquid as `{{ Workflow.Output.Report.RowCount }}` (or the **Output name** you entered). It has three outcomes: **Done** when the report has rows, **No rows** when it has none, and **Failed** when it cannot run. Use **No rows** to skip an email when there is nothing to report.

| Value | What it holds |
| --- | --- |
| `ReportId`, `Title` | The report. |
| `RowCount` | The number of rows. |
| `Columns` | The column labels. |
| `Rows` | The first 1,000 rows, each with its values by column label. |
| `Errors` | The reasons the report could not run, if any. |

<YouTube video="report-builder-workflows" title="Video of saving an Excel copy in the media library" start={157} end={181} />

**Export Report** stores `FileName`, `Format`, `RowCount` and `Size`. With a **Media folder**, such as `reports/weekly`, it saves the file in the media library and stores its `Path` and `Url`; the folder can use Liquid. Without one, the file is kept in the workflow as `Base64` for a custom activity to use.

## When something goes wrong

Every report task has a **Failed** outcome. It is taken when the report cannot be found or is not published, when the report cannot run, when the email cannot be sent, or when a media folder is set and the Media feature is off. **Create Report Share Link** also fails when there is no recipient, when a recipient is not a user of the site, or when it should email the link and the Email feature is off. The reason is written to the site's logs.
