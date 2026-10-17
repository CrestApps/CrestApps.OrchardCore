---
sidebar_label: Reports
sidebar_position: 35
title: Reports
description: Find a report, set its date range and filters, and export it to CSV or Excel.
technical_manual:
  - contact-center/report-catalog
  - modules/reports
  - omnichannel/crm
---

Reports show what happened in your contact center over a period you choose: calls, queues, agents' time, campaigns, leads and AI conversations. Every report lives under the **Reports** menu, grouped by category: *Executive*, *Operations*, *Queue & Routing*, *Agent Performance*, *Workforce & Payroll*, *Billing & Usage*, *CRM & Campaigns*, *Compliance & Audit*, and *Technical & IT*. Reports from other modules can appear under *General*.

| | |
| --- | --- |
| **Menu** | Reports > *category* > *report* |
| **Permissions** | View Contact Center reports (supervisors and administrators have it by default); View Omnichannel reports for the CRM and campaign reports (administrators have it by default) |
| **Features** | Reports, for CSV export; Reports (OpenXml) adds Excel export. Contact Center reports appear when Contact Center Work Distribution is also on; the CRM reports come with Omnichannel Management and Omnichannel CRM. |

<AskYourAdmin />

<YouTube video="contact-center-agent-walkthroughs" title="Video of opening a report, changing the date range and filters, and exporting it" start={802} end={833} />

You only see the reports your permissions allow. If the **Reports** page says *No reports are available*, ask your administrator to turn on a feature that adds reports, or to give you the permission.

## Run a report

1. Open **Reports** and pick a category, then a report.
2. Set the **Date range**. It starts on **today** (from the start to the end of the day), in the site's time zone. Pick a preset or a custom range (see [Date range choices](#date-range-choices)). A Contact Center report covers up to 400 days, and a report on agents' time up to 31 days (see [Agent time over long periods](#agent-time-over-long-periods)).
3. Set the filters the report offers, such as **Queue group**, **Queue**, **Agent**, **Channel**, **Direction**, **Campaign group**, **Campaign**, **Source**, **Status** or **Disposition**. Each report shows only the filters it uses. In **Disposition** you can tick several dispositions at once.
4. Click **Show**.
5. To keep a copy, click **Export CSV**. When the Reports (OpenXml) feature is on, the button becomes an **Export** menu with **Export CSV** and **Export Excel (.xlsx)**. Excel puts each section of the report on its own worksheet.

How the filters work:

- A filter left on *All* (for example *All queues*) does not narrow the report.
- Filters combine: picking a queue, an agent, the Voice channel and the Inbound direction shows only what matches all four.
- Every number, total, percentage and average is worked out again from the filtered records, so it always matches what you picked.
- An export uses the same date range and filters as the report on screen. It keeps the detail, subtotal and grand-total rows, in the order shown.
- If a report needs a feature your site does not use (for example call transfers or recording), it shows a notice naming that feature instead of showing zeroes. A report that still has real figures leaves out only the columns that feature would fill.

Reports cannot be scheduled or emailed yet; run and export them when you need them.

## Agent time over long periods

The reports on agents' time read every state change of every agent in the period: all the *Workforce & Payroll* reports, **Agent presence audit** and **Agent activity timeline**. To keep them quick, they have these limits:

- **31 days at a time.** A longer period shows *This period is too long for this report*, with the longest period allowed and the number of days you picked, and no figures. Pick 31 days or fewer, for example one month at a time. An export has the same limit. Your administrator can change both limits.
- **5,000 rows on screen.** **Agent presence audit** lists the most recent 5,000 changes and **Agent activity timeline** the first 5,000 entries, with a note above the table that gives the total. Pick an agent or a shorter period to see the rest. An export of **Agent presence audit** lists every change; **Agent activity timeline** exports its first 5,000 entries. The timeline's summary at the top still counts every entry.
- **Too much to read in time.** If the period still holds more than the database can read in time, the report shows *This period is too large to report*. Pick an agent or a shorter period and run it again.

Reports of agents' time over many days are quick because the site keeps a summary of each finished day for every agent. The figures are the same as adding up every change: the first and last day of the period, today, and any day that changed after it was summarized are always worked out from the changes themselves.

### Date range choices

| Choice | What it covers |
| --- | --- |
| **Relative days** | **Today**, **Yesterday**, **Last 7 Days**, **Last 30 Days**, **Last 90 Days**. |
| **Calendar periods** | **This Week**, **Last Week**, **This Month**, **Last Month**, **This Quarter**, **Last Quarter**, **This Year**, **Last Year**. |
| **Rolling months** | **Last 3 Months**, **Last 6 Months**, **Last 12 Months**. |
| **Custom Range** | A start and an end date and time that you pick. Empty, they default to today from 00:00 to 23:59. |
| **On or before** | Everything up to a date and time, with no start. |
| **On or after** | Everything from a date and time, with no end. |

The button always shows the range you picked in words, for example *From Jan 1, 2026 to Jan 31, 2026*. Weeks start on the first day of the week for your language settings.

## Which report answers which question

| Question | Report |
| --- | --- |
| How many calls came in and out, and how many were answered or abandoned? | **Call insights** |
| Are calls sounding bad, for whom, and why? | **Call quality** |
| How long do agents talk, hold and ring, and how long do callers wait? | **Talk, hold, ring and queue wait** |
| What did each agent handle, and what is their average handle time? | **Agent productivity** |
| How busy is each queue right now and over the period? | **Queue usage** |
| How far along is each campaign or subject, and how many numbers were not in service? | **Campaign summary**, **Subject inventory** |
| What did an agent do, minute by minute? | **Agent activity timeline** |
| How much paid time did each agent spend in each state? | **Reconciled payroll timecard** and the other *Workforce & Payroll* reports |
| Which dispositions are agents choosing? | **Disposition breakdown** |
| Which numbers did the dialer call, and how did each call end? | **Dialer call detail** (see [below](#dialer-call-detail)) |
| How often did the AI hand a conversation to a person? | **AI handoff & containment** |
| How many leads converted, from which source and list, and what is in the sales pipeline? | **Lead funnel**, **Lead conversion by source and list**, **Opportunity pipeline** (see [Leads, Accounts and Opportunities](leads-accounts-opportunities.md#reports)) |

## Dialer call detail

**Dialer call detail**, under *CRM & Campaigns*, lists the phone numbers the dialer called, one row per call attempt, with how each one ended. Use it when you want the numbers behind a count: every number that ended as *No answer* today, or everything a campaign dialed this week.

| Column | What it shows |
| --- | --- |
| **Phone number** | The number that was dialed. |
| **Contact** | The contact the number belongs to. |
| **Campaign** | The campaign the call was made for. |
| **Last disposition** | The disposition the attempt ended with. A disposition that has since been deleted shows as *(Unknown disposition)*; an attempt that has not ended yet shows *(No disposition)*. |
| **Attempts** | How many times this contact had been tried, counting this attempt. |
| **Completed (UTC)** | When the attempt ended. Empty while the attempt is still scheduled or being dialed. |
| **Agent** | The user the attempt was handed to, or *(Unassigned)*. |
| **Status** | The status of the attempt, such as *Completed*, *Scheduled* or *Dialing*. |

How it works:

- The report opens with **Source** set to **Dialer**, so it shows dialed work only. Change **Source** to see other work, or pick *All sources*.
- The **Date range** covers the time an attempt ended. An attempt that has not ended yet is listed under the time it is scheduled to be dialed.
- Filter by **Campaign group**, **Campaign**, **Channel**, **Source**, **Status** and **Disposition**. Tick several dispositions, for example *No answer* and *Busy*, to list the numbers that ended either way.
- Rows are listed newest first, 100 to a page. The line above the table says which rows you are looking at; type a page number in **Page** and click **Show** to move to another page.
- **Export CSV** (or **Export Excel (.xlsx)**) downloads every matching row, not only the page on screen, so the list can be worked outside the system or loaded again later.

The [report catalog](../contact-center/report-catalog.md) in the Technical Manual lists every Contact Center and CRM report with its filters, columns and exact formulas, such as how answer rate, service level and average handle time are calculated.
