---
sidebar_label: Azure Blob Storage
title: Omnichannel Messaging - Azure Blob Storage for Pictures
description: Keep the pictures exchanged in messaging conversations in Azure Blob Storage so they survive redeployments, with the same encryption at rest.
---

| | |
| --- | --- |
| **Feature Name** | Omnichannel Messaging - Azure Blob Storage |
| **Feature ID** | `CrestApps.OrchardCore.Omnichannel.Messaging.Azure` |

By default the pictures sent and received in the [messaging workspace](./messaging-workspace.md) are kept in the tenant's `App_Data\Sites\<tenant-name>\MessagingAttachments` folder. When the host's file system is replaced on deployment (containers, App Service slot swaps, ephemeral disks), that folder goes with it and the pictures disappear from conversations. Enable this feature to keep them in Azure Blob Storage instead.

## What changes

- Pictures are written to, and read from, a blob container instead of the application-data folder.
- **Encryption at rest is unchanged.** Each picture is encrypted with the tenant's data protection keys before it is sent to Azure, so Azure only holds ciphertext.
- If the Azure settings are missing or incomplete, an error is logged and the local store stays in effect.

:::warning[Persist the data protection keys too]
Pictures are encrypted with the tenant's data protection keys. If those keys are also lost on deployment (the default key ring lives in `App_Data`), the blobs can no longer be decrypted and the pictures stay unreadable. Store the key ring somewhere durable as well, for example with the Azure Data Protection feature.
:::

Pictures already stored locally are **not** moved. Copy the encrypted files from the `MessagingAttachments` folder into the container (same file names, under `BasePath` when set) if you need to keep them.

## Configuration

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Omnichannel": {
        "Messaging": {
          "AzureAttachments": {
            "ConnectionString": "<storage-connection-string>",
            "ContainerName": "messaging-attachments",
            "BasePath": "{{ ShellSettings.Name }}",
            "CreateContainer": true
          }
        }
      }
    }
  }
}
```

| Setting | Description |
| --- | --- |
| `ConnectionString` | Azure Storage account connection string. |
| `ContainerName` | Blob container name. Supports Liquid, such as `messaging-{{ ShellSettings.Name }}` for a container per tenant. |
| `BasePath` | Optional folder inside the container. Supports Liquid; `{{ ShellSettings.Name }}` keeps tenants apart in a shared container. |
| `CreateContainer` | Creates the container when the tenant starts if it does not exist. |

The same environment-variable form works, for example `OrchardCore__CrestApps__Omnichannel__Messaging__AzureAttachments__ConnectionString`.
