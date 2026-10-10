---
sidebar_label: Reusable Views
title: Reuse Data with Views
description: Save a prepared data set once - joined, filtered and calculated - and use it in many reports, optionally refreshed on a schedule.
technical_manual:
  - modules/report-builder/large-data
---

A view saves a prepared data set (joined, filtered and calculated) so other reports don't have to repeat that work. For example, a *Customers with orders* view joins customers to their orders and works out each order's profit once; every sales report then starts from the view.

Watch the short video, then follow the steps below.

<video controls preload="metadata" width="100%" poster="/img/docs/report-builder-views.jpg" aria-label="Video: creating a reusable view, scheduling its refresh, and using it in a report">
  <source src="/img/docs/report-builder-views.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/report-builder-views.vtt" srcLang="en" label="English" default />
</video>

| | |
| --- | --- |
| **Menu** | Reports > Report Views |
| **Permission** | Build reports and manage own custom reports and views |
| **Feature** | Report Builder |

<AskYourAdmin />

## Create a view

1. Open **Reports > Report Views** and click **Add View**.
2. On the **Settings** tab, enter a **View name** (other reports list the view by this name) and a **Description** that tells other report builders what the view prepares.
3. Design it like a report on the **Design** and **Data model** tabs: add data sets, joins, calculated fields, filters and columns. The columns are what reports can use.
4. Click **Save**.

A view has no visuals, no sharing, no drafts and no versions: it is a building block for reports. Every report builder can use every view; only its owner, and the people who manage every report, can change or delete it.

## Use a view in a report

In any report, click **Add data set**, pick **Report views** as the data source, and pick your view. Its columns appear as fields. You can join a view to other data sets, filter it, and add more calculated fields, like any other data set.

A view can also read other views. A view cannot use itself, directly or through other views, and a view that a report or another view uses cannot be deleted.

## Refresh a view on a schedule

A view runs every time a report reads it. For a view over a lot of data, pick a schedule under **Refresh** on its **Settings** tab: **Live** (the default), **Every 15 minutes**, **Every hour**, **Every 6 hours** or **Every day**.

- A scheduled view's result is stored, and reports read it quickly, as of the last refresh.
- The **Settings** tab shows when the view last refreshed and how many rows it holds. Click **Refresh now** to update it at once.
- Until its first refresh, and after you change its design, reports run the view live.
- If a refresh fails, reports keep reading the rows of the last successful refresh, and the view says **The last refresh failed**.

The **Report Views** list shows each view's schedule and when it last refreshed.

:::warning[A scheduled view reads data with its owner's access]
A scheduled view refreshes on its own, with the access of the person who owns it. Everyone who uses the view sees what the owner may see. Schedule only views whose data everyone who can use the view may see.
:::

Next: [Share and run reports](share-and-run.md).
