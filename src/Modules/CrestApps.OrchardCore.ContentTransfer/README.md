# Content Transfer

Provides bulk content import and export with pluggable file format support.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Content Transfer | `CrestApps.OrchardCore.ContentTransfer` | Provides bulk content import and export with pluggable file format support. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- `OrchardCore.Resources`
- [`CrestApps.OrchardCore.Resources`](../CrestApps.OrchardCore.Resources/README.md)

## Extension points

- `IContentExportBatchHandler` (CrestApps.OrchardCore.ContentTransfer.Abstractions): An optional export hook that runs once per export page, before the page's content items are mapped to rows.
- `IContentFieldImportHandler` (CrestApps.OrchardCore.ContentTransfer.Abstractions): Handles import and export operations for a specific content field type.
- `IContentImportHandler` (CrestApps.OrchardCore.ContentTransfer.Abstractions): Handles import and export operations at the content item level.
- `IContentImportHandlerResolver` (CrestApps.OrchardCore.ContentTransfer.Abstractions): Resolves import/export handlers for content parts and fields by their type name.
- `IContentImportManager` (CrestApps.OrchardCore.ContentTransfer.Abstractions): The main service that orchestrates the import and export of content items.
- and 13 more contracts in the same projects.

## Import safety

- Imported HTML body text is sanitized as the HTML body editor sanitizes it, unless the type's HTML body settings turn sanitizing off.
- CSV is written through the shared guarded `CsvWriter`: a value that starts like a formula is prefixed with `'`.
- Spreadsheet imports are bounded by `ContentImportOptions`: `MaxColumns` (1,000), `MaxRows` (1,048,576), `MaxCellLength` (1,048,576 characters, which also bounds a CSV cell, so an unclosed quote fails the file instead of reading it whole) and `MaxSharedStrings` (2,000,000). The total size of a workbook's shared strings and the decompressed size of a workbook are not bounded yet.
- Chunked uploads must arrive in order; the temporary file grows with the chunks and is never sized up front.

## Documentation

See [Content Transfer](../../CrestApps.Docs/docs/modules/content-transfer.md) in the technical manual.
