---
sidebar_label: Reports
sidebar_position: 35
title: Reports
description: Find a report, set its date range and filters, and export it to CSV or Excel.
---

Every report lives under the **Reports** menu, grouped by category: *Agent Performance*, *Billing & Usage*, *Compliance & Audit*, *CRM & Campaigns*, *Executive*, *Operations*, *Queue & Routing*, *Technical & IT*, and *Workforce & Payroll*.

| | |
| --- | --- |
| **Menu** | Reports > *category* > *report* |
| **Permissions** | View Contact Center reports (the *Supervisor* role has it); View Omnichannel reports for the CRM reports |
| **Features** | Reports (`CrestApps.OrchardCore.Reports`) gives CSV export; Reports OpenXml (`CrestApps.OrchardCore.Reports.OpenXml`) adds Excel. Contact Center reports appear automatically when Contact Center Work Distribution is on. |

<video controls preload="metadata" width="100%" aria-label="Screencast of opening a report, changing the date range and filters, and exporting it">
  <source src="/img/docs/um-reports.mp4" type="video/mp4" />
</video>

## Run a report

1. Open **Reports** and pick a category, then a report.
2. Set the **Date range**. It starts on **today**, in the site's time zone. Pick a preset (*Yesterday*, *Last 7 Days*, *Last 30 Days*, *This Month*, *Last Quarter* and so on) or a **Custom Range**. A range can cover up to 400 days.
3. Set the filters the report offers, such as **Queue group**, **Queue**, **Agent**, **Channel**, **Direction**, **Campaign group**, **Campaign**, **Source** or **Status**. Each report shows only the filters it uses.
4. Click **Show**.
5. To keep a copy, click **Export CSV**. With the Reports OpenXml feature enabled you can also export to **Excel (.xlsx)**, which puts each section of the report on its own worksheet.

## Which report answers which question

| Question | Report |
| --- | --- |
| How many calls came in and out, and how many were answered or abandoned? | **Call insights** |
| Are calls sounding bad, for whom, and why? | **Call quality** |
| How long do agents talk, hold and ring, and how long do callers wait? | **Talk, hold, ring and queue wait** |
| What did each agent handle, and what is their average handle time? | **Agent productivity** |
| How busy is each queue right now and over the period? | **Queue usage** |
| How far along is each campaign or subject? | **Campaign summary**, **Subject inventory** |
| What did an agent do, minute by minute? | **Agent activity timeline** |
| How much paid time did each agent spend in each state? | **Reconciled payroll timecard** and the other *Workforce & Payroll* reports |
| Which dispositions are agents choosing? | **Disposition breakdown** |
| How often did the AI hand a conversation to a person? | **AI handoff & containment** |

The [report catalog](../contact-center/report-catalog.md) lists every Contact Center report with its filters.
