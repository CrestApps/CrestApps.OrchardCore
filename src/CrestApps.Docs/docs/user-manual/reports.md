---
sidebar_label: Reports
sidebar_position: 35
title: Reports
description: Find a report, set its date range and filters, and export it to CSV, Excel or PDF.
technical_manual:
  - contact-center/report-catalog
  - contact-center/agents-queues-dialer
  - modules/report-builder/index
  - modules/reports
  - omnichannel/crm
---

Reports show what happened in your contact center over a period you choose: calls, queues, agents' time, campaigns, leads and AI conversations. Every report lives under the **Reports** menu, grouped by category: *Executive*, *Operations*, *Queue & Routing*, *Agent Performance*, *Workforce & Payroll*, *Billing & Usage*, *CRM & Campaigns*, *Compliance & Audit*, and *Technical & IT*. Reports from other modules can appear under *General*.

| | |
| --- | --- |
| **Menu** | Reports > *category* > *report* |
| **Permissions** | View Contact Center reports (supervisors and administrators have it by default); View Omnichannel reports for the CRM and campaign reports (administrators and supervisors have it by default) |
| **Features** | Reports, for CSV export; Reports (OpenXml) adds Excel export, and Reports (PDF) adds PDF export. Contact Center reports appear when Contact Center Work Distribution is also on; the CRM reports come with Omnichannel Management and Omnichannel CRM. |

<AskYourAdmin />

<YouTube video="contact-center-agent-walkthroughs" title="Video of opening a report, changing the date range and filters, and exporting it" start={802} end={833} />

You only see the reports your permissions allow. If the **Reports** page says *No reports are available*, ask your administrator to turn on a feature that adds reports, or to give you the permission.

To build your own reports with drag and drop, see [Report Builder](report-builder/index.md).

## Run a report

1. Open **Reports** and pick a category, then a report.
2. Set the **Date range**. It starts on **today** (from the start to the end of the day), in the site's time zone. Pick a preset or a custom range (see [Date range choices](#date-range-choices)). A Contact Center report covers up to 400 days, and a report on agents' time up to 31 days (see [Agent time over long periods](#agent-time-over-long-periods)).
3. Set the filters the report offers, such as **Queue group**, **Queue**, **Agent**, **Channel**, **Direction**, **Campaign group**, **Campaign**, **Source**, **Status**, **Interaction type**, **Dialer profile**, **Queue state**, **Disposition**, **Activity ID** or **Reason**. Each report shows only the filters it uses. In **Disposition** you can tick several dispositions at once.
4. Click **Show**.
5. To keep a copy, click **Export CSV**. When the Reports (OpenXml) or Reports (PDF) feature is on, the button becomes an **Export** menu with **Export CSV**, **Export Excel (.xlsx)** and **Export PDF**. Excel puts each section of the report on its own worksheet. PDF lays the report out as it looks on screen, with its charts, and numbers the pages; a wide table turns the pages sideways.

How the filters work:

- A filter left on *All* (for example *All queues*) does not narrow the report.
- Filters combine: picking a queue, an agent, the Voice channel and the Inbound direction shows only what matches all four.
- Every number, total, percentage and average is worked out again from the filtered records, so it always matches what you picked.
- An export uses the same date range and filters as the report on screen. It keeps the detail, subtotal and grand-total rows, in the order shown.
- If a report needs a feature your site does not use (for example call transfers or recording), it shows a notice naming that feature instead of showing zeroes. A report that still has real figures leaves out only the columns that feature would fill.

Reports cannot be scheduled or emailed yet; run and export them when you need them.

## Email a report on a schedule

With **Report Subscriptions** on, any report can be emailed every day, week or month, such as your sales every Monday morning.

1. On **Reports > Subscriptions**, click **Add subscription**.
2. Choose the **Report**, and when to **Send** it: every day for the day before, every week for the 7 days before, or every month for the month before.
3. Choose the **Day of the week** or **Day of the month**, and the **Hour**, in your site's time zone.
4. Under **Email to**, enter the addresses, separated by commas. Choose a file format under **Attach the report as** to get the report as a file too.
5. Click **Save**, or **Save and send now** to get one straight away.

The email shows the report's numbers and tables. If one doesn't go out, such as when your site's email isn't set up, the list shows why. Untick **On** to pause a subscription.

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
| How far along is each campaign or subject, and how many numbers were not in service? | **Campaign summary** (see [below](#campaign-summary)), **Subject inventory** |
| How many of a campaign's pending records can be dialed right now, and why can the rest not? | **Campaign summary** (see [below](#campaign-summary)) |
| What did an agent do, minute by minute? | **Agent activity timeline** |
| Why is an available agent not getting calls, and how long has each agent waited? | **Agent availability and wait time** (see [below](#agent-availability-and-wait-time)) |
| How much paid time did each agent spend in each state? | **Reconciled payroll timecard** and the other *Workforce & Payroll* reports |
| Which dispositions are agents choosing? | **Disposition breakdown** |
| Which numbers did the dialer call, and how did each call end? | **Dialer call detail** (see [below](#dialer-call-detail)) |
| Is the dialer dialing, waiting for agents, out of work, or stuck? | **Dialer activity and health** (see [below](#dialer-activity-and-health)) |
| What is waiting in each campaign queue right now, and why is it not being dialed? | **Dialer queue inventory** (see [below](#dialer-queue-inventory)) |
| Why was a record not called, or why did an agent not get a call? Has routing stopped looking at a queue? | **Dialer decision log** (see [below](#dialer-decision-log)) |
| How often did the AI hand a conversation to a person? | **AI handoff & containment** |
| How many leads converted, from which source and list, and what is in the sales pipeline? | **Lead funnel**, **Lead conversion by source and list**, **Opportunity pipeline** (see [Leads, Accounts and Opportunities](leads-accounts-opportunities.md#reports)) |
| What did the store sell, and what did it keep after refunds? What is an average order worth? Which products, categories and countries sold most? How much tax did orders charge, and what did promotion codes take off? | **Sales**, **Sales by product**, **Sales by category**, **Sales by country**, **Tax on orders**, **Discounts on orders**, **Refunds** under *Commerce* (see [Carts and orders](commerce/carts-and-orders.md#store-reports)) |
| Who buys, how many come back, and how many checkouts end in a sale? | **Customers**, **Checkouts** under *Commerce* |
| Which paid orders still have to ship, how fast does the store ship, and what is running out of stock? | **Orders to ship**, **Shipping times**, **Stock on hand**, **Stock movements** under *Commerce* |
| Are buyers downloading what they bought, and what do they think of it? | **Downloads**, **Product reviews** under *Commerce* |

## Agent availability and wait time

**Agent availability and wait time**, under *Queue & Routing*, answers "why is this agent not getting calls?". It shows each agent as they are right now: how long they have waited for a call, the calls they took today, whether their workspace is really connected, anything stuck on them, and, for every queue and campaign they can work, whether they can be given work and why not. Use it when an agent says they are free but nothing comes through, when the live dashboard and the time reports disagree, or before you move people between queues.

The report is live: it ignores the **Date range** and reads everything as of the moment you click **Show**. "Today" starts at midnight in the site's time zone. Pick an **Agent** to look at one person.

### Who is listed

- If you oversee queues or campaigns (they are on your own [agent entitlement](skills-and-entitlements.md) record), the report lists every agent who is entitled to, or signed in to, one of them. That includes agents the [live dashboard](live-dashboard.md) leaves out, such as an agent who shows as Available but is signed in to nothing, or only to a queue you do not oversee.
- If you oversee nothing, the report lists every agent.
- If you pick an **Agent**, the report shows that agent, whoever oversees them.

### Agents

| Column | What it shows |
| --- | --- |
| **Presence** | The agent's state now, with its reason, for example *Break (Lunch)*. *Busy* means on a call. |
| **Available since (UTC)** and **Idle time** | For an Available agent, when they last became free and how long ago that was. Routing offers work first to the agent who has been free the longest, by this time. |
| **Last call assigned (UTC)** | When routing last gave the agent work. |
| **Last call ended (UTC)** | When the agent's last call ended. |
| **Calls today** | How many times the agent went on a call today. |
| **Longest wait between calls** | The longest stretch today the agent was ready for a call (Available, or being offered one) without getting one, including the stretch still running now. |
| **Signed in to** | The queues and campaigns the agent is signed in to, or *Nothing*. |
| **Live session** | *Yes* when the agent's workspace is connected and has checked in recently (within 90 seconds, unless your administrator changed it). *No: offline, disconnected or no recent heartbeat* means the workspace was closed, lost its connection or stopped checking in. *No session* means the agent has not opened their workspace. Routing only offers work to an agent with a live session. |
| **Heartbeat age** | How long ago the agent's workspace last checked in. |
| **On live dashboard** | Whether your live dashboard shows the agent, and if not, why: *not signed in to any queue or campaign*, or *not signed in to a queue or campaign you oversee*. |
| **Stale-state flags** | Anything stuck on the agent (see [Stale-state flags](#stale-state-flags)). Shown in red. |
| **History check** | A note when the agent's state now and their recorded history disagree (see [History check](#history-check)). Shown in red. |

### Why each queue or campaign is or is not giving work

One row for every queue and campaign the agent is entitled to or signed in to.

| Column | What it shows |
| --- | --- |
| **Queue or campaign** and **Type** | The queue or campaign, and which one it is. |
| **Entitled** | Whether a manager has allowed the agent to work it. |
| **Signed in** | Whether the agent is signed in to it now. |
| **Waiting** | How many items are waiting in it. |
| **Eligible now** | *Yes* when the agent can be given work from it right now. |
| **Why not eligible** | Every reason the agent cannot be given work from it, or, when they can, why they still may not be getting any. Shown in red when the agent is not eligible. |
| **Routing detail** | When work is waiting and the agent passes the checks above, what routing itself says about the agent for the next waiting item, in its own words: for example a missing skill, their place in the longest-idle line, or that they already had the call. |

The reasons in **Why not eligible** are the checks routing makes, in the same order:

| Reason | What it means and what to do |
| --- | --- |
| **The queue no longer exists**, **The queue is turned off**, **Closed by its business hours** | Nobody gets work from this queue now. Check the queue's settings or its business hours. |
| **Not entitled to this queue** (or campaign) | A manager has not allowed the agent to work it. Change the agent's entitlements. |
| **Not signed in to this queue** (or campaign) | The agent is allowed to work it but did not sign in to it. Ask them to sign in to it from their workspace. |
| **No live session** | The agent's workspace is closed, disconnected or has stopped checking in. Ask them to reopen or reload it. |
| **The live session is not signed in to it** | The workspace the agent has open is not signed in to this queue. Ask them to sign in again. |
| **Not Available (presence is ...)** | The agent is on a break, on a call, in wrap-up or in another state that does not take work. |
| **At capacity (x/y active interactions)** | The agent is already handling as many interactions as they are allowed at once. |
| **Routing passes this agent over for the next waiting item** | The agent passes every check, but routing chose not to offer them the next item; the **Routing detail** says why, for example a missing required skill, or that they declined the call or transferred it away, so it goes to someone who has not had it first. |
| **None: next in line for the waiting work** | Routing will offer the agent the next waiting item. |
| **None: eligible, but another agent is ahead in line** | The agent can take work, but someone else gets the next item first. |
| **None, but no work is waiting** | The agent can take work; there is simply nothing waiting. |
| **None, but no waiting item can be worked now** | Items are waiting, but none can be offered yet, for example campaign records scheduled for later. |

An agent with no queue or campaign at all shows one row: **Not entitled to or signed in to any queue or campaign.**

### Stale-state flags

A flag means something is holding the agent in a state that nothing real is behind. For an agent the live dashboard shows, **Free agent** in the **More** menu of their card releases it.

| Flag | What it means |
| --- | --- |
| **Available with no live session** | The agent is marked Available but their workspace is not connected, so routing passes them over. The time reports still count them as Available. |
| **Unresolved soft-phone call record** | A call is still open on the agent's soft phone although the phone service no longer has it, cannot find it, or the browser that placed it is gone. This is how an agent can show *On a call* with nobody on the line. |
| **Open interaction although the provider reports the call ended** | A Contact Center call is still open although the phone service says it ended. |
| **Busy with no live interaction** and **Reserved with no live interaction** | The agent's state says they are on a call or being offered one, but there is no call or offer behind it. |
| **Held reservation that no offer or call backs** | The agent is still holding an offer that is no longer ringing. |
| **Work assigned with no offer or call behind it** | Work is still assigned to the agent although no offer or call is in progress. |
| **Holds reservation ...** | An offer may be ringing for the agent right now. Not a problem on its own; it turns into a held-reservation flag if it stays. |
| **Stale work not checked** | Stuck calls and states are found by the Contact Center Supervision & Live Dashboard feature, which is off. Ask your administrator to turn it on. |

### History check

The *Workforce & Payroll* reports, such as **Agent time summary**, count an agent's time from the state changes the system recorded, up to now when no later change is recorded. The **History check** column says when that record and the agent's state now disagree:

- **No sign-out is recorded after the change to ... so the time summary counts this agent as ... up to now, but the agent has no live session.** The agent left without signing out (for example they closed the browser), so the time reports keep counting them, for example as Available for many hours, although nobody is there.
- **The profile says ..., but the last recorded change is to ...** The agent's state now is not the one the record ends with.
- **No state change is recorded for this agent.** The time reports show no time for an agent who is signed in now.

### Find out why an agent is not getting calls

1. Open **Reports > Queue & Routing > Agent availability and wait time**, pick the **Agent** and click **Show**.
2. In **Agents**, check **Presence**, **Live session** and **Stale-state flags**. A red flag usually is the answer: use **Free agent** on the live dashboard, or ask the agent to reload their workspace.
3. In **Why each queue or campaign is or is not giving work**, find the queue or campaign that has work **Waiting** and read **Why not eligible** and **Routing detail**.
4. If **On live dashboard** says *No*, the row says why the board does not show the agent.

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

## Dialer activity and health

**Dialer activity and health**, under *Operations*, tells you what the dialer is doing for each campaign it dials automatically, and what it did over the period you pick. It appears when the Contact Center Paced Dialing feature is on.

A dialer that places no calls can be in three very different situations, and the report keeps them apart:

| State | What it means | What to do |
| --- | --- | --- |
| **Stalled** | Records are waiting but the dialer has not finished a pass over the campaign for several minutes (five, unless your administrator changed it). The report shows the warning *Due work is waiting but no pacing cycle has completed for N minutes*. | Tell your administrator: the dialer has stopped, not run out of work. |
| **Idle: no agent available** | The dialer keeps checking the campaign and finds records it may call, but no agent is free to take the call. | Sign more agents in to the campaign, or bring agents back from a break. |
| **Nothing to call** / **Nothing due** | The dialer keeps checking and finds nothing it may call right now: the campaign has no records waiting, or they are scheduled for later or waiting out their retry delay. | Nothing, unless you expected records to be due. Load more records if the campaign has run out. |

Other states are **Dialing** (the last pass placed calls), **Holding back** (the dialer chose not to call: the compliance check refused the numbers, the abandoned-call cap was reached, or calls already ringing cover the free agents) and **Starting** (records arrived a moment ago).

The report has these sections:

| Section | What it shows |
| --- | --- |
| **Dialer right now** | The overall state, how many campaigns have records waiting, and how many are stalled, idle, holding back, dialing or have nothing due. |
| **Campaign queues** | One row per campaign: its state, when the dialer's last pass started and finished, how it ended (for example *Placed 2*, *No agents* or *Nothing due*, with how many passes in a row ended that way), why, the **Last call placed** and the **Last call assigned** (when, which agent, which record), and how many passes, calls and routing decisions there were in the period. |
| **Dialer profiles** | One row per dialer profile: its mode, its last pass, the last call placed, and the calls placed, answered and abandoned and the attempts suppressed in the period. |
| **Calls per hour** | A chart of calls placed, answered by a person and abandoned, hour by hour (day by day for a period longer than a month). |
| **Campaign routing decisions per hour** | A chart of how often a record was routed to an agent and how often no agent was available. |
| **Suppressed attempts by reason** | How many calls the compliance check refused, by reason: do not call, the national do-not-call registry, outside the calling window, number not in service, compliance check unavailable, abandonment rate cap reached, out of attempts, waiting out the retry delay and no number to dial. |

The state and the **Last cycle** columns always describe right now, whatever date range you pick; the counts and charts cover the date range. Times are shown in the site's time zone. The same state is shown as a banner on the [live dashboard](live-dashboard.md#dialer-banner).

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

The **Campaign summary** report splits the same pending records the same way (see [below](#campaign-summary)), so the two reports agree. This report's **Held back** counts every record that cannot be dialed now; the Campaign summary shows scheduled, not-in-a-queue and assigned records in columns of their own, so its **Held back** is what is left.

The **Why records are held back** table lists each reason again, with the **Count** of records and, for records that wait by themselves (scheduled for later, retry delay), when the first becomes due.

What to do about a flag:

- **The stamped dialer profile is turned off**: turn the profile on (see [Dialer Profiles](dialer-profiles.md)), or load the records again under a profile that is on (see [Dialer loads](load-inventory.md#dialer-loads)).
- **The stamped dialer profile no longer exists**: load the records again under a profile that exists.
- **Pending activities in no queue**: load the records into the campaign again so they are queued.
- **Stamped differently from their activity**: the dialer uses the stamped profile. Load the records again if they should be dialed under the activity's own profile.

## Campaign summary

**Campaign summary**, under *CRM & Campaigns*, shows how far along each campaign is: how many of the records created in the date range are completed, pending, in progress, failed or cancelled, and how many numbers were not in service. Its pending records are also split by what the dialer can do with them **right now**, so you can see per campaign how many can be dialed and why the rest cannot.

Filter it by **Campaign group**, **Campaign**, **Channel**, **Source**, **Status**, **Interaction type** (*Manual* or *Automated*), **Dialer profile** and **Queue state**.

- **Dialer profile** keeps the records whose activity carries the profile and the records whose queue item is stamped with it, because the dialer dials a record under the profile stamped on it. Turned-off profiles are listed too, marked *(turned off)*.
- **Queue state** keeps only the pending records in one state: *Dialable now*, *Held back*, *Scheduled for later*, *Not in any queue* or *Assigned to a user*. Records that are not pending have no queue state, so they drop out.

The **Campaigns** table has these columns beside the progress columns (**Total**, **Completed**, **Pending**, **In progress**, **Failed**, **Cancelled**, **Not in service**, **Attempts**, **Completion**):

| Column | What it shows |
| --- | --- |
| **Dialable now** | Pending records the dialer could dial right now. |
| **Held back** and **Held back because** | Pending records that cannot be dialed now for any other reason, with the reasons: the dialer profile is turned off or no longer exists, waiting out the retry delay, out of attempts, outside the calling window, do not call, number not in service and the other compliance reasons. |
| **Scheduled for later** | Pending records that wait for a time set on them, such as a follow-up after *Try again*. |
| **Not in any queue** | Pending records that are in no queue, so nothing will dial them until they are loaded again. |
| **Assigned to a user** | Pending records a user works from their own list. |
| **Not explained** | Queued records past the first 2,000 of a campaign, which are counted but not checked one by one. |
| **Attempts distribution** | How many of the campaign's records have been tried 0, 1, 2, 3, 4, or 5 or more times. |
| **Last attempt** | When a call was last placed for one of the campaign's records. |
| **Next due** | When the first record that waits for a time becomes due. |
| **Stamped dialer profile** | The dialer profiles stamped on the campaign's queued pending records, with how many carry each. |

**Dialable now**, **Held back**, **Scheduled for later**, **Not in any queue**, **Assigned to a user** and **Not explained** always add up to **Pending**. Pending records that belong to no campaign are not split. The split shows where the records the date range covers stand **now**, whatever the date range; with a date range that covers every record, it matches the **Dialer queue inventory** report.

The **Why pending records are not dialing** table lists every reason a campaign's pending records cannot be dialed now, with the number of records and, for records that wait for a time, when the first becomes due.

## Dialer decision log

**Dialer decision log**, under *Queue & Routing*, lists every decision routing and the dialer made about queued work: each time routing looked for an agent for a record, and each time the dialer refused to call a record. Use it to answer "why was this record not called?" or "why did this agent not get that call?" without asking for the system logs.

| | |
| --- | --- |
| **Menu** | Reports > Queue & Routing > Dialer decision log |
| **Permission** | View Contact Center reports |
| **Feature** | Contact Center Work Distribution and Reports. Suppressions are listed when the Contact Center Outbound Dialer is on. |

<AskYourAdmin />

### See the decisions made about one record or one agent

1. Open **Reports > Queue & Routing > Dialer decision log**.
2. Set the **Date range**.
3. To see one record, type its activity ID in **Activity ID**. You find the ID in the address of the activity's page.
4. To see one agent, pick them in **Agent**. The report then lists every routing decision that considered or chose the agent, and every dialer refusal of a call that was reserved for them.
5. Click **Show**. The **Decisions** table lists the newest decisions first, with the reason for each.

You can also narrow the report by **Campaign**, **Queue** and **Reason**. Filters combine.

### What the report shows

**Summary** counts the decisions in the period: all of them, the routing decisions, how many assigned work and how many did not, the dialer's suppressions, and how many activities they were about.

**Decisions by reason** counts the decisions of each kind (*Routing* or *Suppression*) by reason, the most frequent first, with when each reason was last seen.

**Queues with work due now** lists each queue and campaign that has work waiting that could be handed out now, with when routing last made a decision for it. Routing looks at a queue about once a minute, so a queue whose **Status** says *No routing decision in the last 15 minutes* (or the time your administrator set) is not being looked at, and its work is stuck. This section describes the queues as they are now, whatever date range you picked. It is shown when no **Agent**, **Activity ID** or **Reason** is picked.

**Decisions** has one row per decision:

| Column | What it shows |
| --- | --- |
| **Last made (UTC)** | When the decision was last made. |
| **First made (UTC)** | When it was first made. |
| **Times** | How many times in a row routing made exactly this decision. While a queue has work and nobody can take it, routing makes the same decision every minute; the report shows those as one row with the count, instead of one row per minute. |
| **Kind** | *Routing* (routing looked for an agent) or *Suppression* (the dialer refused to call). |
| **Queue** | The queue, or *Campaign: name* for a campaign's own queue. |
| **Campaign** | The campaign the record belongs to. |
| **Activity** | The ID of the activity (the record) the decision was about. |
| **Outcome** | *Assigned*, *Not assigned* or *Suppressed*. |
| **Reason** | Why: for example *No agents available*, *No available agent matched the routing policy*, *Do not call* or *Outside the calling window*. |
| **Agent** | The agent routing chose, or the agent the suppressed call was reserved for. |
| **Candidates** | For routing: every agent routing considered, and for each one that could not take the work, why not (for example *At capacity* or a missing skill). |
| **Destination** | For a suppression: the number that would have been dialed. |
| **Details** | For a suppression: the dialer's explanation. |

The screen lists the 500 most recent decisions; **Export CSV** and **Export Excel (.xlsx)** list up to 10,000.

Tips:

- A queue that shows *No routing decision* while it has work due is the first place to look when nothing is dialing. Check that agents are signed in to it on the **Live dashboard**.
- A campaign that uses predictive over-dialing places some calls without asking routing first, so it can show as quiet while those calls are in progress.

## Omnichannel reports

Omnichannel adds 29 reports under **CRM & Campaigns** and the other categories, and each needs **View Omnichannel reports**:

- **Five activity reports:** Activity summary, Campaign performance, Disposition breakdown, Dialer call detail, and AI handoff & containment.
- **Twenty-one workload and performance reports:** Activity backlog, Activity aging, Activity source performance, CRM channel performance, Activity kind performance, Activity assignment performance, Activity attempt analysis, Contact type workload, Activity urgency performance, Assigned user performance, Activity creation by user, User completion time, Daily user productivity, Campaign source mix, Campaign channel mix, Campaign disposition mix, Campaign attempt performance, Overdue workload by user, Address usage, Customer workload, and Scheduled completion performance.
- **Three CRM reports** with the Omnichannel CRM feature: Lead funnel, Lead conversion by source and list, and Opportunity pipeline.

The [report catalog](../contact-center/report-catalog.md) in the Technical Manual lists every Contact Center and CRM report with its filters, columns and exact formulas, such as how answer rate, service level and average handle time are calculated.
