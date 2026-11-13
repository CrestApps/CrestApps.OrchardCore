---
sidebar_label: Attachments
title: Attachments
description: "The shared Attachments building block: files attached to any record (a RecordRef), one upload policy that matches each file by its own bytes, scanning through Orchard Core's file events, encrypted storage in the tenant's application data or Azure Blob Storage, access providers per owner kind, signed links, and the timeline's files."
user_manual: user-manual/administration/attachments
---

**Attachments** keeps the files attached to records. Any module can let people attach files to its records; the timeline uses it for the files of a note or comment, and tickets will too.

| Feature | Id | Depends on |
| --- | --- | --- |
| Attachments | `CrestApps.OrchardCore.Attachments` | nothing |

| Project | Holds |
| --- | --- |
| `CrestApps.OrchardCore.Attachments.Abstractions` | `FileFormat` and `FileFormats` (the formats the site knows, matched by their bytes), `Attachment`, `AttachmentUpload`, `AttachmentResult`, `AttachmentOptions`, `IAttachmentService`, `IAttachmentScanner`, `IAttachmentAccessProvider`, `IAttachmentAccessService`, `AttachmentsConstants` and the registration helpers |
| `CrestApps.OrchardCore.Attachments.Core` | `AttachmentStore` and its index (collection `Attachments`), `IAttachmentFileStore` and `EncryptedAttachmentFileStore`, `DefaultAttachmentService`, `DefaultAttachmentAccessService` and `AddAttachmentsCore()` |
| `CrestApps.OrchardCore.Attachments` (module) | The migration, the scanner that runs Orchard Core's file events, and the download endpoint |

Messaging uses the same `FileFormats` for the files its channels carry. Its attachments keep their own store for now; moving them onto this block is listed as a gap in the building-blocks catalog.

## Attaching files

```csharp
var result = await attachmentService.AddAsync(
[
    new AttachmentUpload
    {
        Owner = new RecordRef("Ticket", ticketId),
        FileName = file.FileName,
        DeclaredContentType = file.ContentType,
        Content = bytes,
        UploadedBy = new PartyRef(PartyKinds.User, userId),
    },
]);
```

`AddAsync` checks every file before it stores any, so a refused file never leaves the ones before it behind. A file is refused when the upload carries more than `MaxFilesPerUpload` files, when it is larger than `MaxFileBytes`, when its bytes match none of the allowed formats, or when a scanner refuses it. The format comes from the bytes wherever the format has a signature, never from the name or type the sender gave, so an SVG or HTML page named `.png` is refused rather than stored as a picture. The record keeps the format's media type; the file name is kept for downloads.

`ListAsync(owner)` lists a record's files newest first, `GetAsync(ids)` and `FindAsync(id)` load records, `ReadAsync(attachment)` reads the bytes and `DeleteAsync(id)` removes the record and the bytes.

## Upload policy

`AttachmentOptions` is bound from the `CrestApps:Attachments` section of the tenant's configuration:

```json
{
  "CrestApps": {
    "Attachments": {
      "MaxFileBytes": 20971520,
      "MaxFilesPerUpload": 10,
      "AllowedFormats": [ "JPEG", "PNG", "PDF" ]
    }
  }
}
```

An empty `AllowedFormats` accepts every format in `FileFormats.All`: JPEG, PNG, GIF, WebP, PDF, Word, Excel, PowerPoint, text, CSV and vCard.

## Storage

The bytes are encrypted with the tenant's data protection keys and kept in `App_Data/Sites/{tenant}/Attachments`, or in an Azure Blob Storage container when the `CrestApps:Attachments:AzureBlob` section names one (`ConnectionString`, `ContainerName`, optional `BasePath`). A file's name in storage is a hash of its id, so no id can name a path outside the folder. The files of a removed tenant are kept.

## Scanning

The module runs every file through Orchard Core's `FileCreationService`, so each `IFileEventHandler` the site has, such as the antivirus feature, can refuse it before it is stored. A module can add its own check with `AddAttachmentScanner<T>()`; every scanner must accept a file.

## Access

`IAttachmentService` doesn't check who may read or change a file; a page that acts for a user asks `IAttachmentAccessService`, which asks the `IAttachmentAccessProvider` registered for the owner's kind. A kind without a provider is refused.

```csharp
services.AddAttachmentAccessProvider<TicketAttachmentAccess>();
```

## Downloads

`GET /attachments/{id}` serves a file to a signed-in user the owner's provider allows. A guest who holds a signed link gets it too: `IAttachmentService.CreateSignedPath(id, lifetime)` returns `/attachments/{id}?token=...`, for e-mails and guests. Anyone else is asked to sign in, and a signed-in user who may not read the record gets *not found*.

Only pictures recognized by their bytes are shown in the page; every other file, and a picture asked for with `?download=true`, is a download. Every response says `X-Content-Type-Options: nosniff`, is not cached, and carries a sandboxing content security policy.

## The timeline's files

When Attachments is enabled, the timeline card's composer takes files. They belong to the entry (owner kind `TimelineEntry`), so they are exactly as visible as the entry: whoever may read the note or comment may download them. Redacting an entry removes its files too.
