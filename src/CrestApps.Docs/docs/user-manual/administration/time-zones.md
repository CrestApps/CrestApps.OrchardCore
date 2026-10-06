---
sidebar_label: Time Zones
title: Time Zones
description: Give time zones friendly names, set the site's default time zone, and let people use their own.
technical_manual:
  - modules/time-zones
---

Time zone menus normally list hundreds of technical names such as *America/New_York*. The **Time Zones** feature replaces them with a short list of friendly names you control, such as *Eastern Time (US & Canada)*. Use it so editors and agents pick the right zone quickly, and so every screen uses the same names.

| | |
| --- | --- |
| **Menu** | Tools > Time Zones; Settings > General (site default) |
| **Permission** | Manage time zone maps; Manage settings (site default) |
| **Feature** | Time Zones; User Time Zone (Orchard Core) for a time zone per person |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of creating a time zone map and selecting it as the site default">
  <source src="/img/docs/time-zones.mp4" type="video/mp4" />
</video>

## How the list works

Each entry on **Tools > Time Zones** is a **time zone map**: a friendly name linked to one real time zone. When the feature is on, the time zone menus show **only** the names on this list, in alphabetical order. That includes:

- **Default Time Zone** under **Settings > General**.
- A person's own time zone, when **User Time Zone** is on.
- The time zone of a contact, and the time zone menus on activity screens.

The list starts with about twenty common zones from around the world, such as *Pacific Time (US & Canada)*, *Coordinated Universal Time (UTC)*, *Central European Time* and *India Standard Time*. You can edit or delete them.

:::note[A zone is missing from a menu?]
Add a map for it. A time zone that has no map on the list cannot be picked anywhere.
:::

## Add a time zone

1. Open **Tools > Time Zones** and click **Add Time Zone Map**.
2. Enter the **Name** people will see, for example *Mexico City Time*.
3. Pick the **Time zone** it stands for, for example *America/Mexico_City*.
4. Click **Save**.

Each name can be used once, and each time zone can have only one map. The name cannot be changed after you save; to rename a map, delete it and add a new one.

The list shows each map's name, its time zone, when it was last changed and who created it. Use the **Search** box to find one, **Edit** to point a map at a different time zone, and **Delete** to remove it.

## Set the site's time zone

The site's time zone is used to show dates and times to everyone who has not picked their own.

1. Open **Settings > General**.
2. Pick a name from **Default Time Zone**.
3. Click **Save**.

## Let each person use their own time zone

When your administrator turns on the Orchard Core **User Time Zone** feature, each person can pick their own time zone on their profile, and dates and times are shown to them in that zone. People who do not pick one see the site's time zone. See the [Orchard Core Users documentation](https://docs.orchardcore.net/en/latest/reference/modules/Users/) for the details.
