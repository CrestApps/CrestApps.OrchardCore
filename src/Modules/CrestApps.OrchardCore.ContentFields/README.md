# CrestApps.OrchardCore.ContentFields

Provides custom Orchard Core content fields maintained by CrestApps.

## Features

- Adds `PhoneField`, a content field that stores a phone number together with its ISO country code so the correct country flag is always displayed
- Uses the `intl-tel-input` library (from `CrestApps.OrchardCore.Resources`) for country-aware phone number entry
- Server-side validation via `IPhoneNumberService`

## Usage

Enable the **CrestApps Content Fields** feature, then add a `PhoneField` to any content part through the content definition UI or a data migration with `.OfType("PhoneField")`.
