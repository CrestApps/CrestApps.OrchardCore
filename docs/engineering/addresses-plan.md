# Addresses plan

Status: AD-D4, AD-D5 and AD-D8 chosen 2026-10-16 (the recommended options); the other decisions stand as
proposed. Comes before the checkout contact step (CU3) of the
[Customers plan](commerce/customers-plan.md), whose billing form is the first to need it.

## Goal

One address structure for the whole platform, which works for any country and can be reused wherever an address is
needed:

- **One value.** The customer address book, order billing (and shipping later), taxation and content types all hold
  the same `Address`.
- **Any country.** Forms show the fields a country uses, in its order and with its labels. Addresses are checked
  against the country's rules and printed the way its post expects.
- **One editor.** Checkout, My Account, the admin and content fields render the same address editor, and a theme can
  override it once.
- **On content.** An `AddressField` puts addresses on any content type, as many as it needs.
- **Free to use.** The country data costs nothing to use or redistribute, for us or for anyone who uses the project.

## Where things stand

The Addresses module was added on 2026-10-12, for 3.0.0, which is not released yet (the last release is 2.1.5).
No released site depends on it, so its features can still change without an upgrade path.

| Piece | Today |
| --- | --- |
| `Addresses.Abstractions` | The immutable `Address` value (name, company, two street lines, country, region, county, city, district, postal code, phone), `CountryInfo`, `ICountryService` and `DefaultCountryService`, which lists the ISO countries from the runtime. Taxation, Checkout, Subscriptions and Customers use them. |
| Addresses module | One feature, `CrestApps.OrchardCore.Addresses`, depending on Contents, ContentFields and Title. It defines Country, Region, County, City and District content types, seeds 249 Country items, and replaces `ICountryService` with the countries' content (`ContentCountryService`). |
| `AddressPart` | An attachable part whose country, region, county, city and district are content pickers. `IAddressResolver` turns it into an `Address`. **Nothing outside the module's tests uses either.** |
| Region, County, City, District content | Nothing is seeded and nothing reads them. Tax jurisdictions keep their region and county as text. |
| Customers (#923, open) | `CustomerAddress` repeats the address fields (no county, district, dependent locality or sorting code). The form is US-shaped: fixed field order, US labels, free-text region. `GetLines` prints "City Region PostalCode" on one line. |
| Taxation | `CatalogTaxJurisdictionResolver` matches an address's country, region, county, city and postal code to a jurisdiction by exact, case-insensitive text. A customer who types "California" never matches a jurisdiction for "CA". |
| ContentFields module | `PhoneField`, as one feature that depends on PhoneNumbers. |

What doesn't work for addresses that customers type:

- **Pickers need the place to exist first.** An address can only point at a city that is already a content item, and
  only countries are seeded. Nobody can seed every city in the world, and a picker is an admin control.
- **References break.** Deleting a City item breaks every address that pointed at it.
- **No country rules.** Nothing says which fields a country uses or requires, what they are called, what its postal
  codes look like, which regions it has, or how it prints an address.
- **Too heavy to depend on.** Customers and checkout need addresses without Contents, ContentFields, Title and 249
  seeded content items.

## Country data and licenses

The per-country rules come from **Google's address data**, the data behind
[libaddressinput](https://github.com/google/libaddressinput). Its README says the libraries use "address metadata from
Google's Address Data Service", and states: "Data licensed under the CC-BY 4.0." The data is taken from that service
(`chromium-i18n.appspot.com/ssl-address/data`), not from the repository's `testdata/countryinfo.txt`: that file is test
data and lags the service (it still has Korea's six-digit postal codes, replaced in 2015; the service has 252
countries, the file 245). Each country has:

- `fmt`: the fields and their order, such as `%N%n%O%n%A%n%Z %C` for Germany, where `%N` is the name, `%O` the company, `%A` the
  street lines, `%D` the dependent locality, `%C` the city, `%S` the region, `%Z` the postal code, `%X` the sorting
  code and `%n` a line break;
- `require` and `upper`: the fields that must be filled in, and those written in capitals on an envelope;
- `zip` and `zipex`: the postal code pattern and examples;
- `state_name_type`, `sublocality_name_type`, `zip_name_type`: what the region, the dependent locality and the postal
  code are called (state, province, prefecture, island, parish, emirate…; postal code, ZIP, PIN…);
- `sub_keys`, `sub_names`, `sub_lnames`, `sub_isoids`: the regions, with local and Latin names and their ISO 3166-2
  codes (first-level regions only).

| Source | License | Fees | What we must do |
| --- | --- | --- | --- |
| Google's address data | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/): use, change and redistribute for any purpose, commercial included | None | Credit Google, link the license, and say that we changed the data's form. Add no terms that stop others reusing it. |
| Unicode CLDR (later, for region names in other languages) | [Unicode License v3](https://www.unicode.org/license.txt): "free of charge", selling copies included | None | Keep Unicode's notice with the data or in our documentation. |
| libaddressinput's code | Apache 2.0 | None | Nothing: we write our own code and take only the data. |
| ISO 3166-2 itself | ISO claims copyright and sells the database | Yes | Not used. The region codes come with Google's data. |

Our packages are MIT, and a CC BY data file can ship inside an MIT package. A `THIRD-PARTY-NOTICES.md` in the
package and a docs page carry the credit. Anyone who uses or redistributes our packages owes nothing and only keeps
that notice.

We never call the Address Data Service from a running site. It has no service level, and a store would otherwise send
addresses to it. A script copies the data into the repo, and we refresh it when we choose.

## Decisions (2026-10-16)

| # | Proposal | Why |
| --- | --- | --- |
| AD-D1 | **One `Address` value everywhere.** Billing, shipping, mailing, home, work and head office are what a holder calls an address, not kinds of address. An address book entry is an id, a label and an `Address`. A business's name and tax id stay on the customer or account; the address keeps an optional company line for the envelope. | One structure to format, validate, index and copy onto orders. |
| AD-D2 | **Country rules from Google's address data** (CC BY 4.0), copied into the repo with a notice, never fetched at run time. Countries without data use a generic layout with every field optional except the street, the city and the country. | Free, maintained and complete enough; no outside dependency or address leaving the site. |
| AD-D3 | **`Address` gains `DependentLocality` (%D) and `SortingCode` (%X).** `Region` holds the region's ISO 3166-2 code without the country prefix (`CA` for California, `13` for Tokyo) when the country lists regions, and text otherwise. `County` and `District` stay, for tax. | Brazil's *bairro*, a Chinese district or a UK locality need %D; France's CEDEX needs %X. Codes make tax matching work whatever the customer typed. |
| AD-D4 | **Split the module into a light base and optional managed countries.** `CrestApps.OrchardCore.Addresses` keeps its id but becomes the base: country rules, checks, formatting, the editor and display shapes, the ISO country list. It needs no content features. A new `CrestApps.OrchardCore.Addresses.Countries` feature keeps the Country content type, its seed and `ContentCountryService`, for stores that want to rename or limit the countries they serve. | Customers, checkout and shipping depend on the base cheaply. 3.0.0 is unreleased, so only local sites need to turn the new feature on. |
| AD-D5 | **Remove `AddressPart`, `IAddressResolver` and the Region, County, City and District content types.** Regions come from the country data instead. | Nothing uses them; pickers can't hold addresses customers type; removing them before 3.0.0 costs nothing. |
| AD-D6 | **Checks cover the form, not delivery.** Required fields, the postal code pattern, and a region from the country's list. Whether a door exists needs a postal database, which is a paid service; an `IAddressVerifier` hook for one comes later and is off by default. | Free and offline, and catches most mistakes. |
| AD-D7 | **One editor shape and one display shape.** `AddressEditor` (with a prefix, so a page can hold several) and `AddressDisplay`. Choosing a country re-lays the fields in the browser from the country's rules, and the server checks the same rules. | The same form everywhere, overridden once by a theme. |
| AD-D8 | **`AddressField` is a feature of the ContentFields module** ("Address Field"), depending on the Addresses base. It stores the `Address` value, can be added several times to a type, and is indexed by country, region, city and postal code. | Where the user expects fields; its own feature keeps PhoneField's feature free of Addresses. |

## Design

### Abstractions (`CrestApps.OrchardCore.Addresses.Abstractions`)

- **`Address`:** adds `DependentLocality` and `SortingCode`, and `Clone()` copies them.
- **`AddressFormat`:** one country's rules:
  - the field layout: lines of fields with the literal text between them, taken from `fmt`;
  - the required fields, the fields in capitals, the postal code pattern and examples;
  - the label kind for the region, the dependent locality and the postal code;
  - the country's regions (`AddressRegion`: code, name, Latin name), with a flag for whether a region must come from the list.
- **`IAddressFormatProvider.GetFormatAsync(countryCode)`:** the country's format, or the generic one.
- **`IAddressValidator.ValidateAsync(address)`:** returns problems, each naming the field and the reason (missing,
  postal code not matching, unknown region), so callers put them on their own model keys.
- **`IAddressFormatter`:** `GetLines(address, options)` prints lines in the country's order, with options for
  capitals (an envelope) and whether to include the country. It replaces today's `AddressExtensions.GetLines`.
- Field identifiers (`AddressFieldName`: Name, Company, AddressLines, DependentLocality, City, Region, PostalCode,
  SortingCode, Country, Phone) tie the format, the checks and the editor together.

### Core (`CrestApps.OrchardCore.Addresses.Core`, new)

- The country data as an embedded JSON resource: country formats and first-level regions only, keeping the fields we
  use (about 130 KB, 30 KB compressed).
- `DefaultAddressFormatProvider` reads it once and caches it. When managed countries are on, it still uses the data
  for formats; `ICountryService` only decides which countries are offered and their names.
- `DefaultAddressValidator`, `DefaultAddressFormatter`.
- `utilities/address-data/`: the script that reads the service into the embedded JSON and records the day it was
  taken. Running it is a deliberate change reviewed like code; a test fails until the notice names the same day.

### Addresses module

- **Base feature `CrestApps.OrchardCore.Addresses`** (no content dependencies):
  - registers the provider, validator, formatter and `DefaultCountryService`;
  - the `AddressEditor` and `AddressDisplay` shapes and their views;
  - a small script (Vite, no jQuery) that re-lays the fields when the country changes, from the formats written into
    the page for the countries offered;
  - labels are localized strings per label kind ("State", "Province", "Prefecture", "ZIP code", "Postal code"…);
    region names come from the data, in the local script with the Latin name beside it.
- **`CrestApps.OrchardCore.Addresses.Countries`:** today's Country content type, seed and `ContentCountryService`,
  depending on the base and on Contents and Title.
- Removed: `AddressPart`, `IAddressResolver`, the Region, County, City and District types, and their parts of
  `GeographicAreaIndex`.

### Customers and checkout

- `CustomerAddress` becomes `AddressId`, `Label` and `Address`. #923 is unmerged, so it changes before it merges and
  no stored address needs converting.
- My Account, the admin customer page and the checkout contact step render `AddressEditor`, and check with
  `IAddressValidator`.
- Orders keep the `Address` copy they already keep.

### Taxation

- No change to the matching. Addresses entered through the editor carry region codes, so jurisdictions entered with
  codes match. The jurisdiction editor's region box can later offer the country's regions.

### `AddressField` (ContentFields module)

- Feature "Address Field" (`CrestApps.OrchardCore.ContentFields.Address`), depending on the Addresses base.
- Stores the `Address` value. Settings: required, the countries offered, and whether to ask for name, company and phone.
- Editor and display through the shared shapes; a Liquid filter and a display shape for printing.
- Indexed by country, region, city and postal code (`AddressFieldIndex`) so content can be listed by place.
- Recipe and content-definition schemas like `PhoneField`'s.

## Slices

| Slice | Scope | Exit gate |
| --- | --- | --- |
| **AD1 Country rules** | `Address` gains two fields; `AddressFormat`, provider, validator, formatter; Addresses.Core with the embedded data, the import script and the notice | Formats, checks and printing are right for the United States, Canada, the United Kingdom, Germany, France, Japan, Brazil and a country with no data; tests pin each. |
| **AD2a Module split** | Base feature without content dependencies; Address Countries feature; `AddressPart`, resolver and the other geography types removed; docs | A site with only Addresses lists the ISO countries; enabling Address Countries seeds and lists Country content. |
| **AD2b Shared editor** | `AddressEditor` and `AddressDisplay` shapes, the formats endpoint and the re-layout script | Choosing a country in the editor shows its fields, labels and regions; a bad postal code is refused on the server; the docs build. |
| **AD3 Customers on the shared address** | `CustomerAddress` wraps `Address`; My Account and the admin use the editor; #923 amended before it merges | The live pass from CU2 passes again, with a Japanese and a British address. |
| **CU3** (Customers plan) | The checkout contact step, using the editor | As in the Customers plan. |
| **AD4 Address Field** | `AddressField` feature, settings, index, Liquid, schemas, docs | A content type with Home and Work address fields saves, prints and lists by city. |

AD1 to AD3 come before CU3. AD4 can follow CU3. AD2 was split in two to keep each PR reviewable.

### AD1 as built

- `Address` gained `DependentLocality` and `SortingCode`. Abstractions gained `AddressComponent`, `AddressFormat`
  (layouts, required and capitalized parts, postal code pattern and examples, labels, regions, `FindRegion`),
  `AddressLayoutLine`, `AddressRegion`, `AddressLabels`, and `IAddressFormatProvider`, `IAddressValidator`,
  `IAddressFormatter`.
- `CrestApps.OrchardCore.Addresses.Core`: the embedded snapshot (252 countries, about 130 KB, 2026-10-16),
  `DefaultAddressFormatProvider` (singleton), `DefaultAddressValidator`, `DefaultAddressFormatter`,
  `AddAddressesCore()`, and `THIRD-PARTY-NOTICES.md` packed with the package. The Addresses module calls
  `AddAddressesCore()`; Customers adopts it in AD3.
- What the current data changed from the plan's examples: the United States writes `City, ST ZIP`; Japan has no city
  part (the city and ward are written in the street lines); France no longer writes a CEDEX, though 15 countries and
  territories, most of them French overseas, still use the sorting code.
- Printing: text before a line's first part belongs to it (Monaco's `MC-`), and text between parts is written only
  between two that have a value. A part the country doesn't use is not printed.

### AD2a as built

- `CrestApps.OrchardCore.Addresses` keeps its id with no dependencies and only calls `AddAddressesCore()`. The new
  `CrestApps.OrchardCore.Addresses.Countries` ("Address Countries") holds the Country type, its seed,
  `GeographicAreaIndex` (now countries only) and `ContentCountryService`.
- `AddressMigrations` keeps its class name and version 3, so sites that ran it don't run it again; new sites get the
  Country type only. Sites that ran the earlier steps keep the old Region, County, City, District and `AddressPart`
  definitions until someone removes them; nothing reads them.
- Removed: `AddressPart`, `IAddressResolver`, `DefaultAddressResolver` and its tests, and the obsolete constants.
- Found while testing: `CountryContentHandler.ValidatingAsync` never ran for the admin editor, because Orchard
  Core's content admin creates and publishes without calling `ContentManager.ValidateAsync`. Duplicate and
  malformed codes were saved. `CountryCodeValidator` now holds the rule, `CountryCodeDisplayDriver` applies it in
  the editor, and the handler still covers recipes and the content API.

## Out of scope

- Checking that an address exists (a paid postal database); the `IAddressVerifier` hook comes later.
- Second-level regions (cities and districts for China, Korea and others).
- Writing an address in both its local script and Latin (`lfmt`), and region names in other languages from CLDR.
- Geocoding, maps and distances.
- Address autocomplete services.

## Open questions

- Are there sites, outside local test sites, where the Addresses feature is already on and Country content was edited?
  They would turn on the Countries feature after AD2.
- Should the region list also replace the free-text region on tax jurisdictions now, or in a later Taxation change?
