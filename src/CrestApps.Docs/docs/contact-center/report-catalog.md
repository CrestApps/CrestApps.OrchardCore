---
sidebar_label: Enterprise report catalog
sidebar_position: 5
title: Enterprise Contact Center Report Catalog
description: Definitions, formulas, filters, drill paths, exports, permissions, and limitations for the built-in Contact Center and CRM reports.
---

# Enterprise Contact Center Report Catalog

The Contact Center Analytics and Omnichannel Management features contribute 79 immediately runnable reports to the shared **Reports** area. The admin menu organizes them into **Executive**, **Operations**, **Queue & Routing**, **Agent Performance**, **Workforce & Payroll**, **Billing & Usage**, **CRM & Campaigns**, **Compliance & Audit**, and **Technical & IT** groups. The catalog intentionally reports only facts represented by durable Contact Center or CRM data. It does not infer schedules, pay rates, quality scores, survey responses, or customer-resolution outcomes that have not been collected.

## Shared report behavior

| Capability | Behavior |
| --- | --- |
| Reporting population | Unless a report says otherwise, one interaction or CRM activity enters the report when its `CreatedUtc` is within the inclusive UTC bounds resolved from the selected tenant-local date and time range. This cohort rule prevents the same item from moving between periods when it later ends or completes. Backlog and aging are exceptions: they include currently nonterminal activities created on or before **To**, including older open work. |
| Default filters | Every report has tenant-local **From** and **To** date/time controls. Interaction reports add queue, agent, channel, and direction. Workforce reports add agent. Contact Center campaign/subject reports add campaign, channel, source, and activity status. Omnichannel reports add campaign, channel, source, and activity status. The same filter values are applied to browser results and exports. |
| Time grouping | Durable source timestamps are stored and compared in UTC. Date/time controls are displayed in the tenant time zone and converted to UTC before the query runs, including daylight-saving transitions. Daily rows currently use UTC dates. No percentage or average is averaged across displayed rows; totals are recalculated from raw counts and durations. |
| Sorting | Summary tables default to descending population. Daily tables sort chronologically. Interaction detail sorts newest first. Aging and attempt reports sort by ascending bucket. |
| Visualizations | The shared renderer supports KPI cards, tables, horizontal bars, and responsive Chart.js line, bar, stacked-bar, and doughnut charts. Heat-map, gauge, funnel, Sankey, and timeline renderers remain recommended extensions where noted below. |
| Export | CSV is built in. Excel is available when `CrestApps.OrchardCore.Reports.OpenXml` is enabled. Export actions appear in a report toolbar at the right edge of the first visible section heading and retain the active filter values. The toolbar and visible report heading are not exported; CSV starts with the data headings, and Excel uses the report title for worksheet tab names. PDF and JSON are not currently provided. |
| Scheduling | Reports are currently interactive and exportable. Scheduled delivery by email, collaboration channel, or SFTP is not yet implemented; daily, weekly, and monthly schedules are the recommended baseline when scheduling is added. |
| Permissions | Contact Center reports require **View Contact Center reports** and are granted to supervisors and administrators by default. CRM reports require **View Omnichannel reports** and are granted to administrators by default. Tenant isolation is enforced by Orchard shell scope and tenant-local YesSql collections. |
| Drill-down | The current renderer presents report sections on one page. The drill paths below define the stable implementation target for linked drill-down navigation. |

Campaign and disposition dimensions render their configured display names. If a referenced catalog entry has been deleted, the report shows **Unknown campaign** or **Unknown disposition** instead of exposing the stored identifier. User table cells render through Orchard Core's cached `UserDisplayName` shape: the account username is the default text, and the **User Display Name** feature replaces it through the configured `IDisplayNameProvider`. Browser rendering preserves the enclosing admin layout, while the export resolver suppresses layout rendering so CSV and Excel contain only the resolved text value and never the surrounding admin-page HTML. Usernames are resolved from stable user identifiers when a report runs and are not duplicated in activity indexes. Deleted or unavailable users are shown as **Unknown user** or **Unknown agent**.

## Canonical KPI definitions

These definitions are authoritative across the catalog.

| KPI | Exact definition and edge cases |
| --- | --- |
| Interactions | Count of interaction records in the report cohort. Every transfer, consult, conference, callback, or requeue remains part of the same interaction unless it creates a separate communication attempt and therefore a separate interaction record. |
| Inbound offered | Count of inbound interaction records. Queue overflows and requeues do not create another offered interaction. An inbound callback promoted into an outbound attempt is counted as an outbound interaction, not another inbound offered interaction. |
| Answered | Count of interactions with `AnsweredUtc`. `Inbound answered` restricts the numerator to inbound interactions. |
| Answer rate | `Answered interactions / all interactions` in all-direction reports. Inbound answer rate is `Inbound answered / Inbound offered`. A zero denominator produces zero. |
| Abandoned | Inbound interactions with no `AnsweredUtc` and final status `Ended`. Failed interactions are excluded. All abandons are included because a configurable short-abandon threshold is not yet stored. Transfers, consults, and conferences after answer cannot be abandoned. |
| Abandonment rate | `Abandoned / Inbound offered`. Percentages are recalculated from counts at every aggregation level. |
| Average speed of answer (ASA) | `Sum(AnsweredUtc - CreatedUtc for inbound answered interactions) / Inbound answered`. Negative durations are clamped to zero. Abandoned, failed, and outbound interactions are excluded. |
| Connected duration | For a completed answered interaction, `EndedUtc - AnsweredUtc`. This interval includes hold because interaction history does not yet persist separate channel-neutral hold segments. |
| Wrap-up duration | `WrapUpCompletedUtc - WrapUpStartedUtc` when both values exist and completion is not before start. Incomplete or invalid intervals contribute zero seconds and remain visible in the wrap-up completion counts. |
| Average handle time (AHT) | `Sum(connected duration + completed wrap-up duration) / handled interactions`, where handled means answered with a valid end time. This is operationally equivalent to talk + hold + after-contact work with the current channel-neutral timestamps. Abandoned and failed interactions are excluded. |
| Transfer rate | `Answered interactions with at least one transfer history entry / Answered interactions`. Multiple transfers increase transfer volume but not the interaction-level transfer-rate numerator. Consultative and blind transfers are included when recorded; conferences are not transfers unless a transfer history entry also exists. |
| Recording coverage | `Answered voice interactions with a non-empty recording reference / Answered voice interactions`. Paused or stopped recordings count as covered when a recording reference exists. |
| Service level | Per queue: `Inbound interactions answered within the queue SLA threshold / (Inbound answered + abandoned)`. Failed interactions are excluded. All abandons remain in the denominator because no short-abandon threshold is stored. The original interaction is counted once after overflow or requeue. A queue without a positive threshold displays no service-level percentage. |
| Completion rate | `Completed CRM activities / Activities in the group`. Failed, cancelled, purged, pending, and in-progress activities remain in the denominator. Reopened work is represented by a new or nonterminal activity and is not treated as completed. |
| Average attempts | `Sum(max(Attempts, 0)) / Activities in the group`. |
| Overdue | A nonterminal activity whose `ScheduledUtc` is before the report's `ToUtc` as-of boundary. |
| Observed signed-in time | Sum of clipped agent-presence intervals whose current status is not `Offline`. The interval begins at the durable presence transition time, ends at the next transition, and is clipped to the selected report boundaries. Time before the first known transition is unknown and excluded. |
| Productive presence | `Available + Reserved + Busy + WrapUp` observed presence duration. This is a presence classification, not proof of payroll eligibility or schedule adherence. |
| Agent utilization | `(Busy + WrapUp) / Observed signed-in time`. Break, away, meeting, training, do-not-disturb, and other signed-in not-ready states remain in the denominator. |
| Agent occupancy | `(Busy + WrapUp) / (Available + Reserved + Busy + WrapUp)`. Offline and explicitly not-ready states are excluded. This is a presence-derived operational occupancy measure and does not use workforce schedules. |
| Activity cycle time | For completed CRM activities, `CompletedUtc - CreatedUtc`, clamped to zero. Median is recalculated from the sorted raw duration population. |
| Usage for billing | Raw interaction count and measured connected, wrap-up, queue-wait, transfer, and recording usage. The platform does not apply prices, contracts, taxes, minimum billing increments, or currency conversion. |
| Transcript coverage | `Answered interactions with a non-empty transcript reference / Answered interactions`, grouped by channel. A transcript reference indicates availability, not transcript completeness or quality. |

## Report catalog

Each report uses the shared date/time filter, export, permission, scheduling, and aggregation behavior above. “Columns/KPIs” names the complete default output.

## Running and filtering a report

1. Enable **Reports** and the feature that contributes the report: **Contact Center Reports & Analytics** or **Omnichannel Management**.
2. Open **Reports** in the admin menu and select a report.
3. Set **From** and **To** in the tenant's local time zone. The default is the last 30 days through the end of the current tenant-local day.
4. Narrow the population with the displayed dimensions. Interaction reports offer queue, agent, channel, and direction; workforce reports offer agent; CRM reports offer campaign, channel, source, and status.
5. Select **Show**. Every metric, table row, total, percentage, and duration is recalculated from the filtered raw population.
6. Select **Export CSV** or an enabled Excel export. Export actions submit the same filter form, so downloaded data matches the visible report.

An empty dimension means **All**. If **From** is later than **To**, the report swaps the resolved UTC boundaries before querying. Filters combine with logical AND; for example, selecting Queue A, Agent B, Voice, and Inbound returns only interactions matching all four dimensions.

### Executive and operational reports

| # | Report | Purpose and business value | Type and granularity | Filters; grouping; sorting | Columns/KPIs | Visualization and drill-down |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | Executive performance dashboard | Gives executives and directors a concise, presentation-ready view of demand, accessibility, responsiveness, efficiency, transfers, recording coverage, channel adoption, queue SLA health, and agent workload. Enables capacity, provider, customer-experience, and operating-model investment decisions. | Historical interactive dashboard; enterprise cohort plus daily, channel, queue, agent, and channel-detail views. | Date/time, queue, agent, channel, and direction; day/channel/queue/agent; chronological trend, highest-volume queues, and highest-volume agents. | Interactions, inbound offered, inbound answered, inbound answer rate, abandoned, abandonment rate, failed, ASA, AHT, transfer rate, recording coverage, daily offered/answered/abandoned, channel volume, queue service level, handled by agent. | KPI hero cards + daily multi-series line chart + channel-mix doughnut + queue service-level bar chart + top-agent workload bar chart + channel detail table; enterprise → channel/queue/agent → interaction detail → recording/transcript. |
| 2 | Call insights | Gives operations leaders a broad interaction outcome and duration summary with channel/status breakdowns and daily volume. Supports trend and exception review. | Historical dashboard; enterprise cohort and one day per row. | Channel, direction, status, provider; day/channel/status; chronological daily rows. | Total, inbound, outbound, answered, abandoned, failed, answer rate, abandonment rate, AHT, ASA, connected duration, wrap-up duration. | KPI cards + bars + daily trend; day → channel/status → interaction detail. |
| 3 | Interaction volume trend | Shows demand and outcome movement over time for directors, workforce planners, and analysts. Supports staffing and anomaly detection without claiming a forecast. | Historical trend; one UTC day per row. | Channel, direction, queue, campaign; day/week/month; chronological. | Date, interactions, answered, abandoned, failed. | Trend line + stacked bars; day → interval → interaction detail. |
| 4 | Interval performance | Combines daily workload, outcomes, rates, ASA, and AHT for operations reviews. Enables interval-level staffing and service remediation. | Historical interval report; one UTC day per row. | Queue, channel, direction, provider; day/week/month; chronological or worst abandonment/ASA. | Date, interactions, answered, abandoned, answer rate, abandonment rate, ASA, AHT. | Trend line + table; day → queue/channel → interaction detail. |

### Queue and routing reports

| # | Report | Purpose and business value | Type and granularity | Filters; grouping; sorting | Columns/KPIs | Visualization and drill-down |
| --- | --- | --- | --- | --- | --- | --- |
| 5 | Queue usage | Shows handled outcomes, duration, configured SLA threshold, and current waiting depth. Supervisors use it to rebalance live operations. | Historical plus near-real-time snapshot; one queue per row. | Queue, channel, direction; queue/day; waiting then handled volume. | Queue, handled, answered, abandoned, AHT, ASA, waiting now, SLA threshold. | Table + waiting gauges; queue → interval → interaction detail. |
| 6 | Queue service level | Measures threshold attainment with a mathematically stable denominator. Enables SLA governance and queue-policy changes. | Historical; one queue per row. | Queue, channel; queue/day/week; ascending service level. | Queue, SLA threshold, eligible offered, answered within SLA, service level, ASA. | Gauge + table; queue → interval → answered/abandoned detail. |
| 7 | Queue abandonment analysis | Identifies queues where customers leave before answer and quantifies wait before abandonment. Supports staffing and routing remediation. | Historical; one queue per row. | Queue, entry point, channel; queue/day; descending abandonment rate or volume. | Queue, inbound offered, answered, abandoned, abandonment rate, average wait before abandon. | Heat map + table; queue → interval → abandoned interaction detail. |
| 8 | Channel performance | Compares interaction outcomes and durations across supported media. Helps allocate channel capacity and identify channel-specific failures. | Historical; one channel per row. | Channel, direction, queue; channel/day; descending interactions. | Channel, interactions, answered, abandoned, failed, answer rate, abandonment rate, ASA, AHT. | Stacked bars + table; channel → queue/agent → interaction detail. |
| 9 | Direction performance | Separates inbound and outbound operating behavior without mixing inbound SLA semantics with outbound attempts. | Historical; one direction per row. | Direction, channel, provider; direction/day; descending interactions. | Direction, interactions, answered, abandoned, failed, answer rate, abandonment rate, ASA, AHT. | Side-by-side bars; direction → outcome → interaction detail. |
| 10 | Provider performance | Compares normalized outcomes and duration across communications providers. Supports provider reliability and sourcing decisions. | Historical; one provider per row. | Provider, channel, direction; provider/day; descending failures or interactions. | Provider, interactions, answered, abandoned, failed, answer rate, abandonment rate, ASA, AHT. | Scatter plot + table; provider → status → interaction detail. |
| 11 | Interaction outcome summary | Shows volume and response metrics by normalized lifecycle status. Helps operations teams isolate failures and unfinished work. | Historical; one status per row. | Status, channel, direction, provider; status/day; descending interactions. | Outcome, interactions, answered, abandoned, failed, answer rate, abandonment rate, ASA, AHT. | Stacked bar + table; outcome → provider/queue → interaction detail. |

### Agent reports

| # | Report | Purpose and business value | Type and granularity | Filters; grouping; sorting | Columns/KPIs | Visualization and drill-down |
| --- | --- | --- | --- | --- | --- | --- |
| 12 | Agent productivity | Gives supervisors handled volume, channel mix, connected time, wrap-up, AHT, and completed CRM work. Enables balanced coaching based on workload and outcomes. | Historical; one agent per row. | Agent, team, queue, channel; agent/team/day; descending handled. | Agent, handled, inbound handled, outbound handled, connected duration, wrap-up duration, average wrap-up, AHT, activities completed. | Table + scatter plot; agent → day → interaction → activity. |
| 13 | Agent handle time analysis | Separates connected and wrap-up contributions to handle time by agent. Supports targeted process and coaching review without treating lower AHT as inherently better. | Historical; one agent per row. | Agent, queue, channel, direction; agent/day; descending AHT or volume. | Agent, handled, average connected duration, average wrap-up, AHT, total handle time. | Scatter plot (volume vs AHT) + table; agent → interaction detail → recording/transcript. |
| 14 | Agent wrap-up performance | Shows wrap-up starts, completions, completion rate, and duration. Helps supervisors identify incomplete after-contact work and workflow friction. | Historical; one agent per row. | Agent, queue, channel; agent/day; ascending completion rate or descending duration. | Agent, wrap-up started, wrap-up completed, completion rate, average wrap-up, total wrap-up. | Table + bars; agent → incomplete/completed wrap-up interactions → CRM activity. |

### Interaction, transfer, and recording reports

| # | Report | Purpose and business value | Type and granularity | Filters; grouping; sorting | Columns/KPIs | Visualization and drill-down |
| --- | --- | --- | --- | --- | --- | --- |
| 15 | Interaction detail | Provides the auditable source row behind summary reports. Analysts and supervisors use it to reconcile totals and investigate individual contacts. | Historical drill-down; one interaction per row. | Interaction, queue, agent, channel, direction, provider, status; optional grouping by day; newest first. | Started UTC, interaction id, channel, direction, status, queue, agent, provider, wait, connected duration, wrap-up, transfer count. | Table/timeline; interaction → event lifecycle → CRM activity → recording/transcript. |
| 16 | Transfer analysis | Quantifies transfer outcomes, destination types, completion, and latency. Supports routing improvement and first-owner coaching without claiming FCR. | Historical; one target-type/result pair per row. | Target type, result, queue, agent, channel; target/result/day; descending transfers. | Target type, result, transfers, completed, completion rate, average completion time. | Sankey + table; target/result → interaction → transfer history. |
| 17 | Recording coverage | Finds answered voice interactions lacking a recording reference and compares provider coverage. Supports compliance and troubleshooting. | Historical; one provider per row. | Provider, queue, agent; provider/day; ascending coverage. | Provider, answered voice interactions, with recording, without recording, coverage. | Gauge + table; provider → uncovered interaction → event/audit detail. |

### Campaign and subject reports

| # | Report | Purpose and business value | Type and granularity | Filters; grouping; sorting | Columns/KPIs | Visualization and drill-down |
| --- | --- | --- | --- | --- | --- | --- |
| 18 | Campaign summary | Shows Contact Center campaign inventory progress and attempts. Directors use it to compare throughput and unresolved workload. | Historical; one campaign per row. | Campaign, status, source, channel; campaign/day; descending total. | Campaign, total, completed, pending, in progress, failed, cancelled, attempts, completion rate. | Stacked bar + table; campaign → status → activity → interaction. |
| 19 | Subject inventory | Shows progress by CRM subject content type. Business owners use it to understand which workflows generate backlog or failures. | Historical; one subject type per row. | Subject type, status, source; subject/day; descending total. | Subject type, total, completed, pending, in progress, failed, cancelled, attempts, completion rate. | Stacked bar + table; subject → activity → interaction/disposition. |

### CRM activity reports

| # | Report | Purpose and business value | Type and granularity | Filters; grouping; sorting | Columns/KPIs | Visualization and drill-down |
| --- | --- | --- | --- | --- | --- | --- |
| 20 | Activity summary | Gives CRM operations a top-level inventory and completion view with source, channel, status, and daily creation breakdowns. | Historical dashboard; cohort plus one day per row. | Source, channel, status, campaign; day/source/channel/status; chronological daily rows. | Total, completed, pending, in progress, failed, cancelled, completion rate, daily created. | KPI cards + bars + trend; dimension → activity list → activity. |
| 21 | Activity backlog | Quantifies currently nonterminal, unassigned, overdue, and reserved work created on or before **To**. Enables backlog clearance and assignment decisions without dropping older open work. | Current-state dashboard constrained by creation cutoff; one status per row. | Status, source, channel, campaign; status; descending open count. | Open, unassigned, overdue, reserved; status totals and progress columns. | KPI cards + stacked bars; status → activity. |
| 22 | Activity aging | Places currently open work created on or before **To** into stable age bands and highlights unassigned and overdue work. Supports backlog risk and staffing decisions. | Current-state aging report; one age bucket per row. | Source, channel, campaign, status; age bucket; oldest first. | Age bucket, activities, share, unassigned, overdue. | Aging histogram + table; bucket → activity detail. |
| 23 | Activity source performance | Compares manual, automatic, dialer, callback, inbound, workflow, and API work progress. Supports automation and workload-source decisions. | Historical; one source per row. | Source, status, channel; source/day; descending total. | Source, total, completed, in progress, pending, failed, cancelled, completion rate, average attempts. | Stacked bars + table; source → status → activity. |
| 24 | CRM channel performance | Shows CRM work progress by communication channel independent of interaction session outcomes. Helps identify channel workflow bottlenecks. | Historical; one channel per row. | Channel, source, campaign, status; channel/day; descending total. | Channel and standard activity progress columns. | Stacked bars + table; channel → campaign/status → activity. |
| 25 | Activity kind performance | Compares calls, messages, meetings, tasks, and future work kinds. Supports workload-mix planning. | Historical; one activity kind per row. | Kind, source, status; kind/day; descending total. | Activity kind and standard activity progress columns. | Donut + table; kind → source/status → activity. |
| 26 | Activity assignment performance | Shows progress by assignment lifecycle state and surfaces work stalled before ownership. Supports reservation and routing improvements. | Historical; one assignment status per row. | Assignment status, assignee, queue, source; assignment/day; descending total. | Assignment status and standard activity progress columns. | Funnel + table; assignment status → activity/reservation detail. |
| 27 | Activity attempt analysis | Shows outcome by nonnegative attempt count. Helps tune retries and identify diminishing returns. | Historical; one attempt count per row. | Attempt count, source, campaign, status; attempts; ascending attempts. | Attempts, activities, completed, failed, completion rate. | Funnel + table; attempt count → campaign/source → activity interactions. |
| 28 | Contact type workload | Compares activity workload across configured CRM contact content types. Supports business-unit and data-model planning. | Historical; one contact content type per row. | Contact type, source, campaign, channel; contact type/day; descending total. | Contact type and standard activity progress columns. | Bars + table; contact type → activity → contact timeline. |
| 29 | Activity urgency performance | Shows whether urgent work is completing or accumulating. Supports priority-policy and staffing decisions. | Historical; one urgency level per row. | Urgency, status, source, channel; urgency/day; urgency then total. | Urgency and standard activity progress columns. | Heat map + table; urgency → status → activity. |
| 30 | Campaign performance | Gives CRM campaign owners completed-versus-pending progress across activity inventory. Supports campaign pacing and remediation. | Historical; one campaign display name per row. | Campaign, source, channel, status; campaign/day; descending total. | Campaign display name, total, completed, pending, in progress, failed, cancelled, completion rate. | Stacked bars + table; campaign → status → activity. |
| 31 | Disposition breakdown | Shows how completed activities were dispositioned and each disposition's share. Enables outcome governance and workflow review. | Historical by completion date; one disposition display name per row. | Disposition, campaign, subject, channel, agent; disposition/day; descending completed. | Disposition display name, completed, share of completed activities. | Donut + table; disposition → completed activity → interaction history. |

### Additional operations, queue, and interaction reports

| # | Report | Purpose and business value | Type and granularity | Filters; grouping; sorting | Columns/KPIs | Visualization and drill-down |
| --- | --- | --- | --- | --- | --- | --- |
| 32 | Hour-of-day performance | Identifies recurring demand and service patterns for intraday operating decisions. | Historical; one UTC hour per row. | Standard interaction filters; hour; chronological. | Standard interaction performance columns including volume, outcomes, rates, ASA, and AHT. | Table; hour → interaction detail. |
| 33 | Day-of-week performance | Compares recurring weekday workload and outcomes for staffing-pattern review. | Historical; one weekday per row. | Standard interaction filters; weekday; descending volume. | Standard interaction performance columns. | Table; weekday → interaction detail. |
| 34 | Queue performance summary | Gives floor managers one consistent comparison of workload, outcomes, ASA, and AHT by queue. | Historical; one queue per row. | Date/time, queue, agent, channel, direction; queue; descending volume. | Queue, interactions, answered, abandoned, failed, answer rate, abandonment rate, ASA, AHT. | Table; queue → interaction detail. |
| 35 | Queue wait time analysis | Quantifies customer waiting effort and queue-time consumption. | Historical; one queue per row. | Standard interaction filters; queue; descending total wait. | Queue, interactions, total wait, average wait, maximum wait. | Table; queue → high-wait detail. |
| 36 | Queue handle time analysis | Quantifies connected plus after-contact work time consumed by each queue. | Historical; one queue per row. | Standard interaction filters; queue; descending total handle time. | Queue, interactions, total handle time, average handle time, maximum handle time. | Table; queue → interaction detail. |
| 37 | Queue transfer performance | Finds queues that transfer work frequently and supports routing redesign. | Historical; one queue per row. | Standard interaction filters; queue; descending transfer volume. | Queue, handled, transferred interactions, transfer events, transfer rate. | Table; queue → transfer detail. |
| 38 | Interaction lifecycle duration | Separates wait, connected, wrap-up, and end-to-end duration by final state. | Historical; one status per row. | Standard interaction filters; status; status order. | Status, interactions, average wait, connected, wrap-up, end-to-end duration. | Table; status → interaction detail. |
| 39 | Long interaction detail | Supports cost, coaching, and exception review for sessions lasting at least 15 connected minutes. | Historical detail; one interaction per row. | Standard interaction filters; newest first. | Standard interaction detail columns. | Table; interaction → recording/transcript. |
| 40 | Failed interaction detail | Gives IT and operations an auditable list of failed communication attempts. | Historical detail; one failed interaction per row. | Standard interaction filters; newest first. | Standard interaction detail columns. | Table; interaction → provider/event history. |
| 41 | Abandoned interaction detail | Gives queue managers the source rows behind abandonment totals. | Historical detail; one abandoned interaction per row. | Standard interaction filters; newest first. | Standard interaction detail columns. | Table; interaction → queue history. |
| 42 | High-wait interaction detail | Identifies interactions with at least 60 seconds of observed wait. | Historical detail; one interaction per row. | Standard interaction filters; newest first. | Standard interaction detail columns. | Table; interaction → queue history. |

### Additional agent performance reports

| # | Report | Purpose and business value | Type and granularity | Filters; grouping; sorting | Columns/KPIs | Visualization and drill-down |
| --- | --- | --- | --- | --- | --- | --- |
| 43 | Agent interaction volume | Compares workload and supporting outcomes by user. | Historical; one agent per row. | Date/time, agent, queue, channel, direction; agent; descending handled. | Agent, handled, answered, failed, transfers, recorded, AHT. | Table; agent → interaction detail. |
| 44 | Agent outcome performance | Surfaces agents with high failed interaction volume without treating failure as a quality score. | Historical; one agent per row. | Standard interaction filters; agent; descending failures. | Agent and standard agent performance columns. | Table; agent → failed interactions. |
| 45 | Agent inbound performance | Isolates inbound workload and outcomes by agent. | Historical; one agent per row. | Standard interaction filters; agent; descending inbound volume. | Agent and standard agent performance columns. | Table; agent → inbound detail. |
| 46 | Agent outbound performance | Isolates outbound workload and outcomes by agent. | Historical; one agent per row. | Standard interaction filters; agent; descending outbound volume. | Agent and standard agent performance columns. | Table; agent → outbound detail. |
| 47 | Agent transfer performance | Supports coaching and routing review with transfer volume by agent. | Historical; one agent per row. | Standard interaction filters; agent; descending transfers. | Agent, handled, answered, failed, transfers, recorded, AHT. | Table; agent → transfer detail. |
| 48 | Agent recording coverage | Finds agent-associated answered interactions lacking recording references. | Historical; one agent per row. | Standard interaction filters; agent; descending recorded volume. | Agent and standard agent performance columns. | Table; agent → uncovered interaction. |
| 49 | Assigned user performance | Compares CRM activity progress and attempts by assigned user. | Historical; one user per row. | CRM filters; assigned user; descending activity count. | Assigned user and standard activity progress columns. | Table; user → activity detail. |
| 50 | User completion time | Measures average, median, and maximum CRM activity cycle time by assigned user. | Historical; one user per row. | CRM filters; assigned user; descending average cycle time. | User, completed, average cycle time, median cycle time, maximum cycle time. | Table; user → completed activities. |
| 51 | Daily user productivity | Shows daily completed work, attempts, and cycle time by assigned user. | Historical; one user/day row. | CRM filters; day/user; chronological. | UTC date, user, completed, average cycle time, attempts. | Table; day/user → activity detail. |
| 52 | Overdue workload by user | Supports supervisor intervention by showing overdue volume and age per owner. | Current-state as-of report; one user per row. | CRM filters; user; descending overdue count. | User, overdue, unassigned, average overdue age, maximum overdue age. | Table; user → overdue activities. |

### Workforce and payroll reports

| # | Report | Purpose and business value | Type and granularity | Filters; grouping; sorting | Columns/KPIs | Visualization and drill-down |
| --- | --- | --- | --- | --- | --- | --- |
| 53 | Agent time summary | Provides observed on-duty and state-duration inputs for workforce and payroll review. | Historical presence-duration report; one agent per row. | Date/time and agent; agent; descending signed-in time. | Agent, signed-in, available, busy, wrap-up, break, other not-ready, utilization. | Table; agent → presence audit. |
| 54 | Daily agent timecard | Provides day-level observed timecard inputs without applying schedules or pay rules. | Historical; one UTC day/agent row. | Date/time and agent; day/agent; chronological. | Date, agent, signed-in, productive presence, busy + wrap-up, break + away, first observed, last observed. | Table; day/agent → presence audit. |
| 55 | Presence status duration | Shows how signed-in time is distributed across every presence state. | Historical; one status per row. | Date/time and agent; status; descending duration. | Presence status, duration, share of signed-in time, intervals. | Donut/table; status → agent intervals. |
| 56 | Agent break and away analysis | Quantifies break frequency and duration for workforce review. | Historical; one agent per row. | Date/time and agent; agent; descending break time. | Agent, breaks, total break time, average break, longest break. | Table; agent → presence audit. |
| 57 | Ready versus not-ready time | Separates ready, actively working, and not-ready time. | Historical; one agent per row. | Date/time and agent; agent. | Agent, ready time, working time, not-ready time, ready share. | Stacked bars/table; agent → status durations. |
| 58 | Agent utilization | Measures busy plus wrap-up time as a share of all observed signed-in time. | Historical; one agent per row. | Date/time and agent; agent; descending utilization. | Agent, working time, signed-in time, utilization. | Bar/table; agent → time summary. |
| 59 | Agent occupancy | Measures busy plus wrap-up time against available handling time. | Historical; one agent per row. | Date/time and agent; agent; descending occupancy. | Agent, working time, available handling time, occupancy. | Bar/table; agent → time summary. |
| 60 | Presence reason breakdown | Quantifies time associated with configured break/not-ready reasons. | Historical; one status/reason row. | Date/time and agent; status/reason; descending duration. | Status, reason, duration, intervals. | Table; reason → presence audit. |
| 61 | Agent presence audit | Provides the durable transition ledger used to reconcile time reports. | Historical detail; one transition per row. | Date/time and agent; newest first. | Changed UTC, agent, previous/current/requested status, reason, queue count, campaign count, event. | Table; transition → event record. |
| 62 | Queue signed-in hours | Attributes observed signed-in duration to queue memberships active during each presence interval. | Historical; one queue per row. | Date/time and agent; queue; descending duration. | Queue id, signed-in time, agent intervals. | Table; queue → agent presence audit. |
| 63 | Campaign signed-in hours | Attributes observed signed-in duration to campaign memberships active during each presence interval. | Historical; one campaign per row. | Date/time and agent; campaign; descending duration. | Campaign id, signed-in time, agent intervals. | Table; campaign → agent presence audit. |
| 64 | Payroll timecard inputs | Exports observed on-duty and state-classification durations for payroll review. It intentionally does not calculate wages. | Historical; one agent per row. | Date/time and agent; agent. | Agent, observed on-duty, productive presence, break + away, meeting + training, other not-ready. | Table/export; agent → daily timecard. |

### Billing and usage reports

| # | Report | Purpose and business value | Type and granularity | Filters; grouping; sorting | Columns/KPIs | Visualization and drill-down |
| --- | --- | --- | --- | --- | --- | --- |
| 65 | Queue usage for billing | Supplies queue-level measured usage for invoice and client chargeback reconciliation. | Historical; one queue per row. | Standard interaction filters; queue; descending connected time. | Queue, interactions, answered, connected, wrap-up, queue wait, transfers, recordings. | Table/export; queue → interaction detail. |
| 66 | Agent usage for billing | Supplies agent-level measured service time for payroll and internal allocation. | Historical; one agent per row. | Standard interaction filters; agent; descending connected time. | Agent and standard usage columns. | Table/export; agent → interaction detail. |
| 67 | Provider usage for billing | Reconciles provider invoices against normalized platform usage. | Historical; one provider per row. | Standard interaction filters; provider; descending connected time. | Provider and standard usage columns. | Table/export; provider → interaction detail. |
| 68 | Channel usage for billing | Allocates measured usage across voice, SMS, email, chat, and future channels. | Historical; one channel per row. | Standard interaction filters; channel; descending connected time. | Channel and standard usage columns. | Table/export; channel → interaction detail. |
| 69 | Daily usage for billing | Supplies invoice-period daily usage totals for reconciliation. | Historical; one UTC day per row. | Standard interaction filters; day; chronological. | Date and standard usage columns. | Trend/table/export; day → interaction detail. |

### Additional CRM and campaign reports

| # | Report | Purpose and business value | Type and granularity | Filters; grouping; sorting | Columns/KPIs | Visualization and drill-down |
| --- | --- | --- | --- | --- | --- | --- |
| 70 | Activity creation by user | Audits which user or system actor created CRM work and its eventual outcomes. | Historical; one creator per row. | CRM filters; creator; descending total. | Creator and standard activity progress columns. | Table; creator → activity detail. |
| 71 | Campaign source mix | Compares campaign workload and outcomes by activity source. | Historical; one campaign/source row. | CRM filters; campaign/source; descending total. | Campaign, source, activities, completed, failed, completion rate. | Stacked table; campaign → source → activities. |
| 72 | Campaign channel mix | Compares campaign workload and outcomes by channel. | Historical; one campaign/channel row. | CRM filters; campaign/channel; descending total. | Campaign, channel, activities, completed, failed, completion rate. | Stacked table; campaign → channel → activities. |
| 73 | Campaign disposition mix | Shows campaign results by durable disposition. | Historical; one campaign/disposition display-name row. | CRM filters; campaign/disposition; descending total. | Campaign display name, disposition display name, activities, completed, failed, completion rate. | Table; campaign → disposition → activities. |
| 74 | Campaign attempt performance | Shows how campaign outcomes vary by attempt count. | Historical; one campaign/attempt row. | CRM filters; campaign/attempt; descending total. | Campaign, attempts, activities, completed, failed, completion rate. | Funnel/table; campaign → attempt → activities. |
| 75 | Channel endpoint usage | Supports technical capacity and configuration review by endpoint. | Historical; one endpoint per row. | CRM filters; endpoint; descending total. | Endpoint and standard activity progress columns. | Table; endpoint → activities. |
| 76 | Customer workload | Shows CRM work volume, outcomes, and attempts per customer record. | Historical; one customer per row. | CRM filters; customer; descending total. | Customer and standard activity progress columns. | Table; customer → CRM timeline. |
| 77 | Scheduled completion performance | Compares activities completed by their scheduled time with late completions. | Historical; one schedule-result row. | CRM filters; result; result order. | Schedule result, activities, share, average absolute variance. | KPI/table; result → activities. |

### Additional compliance and technical reports

| # | Report | Purpose and business value | Type and granularity | Filters; grouping; sorting | Columns/KPIs | Visualization and drill-down |
| --- | --- | --- | --- | --- | --- | --- |
| 78 | Transcript coverage | Finds answered interactions lacking transcript references by channel. | Historical; one channel per row. | Standard interaction filters; channel. | Channel, answered, with transcript, without reference, coverage. | Gauge/table; channel → uncovered interaction. |
| 79 | Call leg performance | Gives IT teams provider-leg volume, answer state, status, and duration. | Historical; one leg status per row. | Standard interaction filters; leg status. | Leg status, legs, answered, average duration. | Table; status → interaction/provider detail. |

## Data validation and reconciliation

Use the following acceptance dataset whenever report projections or formulas change:

1. Include inbound answered, inbound abandoned, inbound failed, and outbound answered/failed interactions.
2. Include one interaction with multiple transfers, one consultative transfer, one conference without a transfer, one callback promoted to outbound work, one queue overflow, and one requeue.
3. Include voice, chat, email, SMS, and social messaging interactions where adapters provide those channels.
4. Include completed, pending, in-progress, failed, cancelled, purged, unassigned, reserved, overdue, and multi-attempt CRM activities.
5. Reconcile interaction-detail counts to agent, queue, channel, direction, provider, and enterprise totals. Reconcile CRM activity rows to source, campaign, subject, status, and disposition totals.
6. Recalculate every rate from raw numerator and denominator counts. Never average displayed percentages.
7. Test tenant-local date selection across UTC date boundaries and daylight-saving transitions. Persist and compare source timestamps in UTC.
8. Test with paged/projection-backed queries before production use at multi-million-row scale. The current first implementation aggregates the selected cohort in tenant memory and is intended for bounded operational date ranges.

## Known data limitations

The following common report families are intentionally not emitted until their source data exists:

- **Workforce planning:** forecast accuracy, staffing requirement, schedule adherence, conformance, shrinkage, overtime, and paid-hours compliance require forecasts, schedules, employment calendars, and pay policies. The included occupancy, utilization, and timecard reports use durable observed presence only.
- **Quality management:** evaluation scorecards, calibration, coaching completion, compliance failures, and evaluator productivity require the planned quality module.
- **Customer experience:** CSAT, NPS, customer effort, and first-contact resolution require survey responses and a durable case/resolution-reopen correlation model. Disposition alone is not a valid substitute for FCR.
- **Advanced interaction analytics:** sentiment, topic, silence, interruption, script adherence, and AI summaries require transcript analytics and an enabled provider.
- **IVR journey analytics:** menu path, containment, self-service completion, and opt-out require multi-step IVR event capture.
- **Historical backlog reconstruction:** activity reports use the current persisted activity status. Backlog and aging include all currently nonterminal work created by the selected **To** boundary, but they cannot reconstruct whether an activity that is completed today was still open at a past boundary without a dedicated activity-state history projection.
- **Payroll and billing money:** reports provide measured durations and counts, but wages, premiums, contracted rates, billing increments, taxes, discounts, currency, and invoice totals require organization-specific rate and policy data that is not persisted by these modules.

These exclusions prevent plausible-looking but operationally false enterprise KPIs.
