# CrestApps.OrchardCore.Addresses

Provides address-related content types and reusable parts for Orchard Core. The module turns the canonical
ISO 3166-1 country list into editable content so administrators can manage countries, regions, and cities
through the standard content management experience, and reuse them across checkout, taxation, and any other
address-aware feature.

## Features

- **Country content type** – seeded on enable from the canonical ISO 3166-1 list. Each country carries an
  ISO alpha-2 `Code` used for money-safe matching in taxation and checkout.
- **Region content type** – a state, province, or region that references its parent country and stores an
  optional abbreviation.
- **City content type** – a city that references its parent region.
- **Address part** – a reusable, attachable part that captures street lines, city, postal code, and optional
  region and country selectors. Attach it to any content type that needs a postal address.

## Country selectors

When this feature is enabled it registers a content-backed `ICountryService`. Country dropdowns across the
platform (for example the taxation jurisdiction editor) are then populated from the managed `Country` content
items. When no country content exists yet, the service falls back to the canonical ISO list so selectors are
never empty.

When the feature is disabled, the default `ICountryService` implementation returns the canonical ISO list, so
dependent features keep working without any address content.

## Data storage

Country content items are indexed by `CountryIndex` (ISO code and display name) for efficient lookup. Taxation
continues to store the ISO alpha-2 country code, so enabling or disabling this module never orphans existing
tax jurisdictions or rules.

## Dependencies

- `OrchardCore.Contents`
- `OrchardCore.ContentFields`
- `OrchardCore.Title`
