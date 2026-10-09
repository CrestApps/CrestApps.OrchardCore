---
sidebar_label: Content Transfer
sidebar_position: 2
title: Content Transfer
description: Bulk import and export of Orchard Core content by using pluggable file formats.
user_manual:
  - user-manual/administration/import-and-export
---

| | |
| --- | --- |
| **Feature Name** | Content Transfer |
| **Feature ID** | `CrestApps.OrchardCore.ContentTransfer` |
| **Optional format feature** | `CrestApps.OrchardCore.ContentTransfer.OpenXml` |
| **Optional storage feature** | `CrestApps.OrchardCore.ContentTransfer.Azure` |

Bulk import and export Orchard Core content by using the enabled transfer file formats.

:::tip[Using the import and export screens]
The step-by-step instructions for administrators and content managers (uploading a file, following an
import, downloading rejected rows, exporting with filters and downloading a queued export) are in the User
Manual: [Bulk Import and Export](../user-manual/administration/import-and-export.md). This page covers the
features, permissions, configuration, hosting limits and extension points.
:::

## Getting started

1. Enable `CrestApps.OrchardCore.ContentTransfer` (**Content Transfer**). It provides CSV support.
2. Enable `CrestApps.OrchardCore.ContentTransfer.OpenXml` (**Content Transfer (OpenXml)**) when you also want Excel workbook (`.xlsx`) support.
3. Grant the [permissions](#permissions) to the roles that import or export.
4. The admin menu gains **Content** -> **Import** (the **Bulk Import** list) and **Content** -> **Export** (the **Bulk Export** list).

Every content type takes part by default. The content type settings `AllowBulkImport` and `AllowBulkExport`
(**Allow bulk import** / **Allow bulk export** in the content type editor) default to `true`; set one to
`false` to keep a type off that screen.

## Permissions

| Permission | Key | Grants |
| --- | --- | --- |
| List content transfer entries | `ListContentTransferEntries` | The **Content** -> **Import** menu and the **Bulk Import** list. |
| Delete content transfer entries | `DeleteContentTransferEntries` | Deleting entries. Implies `ListContentTransferEntries`. |
| Import content items from file | `ImportContentFromFile` | Importing any content type, downloading templates and error files, pausing and resuming imports. |
| Import *type* content items from file | `ImportContentFromFile_{ContentType}` | The same for one content type. Implied by `ImportContentFromFile`. |
| Export content items from file | `ExportContentFromFile` | The **Content** -> **Export** menu and exporting any content type. |
| Export *type* content items from file | `ExportContentFromFile_{ContentType}` | The same for one content type. Implied by `ExportContentFromFile`. |

The four permissions that are not per content type are granted to the **Administrator** role by default.

## Supported file formats

The base module always supports CSV files (`.csv`).

Enable **Content Transfer (OpenXml)** feature to add Excel workbook support (`.xlsx`).

- `.csv` keeps the base import and export experience lightweight and does not require OpenXml packages
- `.xlsx` becomes available automatically in the import and export UI when the optional OpenXml feature is enabled
- large imports and exports can stream in batches regardless of the enabled transfer format
- older `.xls` files are not supported

## Import processing

An upload is stored through the content transfer file store and saved as a `ContentTransferEntry` with the
**Pending** status, and processing is triggered right away. The **Imported Files Processor** background task
(every 10 minutes) is the safety net that picks up pending entries and resumes stalled ones.

Large files are supported. When a file exceeds the configured chunk size it is uploaded to the server in chunks, and the import UI reports a clear, specific message when an upload is rejected (for example when a file exceeds the maximum allowed size). See [Large file uploads](#large-file-uploads) to tune the limits.

Validation runs through `IContentManager.ValidateAsync()`. Failed rows are tracked, and rejected rows can be downloaded again in the same file format as the original import as long as that format feature is still enabled.

Queued imports follow the same background-job pattern used by the local DNC list importer. The admin list updates the status inline before work starts or stops, so entries can move through **Pending**, **Processing**, **Paused**, **Deleting**, **Completed**, **Completed with errors**, and **Failed** states without briefly showing stale values. A **Processing** entry that has saved no progress for 10 minutes is treated as stalled, and **Resume import** continues paused, failed, pending, and stalled imports from the last saved batch. Deleting an entry removes the entry and its stored file in the background; it never deletes imported content items.

Only one run works on an import at a time: each run holds a lock named after the entry, and a run that cannot
take the lock leaves the import alone. The lock expires after 30 minutes and cannot be extended, so a run works
on an import for at most 25 minutes, saves its progress after the batch in flight, and gives up the lock. The
import then continues right away under a fresh lock from its saved row. Large imports finish the same way, in
slices that each hold a valid lock.

Imports save drafts by default. When **Publish imported content** is checked, items are published after create or update. When a row includes an existing `ContentItemId`, the import updates a new latest version of that item and then either keeps that version as a draft or publishes it based on the checkbox. For versionable content types, exports still include `ContentItemVersionId` for reference, but imports ignore that value entirely.

### Omnichannel contact imports

For Omnichannel contacts, the import UI can also expose duplicate-phone filtering, a lead-country selector for phone normalization, and national do-not-call registry checks (the options are contributed by **Omnichannel Management** through `IDisplayDriver<ImportContent>`). Duplicate-phone filtering is enabled by default, skipped duplicate rows are recorded in the error export with the reason, and duplicate detection checks both the current import batch and existing contact phone numbers already stored in Orchard before the batch commits. When a row includes an existing `ContentItemId`, duplicate detection treats matching phone numbers on that same content item as an update instead of a conflict. The database lookup also falls back to older stored phone values that predate the normalized-phone index columns, so re-importing the same contact list is still rejected while older tenants finish reindexing. See [DNC Registry](./dnc-registry) for registry configuration and global enforcement.

For content types that attach `OmnichannelContactPart`, each import file should contain leads from a single country unless every phone number in the file already uses E.164. Selecting that lead country in the import UI is required so non-E.164 values are normalized before duplicate checks, before DNC registry providers receive the lookup values, and before contact-method storage runs. The picker shows the same `Country (+calling code)` labels used by the Local DNC import UI.

The Omnichannel contact columns `DoNotCall`, `DoNotSms`, and `DoNotEmail` advertise `true` and `false` as the expected values in the import metadata so spreadsheet templates make the required boolean values clear.

The User Manual describes these options for operators on the [Contacts](../user-manual/contacts.md) and [Leads, Accounts and Opportunities](../user-manual/leads-accounts-opportunities.md) pages.

## Running on more than one instance

A single instance needs nothing below. When the site runs on more than one instance (a scaled-out App
Service plan, several containers behind a load balancer), three things have to be shared between them.

**Import and export files.** By default the files are kept on the local disk, under
`App_Data/Sites/{tenant}/Temp`. Another instance cannot read them, and a container that is restarted or
redeployed loses them, so an import that was still running fails with *The import file no longer exists*.
Enable **Content Transfer - Azure Blob Storage** to keep them in a blob container every instance reads; see
[Store import and export files in Azure Blob Storage](#store-import-and-export-files-in-azure-blob-storage).
The same applies to a single container whose `App_Data` folder is not on persistent storage.

**The import lock.** The lock that keeps two runs off the same import is held in memory unless Orchard Core's
**Redis Lock** feature (`OrchardCore.Redis.Lock`) is enabled and Redis is configured. Without it each instance
has its own lock, and two instances can import the same file at the same time, which creates every row twice.

**Chunked uploads.** A large file is uploaded in chunks, and the chunks are put back together in Orchard Core's
[temporary directory](https://docs.orchardcore.net/en/latest/reference/core/temporary-file-storage/) before the
file is saved to the store. By default that directory is local to each instance, so keep session affinity on
(ARR affinity on Azure App Service, sticky sessions on other load balancers) so every chunk of an upload reaches
the same instance. Pointing `OrchardCore:TempDirectory:Path` at a file share that every instance mounts removes
that need. The chunks are deleted as soon as the file is saved, so only uploads in progress use that directory.

### Store import and export files in Azure Blob Storage

| | |
| --- | --- |
| **Feature Name** | Content Transfer - Azure Blob Storage |
| **Feature ID** | `CrestApps.OrchardCore.ContentTransfer.Azure` |

The feature depends on **Content Transfer** and has no admin screen of its own. It is configured through the
`CrestApps:ContentTransfer:AzureBlobStorage` shell configuration section, which sits under the `OrchardCore`
key in the application's root `appsettings.json`:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContentTransfer": {
        "AzureBlobStorage": {
          "ConnectionString": "",
          "ContainerName": "content-transfer",
          "BasePath": "{{ ShellSettings.Name }}",
          "CreateContainer": true
        }
      }
    }
  }
}
```

| Setting | Description |
| --- | --- |
| `ConnectionString` | Azure Storage account connection string. **Required.** |
| `ContainerName` | Azure Blob container name. **Required.** Supports Liquid, so `content-transfer{{ ShellSettings.Name }}` gives each tenant its own container. See [Separating tenants](#separating-tenants). |
| `BasePath` | Optional subdirectory inside the container. Supports Liquid, so `{{ ShellSettings.Name }}` separates tenants that share a container. |
| `CreateContainer` | When `true`, the container is created automatically if it does not already exist. |
| `RemoveContainer` | When `true`, the container is removed when the tenant is deleted. |
| `RemoveFilesFromBasePath` | Removes only the configured `BasePath` contents when the tenant is deleted. Use this instead of `RemoveContainer` when the container is shared. |

:::warning
`ConnectionString` and `ContainerName` are both required. When either is missing, the feature stays enabled
but **does not take over storage**: an error is written to the log and the local file system keeps serving
the files.
:::

Files already on the local disk are not moved. Let imports that are running finish, or upload them again,
after you enable the feature.

#### Separating tenants

`ContainerName` and `BasePath` both accept Liquid, with the tenant's `ShellSettings` available, so one
configuration in the root `appsettings.json` serves every tenant. There are two ways to keep tenants apart:

- **A container per tenant.** Set `ContainerName` to a template such as
  `content-transfer{{ ShellSettings.Name }}` and leave `BasePath` empty. Each tenant's import and export files
  are in a container of their own, created automatically when `CreateContainer` is `true`, and access policies
  or lifecycle rules can be applied to one tenant at a time. Set `RemoveContainer` to `true` to delete the
  tenant's container when the tenant is deleted.
- **One shared container.** Keep a fixed `ContainerName` and set `BasePath` to `{{ ShellSettings.Name }}`.
  Every tenant's import and export files are in the same container, separated by a folder prefix. Use
  `RemoveFilesFromBasePath`, not `RemoveContainer`, so deleting one tenant does not delete the other tenants'
  files.

```json
{
  "ContainerName": "content-transfer{{ ShellSettings.Name }}",
  "CreateContainer": true
}
```

For a tenant named `Contoso`, this resolves to the container `content-transfercontoso`.

The resolved container name is lowercased and must be a valid Azure container name: 3 to 63 characters,
lowercase letters, digits and single hyphens, starting and ending with a letter or digit. A tenant name that
breaks these rules, for example one with an underscore, resolves to an invalid name, and an error is logged
when the tenant starts. Use the shared container for such tenants.

## Export processing

Export supports:

- published, latest, or all versions
- created and modified date filters
- owner filtering
- immediate download for smaller exports
- queued background processing for larger exports
- extra options contributed by other modules for the selected content type (see [Contributing export options](#contributing-export-options))

An export whose item count exceeds `ExportQueueThreshold` (default `500`), or whose contributed options set `ExportRequest.RequiresQueue`, is saved as a queued entry and processed in the background by the **Export Files Processor** task (every 5 minutes, and also triggered immediately). Smaller exports stream straight to the response. When notifications are enabled, users receive an in-app notification when a queued export is ready.

Modules can add their own options to the export form for specific content types. For example, when the Omnichannel management feature is enabled and you export a contact content type, the form shows a **CRM last activity** section that appends each contact's most recent completed activity to the export. See [Omnichannel management](../omnichannel/management#export-contacts-with-their-last-activity) for that feature, and [Contributing export options](#contributing-export-options) to add your own.

The export pipeline initializes missing parts on the parent content item before part handlers run, so Open XML (`.xlsx`) exports do not fail with a JSON node cycle when a type includes a part that is not yet materialized on a specific content item.

## Configuration

Configure the module in `appsettings.json` under the `OrchardCore:CrestApps:ContentTransfer` section:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContentTransfer": {
        "ImportBatchSize": 100,
        "ExportBatchSize": 200,
        "ExportQueueThreshold": 500,
        "MaxUploadFileSize": 1073741824,
        "MaxUploadChunkSize": 26214400,
        "TemporaryFileLifetime": "01:00:00"
      }
    }
  }
}
```

| Setting | Default | Description |
| --- | --- | --- |
| `ImportBatchSize` | `100` | Number of rows processed per import batch. |
| `ExportBatchSize` | `200` | Number of content items written per export batch. |
| `ExportQueueThreshold` | `500` | Maximum item count for immediate export before the request is queued. |
| `MaxUploadFileSize` | `1073741824` (1 GB) | Maximum size, in bytes, of a file that can be uploaded for bulk import. Set to `0` to disable the size check. |
| `MaxUploadChunkSize` | `26214400` (25 MB) | Size, in bytes, of each chunk when an upload is streamed to the server. The default stays below the common pre-application request-body limit (about 28.6 MB / `30000000` bytes) so uploads work out of the box on IIS and most reverse proxies. Set to `0` to disable chunked uploads. |
| `TemporaryFileLifetime` | `"01:00:00"` (1 hour) | How long an in-progress chunked upload's temporary file is kept before a background task purges it. Uses the `d.hh:mm:ss` time-span format. |

All values are read from the tenant's shell configuration, so they can be set globally for every tenant or overridden for a single tenant. See [Overriding the upload limits](#overriding-the-upload-limits).

### Limiting how much of the database an import uses

A large import pauses after each batch, so it never holds the database at its limit while people are using the site. The pause is proportional to how long the batch took, so it adapts to the size of the database and to how busy it is: when other work slows the batches down, the pauses grow with them. The same setting paces the import and deletion of local Do Not Call lists.

The limit is shared by every tenant of the application. Bulk batches run one at a time across all tenants, and a batch keeps its turn through the pause that follows it, so one tenant deleting a list and ten tenants importing files load the database the same: each job takes longer instead. The turn lives in memory, so a restart can never leave it taken, and a batch that waits more than five minutes for its turn runs anyway, so one stuck job can slow the others down but never stop them. Each application instance keeps its own turn, so a deployment scaled to several instances runs one batch per instance.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "BackgroundWork": {
        "Pacing": {
          "DatabaseShare": 0.25,
          "MaxPause": "00:00:30"
        }
      }
    }
  }
}
```

| Setting | Default | Description |
| --- | --- | --- |
| `DatabaseShare` | `0.25` | The share of time, from `0.05` to `1`, the job spends working against the database. At `0.25` a batch that took one second is followed by a three-second pause. `1` turns pacing off and runs the batches back to back. |
| `MaxPause` | `"00:00:30"` | The longest pause after one batch, however slow the batch was. |

Lower the share to protect a small database during business hours; raise it to finish a large import sooner on a quiet database.

### Large file uploads

Bulk import is meant to ingest large data files, so the upload limits are independent from the global
media library limits. This means you can allow very large imports without weakening the size
restrictions that protect the media library.

When a selected file is larger than `MaxUploadChunkSize`, the import UI streams it to the server in
chunks. Each request body stays bounded to a single chunk (plus a small overhead), so the server never
has to accept a single oversized request. The assembled file is validated against `MaxUploadFileSize`
before processing, and abandoned temporary upload files are purged automatically based on
`TemporaryFileLifetime`.

### Overriding the upload limits

`MaxUploadFileSize` defaults to **1 GB**. The default is not a hard limit — it is a 64-bit byte value bound
from configuration, so you can set it to any size your imports require (well beyond 1 GB). To allow larger
(or smaller) imports, override it with the size in **bytes**. Use this reference for common values:

| Size | Bytes |
| --- | --- |
| 100 MB | `104857600` |
| 250 MB | `262144000` |
| 500 MB | `524288000` |
| 1 GB (default) | `1073741824` |
| 2 GB | `2147483648` |
| 5 GB | `5368709120` |
| 10 GB | `10737418240` |

Keep `MaxUploadChunkSize` at a moderate value (the default is 25 MB) even when you raise the maximum file
size, so individual requests stay small. Leaving it below `30000000` bytes also keeps uploads working
without extra host configuration (see [Hosting and proxy limits](#hosting-and-proxy-limits)). The total
file size, not the chunk size, is what `MaxUploadFileSize` limits.

**Raise the maximum import size to 5 GB (all tenants).** Edit the application root `appsettings.json`:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "ContentTransfer": {
        "MaxUploadFileSize": 5368709120
      }
    }
  }
}
```

**Override the limit for a single tenant.** Add the setting to that tenant's configuration file at
`App_Data/Sites/{TenantName}/appsettings.json` (use `Default` for the default tenant). That file is already
scoped to the tenant, so its JSON starts at `CrestApps`, **without** the `OrchardCore` wrapper; a value nested
under `OrchardCore` there is never read. Tenant settings take precedence over the application root settings
(see [Per-tenant configuration](../configuration.md#per-tenant-configuration)):

```json
{
  "CrestApps": {
    "ContentTransfer": {
      "MaxUploadFileSize": 2147483648
    }
  }
}
```

**Override with environment variables.** Configuration keys map to environment variables by replacing the
nesting with a double underscore, which is convenient for containers and CI:

```bash
OrchardCore__CrestApps__ContentTransfer__MaxUploadFileSize=5368709120
```

**Remove the size cap.** Set `MaxUploadFileSize` to `0` to disable the file-size check entirely. This is
not recommended on internet-facing sites because it removes a safeguard against very large uploads; keep
a sensible maximum and only raise it as far as your imports actually require.

**Disable chunking.** Set `MaxUploadChunkSize` to `0` to require the whole file in a single request. This
is only suitable for smaller imports because the entire file must fit within one request body.

After changing any of these settings, restart the tenant (or the application) so the new shell
configuration is loaded.

### Hosting and proxy limits

The module raises the per-request body limit for the bulk import endpoint to one chunk plus a small
overhead, at the Kestrel / ASP.NET Core level, on every request. For most deployments — Kestrel directly,
Linux, containers, `dotnet run`, Azure App Service on Linux, or behind a non-IIS reverse proxy — you do
**not** need any additional request-size configuration.

Two cases enforce a limit **before** the request reaches the application, so the module cannot raise them
from code. The default `MaxUploadChunkSize` (25 MB) stays below both of the limits below, so the default
configuration works without changes. You only need the guidance here if you **raise** `MaxUploadChunkSize`
above the host limit, or disable chunking:

- **IIS hosting on Windows (in-process or out-of-process).** IIS request filtering caps the request body at
  `maxAllowedContentLength` (about 28.6 MB / `30000000` bytes by default) and rejects anything larger with
  an IIS-level `404.13` before the app runs. The default chunk size is below this, so the default upload
  works out of the box. If you raise `MaxUploadChunkSize` to `30000000` bytes or more, also raise
  `maxAllowedContentLength` in `web.config` (to at least `MaxUploadChunkSize` plus overhead). This applies
  to IIS only; it is not needed for Kestrel-based hosting.

  ```xml
  <system.webServer>
    <security>
      <requestFiltering>
        <requestLimits maxAllowedContentLength="33554432" />
      </requestFiltering>
    </security>
  </system.webServer>
  ```

- **Reverse proxies** such as Nginx (`client_max_body_size`) or Apache (`LimitRequestBody`) must allow at
  least one chunk plus overhead.

When chunking is disabled (`MaxUploadChunkSize` set to `0`), every layer above instead has to allow the
entire `MaxUploadFileSize` in a single request, which is another reason to keep chunking enabled for large
imports.

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

For export specifically:

- Optional `IDisplayDriver<ExportRequest>` implementations add option UI to the export form and persist the chosen options onto the export entry (see [Contributing export options](#contributing-export-options)).
- Optional `IContentExportBatchHandler` implementations pre-load per-page data in a single query, before the page's rows are mapped, so row handlers do not query one record at a time.
- An `IContentImportHandler` can set `ContentExportContext.Exclude` while mapping a row to omit that content item from the output (a contributed row filter for export).

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

## Contributing export options

The bulk export form is display-managed, so any module can add its own options to it without changing the base module. This is how the Omnichannel management feature adds its **CRM last activity** options to contact exports.

The building blocks are:

- `ExportRequest` — the display-driven model for the export form. Register an `IDisplayDriver<ExportRequest>` to contribute an editor shape.
- `ExportRequest.Entry` — the export entry the driver writes options onto (as parts). `ExportRequest.ContentType` / `ContentTypeDefinition` identify the content type being exported. Set `ExportRequest.RequiresQueue` to force the queued/background path when your options must be read back from the persisted entry.
- `IContentImportHandler` — reads the stored options from `context.Entry` and contributes export-only columns and values.
- `IContentExportBatchHandler` (optional) — pre-loads per-page data in one query so the row handler never queries one record at a time.
- `ContentExportContext.Exclude` (optional) — set while mapping a row to drop that content item from the output.

### Add the option UI

The export form is a single form for all content types, and the content type is chosen client-side. So a driver renders its section for every export and gates visibility itself (usually with a small script that shows the section only for the relevant content types), then validates server-side in `UpdateAsync` using `ExportRequest.ContentType`.

```csharp
using CrestApps.OrchardCore.ContentTransfer;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;

public sealed class ProductExportOptionsDisplayDriver : DisplayDriver<ExportRequest>
{
    public override IDisplayResult Edit(ExportRequest model, BuildEditorContext context)
        => Initialize<ProductExportOptionsViewModel>("ProductExportOptions_Edit", viewModel =>
        {
            // Populate any select lists the option needs. The content type is not known yet.
        }).Location("Content:20");

    public override async Task<IDisplayResult> UpdateAsync(ExportRequest model, UpdateEditorContext context)
    {
        var viewModel = new ProductExportOptionsViewModel();
        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        if (viewModel.IncludeCatalogSnapshot && IsProductType(model.ContentType))
        {
            model.Entry.Put(new ProductExportOptionsPart
            {
                IncludeCatalogSnapshot = true,
            });

            // Force the queued path so the export handler can read the option from the saved entry.
            model.RequiresQueue = true;
        }

        return Edit(model, context);
    }
}
```

The display manager wraps driver output in an `ExportRequest_Edit` shape, so the base module provides the `ExportRequest.Edit.cshtml` container template automatically. Your driver only needs its own shape template (`ProductExportOptions.Edit.cshtml` in this example). Register the driver from your feature startup:

```csharp
services.AddScoped<IDisplayDriver<ExportRequest>, ProductExportOptionsDisplayDriver>();
```

### Read the option back and add columns

The export entry is available to `IContentImportHandler` as `context.Entry` on both `GetColumns` (via `ImportContentContext`) and `ExportAsync` (via `ContentExportContext`). Return export-only columns when your option is enabled, and fill them per row:

```csharp
public IReadOnlyCollection<ImportColumn> GetColumns(ImportContentContext context)
{
    if (context.Entry is null || !context.Entry.TryGet<ProductExportOptionsPart>(out var options) || !options.IncludeCatalogSnapshot)
    {
        return [];
    }

    return [new ImportColumn { Name = "Catalog snapshot", Type = ImportColumnType.ExportOnly }];
}
```

### Pre-load per-page data (avoid one query per row)

The export loop already pages content items in batches. Implement `IContentExportBatchHandler` to load everything a page needs in one query, before its rows are mapped, and cache it for the row handler. Register the concrete handler once and forward both interfaces to it so the same scoped instance writes and reads the cache:

```csharp
services.AddScoped<ProductExportHandler>();
services.AddScoped<IContentImportHandler>(sp => sp.GetRequiredService<ProductExportHandler>());
services.AddScoped<IContentExportBatchHandler>(sp => sp.GetRequiredService<ProductExportHandler>());
```

```csharp
public async Task PrepareExportBatchAsync(ContentExportBatchContext context)
{
    // context.ContentItems is the current page; load related data for all of them in one query and cache it.
}
```

### Filter which content items are exported

To keep only rows that match your option, set `ContentExportContext.Exclude` while mapping the row. The export writer counts the item as processed but does not write it:

```csharp
public Task ExportAsync(ContentExportContext context)
{
    if (ShouldOmit(context))
    {
        context.Exclude = true;
    }

    return Task.CompletedTask;
}
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

// Export-form options (see Contributing export options):
services.AddScoped<IDisplayDriver<ExportRequest>, ProductExportOptionsDisplayDriver>();
services.AddScoped<ProductExportHandler>();
services.AddScoped<IContentImportHandler>(sp => sp.GetRequiredService<ProductExportHandler>());
services.AddScoped<IContentExportBatchHandler>(sp => sp.GetRequiredService<ProductExportHandler>());
```

## Practical guidance

When extending Content Transfer:

- use `IContentImportHandler` for content-item level values
- use `IContentPartImportHandler` for reusable parts with one or more related columns
- use `StandardFieldImportHandler` for most custom fields
- use `AdditionalNames` when you need to keep old import templates working
- keep row-filter state scoped to the current import
- put optional file-format support in a separate feature so tenants can enable only what they need
