---
sidebar_label: Assets
title: Assets and Fleet
description: "Equipment, vehicles, tools and units installed at customers: types with attributes and meters, a hierarchy, warranty and meter readings; and Fleet, which books vehicles and equipment on their own calendars and keeps van stock."
user_manual:
  - user-manual/field-service/assets
---

The **Assets** module (`CrestApps.OrchardCore.Assets`, with `Assets.Abstractions` and `Assets.Core`) keeps the things a business services or uses: its own vehicles, equipment and tools, and the units installed at its customers. It references no vertical, so field service, CRM and others build on it.

The admin screens are described in the User Manual: [Assets](../user-manual/field-service/assets.md).

## Features

| Feature | Id | Depends on |
| --- | --- | --- |
| Assets | `CrestApps.OrchardCore.Assets` | — |
| Fleet | `CrestApps.OrchardCore.Assets.Fleet` | Assets, Calendars Resources |

## Records

All live in the `Assets` YesSql collection.

- **`AssetType`**: `Name`, `Category` (`Equipment`, `Vehicle`, `Tool`, `CustomerUnit`, `Component`, `Facility`), `Description`, `Attributes` (`AssetAttributeDefinition`: `Name`, `Kind` Text, Number, Date or YesNo) and `Meters` (`MeterDefinition`: `Name`, `Unit`, `Cumulative`, true by default). Two attributes or meters can't share a name.
- **`Asset`**: `Number` (the next `A-00042` when left empty, unique), `Name`, `AssetTypeId`, `ProductId`, `SerialNumber`, `Manufacturer`, `Model`, `OwnerKind` (`Internal`, `Customer`, `Company`) and `OwnerId` (required unless internal), `Location` (an `Address`, with coordinates once geocoded; a changed address loses its old ones), `ParentAssetId` (the hierarchy; an asset can't be inside itself, directly or further up), `Status` (`Active`, `InService`, `OutOfService`, `Retired`), `InstallDate`, `Warranty` (`StartDate`, `EndDate`, `Provider`, `Terms`; `Covers(date)`), `Attributes` (values of its type's attributes, by name), `Documents` (media paths) and `Fleet`.
- **`AssetMeterReading`** (only ever added): `AssetId`, `Meter`, `Value`, `Utc`, `Source` (`Manual`, `WorkOrder`, `Api`), `SourceId`, `RecordedBy`.

`AssetService.RecordReadingAsync` refuses a meter the asset's type doesn't have, and a reading of a cumulative meter lower than the last one before it. `ReadingsAsync`, `ChildrenAsync` and `AncestorsAsync` read an asset's history and hierarchy.

## Fleet

For a vehicle, equipment or tool the business owns, `AssetFleet` keeps `Schedulable`, `CalendarId`, `Plate`, `Vin`, `Capacity`, `FuelType`, `HomeBase`, `DefaultDriverWorkerId`, `CarriesStock` and `StockLocationId`.

- **Booking.** A schedulable asset gets its own calendar of the Calendars **Equipment** type, which allows one booking at a time, so the calendar refuses a visit that overlaps another. `FleetCalendarHandler` creates it when the asset is saved, finds it again by source (`Asset`, the asset's id) and keeps its name.
- **Van stock.** With Inventory on, an asset that carries stock gets its own Inventory `StockLocation`, owned by the asset (`OwnerKind` `Asset`). Parts used on a job come off it with `IInventoryService.ConsumeAsync`. A van's location never fulfills orders.

## Inventory changes

- Stock locations moved from the Inventory settings to a `StockLocation` store (`ItemId` is the location id, `Name`, `Address`, `Position`, `OwnerKind`, `OwnerId`). A migration step moves the locations kept in the settings into it once it has run, keeping their ids and their order, so stock levels and movements still point at them; until then `InventoryLocationService` also reads the settings. The settings page edits the site's own locations in the store; locations owned by other records are managed with their owner.
- `StockMovement` gains `ReferenceType` and `ReferenceId` (indexed) beside `OrderId`, and the `Consumed` kind.
- `IInventoryService.ConsumeAsync(stockItemId, locationId, quantity, referenceType, referenceId, reason, actor)` uses units up at a location, refusing more than it has available, and records what they were for.

## Permissions

| Permission | Allows |
| --- | --- |
| `ManageAssets` (Manage assets) | Asset types, assets and meter readings. |
| `ViewAssets` (View assets) | The asset list and pages. Implied by Manage assets. |
