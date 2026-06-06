# CrestApps.OrchardCore.ContentFields

Provides custom Orchard Core content field editors maintained by CrestApps.

## Features

- Adds the `InternationalTelephone` editor for Orchard Core `TextField`
- Bundles the `intl-tel-input` library and supporting assets

## Usage

Enable the **CrestApps Content Fields** feature, then choose the `InternationalTelephone` editor on a `TextField` definition or through a data migration with `.WithEditor("InternationalTelephone")`.
