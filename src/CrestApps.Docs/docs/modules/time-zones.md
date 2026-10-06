---
sidebar_label: Time Zones
sidebar_position: 7
title: Time Zones Feature
description: Friendly named time zone maps and grouped time zone selection for Orchard Core.
user_manual:
  - user-manual/administration/time-zones
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

The **Tools** -> **Time Zones** screen requires the `ManageTimeZoneMaps` permission (*Manage time zone maps*), granted
to the **Administrator** role by default. Names are unique and immutable after creation, and each `TimeZoneId` can be
mapped only once. The admin list shows the mapped `TimeZoneId`, the author display name, and the latest created or
modified timestamp as badges.

Once the feature is enabled, `ITimeZoneSelectListProvider` returns only the mapped entries, ordered by name. Every
menu built from it, including the Orchard Core site **Default Time Zone** setting and the **User Time Zone**
profile setting, therefore offers only mapped zones; an unmapped `TimeZoneId` cannot be selected until a map is added.
The Omnichannel contact and activity screens use the same provider.

How administrators add maps and set the site and user time zones is described in the User Manual:
[Time Zones](../user-manual/administration/time-zones.md).

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

The deployment-plan editor groups the **TimeZoneMaps** export step under the **Infrastructure** category.

## Seeded starter maps

The initial migration runs an embedded partial recipe through Orchard Core's recipe executor and creates a starter set of common worldwide mappings. The seed recipe sets `OwnerId` from `parameters('AdminUserId')`, `Author` from `parameters('AdminUsername')`, and shares a single `utcNow()` value through recipe variables so all seeded entries keep consistent audit metadata. The starter mappings include:

- North America: Alaska, Hawaii, Pacific, Mountain, Central and Eastern Time (US & Canada), and Atlantic Time (Canada)
- South America: Brasilia Time
- Coordinated Universal Time (UTC)
- Europe: Western, Central and Eastern European Time, and Moscow Standard Time
- Middle East and Asia: Jerusalem, Gulf Standard, India Standard, China Standard, Singapore and Japan Standard Time
- Oceania: Australian Eastern Time and New Zealand Time

You can edit or delete these entries after the feature is enabled.
