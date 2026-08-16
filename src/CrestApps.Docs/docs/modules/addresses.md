---
sidebar_label: Addresses
sidebar_position: 16
title: Addresses
description: Country, region, county, city, and district content types with a reusable address part and content-backed country selectors for Orchard Core.
---

| | |
| --- | --- |
| **Feature Name** | Addresses |
| **Feature ID** | `CrestApps.OrchardCore.Addresses` |
| **Category** | Content Management |

Provides address-related content types and reusable parts. The module models every geographic component of an address &mdash; except the postal code &mdash; as managed content, so administrators can maintain countries, regions, counties, cities, and districts through the standard Orchard Core content management experience, and reuse them across checkout, taxation, and any other address-aware feature.

## Overview

Rather than shipping a hardcoded, read-only list, the Addresses feature seeds the canonical country list as regular content items. Administrators can then rename, extend, secure, and localize the entries like any other content, and build regions, counties, cities, and districts on top of them.

The feature is built on a small design principle:

> **Addresses are content, not configuration.**

Only the postal code stays as free text; every other component of an address is a content item that can be reused and referenced.

## Content types

| Type | Purpose |
| --- | --- |
| **Country** | A country carrying an ISO 3166-1 alpha-2 `Code`. Seeded on enable from the canonical list. |
| **Region** | A state, province, or region that references its parent country and stores a `Code`. |
| **County** | A county that references its parent region and stores an optional `Code`. |
| **City** | A city that references its parent region, an optional county, and stores an optional `Code`. |
| **District** | A special or tax district that references its parent city and stores an optional `Code`. |

Each type includes a `TitlePart` (with the unique-title editor) for the display name and is `Creatable`, `Listable`, and `Securable`.

## Reusable parts

| Part | Purpose |
| --- | --- |
| **CountryPart** | Holds the ISO alpha-2 `Code` for a country. |
| **RegionPart** | Holds the parent country selector and `Code` for a region. |
| **CountyPart** | Holds the parent region selector and `Code` for a county. |
| **CityPart** | Holds the parent region selector, optional county selector, and `Code` for a city. |
| **DistrictPart** | Holds the parent city selector and `Code` for a district. |
| **AddressPart** | A reusable, attachable part capturing street lines and postal code as text, and country, region, county, city, and district as content-item selectors. Attach it to any content type that needs a postal address. |

Every geographic part standardizes on a money-safe `Code` field. The code is the value that flows into the flat `Address` model consumed by taxation and checkout; when a code is empty, the component's display name is used instead.

## Resolving an address

Because the geographic components of an `AddressPart` are content-item selectors, the module ships an `IAddressResolver` that reduces the part to the flat, money-safe `Address` model. Each selector is loaded and reduced to its stable `Code` (falling back to its display name), and the postal code is copied verbatim. Taxation, Checkout, and Subscriptions therefore keep operating on the string-based `Address` contract and never store a content item id.

```csharp
public interface IAddressResolver
{
    ValueTask<Address> ResolveAsync(ContentItem contentItem);
}
```

## Country selectors

When the feature is enabled it registers a content-backed `ICountryService`. Country dropdowns across the platform &mdash; for example the taxation jurisdiction editor &mdash; are populated from the managed `Country` content items and indexed by `CountryIndex` for efficient lookup. When no country content exists yet, the service falls back to the canonical ISO list so selectors are never empty.

When the feature is disabled, the default `ICountryService` returns the canonical ISO list, so dependent features keep working without any address content.

```csharp
public interface ICountryService
{
    ValueTask<IReadOnlyList<CountryInfo>> GetCountriesAsync();
}
```

## Money safety

Taxation stores stable string codes (the ISO alpha-2 country code and the region, county, city, and district codes) on jurisdictions and rules, not a content item id. The `IAddressResolver` maps every `AddressPart` selector to those same codes, so tax matching stays deterministic. Enabling or disabling the Addresses feature therefore never orphans existing tax data: the stored codes keep resolving against both the content-backed and default services.

## Dependencies

- `OrchardCore.Contents`
- `OrchardCore.ContentFields`
- `OrchardCore.Title`
