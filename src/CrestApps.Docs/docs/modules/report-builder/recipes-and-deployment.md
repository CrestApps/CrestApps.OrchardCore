---
sidebar_label: Recipes and Deployment
title: Report Builder Recipes and Deployment
description: Move designed reports and views between sites with the Designed Reports and Views deployment step and the ReportDesigns recipe step.
user_manual:
  - user-manual/report-builder/index
---

Designed reports and reusable views move between sites with a deployment plan and a recipe step.

## Export with a deployment plan

Add the **Designed Reports and Views** step to a deployment plan to export every designed report and view. Each item is written with its owner's user name, and share links are never exported.

## Import with the `ReportDesigns` recipe step

The `ReportDesigns` recipe step imports them: views first, then reports. Items are matched by `itemId` and replaced, or created with that id, and the owner is matched by `OwnerUserName` when a user with that name exists. An item without a valid `itemId` or a title is reported as a recipe error. Designs are not checked against the target site's data sources on import.

```json
{
  "steps": [
    {
      "name": "ReportDesigns",
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
          "category": "Sales",
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

Enum values are written by name (for example `"aggregate": "Sum"` or `"type": "Chart"`). Export a plan from a site where the report works to get the full query shape.

Importing a view whose query changed, or that is live, drops its stored rows, so a scheduled view runs live until its next refresh (see [Scheduled views](large-data.md#scheduled-views)). A design that references a data source or data set the target site does not have imports, but reports the problem when it runs; enable the same features on both sites.
