# CrestApps.OrchardCore.ContentFields

Provides custom Orchard Core content field editors maintained by CrestApps.

## Features

- Adds the `InternationalTelephone` editor for Orchard Core `TextField`
- Depends on `CrestApps.OrchardCore.Resources` for the shared `intl-tel-input` library assets

## Usage

Enable the **CrestApps Content Fields** feature, then choose the `InternationalTelephone` editor on a `TextField` definition or through a data migration with `.WithEditor("InternationalTelephone")`.
