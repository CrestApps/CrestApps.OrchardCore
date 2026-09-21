---
sidebar_label: Azure Blob Storage
sidebar_position: 2
title: AI Documents - Azure Blob Storage
description: Store uploaded AI documents in Azure Blob Storage instead of the local tenant web root.
---

| | |
| --- | --- |
| **Feature Name** | AI Documents - Azure Blob Storage |
| **Feature ID** | `CrestApps.OrchardCore.AI.Documents.Azure` |

Provides an optional storage provider for uploaded AI documents. When enabled and configured, the feature replaces the default local file-system document store with Orchard Core's Azure Blob Storage file store.

## Overview

By default, AI document uploads are stored on the local file system under:

`wwwroot\<tenant-name>\AIDocuments`

This applies to uploaded documents from:

- **AI Chat Interactions**
- **AI Profiles**
- **AI Chat Sessions**
- **AI Chat Widgets** (admin and frontend)

If you want those uploaded files to live in Azure Blob Storage instead, enable `CrestApps.OrchardCore.AI.Documents.Azure`.

## What the feature changes

When the feature is enabled and valid Azure settings are present:

- uploaded AI documents are written to Azure Blob Storage instead of the tenant web root
- the default `IDocumentFileStore` is replaced with an Orchard Core `BlobFileStore`
- Orchard Core can create the blob container automatically when the tenant starts
- tenant removal can optionally remove the entire container or just the configured base path

If the Azure configuration is missing or incomplete, the default local file-system storage remains active.

## Getting started

1. Enable the `AI Documents - Azure Blob Storage` feature in Orchard Core.
2. Add the `CrestApps:AI:AzureDocuments` settings under the tenant's `OrchardCore` configuration section.
3. Restart the tenant or application if needed so the updated configuration is applied.
4. Upload documents normally through AI Chat Interactions, AI Profiles, or AI Chat Sessions.

## Configuration

Configure the feature under the Orchard Core shell configuration section:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "AzureDocuments": {
          "ConnectionString": "",
          "ContainerName": "somecontainer",
          "BasePath": "some/base/path",
          "CreateContainer": true,
          "RemoveContainer": true
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
| `BasePath` | Optional subdirectory inside the container where AI documents are stored. |
| `CreateContainer` | When `true`, the feature creates the blob container automatically if it does not already exist. |
| `RemoveContainer` | When `true`, the container is removed when the tenant is deleted. |

### Optional cleanup behavior

The feature also supports:

| Setting | Description |
| --- | --- |
| `RemoveFilesFromBasePath` | Removes only the configured `BasePath` contents when the tenant is deleted. Use this instead of `RemoveContainer` when the container is shared with other content. |

`RemoveContainer` takes precedence over `RemoveFilesFromBasePath`. If both are enabled, the whole container is removed.

## Configuration example with comments

```jsonc
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "AzureDocuments": {
          // Set to your Azure Storage account connection string.
          "ConnectionString": "",
          // Set to the Azure Blob container name. It must be lowercase and follow Azure container naming rules.
          "ContainerName": "somecontainer",
          // Optionally, store documents under a subdirectory in the container.
          "BasePath": "some/base/path",
          // Creates the container automatically if it does not already exist.
          "CreateContainer": true,
          // Deletes the entire container when the tenant is removed.
          "RemoveContainer": true
        }
      }
    }
  }
}
```

## Notes

- `BasePath` supports Orchard Core liquid shell-token formatting through Orchard's blob-storage options pipeline.
- Container names are normalized to lowercase during configuration.
- This feature changes only where uploaded files are stored. AI document indexing, chunking, embeddings, and retrieval behavior stay the same.
- Use local file-system storage unless you specifically need shared cloud storage, container-managed retention, or Azure-hosted deployments.

## Related documentation

- [AI Documents overview](./index.md)
- [AI Documents indexing using Azure AI Search](./azure-ai.md)
