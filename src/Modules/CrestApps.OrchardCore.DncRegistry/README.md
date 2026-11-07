# DNC Registry

Provides the core framework for integrating with national do-not-call registries.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| DNC Registry | `CrestApps.OrchardCore.DncRegistry` | Provides the core framework for integrating with national do-not-call registries. |
| USA FTC Do Not Call Registry | `CrestApps.OrchardCore.DncRegistry.UsaFtc` | Integrates with the United States Federal Trade Commission (FTC) National Do Not Call Registry at telemarketing.donotcall.gov. |
| Canada LNNTE-DNCL Registry | `CrestApps.OrchardCore.DncRegistry.CanadaDncl` | Integrates with the Canadian National Do Not Call List (LNNTE-DNCL) maintained by the CRTC. |
| Local Do Not Call Registry | `CrestApps.OrchardCore.DncRegistry.Local` | Provides a local do-not-call registry where administrators can upload CSV files of phone numbers organized by country. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.PhoneNumbers`](../CrestApps.OrchardCore.PhoneNumbers/README.md)

## Extension points

- `INationalDoNotCallRegistry` (CrestApps.OrchardCore.DncRegistry.Abstractions): Represents a national do-not-call registry provider.

## Documentation

See [DNC Registry](../../CrestApps.Docs/docs/modules/dnc-registry.md) in the technical manual.
