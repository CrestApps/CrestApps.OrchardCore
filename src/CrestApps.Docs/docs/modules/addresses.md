---
sidebar_label: Addresses
sidebar_position: 16
title: Addresses
description: Address formats for every country, with checks and printing built on them, optional managed countries, and address suggestions from Google Places through IAddressAutocompleteProvider.
user_manual: user-manual/commerce/customers
---

| | |
| --- | --- |
| **Features** | Addresses (`CrestApps.OrchardCore.Addresses`), Address Countries (`CrestApps.OrchardCore.Addresses.Countries`), Address Suggestions (Google Places) (`CrestApps.OrchardCore.Addresses.GooglePlaces`) |
| **Core** | `CrestApps.OrchardCore.Addresses.Core` |
| **Abstractions** | `CrestApps.OrchardCore.Addresses.Abstractions` |
| **Category** | Content Management |

The Addresses module gives the platform one address structure that works for any country. Customers, checkout, orders and taxation all hold the same `Address` value, and every country's rules decide which parts it has, which are required, and how it is checked and printed.

| Feature | What it adds | Depends on |
| --- | --- | --- |
| **Addresses** | Each country's address format, the checks and the printing (`AddAddressesCore()`). Countries come from the ISO list. | Nothing |
| **Address Countries** | The countries the site serves as `Country` content items, seeded from the ISO list, which administrators can rename, add or remove. | Addresses, `OrchardCore.Contents`, `OrchardCore.ContentFields`, `OrchardCore.Title` |
| **Address Suggestions (Google Places)** | Suggestions as a street is typed in the address editor, from the Places API (New), with the store's key kept on the server. See [Address suggestions](#address-suggestions). | Addresses |

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

## Address suggestions

The address editor suggests addresses as the street is typed, and fills in the rest from the one picked, when an address suggestion provider is set up. The store owner's side is in the [User Manual](../user-manual/commerce/customers.md#address-suggestions).

### The provider seam

`IAddressAutocompleteProvider` (in `CrestApps.OrchardCore.Addresses.Core.Autocomplete`) is how any service plugs in:

```csharp
public interface IAddressAutocompleteProvider
{
    // Whether it is set up to answer, such as with an API key.
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    // Addresses for what was typed, in request.Country when given.
    Task<IReadOnlyList<AddressSuggestion>> SuggestAsync(AddressSuggestionRequest request, CancellationToken cancellationToken = default);

    // The parts of a suggestion, or null.
    Task<Address> ResolveAsync(string suggestionId, string sessionToken, CancellationToken cancellationToken = default);
}
```

`AddressSuggestion` is `(Id, Text)`. `AddressSuggestionRequest` has `Text`, `Country` (two letters, or `null` for anywhere), `SessionToken` (groups a visitor's typing and pick, which some services bill as one session) and `Language` (the request's two-letter UI language). `GetAvailableAsync()` on the registered providers returns the first that `IsAvailableAsync`. Register yours as a scoped `IAddressAutocompleteProvider` in your own feature; it is called only from the server, so its keys never reach the browser.

### Endpoints

The Addresses feature maps two anonymous endpoints, at the tenant's root:

| Method | Path | Answers |
| --- | --- | --- |
| `GET` | `addresses/suggest?q=&country=&session=` | JSON `[{ "id": "...", "text": "..." }]`. `q` shorter than 3 or longer than 120 characters (trimmed) answers `[]` without asking the provider. `country` is used when it's two letters. |
| `GET` | `addresses/resolve?id=&session=` | JSON `{ addressLine1, addressLine2, dependentLocality, city, region, postalCode, country }`. `404` for a missing id, one over 512 characters, or a suggestion the provider can't find. |

Both answer `404` when no provider is available, and `429` when the visitor is over the limit. A `session` value is passed on only when it's up to 64 letters, digits and hyphens.

Each call can cost the store money, so each visitor (`HttpContext.Connection.RemoteIpAddress`) may make 60 calls a minute across both endpoints (`AddressAutocompleteEndpoints.RequestsPerMinute`), counted in `IMemoryCache` by clock minute. Behind a reverse proxy, turn on Orchard Core's **Reverse Proxy Configuration** feature, so the visitor's address is counted rather than the proxy's.

### The editor

`AddressEditor.cshtml` sets `data-suggest-url` and `data-resolve-url` on the editor only when a provider is available, so with none the script offers no suggestions and calls nothing. The script (`address-editor.js`):

- asks `addresses/suggest` after the street field has 3 characters, after a short pause, with the editor's country and a session token, and shows only the latest answer;
- lists the suggestions under the street field (`role="listbox"`, labelled *Suggested addresses*); arrow keys move, **Enter** picks, **Escape** closes;
- on a pick, asks `addresses/resolve`, switches the country select when the address is in another country the editor offers (and waits for its layout), fills the street, second line, dependent locality, city, region and postal code as if typed (raising `input` and `change`), and starts a new session.

### Google Places

**Address Suggestions (Google Places)** registers `GooglePlacesAddressAutocompleteProvider`, which calls the Places API (New) with the named `HttpClient` `GooglePlaces` (5-second timeout):

- `SuggestAsync` posts to `https://places.googleapis.com/v1/places:autocomplete` with `input`, `includedRegionCodes` (the country), `languageCode` and `sessionToken`, and the key in `X-Goog-Api-Key`.
- `ResolveAsync` gets `https://places.googleapis.com/v1/places/{id}` with the session token and the field mask `addressComponents`, and maps the components to an `Address`: the street number before the route in countries that write it first (such as the US, Canada, the UK, Ireland, Australia and France) and after it elsewhere, `subpremise` as the second line, `sublocality`, `locality` (else `postal_town`, else `administrative_area_level_2`) as the city, the short `administrative_area_level_1` as the region, the postal code and the country.
- An error, a timeout or an unexpected answer is logged and gives no suggestions (or `404` from `resolve`); it never breaks the form.

The key is set under **Settings > Address suggestions** (settings group `address-suggestions`, permission `ManageAddressSuggestions`, **Manage address suggestions**, given to Administrator) and kept in `GooglePlacesSettings.ApiKey`, protected with data protection (purpose `CrestApps.OrchardCore.Addresses.GooglePlaces.ApiKey`). The page never shows it again; a blank field keeps it, and **Remove the key** removes it. Restrict the key to the Places API (New) in the Google Cloud console.

## Country lists

`ICountryService.GetCountriesAsync()` lists the countries a site offers. With only the Addresses feature it returns the ISO list from the .NET runtime. With **Address Countries** enabled it returns the published `Country` content items instead, falling back to the ISO list while there are none, so a store can offer only the countries it serves or rename one.

| Content type | Purpose |
| --- | --- |
| **Country** | A country with its ISO 3166-1 alpha-2 code in `CountryPart`'s `Code` field. The ISO list is seeded when the feature is enabled, and no two countries may share a code. |

Countries are indexed by `CountryIndex` (content item id, code, display text, published and latest flags), so the list never loads whole content items. The address formats always come from the shipped data: managed countries decide which countries are offered and what they are called, not how their addresses are written.

## Money safety

Taxation stores codes, not content item ids: the country's ISO code and the region, county, city and district as text. Addresses entered through the address rules carry region codes, so a tax jurisdiction for `CA` matches an address whose customer chose "California". Enabling or disabling either feature never orphans tax data.
