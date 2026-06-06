---
sidebar_label: Content Transfer
sidebar_position: 2
title: Content Transfer
description: Bulk import and export of Orchard Core content by using pluggable file formats.
---

| | |
| --- | --- |
| **Feature Name** | Content Transfer |
| **Feature ID** | `CrestApps.OrchardCore.ContentTransfer` |
| **Optional format feature** | `CrestApps.OrchardCore.ContentTransfer.OpenXml` |

Bulk import and export Orchard Core content by using the enabled transfer file formats.

## Getting started

1. Enable **Content Transfer** under **Tools** -> **Features**.
2. Enable **Content Transfer (OpenXml)** when you also want Excel workbook (`.xlsx`) support.
3. Edit the content type only if you want to opt-out of transfer for that type. **Allow Bulk Import** and **Allow Bulk Export** are enabled by default.
4. Open **Content** -> **Bulk Import** or **Content** -> **Bulk Export**.

By default, content types appear in the import and export screens automatically. Set **Allow Bulk Import** or **Allow Bulk Export** to `false` on a content type when that type should opt out.

## Supported file formats

The base module always supports CSV files (`.csv`).

Enable **Content Transfer (OpenXml)** feature to add Excel workbook support (`.xlsx`).

- `.csv` keeps the base import and export experience lightweight and does not require OpenXml packages
- `.xlsx` becomes available automatically in the import and export UI when the optional OpenXml feature is enabled
- large imports and exports can stream in batches regardless of the enabled transfer format
- older `.xls` files are not supported

## Bulk import

Use **Content** -> **Bulk Import** to upload a transfer file for a content type.

1. Select a content type.
2. Download the template if you need the expected column layout.
3. Upload one of the enabled file formats shown in the UI.
4. Choose whether the imported items should stay as the latest draft or be published immediately.
5. The import is queued with a **Pending** status and processed in the background.

Validation runs through `IContentManager.ValidateAsync()`. Failed rows are tracked, and rejected rows can be downloaded again in the same file format as the original import as long as that format feature is still enabled.

Queued imports now follow the same background-job pattern used by the local DNC list importer. The admin list updates the status inline before work starts or stops, so entries can move through **Pending**, **Processing**, **Paused**, **Deleting**, **Completed**, **Completed with errors**, and **Failed** states without briefly showing stale values. While an import is running, the action menu offers **Pause import**. Paused, failed, pending, and stalled imports show **Resume import** so the background job can continue from the last saved batch.

For Omnichannel contacts, the import UI can also expose duplicate-phone filtering, a lead-country selector for phone normalization, and national do-not-call registry checks. Duplicate-phone filtering is enabled by default, skipped duplicate rows are recorded in the error export with the reason, and duplicate detection checks both the current import batch and existing contact phone numbers already stored in Orchard before the batch commits. When a row includes an existing `ContentItemId`, duplicate detection now treats matching phone numbers on that same content item as an update instead of a conflict. The database lookup also falls back to older stored phone values that predate the normalized-phone index columns, so re-importing the same contact list is still rejected while older tenants finish reindexing. See [DNC Registry](./dnc-registry) for registry configuration and global enforcement.

Bulk imports now default to saving drafts only. Enable **Publish imported content** when the imported items should be published immediately after create or update. When a row includes an existing `ContentItemId`, the import updates a new latest version of that item and then either keeps that version as a draft or publishes it based on the checkbox. For versionable content types, exports still include `ContentItemVersionId` for reference, but imports now ignore that value entirely.

For content types that attach `OmnichannelContactPart`, each import file should contain leads from a single country unless every phone number in the file already uses E.164. Selecting that lead country in the import UI is now required so non-E.164 values are normalized before duplicate checks, before DNC registry providers receive the lookup values, and before contact-method storage runs. The picker shows the same `Country (+calling code)` labels used by the Local DNC import UI.

The Omnichannel contact columns `DoNotCall`, `DoNotSms`, `DoNotEmail`, and `DoNotChat` now advertise `true` and `false` as the expected values in the import metadata so spreadsheet templates make the required boolean values clear.

## Bulk export

Use **Content** -> **Bulk Export** to export content items by using one of the enabled transfer formats.

Export supports:

- published, latest, or all versions
- created and modified date filters
- owner filtering
- immediate download for smaller exports
- queued background processing for larger exports

When notifications are enabled, users receive an in-app notification when a queued export is ready.

The export pipeline now initializes missing parts on the parent content item before part handlers run, so Open XML (`.xlsx`) exports do not fail with a JSON node cycle when a type includes a part that is not yet materialized on a specific content item.

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

Content Transfer is designed to be extended from Orchard modules. The import and export pipeline is built around DI-registered handlers and file-format providers, so custom code can participate without modifying the base module.

## How the pipeline works

At a high level:

1. `IContentImportManager.GetColumnsAsync()` gathers all registered columns for a content type.
2. The selected `IContentTransferFileFormatProvider` writes or reads the transfer file.
3. Registered handlers map each row to and from Orchard content:
   - `IContentImportHandler` for content item level properties
   - `IContentPartImportHandler` for content parts
   - `IContentFieldImportHandler` for content fields
4. Optional `IContentImportRowFilter` implementations can skip rows before the handlers run.
5. Optional `IContentTransferNotificationHandler` implementations can notify users when background exports complete.

## Content item level handlers

Implement `IContentImportHandler` when the data is not owned by a specific part or field.

Typical uses:

- custom content item metadata
- identifiers or external keys
- timestamps or workflow markers stored outside a part

`GetColumns()` defines the columns that should appear in templates and exports. `ImportAsync()` reads values from the current row. `ExportAsync()` writes values back to the export row.

```csharp
using System.Data;
using CrestApps.OrchardCore.ContentTransfer;

public sealed class ProductContentImportHandler : IContentImportHandler
{
    public IReadOnlyCollection<ImportColumn> GetColumns(ImportContentContext context)
        => [
            new ImportColumn
            {
                Name = "ExternalSku",
                Description = "The external SKU used by the product system.",
            },
        ];

    public Task ImportAsync(ContentImportContext context)
    {
        foreach (DataColumn column in context.Columns)
        {
            if (!string.Equals(column.ColumnName, "ExternalSku", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            context.ContentItem.Alter<ProductMetadataPart>(part =>
            {
                part.ExternalSku = context.Row[column]?.ToString()?.Trim();
            });
        }

        return Task.CompletedTask;
    }

    public Task ExportAsync(ContentExportContext context)
    {
        var metadata = context.ContentItem.As<ProductMetadataPart>();
        context.Row["ExternalSku"] = metadata?.ExternalSku;

        return Task.CompletedTask;
    }
}
```

Register the handler in your module `Startup`:

```csharp
services.AddScoped<IContentImportHandler, ProductContentImportHandler>();
```

## Content part handlers

Implement `IContentPartImportHandler` when your custom Orchard `ContentPart` needs import and export support.

Use this when:

- the data belongs to a reusable part
- multiple columns map to the same part
- the part needs custom import/export logic instead of simple property assignment

The `ImportContentPartContext` and map contexts give access to the current part definition, the target content item, the row values, and the export row.

```csharp
using System.Data;
using CrestApps.OrchardCore.ContentTransfer;
using OrchardCore.ContentManagement;

public sealed class ProductPartContentImportHandler : IContentPartImportHandler
{
    public IReadOnlyCollection<ImportColumn> GetColumns(ImportContentPartContext context)
        => [
            new ImportColumn
            {
                Name = $"{context.ContentTypePartDefinition.Name}_Sku",
                Description = "The SKU for the product.",
                IsRequired = true,
            },
            new ImportColumn
            {
                Name = $"{context.ContentTypePartDefinition.Name}_Price",
                Description = "The catalog price.",
            },
        ];

    public Task ImportAsync(ContentPartImportMapContext context)
    {
        var part = context.ContentItem.As<ProductPart>() ?? new ProductPart();

        foreach (DataColumn column in context.Columns)
        {
            if (string.Equals(column.ColumnName, "ProductPart_Sku", StringComparison.OrdinalIgnoreCase))
            {
                part.Sku = context.Row[column]?.ToString()?.Trim();
            }
            else if (string.Equals(column.ColumnName, "ProductPart_Price", StringComparison.OrdinalIgnoreCase)
                && decimal.TryParse(context.Row[column]?.ToString(), out var price))
            {
                part.Price = price;
            }
        }

        context.ContentItem.Apply(part);

        return Task.CompletedTask;
    }

    public Task ExportAsync(ContentPartExportMapContext context)
    {
        var part = context.ContentPart as ProductPart;
        context.Row["ProductPart_Sku"] = part?.Sku;
        context.Row["ProductPart_Price"] = part?.Price;

        return Task.CompletedTask;
    }
}
```

Register the handler with the helper extension so it is resolved for the matching part type:

```csharp
services.AddContentPartImportHandler<ProductPart, ProductPartContentImportHandler>();
```

## Content field handlers

Implement `IContentFieldImportHandler` for custom fields. For most field types, inherit from `StandardFieldImportHandler` from `CrestApps.OrchardCore.ContentTransfer.Core`.

`StandardFieldImportHandler` already handles the common pattern:

- one column per field property
- matching column names
- reading from `ContentFieldImportMapContext`
- writing to `ContentFieldExportMapContext`

You usually only need to provide:

- `BindingPropertyName`
- `SetValueAsync()`
- `GetValueAsync()`
- optional `Description()`, `IsRequired()`, and `GetValidValues()`

```csharp
using CrestApps.OrchardCore.ContentTransfer;

public sealed class RatingFieldImportHandler : StandardFieldImportHandler
{
    protected override string BindingPropertyName => nameof(RatingField.Value);

    protected override Task SetValueAsync(ContentFieldImportMapContext context, string value)
    {
        context.ContentPart.Alter<RatingField>(context.ContentPartFieldDefinition.Name, field =>
        {
            field.Value = int.TryParse(value, out var parsed) ? parsed : null;
        });

        return Task.CompletedTask;
    }

    protected override Task<object> GetValueAsync(ContentFieldExportMapContext context)
    {
        var field = context.ContentPart.Get<RatingField>(context.ContentPartFieldDefinition.Name);

        return Task.FromResult<object>(field?.Value);
    }

    protected override string Description(ImportContentFieldContext context)
        => "A numeric rating from 1 to 5.";

    protected override string[] GetValidValues(ImportContentFieldContext context)
        => ["1", "2", "3", "4", "5"];
}
```

Register the field handler:

```csharp
services.AddContentFieldImportHandler<RatingField, RatingFieldImportHandler>();
```

## Defining columns

Every handler returns one or more `ImportColumn` definitions. These control both the template metadata and the import/export schema.

Useful `ImportColumn` properties:

- `Name` - the primary column name written to templates and exports
- `Description` - shown in the import UI so users know what the column does
- `IsRequired` - marks required columns in the UI
- `AdditionalNames` - alternate accepted column names for backwards compatibility
- `ValidValues` - a list of allowed values to show in the UI
- `Type` - `All`, `ImportOnly`, or `ExportOnly`

For field handlers, the built-in convention is:

`{PartName}_{FieldName}_{PropertyName}`

That keeps custom field columns consistent with the built-in handlers.

## Import row filters

Implement `IContentImportRowFilter` when you want to skip rows before normal import processing.

Use a row filter for scenarios such as:

- duplicate detection
- tenant-specific exclusion rules
- integration checks against an external system
- conditional row rejection based on options stored on the transfer entry

`InitializeAsync()` runs once per import and lets the filter opt in only for relevant imports. Keep any import-scoped state on the filter instance. `ShouldSkipRowAsync()` runs for each row and receives the row, columns, content type definition, transfer entry, and 1-based row index.

```csharp
using CrestApps.OrchardCore.ContentTransfer;

public sealed class ArchivedSkuImportRowFilter : IContentImportRowFilter
{
    private HashSet<string> _archivedSkus = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> InitializeAsync(ContentImportRowFilterInitContext context)
    {
        var isProductType = string.Equals(
            context.ContentTypeDefinition.Name,
            "Product",
            StringComparison.OrdinalIgnoreCase);

        if (!isProductType)
        {
            return Task.FromResult(false);
        }

        _archivedSkus = ["OLD-001", "OLD-002"];

        return Task.FromResult(true);
    }

    public Task<bool> ShouldSkipRowAsync(ContentImportRowFilterContext context)
    {
        var sku = context.Row.Table.Columns.Contains("ProductPart_Sku")
            ? context.Row["ProductPart_Sku"]?.ToString()
            : null;

        return Task.FromResult(!string.IsNullOrWhiteSpace(sku) && _archivedSkus.Contains(sku));
    }
}
```

Register the row filter:

```csharp
services.AddScoped<IContentImportRowFilter, ArchivedSkuImportRowFilter>();
```

## File format providers

Implement `IContentTransferFileFormatProvider` when you want to add another transfer file type beyond the built-in CSV support and optional OpenXml workbook support.

A file format provider is responsible for:

- declaring the extension and content type
- deciding whether it can handle a file
- creating an `IContentTransferFileReader`
- creating an `IContentTransferFileWriter`

The provider list is resolved dynamically, so once your provider is registered the new extension appears automatically in:

- the import file picker
- template download links
- the export format selector
- provider-specific default selection order

```csharp
using CrestApps.OrchardCore.ContentTransfer;

public sealed class JsonLinesContentTransferFileFormatProvider : IContentTransferFileFormatProvider
{
    public string FileExtension => ".jsonl";

    public string ContentType => "application/x-ndjson";

    public bool CanHandle(string fileName)
        => Path.GetExtension(fileName).Equals(FileExtension, StringComparison.OrdinalIgnoreCase);

    public IContentTransferFileReader CreateReader(Stream stream)
        => new JsonLinesContentTransferFileReader(stream);

    public IContentTransferFileWriter CreateWriter(Stream stream, string sheetName)
        => new JsonLinesContentTransferFileWriter(stream);
}
```

Register the provider from your feature startup:

```csharp
services.AddSingleton<IContentTransferFileFormatProvider, JsonLinesContentTransferFileFormatProvider>();
```

If you want the format to be optional, place it in its own Orchard feature or module, following the same pattern as `CrestApps.OrchardCore.ContentTransfer.OpenXml`.

## Export completion notifications

Implement `IContentTransferNotificationHandler` when you want queued exports to notify users through a different channel.

The built-in implementation integrates with Orchard notifications when that feature is enabled, but you can replace or supplement it with your own handler to:

- send email or SMS notifications
- publish a SignalR update
- create an activity record
- bridge export completion into another application

```csharp
services.AddScoped<IContentTransferNotificationHandler, MyContentTransferNotificationHandler>();
```

## Registration summary

The common registration patterns are:

```csharp
services.AddScoped<IContentImportHandler, ProductContentImportHandler>();
services.AddContentPartImportHandler<ProductPart, ProductPartContentImportHandler>();
services.AddContentFieldImportHandler<RatingField, RatingFieldImportHandler>();
services.AddScoped<IContentImportRowFilter, ArchivedSkuImportRowFilter>();
services.AddSingleton<IContentTransferFileFormatProvider, JsonLinesContentTransferFileFormatProvider>();
services.AddScoped<IContentTransferNotificationHandler, MyContentTransferNotificationHandler>();
```

## Practical guidance

When extending Content Transfer:

- use `IContentImportHandler` for content-item level values
- use `IContentPartImportHandler` for reusable parts with one or more related columns
- use `StandardFieldImportHandler` for most custom fields
- use `AdditionalNames` when you need to keep old import templates working
- keep row-filter state scoped to the current import
- put optional file-format support in a separate feature so tenants can enable only what they need
