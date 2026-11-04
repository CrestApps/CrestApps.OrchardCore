---
sidebar_label: Recipes and Deployment
title: Report Builder Recipes and Deployment
description: Move designed reports, report groups and views between sites with the Designed Reports and Views deployment step and the ReportDesigns recipe step.
user_manual:
  - user-manual/report-builder/index
---

Designed reports, the report groups they are listed under, and reusable views move between sites with a deployment plan and a recipe step.

## Export with a deployment plan

Add the **Designed Reports and Views** step to a deployment plan. With **Include all reports and views** on (the default), it exports every designed report and view, including the ones created after the plan. Turn it off to pick the reports and views to export: the views the picked reports and views read are exported with them, so they run on the other site. Report groups are exported too: every group with all the reports, or with picked reports the groups they are in and the groups those are nested in, parents first.

Each item is written with its owner's user name, and share links are never exported. A plan saved before reports could be picked keeps exporting all of them.

## Import with the `ReportDesigns` recipe step

<YouTube video="report-builder-administration" title="Video of importing reports with Package Import" start={102} end={145} />

To import a file that holds the step, open **Tools > Deployments > JSON Import** and paste the JSON, or **Package Import** and choose the file (a `.json` or `.zip` file). Importing the same file again replaces the same reports.

The `ReportDesigns` recipe step imports them: report groups first (`Groups`), then views, then reports. Items are matched by `itemId` and replaced, or created with that id, and the owner is matched by `OwnerUserName` when a user with that name exists. An item that names no owner the site knows, and has no `ownerId`, belongs to the person who runs the recipe, so a recipe can ship ready-made reports for any site; run at setup, with nobody signed in, it has no owner. An item without a valid `itemId` or a title is reported as a recipe error. Designs are not checked against the target site's data sources on import.

```json
{
  "steps": [
    {
      "name": "ReportDesigns",
      "Groups": [
        { "itemId": "sales", "displayText": "Sales", "order": 1 },
        { "itemId": "sales-regional", "displayText": "Regional", "parentId": "sales" }
      ],
      "Views": [
        {
          "itemId": "4z1f5b0c6t0m9z2k8w3d7r5q1v",
          "displayText": "Revenue by region",
          "query": { "dataSets": [], "columns": [] },
          "OwnerUserName": "admin"
        }
      ],
      "Reports": [
        {
          "itemId": "4z1f5b0c6t0m9z2k8w3d7r5q1r",
          "displayText": "Top customers",
          "groupId": "sales-regional",
          "showInAdminMenu": true,
          "query": { "dataSets": [], "columns": [] },
          "visuals": [],
          "sharedRoles": [ "Sales" ],
          "OwnerUserName": "admin"
        }
      ]
    }
  ]
}
```

A group is matched by `itemId` like the other items. Its `parentId` may name a group that comes later in the step; a group whose parent is missing, or whose parents lead back to itself, is imported at the top level. A report names its group in one of three ways:

| Property | Meaning |
| --- | --- |
| `groupId` | The id of a group in the step or on the site. |
| `group` | A path of group names, such as `"Store"` or `"Sales / Regional"`. Each group is found by name, ignoring case, under the one before it, and created when missing. Ready-made reports use it, so they share the site's group of that name. |
| `category` | Written by sites from before report groups: the name of a top-level group, found or created the same way. |

A report whose `groupId` is unknown, and that has no `group` or `category`, is imported in no group.

Enum values are written by name (for example `"aggregate": "Sum"` or `"type": "Chart"`). Export a plan from a site where the report works to get the full query shape.

Importing a view whose query changed, or that is live, drops its stored rows, so a scheduled view runs live until its next refresh (see [Scheduled views](large-data.md#scheduled-views)). A design that references a data source or data set the target site does not have imports, but reports the problem when it runs; enable the same features on both sites.

## Ready-made reports to adapt

These recipes turn on the Report Builder and add reports, in the **Store** report group, that show what a module's built-in reports show, built from its data sources. Open one in the builder, change it, and save your own copy.

| Recipe | Reports | Module |
| --- | --- | --- |
| **Reports: store reports to adapt** (`CommerceReportBuilderReports`) | Sales by day, Sales by product, Sales by country, Tax by jurisdiction, Discounts on orders, Refunds, Disputes, Orders to ship | [Orders](../carts-and-orders.md#store-reports-to-adapt) |
| **Reports: inventory reports to adapt** (`InventoryReportBuilderReports`) | Stock on hand, Stock movements | [Inventory](../inventory.md#reports-to-adapt) |
| **Reports: product review reports to adapt** (`ReviewsReportBuilderReports`) | Reviews by stars, Reviews by product | [Reviews](../reviews.md#reports-to-adapt) |
| **Reports: download reports to adapt** (`DownloadsReportBuilderReports`) | Downloads | [Downloads](../downloads.md#reports-to-adapt) |
| **Reports: subscription reports to adapt** (`SubscriptionsReportBuilderReports`) | New subscriptions, Expiring subscriptions | [Subscriptions](../subscriptions.md#reports-to-adapt) |
| **Reports: shipping reports to adapt** (`ShippingReportBuilderReports`) | Shipments by carrier | [Shipping](../shipping.md#reports-to-adapt) |
