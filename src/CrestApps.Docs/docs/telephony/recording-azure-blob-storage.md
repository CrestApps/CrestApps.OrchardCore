---
sidebar_label: Recording — Azure Blob Storage
sidebar_position: 5
title: Telephony - Azure Blob Storage for Recordings
description: Store Contact Center call recordings in Azure Blob Storage instead of the local tenant application-data folder, keeping the same encryption at rest.
---

| | |
| --- | --- |
| **Feature Name** | Telephony - Azure Blob Storage |
| **Feature ID** | `CrestApps.OrchardCore.Telephony.Azure` |

Provides an optional storage backend for Contact Center call recordings. When enabled and configured, the feature replaces the default local encrypted recording store with an Azure Blob Storage backend — while keeping the exact same encryption at rest.

## Overview

By default, call recordings are stored on the local file system under a tenant-scoped application-data folder:

`App_Data\Sites\<tenant-name>\RecordingMedia`

Recordings are ingested there by any voice provider that records calls (for example [Telnyx](telnyx.md) and [Asterisk](asterisk.md)): the provider records the call on its side, and the provider-specific ingest downloads the finished recording into the store. The recording bytes are always encrypted at rest with the tenant data-protection key and are never stored inside the Contact Center orchestration data.

If you want those recordings to live in Azure Blob Storage instead, enable `CrestApps.OrchardCore.Telephony.Azure`.

## What the feature changes

When the feature is enabled and valid Azure settings are present:

- call recordings are written to Azure Blob Storage instead of the tenant application-data folder
- the default `IRecordingMediaStore` file backend is replaced with an Orchard Core `BlobFileStore`
- **encryption at rest is unchanged**: recordings are still encrypted with the tenant data-protection key *before* they are written to Azure, so Azure only ever holds ciphertext
- Orchard Core can create the blob container automatically when the tenant starts
- tenant-wide media cleanup on tenant removal continues to work (the encrypted store supports tenant purge)

If the Azure configuration is missing or incomplete, the default local encrypted storage remains active.

This feature changes **only where the encrypted recording bytes are stored**. Recording orchestration — start/stop/pause governance, recording-state events, right-to-erasure deletion, and audit — is owned by the Contact Center Call Recording feature (`CrestApps.OrchardCore.ContactCenter.Recording`) and is unaffected. Live bidirectional media streaming (Voice Media) is a separate, ephemeral capability and stores nothing.

## Getting started

1. Enable the `Telephony - Azure Blob Storage` feature in Orchard Core.
2. Add the `CrestApps:Telephony:AzureRecordings` settings under the tenant's `OrchardCore` configuration section.
3. Restart the tenant or application if needed so the updated configuration is applied.
4. Record calls normally through the Contact Center Call Recording feature.

## Configuration

Configure the feature under the Orchard Core shell configuration section:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Telephony": {
        "AzureRecordings": {
          "ConnectionString": "",
          "ContainerName": "telephony-recordings",
          "BasePath": "{{ ShellSettings.Name }}",
          "CreateContainer": true
        }
      }
    }
  }
}
```

### Settings reference

| Setting | Description |
| --- | --- |
| `ConnectionString` | Azure Storage account connection string. |
| `ContainerName` | Azure Blob container name. This must follow Azure container naming rules and should be lowercase. |
| `BasePath` | Optional subdirectory inside the container where recordings are stored. Supports Orchard Core liquid shell-token formatting, so a per-tenant path (for example `{{ ShellSettings.Name }}`) keeps tenants isolated within a shared container. |
| `CreateContainer` | When `true`, the feature creates the blob container automatically if it does not already exist. |

## Encryption note

Enabling this feature does **not** weaken recording protection. The same encrypted store that backs local storage is reused over the Azure blob backend, so every recording is encrypted with the tenant data-protection key before it leaves the application. Azure server-side encryption then applies on top of that ciphertext. Reads decrypt transparently for authorized right-to-access and playback paths.

## Tenant removal and cleanup

Removing a tenant purges that tenant's recording blobs through the base Telephony recording-media cleanup, which blocks tenant removal until the purge completes. The container itself is **not** removed, because it is designed to be shared across tenants through the per-tenant `BasePath`. Use a distinct container per tenant only if your deployment requires it.

## Notes

- `BasePath` supports Orchard Core liquid shell-token formatting through Orchard's blob-storage options pipeline.
- Container names are normalized to lowercase during configuration.
- This feature changes only where encrypted recording bytes are stored. Provider recording, ingest, orchestration, and erasure behavior stay the same.
- Use local file-system storage unless you specifically need shared cloud storage or Azure-hosted deployments.

## Related documentation

- [Telephony overview](index.md)
- [Contact Center overview](../contact-center/index.md)
- [Voice Routing Architecture](../contact-center/voice-routing.md)
