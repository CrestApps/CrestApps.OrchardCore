---
sidebar_label: Shipping
title: Shipping
description: Shipping zones by country and region, the methods each zone offers, the rate providers that price them, and product shipping data.
user_manual: user-manual/commerce/shipping
---

| | |
| --- | --- |
| **Feature** | `CrestApps.OrchardCore.Shipping` |
| **Core** | `CrestApps.OrchardCore.Shipping.Core` |
| **Category** | Commerce |
| **Depends on** | Commerce, Products, Addresses, `OrchardCore.Contents` |

The **Shipping** feature prices shipping. Zones say where; each zone's methods say how and what it costs, through rate providers. Products carry their weight and shipping class in the **Shipping** part.

## Model

- **`ShippingZone`** (`INamedCatalog<ShippingZone>`, one document): `Name`, `Position`, `Countries` (ISO 3166-1 alpha-2), `Regions` (ISO 3166-2, such as `US-AK`), `RestOfWorld` and `Methods`. A country with no region in `Regions` is covered whole.
- **`ShippingMethod`** (an `Entity`, inside its zone): `MethodId`, `Name`, `Description`, `ProviderName`, `Enabled`, `Position`, `Currency` (empty for any), `TaxCategoryCode` and `ClassSurcharges` (per unit, by class, matched ignoring case). The provider's settings are in the method's properties: `FlatRateSettings` (`Amount`, `PerItemAmount`), `TableRateSettings` (`Basis`: `Weight`, `Subtotal` or `Quantity`; `Rows` of `From` and `Amount`; `Maximum`) and `FreeShippingSettings` (`MinimumSubtotal`).
- **`ShippingPart`**: `Weight` (one unit, in the site unit), `ShippingClass`, and `VariantWeights` by variant id. `GetWeight(variantId)` falls back to the product's weight, then to zero.
- **`ShippingSettings`** (group `shipping`): `WeightUnit` (`Kilogram` or `Pound`) and `ShippingClasses`.

## Rates

- **`IShippingRateService`**
  - `FindZoneAsync(address)` returns the first zone by `Position` (then name) that covers the address, else the `RestOfWorld` zone, else `null`.
  - `GetRatesAsync(ShippingRateRequest)` asks the provider of each enabled method of that zone, in order. Methods in another currency, or whose provider is missing (logged), are skipped. Class surcharges are added, and amounts are rounded to the currency and never negative.
- **`ShippingRateRequest`**: `Destination` (`Address`), `Currency`, and `Lines` of `ShippingRequestLine` (`ContentItemId`, `VariantId`, `Title`, `Quantity`, `UnitPrice`, `Weight` per unit, `ShippingClass`), with `Subtotal`, `TotalWeight` and `ItemCount`.
- **`ShippingRate`**: `RateId` (the method id, then `:option` for a provider with several), `ZoneId`, `MethodId`, `ProviderName`, `Name`, `Description`, `Amount`, `Currency` and `TaxCategoryCode`.
- **`IShippingRateProvider`**: `Name`, `DisplayName`, `Description`, and `GetRatesAsync(ShippingRateContext)` returning none, one or several rates. `ShippingRateContext` has the `Zone`, `Method` and `Request`, and `CreateRate(amount, option, name)`.
  - Built in: `Flat`, `Table` (the row with the largest `From` at or below the value; none below the first row or above `Maximum`) and `Free` (from `MinimumSubtotal`).
  - A carrier module registers its own provider, and a `DisplayDriver<ShippingMethod>` for its settings that renders only for methods with its `ProviderName`.

## Admin

- **Commerce > Shipping** (`shipping/zones`, `ShippingZonesController`) lists zones in the order they are tried. A zone's page edits it and lists its methods; **Add method** opens the method editor for a provider (`shipping/zones/{zoneId}/methods/create/{providerName}`).
- The method editor is `IDisplayManager<ShippingMethod>`: `ShippingMethodDisplayDriver` for the common fields, and one driver per provider.
- **Try rates** (`shipping/rates`) runs `IShippingRateService` for an address and an order described by subtotal, weight, items and class.

## Permissions

| Permission | Allows |
| --- | --- |
| **Manage shipping zones, methods and settings** (`ManageShipping`) | Commerce > Shipping and Settings > Commerce > Shipping (registered for the `shipping` settings group). |

Administrators have it by default.

## Recipes and deployments

The **ShippingZones** step creates or updates zones, found by `ItemId` or `Name`. Methods without a `MethodId` get one. Provider settings go in the method's `Properties`, keyed by type name:

```json
{
  "name": "ShippingZones",
  "Zones": [
    {
      "Name": "United States",
      "Countries": ["US"],
      "Methods": [
        { "Name": "Standard", "Description": "3 to 5 business days", "ProviderName": "Flat", "Properties": { "FlatRateSettings": { "Amount": 5, "PerItemAmount": 1 } } },
        { "Name": "Free over $100", "ProviderName": "Free", "Properties": { "FreeShippingSettings": { "MinimumSubtotal": 100 } } }
      ]
    },
    { "Name": "Everywhere else", "RestOfWorld": true, "Position": 99, "Methods": [ { "Name": "International", "ProviderName": "Table", "Properties": { "TableRateSettings": { "Basis": 0, "Rows": [ { "From": 0, "Amount": 20 }, { "From": 2, "Amount": 35 } ] } } } ] }
  ]
}
```

The **Shipping Zones** deployment step (*Commerce*) exports every zone with its methods.

## Coming next

A shipping step at checkout with a taxed shipping line and the shipping address as the tax destination, then shipments.
