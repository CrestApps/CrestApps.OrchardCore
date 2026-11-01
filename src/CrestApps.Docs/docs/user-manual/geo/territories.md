---
sidebar_label: Territories and Maps
title: Territories and Maps
description: "Draw the areas you serve on a map or list their postal codes, find which territory a place is in, and set up maps, geocoding and travel times."
technical_manual:
  - modules/geo
---

Territories group the places you serve, so work can go to the people who cover them. Draw a territory on the map, list its postal codes, or both. A place belongs to the smallest territory drawn around it; a place without coordinates goes by its postal code.

| | |
| --- | --- |
| **Menu** | Territories; Settings > Maps and Geocoding |
| **Permission** | Manage service territories; Manage maps and geocoding settings |
| **Feature** | Geo, Maps and Territories |

<AskYourAdmin />

## Add a territory

1. Go to **Territories** and select **Add territory**.
2. Enter a name, and choose the larger territory it is part of, if any.
3. Under **Area**, draw it with the tools on the left of the map: a rectangle or a polygon (click each corner, then the first one again). Draw more than one shape if the territory has separate parts. Drag a corner to change a shape, move it, cut a hole in it, or erase it. Other territories show in grey.
4. Under **Postal codes**, list its codes, separated by commas. End a code with `*` to take every code that starts with it, such as `021*`.
5. Choose its time zone, and the average travel speed if it differs from the site's: travel in a dense city is slower than in the countryside.
6. Select **Save**.

To paste a shape from another tool, open **The shape as GeoJSON** and paste a Polygon, MultiPolygon, Feature or FeatureCollection; it loads on the map.

Deleting a territory moves the territories inside it up to its parent.

## Find the territory of a place

On the **Territories** page, the map shows every territory. Under **Find the territory of a place**, enter a latitude and longitude, a postal code or both, and select **Find**. The page says which territory covers the place, and whether by its shape or its postal code, and marks the place on the map.

## Set up maps and geocoding

Go to **Settings > Maps and Geocoding**.

- **Geocoding** finds the coordinates of addresses, such as a team's location or a worker's home base, in the background every minute, and retries for about three hours when the provider fails. Until you choose a provider, addresses wait; the page shows how many are waiting, geocoded and not found.
- **Travel times** come from the provider you choose, or from the built-in estimate: the straight-line distance times the **road factor**, at the **average speed**.
- **Maps** use the **tile address** and **attribution** you set. The OpenStreetMap tile server is there for trying things out: a live site must use its own tiles or a commercial tile service.
