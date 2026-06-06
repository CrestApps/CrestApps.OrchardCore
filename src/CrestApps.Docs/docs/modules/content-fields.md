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
| `DefaultCountryCode` | `string` | `null` | ISO country code used to pre-select the flag when the field is empty (e.g. `US`). |

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
            DefaultCountryCode = "US",
            Hint = "Enter a phone number with country code.",
        })
    )
);
```

#### Server-side validation

When the field value is submitted, the display driver uses `IPhoneNumberService` (from `CrestApps.OrchardCore.PhoneNumbers`) to validate that the entered number is a well-formed phone number. Invalid numbers produce a model-state error and the editor is re-displayed.

## Notes

- The shared `intl-tel-input` script and stylesheet are registered by `CrestApps.OrchardCore.Resources`.
- The Omnichannel Management module depends on this feature for `PhoneNumberInfoPart.Number`.
