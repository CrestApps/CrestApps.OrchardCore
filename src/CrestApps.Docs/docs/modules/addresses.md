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

## The address editor

Every page that asks for an address uses the same editor: the customer address book, the admin, the checkout, and content fields. It asks for the country first and lays the other fields out for it, in the country's order, with its labels (State, Prefecture, Post town, ZIP code, Eircode…), marking what the country requires and offering its regions as a list.

`IAddressEditorService` (in `CrestApps.OrchardCore.Addresses.Core.Editing`) builds and checks it:

| Method | |
| --- | --- |
| `BuildAsync(address, options)` | The editor for an address, or for a new one. |
| `PopulateAsync(model, options)` | Lays a posted editor out again for the country it posted, to show it with its errors. |
| `ValidateAsync(model, modelState, prefix, options)` | Checks the posted address against its country and the page's options, and adds each problem to `modelState` under `prefix` and the field's name, in the visitor's language. Returns the `AddressValidationResult`, whose `Address` is the tidied value to keep. |
| `GetLayoutAsync(countryCode)` | The `AddressEditorLayout` of a country: its lines, labels, required parts, regions and an example postal code. |

`AddressEditorOptions` sets what the page asks for besides the country's own parts: `ShowName`, `RequireName`, `ShowCompany`, `ShowPhone`, the `Countries` offered (all the site serves when `null`), and the `DefaultCountry` of a new address. `AddressEditorViewModel.HasAnyValue()` tells an optional address left blank from one started.

The editor is the `AddressEditor` shape, whose model is the `AddressEditorViewModel`. Its fields post under the shape's prefix, so a page binds them back with that prefix:

```csharp
// In a controller.
var editor = await _addressEditor.BuildAsync(customerAddress, options);
model.AddressEditor = await _shapeFactory.CreateAddressEditorAsync(editor, prefix: "Address");

// On post.
var posted = new AddressEditorViewModel();
await TryUpdateModelAsync(posted, "Address");
var result = await _addressEditor.ValidateAsync(posted, ModelState, "Address", options);
```

A display driver can return `Initialize<AddressEditorViewModel>("AddressEditor", …)` instead, with its own prefix. `CreateAddressDisplayAsync(address)` gives the `AddressDisplay` shape, which prints an address through `IAddressFormatter`.

When the country changes, the editor's script asks `~/addresses/formats/{countryCode}` for the country's layout and re-lays the fields. Fields the country doesn't use are set aside and disabled, so they post nothing. Without the script, the server lays the form out again when it comes back. The layout is the same for everyone, in the visitor's language, so the endpoint needs no sign-in and is cached by the browser for an hour.

## Country lists

`ICountryService.GetCountriesAsync()` lists the countries a site offers. With only the Addresses feature it returns the ISO list from the .NET runtime. With **Address Countries** enabled it returns the published `Country` content items instead, falling back to the ISO list while there are none, so a store can offer only the countries it serves or rename one.

| Content type | Purpose |
| --- | --- |
| **Country** | A country with its ISO 3166-1 alpha-2 code in `CountryPart`'s `Code` field. The ISO list is seeded when the feature is enabled, and no two countries may share a code. |

Countries are indexed by `GeographicAreaIndex` (content item id, content type, code, display text, published and latest flags), so the list never loads whole content items. The address formats always come from the shipped data: managed countries decide which countries are offered and what they are called, not how their addresses are written.

## Money safety

Taxation stores codes, not content item ids: the country's ISO code and the region, county, city and district as text. Addresses entered through the address rules carry region codes, so a tax jurisdiction for `CA` matches an address whose customer chose "California". Enabling or disabling either feature never orphans tax data.

## Upgrading from an earlier 3.0 preview

Earlier 3.0 previews kept the Country content in the Addresses feature and had an `AddressPart` whose country, region, county, city and district were content pickers, with Region, County, City and District content types and `IAddressResolver` to read them. A site upgrading from one of those keeps working:

- **Countries.** A site that ran the earlier Addresses migration gets **Address Countries** turned on by itself the first time it starts, so its country list, with any renamed or removed countries, stays as it was. A new site starts without it.
- **Content.** Content types that have `AddressPart` keep it, with its picker fields, and the Region, County, City and District items stay. Nothing is deleted. They are still indexed in `GeographicAreaIndex`, with their parents.
- **Code.** `IAddressResolver`, `DefaultAddressResolver` and the `AddressConstants` names for the removed types and parts (`AddressPart`, `Region`, `RegionPart`, `County`, `CountyPart`, `City`, `CityPart`, `District`, `DistrictPart`) are still there, marked `[Obsolete]`, so code that used them builds with a warning. They will be removed in the next major version.

To move to the new address, add an [Address field](content-fields#addressfield) (the ContentFields module's **Address Field** feature) to the content type, copy each item's address into it, and remove `AddressPart`; code reads the field's `Address` value instead of calling `IAddressResolver`.
