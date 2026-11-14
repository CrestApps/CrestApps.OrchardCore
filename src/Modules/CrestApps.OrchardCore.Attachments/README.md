# Attachments

Files attached to records (a timeline note, a work order): one upload policy that matches each file by its own bytes, scanning through Orchard Core's file events, encrypted storage in the tenant's application data or Azure Blob Storage, and downloads allowed by the module that owns the record or by a signed link.

## Feature

| Feature | Id |
| --- | --- |
| Attachments | `CrestApps.OrchardCore.Attachments` |

## Using it from another module

1. Reference `CrestApps.OrchardCore.Attachments.Abstractions` and gate the integration with `[RequireFeatures(AttachmentsConstants.Features.Attachments)]`, or depend on the feature.
2. Register who may read and change your records' files: `services.AddAttachmentAccessProvider<MyAccess>()`. An owner kind without a provider can't be downloaded through the shared endpoint.
3. Attach files with `IAttachmentService.AddAsync` (a `RecordRef` owner, the bytes, the sender's file name and type, the uploader `PartyRef`). Every file is checked before any is stored.
4. Link to `/attachments/{id}`, or send `IAttachmentService.CreateSignedPath(id, lifetime)` to a guest.

The timeline card takes files when this feature is on; they belong to the entry (owner kind `TimelineEntry`) and are as visible as it.

## Configuration

`CrestApps:Attachments` holds the upload policy (`MaxFileBytes`, `MaxFilesPerUpload`, `AllowedFormats`); `CrestApps:Attachments:AzureBlob` moves the files to an Azure Blob Storage container (`ConnectionString`, `ContainerName`, `BasePath`).

## Recipes and deployment

Attachments have no recipe or deployment step: the files are binary, encrypted with the tenant's keys, and belong to the records they are attached to.

## Documentation

- Technical manual: `src/CrestApps.Docs/docs/modules/attachments.md`
- User manual: `src/CrestApps.Docs/docs/user-manual/administration/attachments.md`
