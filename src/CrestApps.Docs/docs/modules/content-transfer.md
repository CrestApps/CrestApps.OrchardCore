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

1. Enable **Content Transfer** under **Configuration** -> **Features**.
2. Edit the content type you want to transfer and enable **Allow Bulk Import** and/or **Allow Bulk Export**.
3. Open **Content** -> **Bulk Import** or **Content** -> **Bulk Export**.

Only content types with bulk transfer enabled appear in the import and export screens.

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
- register handlers from your module `Startup`

```csharp
services.AddContentPartImportHandler<TitlePart, TitlePartContentImportHandler>();
services.AddContentFieldImportHandler<TextField, TextFieldImportHandler>();
```
