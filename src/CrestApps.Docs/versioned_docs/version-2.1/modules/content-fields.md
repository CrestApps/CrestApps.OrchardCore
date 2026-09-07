---
sidebar_label: Content Fields
sidebar_position: 2
title: Content Fields
description: Adds custom Orchard Core content fields maintained by CrestApps.
---

| | |
| --- | --- |
| **Feature Name** | CrestApps Content Fields |
| **Feature ID** | `CrestApps.OrchardCore.ContentFields` |

Provides custom Orchard Core content fields maintained by CrestApps.

## Overview

This module adds custom content fields for Orchard Core that extend the built-in field library with additional functionality. Each field ships with its own display driver, settings, edit and display views.

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
| `Hint` | `string` | `null` | Help text displayed below the field. |
| `Required` | `bool` | `false` | Whether the field is required. |
| `InitialCountryMode` | `InitialCountryMode` | `Globe` | Controls which country flag is pre-selected when the field is empty. See [Initial country modes](#initial-country-modes). |
| `SpecificCountryCode` | `string` | `null` | ISO country code used when `InitialCountryMode` is `Specific` (e.g. `US`). |

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

## Notes

- The shared `intl-tel-input` script and stylesheet are registered by `CrestApps.OrchardCore.Resources`.
- The Omnichannel Management module depends on this feature for `PhoneNumberInfoPart.Number`.
