---
sidebar_label: Addresses
sidebar_position: 16
title: Addresses
description: Address formats for every country, with checks and printing built on them, and optional managed countries.
---

| | |
| --- | --- |
| **Features** | Addresses (`CrestApps.OrchardCore.Addresses`), Address Countries (`CrestApps.OrchardCore.Addresses.Countries`) |
| **Core** | `CrestApps.OrchardCore.Addresses.Core` |
| **Abstractions** | `CrestApps.OrchardCore.Addresses.Abstractions` |
| **Category** | Content Management |

The Addresses module gives the platform one address structure that works for any country. Customers, checkout, orders and taxation all hold the same `Address` value, and every country's rules decide which parts it has, which are required, and how it is checked and printed.

| Feature | What it adds | Depends on |
| --- | --- | --- |
| **Addresses** | Each country's address format, the checks and the printing (`AddAddressesCore()`). Countries come from the ISO list. | Nothing |
| **Address Countries** | The countries the site serves as `Country` content items, seeded from the ISO list, which administrators can rename, add or remove. | Addresses, `OrchardCore.Contents`, `OrchardCore.ContentFields`, `OrchardCore.Title` |

## The address value

`Address` (in `CrestApps.OrchardCore.Addresses.Abstractions`) is an immutable value: its parts are init-only, and `Clone()` copies it. An order, a shipment or an invoice keeps its own copy, so a later change to the address it came from never changes it.

| Part | |
| --- | --- |
| `Name`, `Company` | The recipient, and the business at the address. |
| `AddressLine1`, `AddressLine2` | The street lines. Some countries, such as Japan, write the city and ward here. |
| `DependentLocality` | The part of a city, for the countries whose post uses one: a suburb, a Brazilian *bairro*, a Chinese or Korean district. |
| `City` | The city or post town. |
| `Region` | The state, province, prefecture or other region, stored by its code when the country has a list (below). |
| `PostalCode`, `SortingCode` | The postal code, and the sorting code a few countries add after the city (the CEDEX of France's overseas departments). |
| `Country` | The ISO 3166-1 alpha-2 code. |
| `County`, `District` | For tax: a county, and a special or tax district. |
| `Phone` | A number for the courier. |

## Address formats

Every country writes an address differently: Germany puts the postal code before the city, the United Kingdom gives the post town and the postcode a line each, and Japan starts with the postal code and goes from the prefecture down. `CrestApps.OrchardCore.Addresses.Core` ships each country's rules and three services built on them, registered with `services.AddAddressesCore()`:

| Service | What it does |
| --- | --- |
| `IAddressFormatProvider` | `GetFormatAsync(countryCode)` returns the country's `AddressFormat`: the lines of the address and the text between the parts (`Layout`, and `LatinLayout` for an address written in Latin letters in Japan, China or Korea), the `Required` and `Uppercase` parts, the `PostalCodePattern` and `PostalCodeExamples`, what the region, city, dependent locality and postal code are called (`RegionLabel` such as `state`, `prefecture` or `emirate`; `PostalCodeLabel` such as `zip`, `pin` or `eircode`; the values are in `AddressLabels`), and the country's `Regions`. A country the data doesn't know gets a generic format with every common part, requiring only the street and the city. |
| `IAddressValidator` | `ValidateAsync(address)` checks the address against its country: a known country, the parts the country requires, the postal code pattern, a region from the country's list, and a postal code that belongs to that region. It returns `AddressProblem`s, each naming the `AddressComponent` and the `AddressProblemKind` (`Missing`, `Invalid`, `Unknown`, `NotInRegion`), and the address tidied up: trimmed, the country and the postal code in capitals, and a region given by its name stored by its code. It checks the form of an address, not that the place exists. |
| `IAddressFormatter` | `GetLinesAsync(address, options)` prints the address in its country's order, leaving out empty parts and the text around them. `Envelope` writes in capitals the parts the country's post wants in capitals; `IncludeCountry` adds the country's name. |

A region in a country with a list of regions is stored by its code: its ISO 3166-2 code without the country (`CA` for California, `13` for Tokyo, `D` for County Dublin), or the key the country's post uses when there is none. `AddressFormat.FindRegion` finds a region by its code, key, name or Latin name. Storing codes is what lets tax jurisdictions match an address whether the customer chose "California" or typed "CA".

### Where the country data comes from

The formats come from Google's address data, the data behind [libaddressinput](https://github.com/google/libaddressinput), which is licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). It is free to use and redistribute, commercially included; the credit is in the package's `THIRD-PARTY-NOTICES.md`, which anyone redistributing the package keeps with it.

The data is a snapshot inside the package. A running site never contacts Google, and no address leaves the site. `utilities/address-data/build-address-data.mjs` refreshes the snapshot from Google's Address Data Service when the maintainers choose to.

## Country lists

`ICountryService.GetCountriesAsync()` lists the countries a site offers. With only the Addresses feature it returns the ISO list from the .NET runtime. With **Address Countries** enabled it returns the published `Country` content items instead, falling back to the ISO list while there are none, so a store can offer only the countries it serves or rename one.

| Content type | Purpose |
| --- | --- |
| **Country** | A country with its ISO 3166-1 alpha-2 code in `CountryPart`'s `Code` field. The ISO list is seeded when the feature is enabled, and no two countries may share a code. |

Countries are indexed by `GeographicAreaIndex` (content item id, content type, code, display text, published and latest flags), so the list never loads whole content items. The address formats always come from the shipped data: managed countries decide which countries are offered and what they are called, not how their addresses are written.

## Money safety

Taxation stores codes, not content item ids: the country's ISO code and the region, county, city and district as text. Addresses entered through the address rules carry region codes, so a tax jurisdiction for `CA` matches an address whose customer chose "California". Enabling or disabling either feature never orphans tax data.
