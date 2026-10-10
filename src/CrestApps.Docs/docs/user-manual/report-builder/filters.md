---
sidebar_label: Filters
title: Filter the Data
description: Narrow a report's data before or after grouping, and let the people who run the report choose their own period or values.
technical_manual:
  - modules/report-builder/index
  - modules/report-builder/large-data
---

Filters decide which data a report shows. A filter can narrow the rows before they are grouped (only this year's orders) or the finished result (only customers whose total is above 1,000). You can also let the people who run the report change a filter, for example to pick their own period.

Watch the short video, then follow the steps below.

<video controls preload="metadata" width="100%" poster="/img/docs/report-builder-filters.jpg" aria-label="Video: adding filters and letting viewers change the period and values">
  <source src="/img/docs/report-builder-filters.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/report-builder-filters.vtt" srcLang="en" label="English" default />
</video>

| | |
| --- | --- |
| **Menu** | Reports > Report Builder > *a report* > Design |
| **Permission** | Build reports and manage own custom reports and views |
| **Feature** | Report Builder |

<AskYourAdmin />

## Add a filter

1. Drag a field onto **Filters**, or click the filter icon next to it in the **Data** pane.
2. Click the filter to set it in **Properties** on the right.
3. Pick a **Condition** and enter a **Value**.

| Setting | What it does |
| --- | --- |
| **Applies to** | **Rows, before grouping** filters the data itself. **Result, after grouping** filters the finished rows, for example customers whose total is above 1,000: pick the result **Column** to compare. |
| **Condition** | How the value is compared. See [Conditions](#conditions). |
| **Value** | What to compare with. A date without a time covers the whole day. For **is one of** and **is none of**, enter one value per line. For **is between**, enter **From** and **To**; leave a side empty to leave it open. |
| **Let viewers change this filter** | Shows the filter above the report so the people who run it can change it. The values you set become its defaults. |
| **Filter label** | The label viewers see. |
| **Control** | How viewers pick values. See [Let viewers change a filter](#let-viewers-change-a-filter). |

Filters combine: a row must match every filter. Filters that viewers cannot change always apply, and viewers cannot see or remove them.

To remove a filter, click the cross on it.

## Conditions

The conditions you can pick depend on the type of the field:

| Field type | Conditions |
| --- | --- |
| Text | **is**, **is not**, **is one of**, **is none of**, **contains**, **does not contain**, **starts with**, **ends with**, **is empty**, **is not empty** |
| Numbers | **is**, **is not**, **is one of**, **is none of**, **is greater than (after)**, **is at least (on or after)**, **is less than (before)**, **is at most (on or before)**, **is between**, **is empty**, **is not empty** |
| Dates and times | **is**, **is not**, **is greater than (after)**, **is at least (on or after)**, **is less than (before)**, **is at most (on or before)**, **is between**, **is in the last number of days**, **is in the next number of days**, **is empty**, **is not empty** |
| Yes or no | **is**, **is not**, **is empty**, **is not empty** |

Text is compared without regard to upper and lower case. **is empty** matches rows with no value, and text that is blank. **is in the last number of days** counts today: the last 7 days are today and the 6 days before.

## Let viewers change a filter

Check **Let viewers change this filter** to show the filter above the report. The people who run the report change it and click **Show**. The values you set are what the report opens with. A filter viewers can change is marked **Viewer** on the **Filters** shelf.

Pick the **Control** viewers use:

| Control | What viewers see | Fits |
| --- | --- | --- |
| **Automatic** | A control that suits the field and condition. | Any field |
| **Text box** | A box to type a value. | Text, numbers, dates |
| **Drop-down list** | A list of the values found in the data, to pick one, or **Any**. | Text, numbers |
| **List with several choices** | A list of the values found in the data, to pick several. | Text, numbers |
| **Recent period** | A choice of **Today**, **Last 7 days**, **Last 30 days**, **Last 90 days**, **Last 12 months** or **All time**. Its **Default period** is what the report opens with. | Dates |
| **Date range** | A start and an end date. | Dates |
| **Number range** | A smallest and a largest number. | Numbers |
| **Yes or no** | A choice of **Any**, **Yes** or **No**. | Yes or no fields |

Some controls set the condition for you: a range uses **is between**, a list with several choices uses **is one of**, and a recent period uses **is in the last number of days**. A list with more than eight values has a search box.

## The date filter a new report starts with

When you add the first data set to a new report, the builder adds a filter on its main date, such as when records were created. It is a **Recent period** that viewers can change, set to the last 30 days. Reading only that period keeps a report over a lot of data quick and complete.

- To start on another period, click the filter and change its **Default period**.
- To show all the data, pick **All time**, or remove the filter with its cross.

## Examples

| You want | Filter |
| --- | --- |
| Only open activities | *Status* **is** *Open*, applied to rows. |
| Orders of three regions | *Region* **is one of** *North*, *South* and *West*, one per line. |
| Customers who spent more than 1,000 | **Result, after grouping**: the *Sum of Total* column **is greater than** 1,000. |
| Records with no email | *Email* **is empty**. |
| A report people run for their own period | The main date with **Let viewers change this filter** and the **Recent period** control. |
| A report people run for one agent at a time | *Assigned to* with **Let viewers change this filter** and the **Drop-down list** control. |

Next: [Visuals](visuals.md).
