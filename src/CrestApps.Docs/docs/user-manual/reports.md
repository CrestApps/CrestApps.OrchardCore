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
2. Set the **Date range**. It starts on **today** (from the start to the end of the day), in the site's time zone. Pick a preset or a custom range (see [Date range choices](#date-range-choices)). A Contact Center report covers up to 400 days.
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
| What is waiting in each campaign queue right now, and why is it not being dialed? | **Dialer queue inventory** (see [below](#dialer-queue-inventory)) |
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

## Dialer queue inventory

**Dialer queue inventory**, under *CRM & Campaigns*, shows what the dialer holds right now: what is waiting in each campaign's queue, which dialer profile those records are stamped with, how many can be dialed now, and why the rest are held back. Use it when a campaign has pending records and agents are available but nothing is being dialed. It answers on one screen, without reading logs.

The report always shows the present. The **Date range** does not change it; the times it shows are in the site's time zone. Filter it by **Campaign group** and **Campaign**.

The **Right now** cards at the top add up every row: **Campaign queues**, **Waiting**, **Reserved**, **Assigned**, **Pending**, **Dialable now**, **Held back**, **Not in any queue** and **Not explained**.

The **Campaign queues by stamped dialer profile** table has one row for each campaign queue and dialer profile its records are stamped with, and one row for each campaign's pending records that are not in its queue at all.

| Column | What it shows |
| --- | --- |
| **Campaign** | The campaign. |
| **Queue** | *Campaign queue* for records in the campaign's queue, or *Not in the campaign queue* for pending records that are not in it. |
| **Stamped dialer profile** | The dialer profile the dialer uses for these records. It is chosen when the records are loaded. *(None stamped)* means the records carry none; *(No longer exists)* means the profile was deleted. A turned-off or deleted profile is shown in red. |
| **Mode** and **Profile on** | The stamped profile's dialing mode, and whether it is turned on. |
| **Activity dialer profile** | The dialer profiles the activities themselves carry, with how many carry each. |
| **Waiting**, **Reserved**, **Assigned** | How many of the queue's records are waiting, offered to an agent, or with an agent. |
| **Oldest waiting** | When the record that has waited longest entered the queue. |
| **Pending** | How many of these records have not been started yet. |
| **Source / type / channel** | Where the pending records came from, whether they are worked by people or automatically, and their channel, with a count for each combination. |
| **Dialable now** | How many pending records the dialer could dial right now. |
| **Held back** and **Held back because** | How many cannot be dialed now, and why: the dialer profile is turned off or no longer exists, scheduled for later, waiting out the retry delay, out of attempts, outside the calling window, do not call, number not in service, or another compliance reason. *Not explained* counts the records past the first 2,000 of a campaign, which are counted but not checked one by one. |
| **Queue pacing** | Whether the system is dialing the queue. The system paces a queue by the record at the front of it, so a turned-off or deleted profile there stops the whole queue, even when the records behind it are stamped with a profile that is on. |
| **Flags** | Problems to fix, in red: records stamped with a different dialer profile than their activity carries, pending records that are in no queue, and a stamped profile that is turned off or no longer exists. |
| **Last dialed** | When the queue last placed a call. |

The **Why records are held back** table lists each reason again, with the **Count** of records and, for records that wait by themselves (scheduled for later, retry delay), when the first becomes due.

What to do about a flag:

- **The stamped dialer profile is turned off**: turn the profile on (see [Dialer Profiles](dialer-profiles.md)), or load the records again under a profile that is on (see [Dialer loads](load-inventory.md#dialer-loads)).
- **The stamped dialer profile no longer exists**: load the records again under a profile that exists.
- **Pending activities in no queue**: load the records into the campaign again so they are queued.
- **Stamped differently from their activity**: the dialer uses the stamped profile. Load the records again if they should be dialed under the activity's own profile.

The [report catalog](../contact-center/report-catalog.md) in the Technical Manual lists every Contact Center and CRM report with its filters, columns and exact formulas, such as how answer rate, service level and average handle time are calculated.
