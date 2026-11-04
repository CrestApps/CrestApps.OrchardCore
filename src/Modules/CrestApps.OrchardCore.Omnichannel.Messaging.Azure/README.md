# Omnichannel Messaging - Azure Blob Storage

Stores the pictures exchanged in messaging conversations in Azure Blob Storage, so they survive redeployments, keeping the same encryption-at-rest as the local store.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Omnichannel Messaging - Azure Blob Storage | `CrestApps.OrchardCore.Omnichannel.Messaging.Azure` | Stores the pictures exchanged in messaging conversations in Azure Blob Storage, so they survive redeployments, keeping the same encryption-at-rest as the local store. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.Omnichannel.Messaging`](../CrestApps.OrchardCore.Omnichannel.Messaging/README.md)

## Documentation

See [Omnichannel Messaging - Azure Blob Storage for Pictures](../../CrestApps.Docs/docs/omnichannel/messaging-azure-blob-storage.md) in the technical manual.
