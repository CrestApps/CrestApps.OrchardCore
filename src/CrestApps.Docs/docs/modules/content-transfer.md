---
sidebar_label: Content Transfer
sidebar_position: 2
title: Content Transfer
description: Bulk import and export of Orchard Core content by using Excel workbooks.
---

| | |
| --- | --- |
| **Feature Name** | Content Transfer |
| **Feature ID** | `CrestApps.OrchardCore.ContentTransfer` |

Bulk import and export Orchard Core content by using Excel workbook files (`.xlsx`).

## Getting started

1. Enable **Content Transfer** under **Tools** -> **Features**.
2. Edit the content type only if you want to opt-out of transfer for that type. **Allow Bulk Import** and **Allow Bulk Export** are enabled by default.
3. Open **Content** -> **Bulk Import** or **Content** -> **Bulk Export**.

By default, content types appear in the import and export screens automatically. Set **Allow Bulk Import** or **Allow Bulk Export** to `false` on a content type when that type should opt out.

## Supported file format

Content Transfer supports only Excel workbooks (`.xlsx`).

- `.xlsx` keeps typed tabular data intact
- large imports and exports can stream in batches
- older `.xls` and `.csv` files are not supported

## Bulk import

Use **Content** -> **Bulk Import** to upload an Excel workbook for a content type.

1. Select a content type.
2. Download the template if you need the expected column layout.
3. Upload the `.xlsx` file.
4. The import is queued and processed in the background.

Validation runs through `IContentManager.ValidateAsync()`. Failed rows are tracked, and rejected rows can be downloaded as an error workbook for correction and re-import.

For Omnichannel contacts, the import UI can also expose duplicate-phone filtering and national do-not-call registry checks. See [DNC Registry](./dnc-registry) for registry configuration and global enforcement.

## Bulk export

Use **Content** -> **Bulk Export** to export content items to Excel.

Export supports:

- published, latest, or all versions
- created and modified date filters
- owner filtering
- immediate download for smaller exports
- queued background processing for larger exports

When notifications are enabled, users receive an in-app notification when a queued export is ready.

## Configuration

Configure the module in `appsettings.json` with the `OrchardCore_ContentsTransfer` section:

```json
{
  "OrchardCore_ContentsTransfer": {
    "ImportBatchSize": 100,
    "ExportBatchSize": 200,
    "ExportQueueThreshold": 500
  }
}
```

| Setting | Default | Description |
| --- | --- | --- |
| `ImportBatchSize` | `100` | Number of rows processed per import batch. |
| `ExportBatchSize` | `200` | Number of content items written per export batch. |
| `ExportQueueThreshold` | `500` | Maximum item count for immediate export before the request is queued. |

## Extensibility

The module supports custom import and export handlers for content parts and fields.

- implement `IContentPartImportHandler` for part-level import/export behavior
- implement `IContentFieldImportHandler`, or inherit from `StandardFieldImportHandler`, for field-level behavior
- implement `IContentImportRowFilter` when you need import-specific row filtering, use `InitializeAsync` to opt in for the current import, and keep any import-scoped state on the filter instance
- register handlers from your module `Startup`

```csharp
services.AddContentPartImportHandler<TitlePart, TitlePartContentImportHandler>();
services.AddContentFieldImportHandler<TextField, TextFieldImportHandler>();
```
