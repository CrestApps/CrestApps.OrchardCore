# SignalR

Provides all services needed to use SignalR functionality.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| SignalR (Deprecated) | `CrestApps.OrchardCore.SignalR` | This feature has been migrated to the Orchard Core Framework and should no longer be used. Instead, use the 'OrchardCore.SignalR' feature. |
| SignalR Redis Backplane (Deprecated) | `CrestApps.OrchardCore.SignalR.Redis` | This feature has been migrated to the Orchard Core Framework and should no longer be used. Instead, use the 'OrchardCore.SignalR.Redis' feature. |
| SignalR Azure Backplane (Deprecated) | `CrestApps.OrchardCore.SignalR.Azure` | This feature has been migrated to the Orchard Core Framework and should no longer be used. Instead, use the 'OrchardCore.SignalR.Azure' feature. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- `OrchardCore.SignalR`
- `OrchardCore.Redis`
- `OrchardCore.SignalR.Redis`
- `OrchardCore.SignalR.Azure`

## Documentation

See [SignalR Feature](../../CrestApps.Docs/docs/modules/signalr.md) in the technical manual.
