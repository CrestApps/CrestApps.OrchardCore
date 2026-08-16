---
sidebar_label: Addresses
sidebar_position: 16
title: Addresses
description: Country, region, and city content types with a reusable address part and content-backed country selectors for Orchard Core.
---

| | |
| --- | --- |
| **Feature Name** | Addresses |
| **Feature ID** | `CrestApps.OrchardCore.Addresses` |
| **Category** | Content Management |

Provides address-related content types and reusable parts. The module turns the canonical ISO 3166-1 country list into editable content so administrators can manage countries, regions, and cities through the standard Orchard Core content management experience, and reuse them across checkout, taxation, and any other address-aware feature.

## Overview

Rather than shipping a hardcoded, read-only list, the Addresses feature seeds the canonical country list as regular content items. Administrators can then rename, extend, secure, and localize the entries like any other content, and build regions and cities on top of them.

The feature is built on a small design principle:

> **Addresses are content, not configuration.**

## Content types

| Type | Purpose |
| --- | --- |
| **Country** | A country carrying an ISO 3166-1 alpha-2 `Code`. Seeded on enable from the canonical list. |
| **Region** | A state, province, or region that references its parent country and stores an optional abbreviation. |
| **City** | A city that references its parent region. |

Each type includes a `TitlePart` (with the unique-title editor) for the display name and is `Creatable`, `Listable`, and `Securable`.

## Reusable parts

| Part | Purpose |
| --- | --- |
| **CountryPart** | Holds the ISO alpha-2 `Code` for a country. |
| **RegionPart** | Holds the parent country selector and abbreviation for a region. |
| **CityPart** | Holds the parent region selector for a city. |
| **AddressPart** | A reusable, attachable part capturing street lines, city, postal code, and optional region and country selectors. Attach it to any content type that needs a postal address. |

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

Taxation stores the ISO alpha-2 country code on jurisdictions and rules, not a content item id. Enabling or disabling the Addresses feature therefore never orphans existing tax data: the stored codes keep resolving against both the content-backed and default country services.

## Dependencies

- `OrchardCore.Contents`
- `OrchardCore.ContentFields`
- `OrchardCore.Title`
