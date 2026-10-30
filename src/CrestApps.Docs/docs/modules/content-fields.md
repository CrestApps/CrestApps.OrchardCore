---
sidebar_label: Content Fields
sidebar_position: 2
title: Content Fields
description: Adds custom Orchard Core content fields maintained by CrestApps.
user_manual:
  - user-manual/administration/content-fields
---

| | |
| --- | --- |
| **Feature Name** | CrestApps Content Fields |
| **Feature ID** | `CrestApps.OrchardCore.ContentFields` |

Provides custom Orchard Core content fields maintained by CrestApps.

<YouTube video="content-fields" title="Video overview of the phone and address fields" />

## Overview

This module adds custom content fields for Orchard Core that extend the built-in field library with additional functionality. Each field ships with its own display driver, settings, edit and display views.

The feature depends on `CrestApps.OrchardCore.Resources`, `CrestApps.OrchardCore.PhoneNumbers`, `OrchardCore.ContentFields`
and `OrchardCore.ContentTypes`.

How administrators add a phone field to a content type, what each setting does, and what editors see is described
in the User Manual: [Content Fields](../user-manual/administration/content-fields.md).

## Included fields

### PhoneField

A content field that stores an international phone number together with its ISO country code so the correct country flag is always displayed when the field is edited again.

The field uses the [intl-tel-input](https://intl-tel-input.com/) library (provided by `CrestApps.OrchardCore.Resources`) to give editors a country-aware phone number input with flag dropdown and automatic formatting.

#### Stored properties

| Property | Type | Description |
| --- | --- | --- |
| `PhoneNumber` | `string` | The full phone number in E.164 format (e.g. `+14155552671`). |
| `CountryCode` | `string` | ISO 3166-1 alpha-2 country code (e.g. `US`, `CA`). Stored separately because some countries share a calling code (e.g. US and CA both use `+1`). |
| `NationalNumber` | `string` | The national (local) portion of the number without the country calling code (e.g. `4155552671`). |

#### Settings

| Setting | Type | Default | Description |
| --- | --- | --- | --- |
| `Hint` | `string` | `null` | Help text displayed below the field (admin label **Hint**). |
| `Required` | `bool` | `false` | Whether the field is required. |
| `InitialCountryMode` | `InitialCountryMode` | `Globe` (admin label **Initial country**) | Controls which country flag is pre-selected when the field is empty. See [Initial country modes](#initial-country-modes). |
| `SpecificCountryCode` | `string` | `null` | Admin label **Country**. ISO country code used when `InitialCountryMode` is `Specific` (e.g. `US`). |

#### Initial country modes

| Mode | Behavior |
| --- | --- |
| **Globe** | Shows the globe icon without pre-selecting any country. This is the default. |
| **Current culture** | Resolves the country from the current request culture's region (e.g. `en-US` resolves to `US`). |
| **Specific** | Always pre-selects the country configured in the **Country** dropdown. |

#### Adding PhoneField via migration

```csharp
await _contentDefinitionManager.AlterPartDefinitionAsync("MyPart", part => part
    .WithField("Phone", field => field
        .OfType("PhoneField")
        .WithDisplayName("Phone Number")
        .WithPosition("1")
        .WithSettings(new PhoneFieldSettings
        {
            Required = true,
            InitialCountryMode = InitialCountryMode.Specific,
            SpecificCountryCode = "US",
            Hint = "Enter a phone number with country code.",
        })
    )
);
```

When `CrestApps.OrchardCore.Recipes` is also enabled, recipe schemas now understand both the `PhoneFieldSettings` envelope used by `ContentDefinition` and the `PhoneNumber` / `CountryCode` / `NationalNumber` payload stored on content items.

#### Server-side validation

When the field value is submitted, the display driver uses `IPhoneNumberService` (from `CrestApps.OrchardCore.PhoneNumbers`) to validate that the entered number is a well-formed phone number. Invalid numbers produce a model-state error and the editor is re-displayed.

### AddressField

Feature **Address Field** (`CrestApps.OrchardCore.ContentFields.Address`), depending on [Addresses](addresses) (`CrestApps.OrchardCore.Addresses`), `OrchardCore.ContentFields` and `OrchardCore.ContentTypes`. It is a feature of its own so the CrestApps Content Fields feature does not depend on Addresses.

A content field holding the shared `Address` value from the Addresses module. A content type can have several, for example a Home and a Work address.

#### Stored properties

| Property | Type | Description |
| --- | --- | --- |
| `Address` | `Address` | The address, or `null` when none was entered: `Name`, `Company`, `AddressLine1`, `AddressLine2`, `DependentLocality`, `City`, `Region` (the region's ISO 3166-2 code without the country, when the country lists regions), `County`, `District`, `PostalCode`, `SortingCode`, `Country` (ISO 3166-1 alpha-2) and `Phone`. |

#### Settings (`AddressFieldSettings`)

| Setting | Type | Default | Description |
| --- | --- | --- | --- |
| `Hint` | `string` | `null` | Help text shown above the address. |
| `Required` | `bool` | `false` | Whether an address must be entered. An optional address left blank is stored as `null`. |
| `ShowName`, `ShowCompany`, `ShowPhone` | `bool` | `true`, `true`, `false` | Which of the recipient, the company and a phone number the editor asks for. |
| `Countries` | `string[]` | `null` | The ISO codes of the countries offered; `null` or empty offers every country `ICountryService` lists. |
| `DefaultCountry` | `string` | `null` | The country a new address starts in. |

#### Editing and display

The editor is the Addresses module's `AddressEditor` shape, bound under the field's prefix and checked with `IAddressEditorService`, so it is laid out for the chosen country and refuses what that country's rules refuse (required parts, postal code pattern, region list, postal code within the region). The display shape `AddressField` prints the address through `IAddressFormatter`. `AddressField` and `Address` are registered with Liquid's member access strategy.

#### Index

`AddressFieldIndex` (a `ContentFieldIndex`) has a row per address field, with `Country`, `Region`, `City` and `PostalCode`, so content can be queried by place:

```csharp
var venues = await session
    .Query<ContentItem, AddressFieldIndex>(index => index.ContentType == "Venue" && index.Country == "CA" && index.Region == "ON" && index.Published)
    .ListAsync();
```

#### Recipes

With CrestApps Recipes on, `AddressField` has a content-definition schema (its settings) and a content schema (the `Address` object), like `PhoneField`.

## Notes

- The shared `intl-tel-input` script and stylesheet are registered by `CrestApps.OrchardCore.Resources`.
- The Omnichannel Management module depends on this feature for `PhoneNumberInfoPart.Number`.
