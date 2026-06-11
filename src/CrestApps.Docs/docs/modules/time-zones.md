---
sidebar_label: Time Zones
sidebar_position: 7
title: Time Zones Feature
description: Friendly named time zone maps and grouped time zone selection for Orchard Core.
---

| | |
| --- | --- |
| **Feature Name** | Time Zones |
| **Feature ID** | `CrestApps.OrchardCore.TimeZones` |

Provides friendly named time zone maps so editors can pick labels like `Eastern Time (US & Canada)` instead of scanning the full Orchard Core IANA time zone list.

## Overview

The module adds:

- an admin UI under **Tools -> Time Zones**
- a catalog-backed store of unique named time zone maps
- an `ITimeZoneSelectListProvider` override that replaces Orchard Core's default time zone select-list implementation
- recipe import support through `TimeZoneMaps`
- deployment export support for time zone maps
- seeded starter mappings for common worldwide time zones

Because this module overrides Orchard Core's `ITimeZoneSelectListProvider`, enabling **Time Zones** changes the time zone menus Orchard Core renders anywhere that service is used. Consumers should resolve `ITimeZoneSelectListProvider` instead of building their own time zone list so the user always sees the mapped, human-friendly names.

Each map stores:

- a unique **Name** shown to editors
- a **TimeZoneId** value stored in Orchard Core data
- **Author**, **OwnerId**, **CreatedUtc**, and **ModifiedUtc** metadata for admin auditing

## Admin management

Enable the feature, then open **Configuration -> Time Zones**.

Create one map entry for each friendly label you want to expose. Names are unique and immutable after creation. The admin list shows the mapped `TimeZoneId`, the author display name, and the latest created or modified timestamp as badges so editors can scan changes quickly.

## Recipe support

Use the `TimeZoneMaps` step to create or update maps:

```json
{
  "name": "TimeZoneMaps",
  "Maps": [
    {
      "Name": "Eastern Time (US & Canada)",
      "TimeZoneId": "America/New_York",
      "OwnerId": "[js: parameters('AdminUserId')]",
      "Author": "[js: parameters('AdminUsername')]"
    },
    {
      "Name": "India Standard Time",
      "TimeZoneId": "Asia/Kolkata",
      "OwnerId": "[js: parameters('AdminUserId')]",
      "Author": "[js: parameters('AdminUsername')]"
    }
  ]
}
```

Recipe imports update existing entries by `ItemId` when provided, then fall back to the unique `Name`. The step also accepts `Author`, `OwnerId`, `CreatedUtc`, and `ModifiedUtc` so seeded or deployed entries can preserve audit metadata.

## Deployment support

When **OrchardCore.Deployment** is enabled, deployment plans can export all time zone maps or a selected subset. The exported plan uses the same `TimeZoneMaps` recipe step shape, so it can be imported directly into another tenant.

## Seeded starter maps

The initial migration runs an embedded partial recipe through Orchard Core's recipe executor and creates a starter set of common worldwide mappings. The seed recipe sets `OwnerId` from `parameters('AdminUserId')`, `Author` from `parameters('AdminUsername')`, and shares a single `utcNow()` value through recipe variables so all seeded entries keep consistent audit metadata. The starter mappings include:

- Pacific, Mountain, Central, Eastern, and Atlantic North American zones
- UTC, Western European, Central European, and Eastern European zones
- India, China, Japan, Gulf, Australia Eastern, and New Zealand zones

You can edit or delete these entries after the feature is enabled.
