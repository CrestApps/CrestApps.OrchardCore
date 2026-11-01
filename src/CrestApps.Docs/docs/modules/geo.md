---
sidebar_label: Geo
title: Geo, Maps and Territories
description: "Geography for every module that works with places: points, geocoding in the background with retries, travel times with a built-in estimate, service territories by shape or postal code, and a Leaflet map component."
user_manual:
  - user-manual/geo/territories
---

The **Geo** module (`CrestApps.OrchardCore.Geo`) gives modules a shared way to work with places. It references no vertical module, so field service, CRM, commerce or classifieds can all build on it. Its contracts are in `CrestApps.OrchardCore.Geo.Abstractions` and its services in `CrestApps.OrchardCore.Geo.Core`.

The admin screens are described in the User Manual: [Territories and Maps](../user-manual/geo/territories.md).

## Features

| Feature | Id | Depends on |
| --- | --- | --- |
| Geo, Maps and Territories | `CrestApps.OrchardCore.Geo` | CrestApps Resources |

## Points and providers

`GeoPoint` is a WGS84 latitude and longitude (`IsValid`, `GeoPoint.From(lat, lng)`). Three provider seams are chosen by name in the site settings:

| Contract | Does |
| --- | --- |
| `IGeocodingProvider` | `GeocodeAsync(GeocodeQuery)` returns `GeocodeCandidate`s (point, confidence, formatted address, postal code, country); `ReverseGeocodeAsync(GeoPoint)` does the reverse. |
| `ITravelTimeProvider` | `GetMatrixAsync(origins, destinations, departureUtc, TravelOptions)` returns a `TravelMatrix` of `TravelLeg`s (duration and meters). |
| `IRouteProvider` | `GetRouteAsync(points)` returns a `GeoRoute` to draw. |

`IGeoService` is what modules call. It uses the configured providers and:

- **Geocoding** throws the provider's errors so callers can retry, and returns a response with no provider when none is set up.
- **Travel times** are cached in the distributed cache (points rounded to about 10 meters, departures to the quarter hour, kept for **Keep travel times**) and fall back to the built-in estimate when the provider is missing or fails.
- **Routes** fall back to straight lines.

The built-in **Haversine** provider (`HaversineTravelTimeProvider`) is always registered: the great-circle distance (`GeoMath.DistanceMeters`) times the **road factor** (1.3), at the **average speed** (40 km/h) or `TravelOptions.SpeedKmh`, such as a territory's.

## Service territories

A `ServiceTerritory` has a name, an optional parent, a shape (GeoJSON Polygon or MultiPolygon, as a geometry, Feature or FeatureCollection), postal codes (a code ending with `*` matches every code that starts with the rest), a time zone, a travel speed and `Members` (teams or people by the identifiers of the modules that own them, Primary or Secondary). The handler tidies postal codes, checks the shape can be read (`GeoShape.TryParse`) and refuses a territory inside itself.

`ITerritoryResolver.ResolveAsync(point, postalCode)` (and the pure `TerritoryMatcher.Match`) finds the territory with the smallest shape the point is in; otherwise the territory with the postal code, an exact code before the longest prefix. The `TerritoryMatch` says which matched.

## Geocoding in the background

`IGeocodeQueue.EnqueueAsync(targetType, targetId, query)` keeps one `GeocodeRequest` per record, replacing an earlier one. The **Geocode addresses** background task runs every minute (`ProcessDueAsync`):

- A place found is written back through the record type's `IGeocodeTarget`, best candidate first.
- A provider error is retried after 1, 5, 30 and 120 minutes, then given up after five attempts.
- No match, or a record that is gone, is given up at once.
- With no provider set up, requests wait an hour at a time without counting an attempt.

The settings page shows how many addresses are waiting, geocoded and not found.

**Workforce** registers targets when Geo is on: a team's location and a worker's home base are geocoded when they have an address and no coordinates. Changing a home base address clears its old coordinates.

## Addresses

`Address` gains optional `Latitude`, `Longitude`, `GeocodedUtc` and `GeocodeSource` (a provider's name, or `Manual` when pinned), copied by `Clone`, for the modules that geocode their addresses.

## Map component

CrestApps Resources vendors Leaflet 1.9.4, Leaflet.markercluster 1.5.3 and Leaflet-Geoman free 2.20.4 (all MIT) as the `leaflet`, `leaflet-markercluster` (with `leaflet-markercluster-default`) and `leaflet-geoman` scripts and styles, and the `crestapps-map` wrapper:

```javascript
const map = window.crestappsMap.create('my-map');           // reads data-tile-url, data-attribution, data-latitude, data-longitude, data-zoom
window.crestappsMap.showShapes(map, [{ label, geoJson }]);   // read-only shapes, fitted
window.crestappsMap.addMarkers(map, [{ latitude, longitude, label }], { fit: true });
window.crestappsMap.pin(map, latitudeInput, longitudeInput); // a draggable pin that writes two inputs
window.crestappsMap.editShapes(map, textarea);              // Geoman drawing kept as a GeoJSON FeatureCollection
```

The Geo module's `_GeoMap` partial renders a map element with the site's tile settings and the scripts. Tiles are requested with the site's origin as referrer, which tile servers such as OpenStreetMap's require.

## Settings

**Settings > Maps and Geocoding** (`GeoSettings`, permission **Manage maps and geocoding settings**): the geocoding, travel-time and route providers, the built-in estimate's average speed and road factor, how long travel times are cached, the tile address and attribution, and where maps open. The default tile server is OpenStreetMap's, for trying things out only: a live site must use its own tiles or a commercial tile service, under the OpenStreetMap tile usage policy.

## Permissions

| Permission | Allows |
| --- | --- |
| `ManageGeoTerritories` (Manage service territories) | The territory list, map, finder and editor. |
| `ManageGeoSettings` (Manage maps and geocoding settings) | The Maps and Geocoding settings. |
