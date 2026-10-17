# CrestApps.OrchardCore.Addresses

One address structure for any country. Every module holds the same immutable `Address` value
(`CrestApps.OrchardCore.Addresses.Abstractions`), and each country's rules decide which parts it has, which are
required, and how it is checked and printed.

## Features

- **Addresses** (`CrestApps.OrchardCore.Addresses`): registers `IAddressFormatProvider`, `IAddressValidator` and
  `IAddressFormatter` from `CrestApps.OrchardCore.Addresses.Core`, with the formats of every country built from
  Google's address data (CC BY 4.0, see that package's `THIRD-PARTY-NOTICES.md`). Countries come from the ISO list.
  It needs no content features.
- **Address Countries** (`CrestApps.OrchardCore.Addresses.Countries`): keeps the countries the site serves as `Country`
  content items, seeded from the ISO list, and lists them through a content-backed `ICountryService`.

The country data is a snapshot in the package; a running site never contacts Google.
`utilities/address-data/build-address-data.mjs` refreshes it.

## Dependencies

- Addresses: none.
- Address Countries: `OrchardCore.Contents`, `OrchardCore.ContentFields`, `OrchardCore.Title`.

Full documentation: <https://docs.crestapps.com/docs/modules/addresses>
