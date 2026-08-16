# CrestApps.OrchardCore.Addresses

Provides address-related content types and reusable parts for Orchard Core. The module models every geographic
component of an address &mdash; except the postal code &mdash; as managed content, so administrators can maintain
countries, regions, counties, cities, and districts through the standard content management experience, and reuse
them across checkout, taxation, and any other address-aware feature.

## Features

- **Country content type** – seeded on enable from the canonical ISO 3166-1 list. Each country carries an
  ISO alpha-2 `Code` used for money-safe matching in taxation and checkout.
- **Region content type** – a state, province, or region that references its parent country and stores a `Code`.
- **County content type** – a county that references its parent region and stores an optional `Code`.
- **City content type** – a city that references its parent region, an optional county, and stores an optional `Code`.
- **District content type** – a special or tax district that references its parent city and stores an optional `Code`.
- **Address part** – a reusable, attachable part that captures street lines and postal code as text, and country,
  region, county, city, and district as content-item selectors. Attach it to any content type that needs a postal
  address.

Every geographic part standardizes on a money-safe `Code` field, and each type references its parent so the full
Country → Region → County → City → District hierarchy can be managed and reused.

## Resolving an address

Because the geographic components of an `AddressPart` are content-item selectors, the module registers an
`IAddressResolver` that reduces the part to the flat, money-safe `Address` model. Each selector is resolved to its
stable `Code` (falling back to its display name) and the postal code is copied verbatim, so taxation, checkout, and
subscriptions keep working against the string-based `Address` contract without ever storing a content item id.

## Country selectors

When this feature is enabled it registers a content-backed `ICountryService`. Country dropdowns across the
platform (for example the taxation jurisdiction editor) are then populated from the managed `Country` content
items. When no country content exists yet, the service falls back to the canonical ISO list so selectors are
never empty.

When the feature is disabled, the default `ICountryService` implementation returns the canonical ISO list, so
dependent features keep working without any address content.

## Data storage

All geographic content items (country, region, county, city, and district) are indexed by the shared
`GeographicAreaIndex` (content type, ISO/money-safe code, parent reference, and display name) for efficient
lookup. Taxation
continues to store stable string codes (ISO alpha-2 country code, plus region, county, city, and district codes),
so enabling or disabling this module never orphans existing tax jurisdictions or rules.

## Dependencies

- `OrchardCore.Contents`
- `OrchardCore.ContentFields`
- `OrchardCore.Title`
