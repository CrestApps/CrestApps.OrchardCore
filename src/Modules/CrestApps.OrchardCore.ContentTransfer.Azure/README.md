# Content Transfer - Azure Blob Storage

Stores Content Transfer import and export files in Azure Blob Storage instead of the local file system, so every instance of a scaled-out site reads the same files.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Content Transfer - Azure Blob Storage | `CrestApps.OrchardCore.ContentTransfer.Azure` | Stores Content Transfer import and export files in Azure Blob Storage instead of the local file system, so every instance of a scaled-out site reads the same files. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.ContentTransfer`](../CrestApps.OrchardCore.ContentTransfer/README.md)

## Documentation

See [Content Transfer](../../CrestApps.Docs/docs/modules/content-transfer.md) in the technical manual.
